using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Application;

/// <summary>
/// stream 읽기, 오류 경계 판단, 멱등 저장, 최종 report 생성을 순서대로 조율하는 Application Service입니다.
/// protocol 세부 구현과 저장 기술을 Port 뒤로 숨겨 use case 자체를 테스트하기 쉽게 유지합니다.
/// </summary>
public sealed class TelemetryIngestService
{
    private readonly ITelemetryStreamReader _streamReader;
    private readonly ITelemetryRepository _repository;

    /// <summary>
    /// use case가 필요한 stream reader Strategy와 Repository Port를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="streamReader">byte stream을 검증된 telemetry 결과로 바꾸는 Strategy입니다.</param>
    /// <param name="repository">event ID 기준 멱등 저장을 담당하는 Port입니다.</param>
    public TelemetryIngestService(
        ITelemetryStreamReader streamReader,
        ITelemetryRepository repository)
    {
        // ?? throw는 null 의존성을 생성 시점에 거절해 잘못 조립된 service를 만들지 않는 guard 문법입니다.
        _streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// frame stream을 순서대로 처리하고 protocol 오류에서는 중단하며 business 오류에서는 다음 frame을 계속 처리합니다.
    /// </summary>
    /// <param name="source">4-byte big-endian 길이 prefix frame이 이어진 읽기 가능한 stream입니다.</param>
    /// <param name="cancellationToken">호출자가 작업을 중단할 때 보내는 신호이며 Result로 바꾸지 않고 전파합니다.</param>
    /// <returns>정상 EOF이면 처리 report, protocol 계약이 깨지면 실패 Result를 반환합니다.</returns>
    public async Task<Result<IngestReport>> IngestAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        var acceptedCount = 0;
        var duplicateCount = 0;
        var businessErrors = new List<Error>();

        // await foreach는 IAsyncEnumerable의 다음 값이 비동기로 준비될 때마다 순서대로 처리하는 문법입니다.
        // WithCancellation은 같은 취소 신호를 비동기 열거에도 명시적으로 연결합니다.
        await foreach (var item in _streamReader
                           .ReadAsync(source, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            if (!item.IsSuccess)
            {
                if (item.Error.Kind == ErrorKind.Protocol)
                {
                    return Result<IngestReport>.Failure(item.Error);
                }

                businessErrors.Add(item.Error);
                continue;
            }

            var reading = item.Value;
            var writeStatus = await _repository.StoreIfAbsentAsync(reading, cancellationToken);
            // switch는 enum의 가능한 저장 결과별로 실행할 branch를 명시합니다.
            switch (writeStatus)
            {
                case RepositoryWriteStatus.Stored:
                    acceptedCount++;
                    break;
                case RepositoryWriteStatus.Duplicate:
                    duplicateCount++;
                    break;
                case RepositoryWriteStatus.Conflict:
                    businessErrors.Add(
                        Error.Business(
                            "telemetry.idempotency_conflict",
                            $"eventId '{reading.EventId}'가 이미 다른 내용으로 저장되어 있습니다."));
                    break;
                default:
                    throw new InvalidOperationException($"알 수 없는 저장 결과입니다: {writeStatus}");
            }
        }

        return Result<IngestReport>.Success(
            IngestReport.Create(acceptedCount, duplicateCount, businessErrors));
    }
}
