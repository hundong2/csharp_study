using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace DocumentObservabilityExercise;

/// <summary>
/// 애플리케이션의 Composition Root입니다.
/// 구체 Adapter, 핵심 Service, 관측 Decorator를 여기서만 조립해 내부 계층이 생성 방법을 모르도록 합니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// 명령행 인자를 보고 일반 데모 또는 자체 테스트를 실행합니다.
    /// args는 `--self-test` 선택을 담고, 프로세스 성공 시 0, 검증 실패 시 1을 반환합니다.
    /// </summary>
    /// <param name="args">프로그램 실행 시 전달된 명령행 인자입니다.</param>
    /// <returns>운영체제와 스크립트가 성공 여부를 판단할 종료 코드입니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Any는 조건을 만족하는 항목이 하나라도 있는지 확인하는 LINQ 메서드입니다.
        // 대소문자 차이로 테스트 모드가 달라지지 않도록 OrdinalIgnoreCase를 사용합니다.
        if (args.Any(argument =>
                string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            return await SelfTests.RunAsync().ConfigureAwait(false);
        }

        return await RunDemoAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 고정 입력 한 건을 변환하며 로그·trace·metric이 함께 생성되는 모습을 보여 줍니다.
    /// 파라미터는 없고 성공 시 0, 요청 또는 변환 실패 시 1을 반환합니다.
    /// </summary>
    /// <returns>데모가 끝까지 성공했는지를 나타내는 프로세스 종료 코드입니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        Console.WriteLine("=== 문서 변환 관측 가능성 데모 ===");

        // 실제 OpenTelemetry exporter 대신 학습 화면에 신호를 보여 주는 작은 listener를 먼저 등록합니다.
        // 생산 코드인 Decorator는 listener의 구체 종류를 전혀 알지 못합니다.
        using var activityListener = CreateDemoActivityListener(
            ConversionTelemetry.DefaultInstrumentationName);
        using var meterListener = CreateDemoMeterListener(
            ConversionTelemetry.DefaultInstrumentationName);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
            });
        });

        using var telemetry = new ConversionTelemetry();
        var repository = new InMemoryConversionReceiptRepository();
        var converter = new SimpleDocumentConverter();
        IConversionWorkflow coreService = new DocumentConversionService(converter, repository);

        // Decorator도 IConversionWorkflow를 구현하므로 호출자는 핵심 Service와 같은 ExecuteAsync 계약만 봅니다.
        var observedService = new ObservedConversionWorkflow(
            coreService,
            telemetry,
            loggerFactory.CreateLogger<ObservedConversionWorkflow>());

        var requestResult = ConversionRequest.Create(
            "job-20260916-001",
            "# 관측 가능성\n**로그**, trace, metric을 함께 연결합니다.",
            "upper");

        if (!requestResult.IsSuccess)
        {
            Console.Error.WriteLine(
                $"[REQUEST-FAILED] {requestResult.Error.Code}: {requestResult.Error.Message}");
            return 1;
        }

        Console.WriteLine($"[SAFE-INPUT] {requestResult.Value}");

        var result = await observedService
            .ExecuteAsync(requestResult.Value, CancellationToken.None)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine($"[REJECTED] {result.Error.Code}: {result.Error.Message}");
            return 1;
        }

        Console.WriteLine(
            $"[RESULT] JobId={result.Value.JobId}, Format={result.Value.TargetFormat}, OutputLength={result.Value.OutputLength}");
        Console.WriteLine($"[REPOSITORY] SavedReceipts={repository.GetSnapshot().Count}");
        Console.WriteLine("원문과 변환 본문은 log/trace/metric에 기록하지 않았습니다.");

        return 0;
    }

    /// <summary>
    /// 데모에서 특정 ActivitySource를 전부 샘플링하고 종료 요약을 출력하는 listener를 만듭니다.
    /// instrumentationName은 구독할 생산자 이름이며, 호출자가 Dispose할 ActivityListener를 반환합니다.
    /// 운영에서는 이 자리를 OpenTelemetry SDK와 exporter가 담당합니다.
    /// </summary>
    /// <param name="instrumentationName">구독할 ActivitySource의 고유 이름입니다.</param>
    /// <returns>등록과 샘플링 설정이 끝난 ActivityListener입니다.</returns>
    private static ActivityListener CreateDemoActivityListener(string instrumentationName)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == instrumentationName,

            // ref는 큰 옵션 값을 복사하지 않고 원본 위치를 참조해 샘플 결정을 내리게 하는 문법입니다.
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = static activity =>
            {
                var outcome = activity.GetTagItem("conversion.outcome") ?? "unknown";
                Console.WriteLine(
                    $"[TRACE] Name={activity.OperationName}, Status={activity.Status}, Outcome={outcome}");
            },
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>
    /// 데모에서 특정 Meter의 long·double 측정을 받아 안전한 태그와 instrument 이름을 출력합니다.
    /// instrumentationName은 구독할 Meter 이름이며, 호출자가 Dispose할 MeterListener를 반환합니다.
    /// 처리 시간 숫자는 실행마다 달라지므로 데모 출력에서는 값 대신 기록 사실만 보여 줍니다.
    /// </summary>
    /// <param name="instrumentationName">구독할 Meter의 고유 이름입니다.</param>
    /// <returns>callback 등록과 Start가 끝난 MeterListener입니다.</returns>
    private static MeterListener CreateDemoMeterListener(string instrumentationName)
    {
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == instrumentationName)
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            static (instrument, measurement, tags, _) =>
            {
                Console.WriteLine(
                    $"[METRIC] {instrument.Name} +{measurement} {FormatTags(tags)}");
            });

        listener.SetMeasurementEventCallback<double>(
            static (instrument, _, tags, _) =>
            {
                Console.WriteLine(
                    $"[METRIC] {instrument.Name} recorded {FormatTags(tags)}");
            });

        listener.Start();
        return listener;
    }

    /// <summary>
    /// Meter callback의 span 형태 태그를 사람이 읽을 수 있는 한 줄로 바꿉니다.
    /// tags는 그 측정의 key-value 목록이며, `key=value`를 쉼표로 이은 문자열을 반환합니다.
    /// </summary>
    /// <param name="tags">할당을 줄이기 위해 ReadOnlySpan으로 전달된 metric 태그입니다.</param>
    /// <returns>태그가 없으면 빈 문자열, 있으면 대괄호로 감싼 안전한 태그 문자열입니다.</returns>
    private static string FormatTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return string.Empty;
        }

        var parts = new string[tags.Length];
        for (var index = 0; index < tags.Length; index++)
        {
            parts[index] = $"{tags[index].Key}={tags[index].Value}";
        }

        return $"[{string.Join(", ", parts)}]";
    }
}
