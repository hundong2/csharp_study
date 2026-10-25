using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Infrastructure.Protocol;

/// <summary>
/// System.IO.Pipelines의 PipeReader로 Stream을 효율적으로 읽고 decoder와 parser Strategy를 연결합니다.
/// partial read와 여러 frame이 한 번에 도착하는 상황을 같은 loop로 처리합니다.
/// </summary>
public sealed class PipelinesTelemetryStreamReader : ITelemetryStreamReader
{
    private readonly IFrameDecoder _decoder;
    private readonly ITelemetryPayloadParser _payloadParser;

    /// <summary>
    /// framing Strategy와 JSON payload Strategy를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="decoder">byte sequence에서 완전한 frame을 찾는 Strategy입니다.</param>
    /// <param name="payloadParser">frame payload를 Domain Model로 바꾸는 Strategy입니다.</param>
    public PipelinesTelemetryStreamReader(
        IFrameDecoder decoder,
        ITelemetryPayloadParser payloadParser)
    {
        // ?? throw는 왼쪽 값이 null일 때 즉시 예외를 던져 잘못 조립된 객체가 나중에 실패하지 않게 합니다.
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _payloadParser = payloadParser ?? throw new ArgumentNullException(nameof(payloadParser));
    }

    /// <summary>
    /// Stream을 PipeReader에 연결하고 frame 결과를 입력 순서대로 비동기 생산합니다.
    /// </summary>
    /// <param name="source">길이 prefix frame들이 이어진 읽기 가능한 stream입니다.</param>
    /// <param name="cancellationToken">대기 중인 read와 비동기 열거를 함께 중단하는 신호입니다.</param>
    /// <returns>각 frame의 성공 또는 입력 오류를 순서대로 내보내는 async sequence를 반환합니다.</returns>
    // async iterator는 async/await와 yield return을 함께 써서 값이 준비될 때마다 하나씩 전달합니다.
    // EnumeratorCancellation은 await foreach가 준 token을 iterator 내부 token과 연결하라고 compiler에 알려 줍니다.
    public async IAsyncEnumerable<Result<TelemetryReading>> ReadAsync(
        Stream source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("읽을 수 있는 Stream이 필요합니다.", nameof(source));
        }

        var reader = PipeReader.Create(
            source,
            new StreamPipeReaderOptions(
                bufferSize: 4_096,
                minimumReadSize: 4,
                leaveOpen: true));

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // await는 read가 완료될 때까지 thread를 붙잡지 않고 제어권을 호출자에게 돌려주는 문법입니다.
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;
                try
                {
                    while (true)
                    {
                        // 한 read에 frame이 많이 들어와도 frame 사이에서 취소를 다시 확인해 오래 지연하지 않습니다.
                        cancellationToken.ThrowIfCancellationRequested();
                        var decodeResult = _decoder.Decode(ref buffer, readResult.IsCompleted);
                        if (decodeResult.Status == FrameDecodeStatus.NeedMoreData)
                        {
                            break;
                        }

                        if (decodeResult.Status == FrameDecodeStatus.ProtocolError)
                        {
                            var error = decodeResult.Error
                                ?? throw new InvalidOperationException("ProtocolError 결과에 Error가 없습니다.");

                            // 복구하지 않고 끝낼 read의 나머지는 버려 final AdvanceTo가 consumed=end가 되게 합니다.
                            buffer = buffer.Slice(buffer.End);
                            yield return Result<TelemetryReading>.Failure(error);
                            yield break;
                        }

                        var parseResult = _payloadParser.Parse(decodeResult.Payload);
                        if (!parseResult.IsSuccess && parseResult.Error.Kind == ErrorKind.Protocol)
                        {
                            // 길이 prefix 덕분에 다음 frame 경계는 알 수 있습니다. 그래도 이 예제는 한 sender/session의
                            // wire 계약이 깨진 뒤 추가 side effect를 만들지 않는 엄격한 fail-fast 정책을 선택합니다.
                            buffer = buffer.Slice(buffer.End);
                            yield return parseResult;
                            yield break;
                        }

                        yield return parseResult;
                    }
                }
                finally
                {
                    // read마다 정확히 한 번 AdvanceTo합니다. 소비자가 yield 직후 열거를 중단해도 이 finally는 실행됩니다.
                    // consumed는 완성 frame 뒤의 시작점이고, examined는 이번에 확인한 끝입니다.
                    reader.AdvanceTo(buffer.Start, buffer.End);
                }

                if (readResult.IsCompleted)
                {
                    yield break;
                }
            }
        }
        finally
        {
            // PipeReader는 await using 대상이 아니라 CompleteAsync로 내부 buffer 자원을 반환합니다.
            await reader.CompleteAsync();
        }
    }
}
