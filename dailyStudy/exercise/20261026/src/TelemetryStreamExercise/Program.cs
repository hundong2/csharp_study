// using 지시문은 아래 코드에서 쓸 다른 namespace의 형식 이름을 매번 길게 적지 않게 가져옵니다.
using System.Text.Json;
using TelemetryStreamExercise.Application;
using TelemetryStreamExercise.Domain;
using TelemetryStreamExercise.Infrastructure;
using TelemetryStreamExercise.Infrastructure.Protocol;
using TelemetryStreamExercise.SelfTesting;

namespace TelemetryStreamExercise;

/// <summary>
/// 구체 Strategy와 Repository Adapter를 조립하고 demo 또는 self-test 실행을 선택하는 Composition Root입니다.
/// 객체 생성 책임을 한곳에 모으면 Domain과 Application은 System.IO.Pipelines나 메모리 저장소를 직접 알 필요가 없습니다.
/// </summary>
public sealed class Program
{
    private const int MaximumFrameLength = 1_024;
    // target-typed new는 왼쪽 형식이 분명할 때 생성자 형식 이름을 생략하는 문법입니다.
    private static readonly JsonSerializerOptions DemoJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// static 진입점만 가진 Composition Root를 실수로 객체화하지 못하게 막습니다.
    /// </summary>
    private Program()
    {
    }

    /// <summary>
    /// 명령줄 인수에 따라 조각난 stream demo 또는 자동 self-test를 실행합니다.
    /// </summary>
    /// <param name="args">`--demo` 또는 `--self-test` 실행 mode를 담은 명령줄 인수입니다.</param>
    /// <returns>성공은 0, demo 실패는 1, 알 수 없는 인수는 2를 담은 Task를 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        if (args.Length == 0 || args.Contains("--demo", StringComparer.Ordinal))
        {
            return await RunDemoAsync();
        }

        Console.Error.WriteLine("사용법: --demo 또는 --self-test");
        return 2;
    }

    /// <summary>
    /// header와 JSON 본문이 여러 read로 잘리는 입력에서 신규·중복·business 오류 처리를 함께 보여 줍니다.
    /// </summary>
    /// <returns>예상한 report가 나오면 0, protocol 또는 결과 계약이 다르면 1을 반환합니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        // var는 오른쪽 값에서 형식이 명확할 때 compiler가 정적 형식을 대신 적게 하는 문법이며 dynamic이 아닙니다.
        var capturedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
        var firstJson = CreateJson("evt-100", "sensor-seoul", capturedAt, 21.5m);
        var secondJson = CreateJson("evt-101", "sensor-busan", capturedAt.AddSeconds(1), 24.25m);
        var invalidJson = CreateJson("evt-102", "sensor-jeju", capturedAt.AddSeconds(2), 500m);

        var bytes = LengthPrefixedFrameEncoder.Concat(
            LengthPrefixedFrameEncoder.EncodeJson(firstJson),
            LengthPrefixedFrameEncoder.EncodeJson(secondJson),
            LengthPrefixedFrameEncoder.EncodeJson(firstJson),
            LengthPrefixedFrameEncoder.EncodeJson(invalidJson));

        var (service, repository) = BuildApplication();

        // await using은 비동기 작업이 끝나거나 예외가 나도 Stream의 DisposeAsync를 반드시 호출하는 문법입니다.
        // 1, 2, 1 byte처럼 작은 chunk를 반복해 4-byte header와 JSON 본문 양쪽이 잘리는 상황을 만듭니다.
        await using var stream = new ChunkedReadStream(bytes, 1, 2, 1, 3, 2, 5, 1, 8);
        var result = await service.IngestAsync(stream, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(
                $"[DEMO:PROTOCOL-FAILURE] {result.Error.Code}: {result.Error.Message}");
            return 1;
        }

        var report = result.Value;
        Console.WriteLine("4-byte big-endian JSON telemetry 수신 결과");
        Console.WriteLine($"stream bytes={bytes.Length}, read calls={stream.ReadCount}");
        Console.WriteLine(
            $"accepted={report.AcceptedCount}, duplicate={report.DuplicateCount}, "
            + $"rejected={report.BusinessErrors.Length}, stored={repository.Count}");

        foreach (var error in report.BusinessErrors)
        {
            Console.WriteLine($"business error: {error.Code} - {error.Message}");
        }

        var isExpected = report.AcceptedCount == 2
            && report.DuplicateCount == 1
            && report.BusinessErrors.Length == 1
            && report.BusinessErrors[0].Code == "telemetry.temperature_out_of_range"
            && repository.Count == 2
            && stream.ReadCount > 4;

        Console.WriteLine(isExpected ? "[DEMO:SUCCESS]" : "[DEMO:UNEXPECTED]");
        return isExpected ? 0 : 1;
    }

    /// <summary>
    /// demo와 운영 진입점이 사용할 구체 Adapter와 Strategy를 생성자 주입으로 연결합니다.
    /// </summary>
    /// <returns>조립된 Application Service와 관찰 가능한 메모리 Repository를 tuple로 반환합니다.</returns>
    private static (TelemetryIngestService Service, InMemoryTelemetryRepository Repository) BuildApplication()
    {
        // DI는 필요한 협력자를 class 내부에서 몰래 만들지 않고 바깥 Composition Root에서 주입하는 설계 방식입니다.
        var repository = new InMemoryTelemetryRepository();
        var decoder = new LengthPrefixedFrameDecoder(MaximumFrameLength);
        var parser = new JsonTelemetryPayloadParser();
        var streamReader = new PipelinesTelemetryStreamReader(decoder, parser);
        var service = new TelemetryIngestService(streamReader, repository);
        return (service, repository);
    }

    /// <summary>
    /// serializer를 사용해 demo 값이 올바르게 escape된 camelCase JSON이 되도록 만듭니다.
    /// </summary>
    /// <param name="eventId">멱등 저장 key로 사용할 event ID입니다.</param>
    /// <param name="deviceId">측정 장치 ID입니다.</param>
    /// <param name="capturedAt">offset을 포함한 측정 시각입니다.</param>
    /// <param name="temperatureCelsius">전송할 섭씨 온도입니다.</param>
    /// <returns>한 telemetry payload의 JSON 문자열을 반환합니다.</returns>
    private static string CreateJson(
        string eventId,
        string deviceId,
        DateTimeOffset capturedAt,
        decimal temperatureCelsius)
    {
        return JsonSerializer.Serialize(
            new DemoContract(eventId, deviceId, capturedAt, temperatureCelsius),
            DemoJsonOptions);
    }

    /// <summary>
    /// demo JSON serializer에 전달할 값 묶음입니다.
    /// </summary>
    /// <param name="EventId">event ID입니다.</param>
    /// <param name="DeviceId">장치 ID입니다.</param>
    /// <param name="CapturedAt">측정 시각입니다.</param>
    /// <param name="TemperatureCelsius">섭씨 온도입니다.</param>
    private sealed record DemoContract(
        string EventId,
        string DeviceId,
        DateTimeOffset CapturedAt,
        decimal TemperatureCelsius);
}
