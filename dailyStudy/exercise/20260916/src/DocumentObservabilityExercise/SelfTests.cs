using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DocumentObservabilityExercise;

/// <summary>
/// 외부 테스트 패키지 없이 핵심 흐름과 관측 계약을 검증하는 작은 자체 테스트 모음입니다.
/// 표준 dotnet test 프로젝트가 아니라 `--self-test`로 실행하는 학습용 검증기라는 점을 구분해야 합니다.
/// </summary>
public static class SelfTests
{
    /// <summary>
    /// 등록된 테스트를 순서대로 실행하고 각 성공·실패를 콘솔에 표시합니다.
    /// 파라미터는 없고 모든 테스트가 통과하면 0, 하나라도 실패하면 1을 반환합니다.
    /// </summary>
    /// <returns>자체 테스트 전체의 프로세스 종료 코드입니다.</returns>
    public static async Task<int> RunAsync()
    {
        // Func<Task>는 "나중에 실행할 비동기 함수"를 값으로 보관하는 delegate 형식입니다.
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("요청 검증과 안전한 ToString", RequestValidationProtectsSensitiveContentAsync),
            ("핵심 Service 변환·저장", CoreServiceConvertsAndSavesAsync),
            ("성공 log·trace·metric 상관관계", SuccessEmitsCorrelatedSignalsAsync),
            ("예상 거절의 Warning·Error span", RejectionIsObservedWithoutThrowingAsync),
            ("호출자 취소 분류와 재전파", CancellationIsObservedAndRethrownAsync),
            ("기술 예외 분류와 민감정보 보호", FaultIsObservedAndRethrownSafelyAsync),
            ("관측 공급자 장애의 업무 격리", TelemetryFailureDoesNotChangeBusinessOutcomeAsync),
        };

        var passed = 0;

        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                passed++;
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"[FAIL] {test.Name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"자체 테스트: {passed}/{tests.Length} 통과");
        return passed == tests.Length ? 0 : 1;
    }

    /// <summary>
    /// 입력 factory가 잘못된 ID·형식을 거절하고 ToString에서 원문을 숨기는지 검사합니다.
    /// 파라미터와 의미 있는 반환값은 없으며, 계약 위반 시 예외를 던져 테스트 실패를 알립니다.
    /// </summary>
    /// <returns>비동기 테스트 목록과 같은 모양을 맞추기 위한 완료 Task입니다.</returns>
    private static Task RequestValidationProtectsSensitiveContentAsync()
    {
        const string secret = "고객 주민번호처럼 로그에 남기면 안 되는 원문";
        var valid = ConversionRequest.Create("safe-job_01", secret, "PLAIN");

        True(valid.IsSuccess, "올바른 요청은 성공해야 합니다.");
        Equal("plain", valid.Value.TargetFormat, "출력 형식은 소문자로 정규화되어야 합니다.");
        True(!valid.Value.ToString().Contains(secret, StringComparison.Ordinal),
            "ToString에 원문이 노출되면 안 됩니다.");
        True(valid.Value.ToString().Contains("SourceLength", StringComparison.Ordinal),
            "안전한 길이 메타데이터는 보여야 합니다.");

        var injectedId = ConversionRequest.Create("job\nforged", "본문", "plain");
        True(!injectedId.IsSuccess, "줄바꿈이 든 작업 ID는 거절해야 합니다.");
        Equal("request.job_id_invalid", injectedId.Error.Code, "안정된 오류 코드를 반환해야 합니다.");

        var nonAsciiId = ConversionRequest.Create("작업-01", "본문", "plain");
        True(!nonAsciiId.IsSuccess, "영문이라고 문서화한 작업 ID에 비 ASCII 문자를 허용하면 안 됩니다.");

        var unsupported = ConversionRequest.Create("job-2", "본문", "pdf");
        True(!unsupported.IsSuccess, "허용 목록 밖 형식은 거절해야 합니다.");
        Equal("request.target_unsupported", unsupported.Error.Code, "형식 오류 코드를 반환해야 합니다.");

        // 값 형식의 default인 0도 실제 성공 값일 수 있으므로 IsSuccess를 보지 않고 Value를 열면 안 됩니다.
        // 이 회귀 검사는 실패 OperationResult<int>가 0을 돌려주지 않고 계약 위반을 알리는지 확인합니다.
        var valueTypeFailure = OperationResult.Failure<int>("request.source_required", "숫자가 없습니다.");
        var blockedFailureValueRead = false;
        try
        {
            _ = valueTypeFailure.Value;
        }
        catch (InvalidOperationException)
        {
            blockedFailureValueRead = true;
        }

        True(blockedFailureValueRead, "실패 Result의 값 형식 default를 성공 값처럼 읽으면 안 됩니다.");

        var blockedUnsafeErrorCode = false;
        try
        {
            _ = OperationResult.Failure<int>("BAD\nCODE", "악의적인 오류 코드입니다.");
        }
        catch (ArgumentException)
        {
            blockedUnsafeErrorCode = true;
        }

        True(blockedUnsafeErrorCode, "로그·trace에 전달될 오류 코드도 제한된 구문이어야 합니다.");

        var blockedUnboundedErrorCode = false;
        try
        {
            _ = OperationResult.Failure<int>("customer.123456", "요청마다 달라지는 코드입니다.");
        }
        catch (ArgumentOutOfRangeException)
        {
            blockedUnboundedErrorCode = true;
        }

        True(blockedUnboundedErrorCode,
            "구문이 안전해도 요청별 값인 오류 코드는 고정 vocabulary 밖이므로 거절해야 합니다.");

        var convertedDocument = new ConvertedDocument(secret, "plain");
        True(!convertedDocument.ToString().Contains(secret, StringComparison.Ordinal),
            "변환 결과 ToString에도 실제 본문이 노출되면 안 됩니다.");
        True(convertedDocument.ToString().Contains("ContentLength", StringComparison.Ordinal),
            "변환 결과의 안전한 길이 메타데이터는 보여야 합니다.");

        var blockedInvalidDocumentFormat = false;
        try
        {
            _ = new ConvertedDocument("본문", "pdf");
        }
        catch (ArgumentOutOfRangeException)
        {
            blockedInvalidDocumentFormat = true;
        }

        True(blockedInvalidDocumentFormat, "변환 결과도 지원하지 않는 형식을 허용하면 안 됩니다.");

        var blockedInvalidReceipt = false;
        try
        {
            _ = new ConversionReceipt("작업-01", "plain", -1);
        }
        catch (ArgumentException)
        {
            blockedInvalidReceipt = true;
        }

        True(blockedInvalidReceipt, "완료 영수증도 잘못된 작업 ID를 생성 시점에 막아야 합니다.");

        var blockedNegativeReceiptLength = false;
        try
        {
            _ = new ConversionReceipt("receipt-01", "plain", -1);
        }
        catch (ArgumentOutOfRangeException)
        {
            blockedNegativeReceiptLength = true;
        }

        True(blockedNegativeReceiptLength, "완료 영수증의 결과 길이는 음수가 될 수 없습니다.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 관측 Decorator 없이 핵심 Service만 실행해 변환 성공 뒤 영수증이 저장되는지 검사합니다.
    /// 파라미터와 반환값은 없고, 실패 시 assertion 예외로 원인을 알립니다.
    /// </summary>
    /// <returns>검증이 모두 끝날 때 완료되는 Task입니다.</returns>
    private static async Task CoreServiceConvertsAndSavesAsync()
    {
        var repository = new InMemoryConversionReceiptRepository();
        var service = new DocumentConversionService(new SimpleDocumentConverter(), repository);
        var request = CreateRequest("core-001", "# hello\n**observability**", "upper");

        var result = await service
            .ExecuteAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        True(result.IsSuccess, "정상 문서는 변환되어야 합니다.");
        Equal("upper", result.Value.TargetFormat, "요청한 출력 형식을 보존해야 합니다.");
        Equal("HELLO OBSERVABILITY".Length, result.Value.OutputLength,
            "정규화하고 대문자로 만든 결과 길이가 맞아야 합니다.");

        var snapshot = repository.GetSnapshot();
        Equal(1, snapshot.Count, "완료 영수증은 한 번만 저장되어야 합니다.");
        Equal(result.Value, snapshot[0], "반환값과 저장 snapshot이 같아야 합니다.");

        var mismatchedRepository = new InMemoryConversionReceiptRepository();
        var mismatchedConverter = new StubDocumentConverter(
            (_, _) => Task.FromResult(
                OperationResult.Success(new ConvertedDocument("BODY", "plain"))));
        var mismatchedService = new DocumentConversionService(
            mismatchedConverter,
            mismatchedRepository);

        await ThrowsAsync<InvalidOperationException>(
                () => mismatchedService.ExecuteAsync(request, CancellationToken.None),
                "Converter가 요청과 다른 형식을 반환하면 Port 계약 위반이어야 합니다.")
            .ConfigureAwait(false);
        Equal(0, mismatchedRepository.GetSnapshot().Count,
            "형식 계약을 어긴 변환 결과는 영수증으로 저장하면 안 됩니다.");
    }

    /// <summary>
    /// 성공 흐름에서 구조화 로그, OK Activity, bounded metric 태그가 함께 생기는지 검사합니다.
    /// 파라미터와 반환값은 없고, 어느 신호에도 민감한 원문이 섞이면 테스트를 실패시킵니다.
    /// </summary>
    /// <returns>Decorator 실행과 모든 signal 검증이 끝날 때 완료되는 Task입니다.</returns>
    private static async Task SuccessEmitsCorrelatedSignalsAsync()
    {
        const string secret = "TOP-SECRET-DOCUMENT-CONTENT";
        var request = CreateRequest("observed-success", secret, "plain");
        var receipt = new ConversionReceipt(request.JobId, request.TargetFormat, 42);
        var inner = new StubWorkflow(
            (_, _) => Task.FromResult(OperationResult.Success(receipt)));

        var instrumentationName = CreateInstrumentationName("success");
        using var signals = new SignalCollector(instrumentationName);
        using var telemetry = new ConversionTelemetry(instrumentationName);
        var logger = new CollectingLogger<ObservedConversionWorkflow>();
        var decorator = new ObservedConversionWorkflow(inner, telemetry, logger);

        var result = await decorator
            .ExecuteAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        True(result.IsSuccess, "안쪽 성공 Result를 그대로 반환해야 합니다.");
        Equal(1, inner.CallCount, "Decorator는 안쪽 Workflow를 정확히 한 번 호출해야 합니다.");

        var activities = signals.GetActivities();
        Equal(1, activities.Length, "변환 한 건당 Activity 한 개가 끝나야 합니다.");
        Equal(ActivityStatusCode.Ok, activities[0].Status, "성공 Activity 상태는 Ok여야 합니다.");
        Equal("succeeded", activities[0].Tags["conversion.outcome"],
            "성공 outcome 태그가 필요합니다.");
        Equal(request.JobId, activities[0].Tags["conversion.job_id"],
            "trace에는 개별 작업을 찾을 correlation 키가 있어야 합니다.");

        var logs = logger.GetEntries();
        True(logs.Any(entry => entry.EventId.Id == 1001 && entry.Level == LogLevel.Information),
            "구조화된 시작 로그가 필요합니다.");
        True(logs.Any(entry => entry.EventId.Id == 1002 && entry.Level == LogLevel.Information),
            "구조화된 성공 로그가 필요합니다.");
        True(logs.SelectMany(entry => entry.State).Any(pair =>
                pair.Key == "JobId" && Equals(pair.Value, request.JobId)),
            "로그 state에 JobId 속성이 따로 보존되어야 합니다.");

        var startLog = Single(
            logs.Where(entry => entry.EventId.Id == 1001).ToArray(),
            "변환 시작 로그");
        Equal(activities[0].TraceId, startLog.State["TraceId"]?.ToString(),
            "시작 로그의 TraceId는 실제 Activity TraceId와 같아야 합니다.");

        var measurements = signals.GetMeasurements();
        Equal(3, measurements.Length, "attempt, completion, duration 세 측정이 필요합니다.");
        True(measurements.Any(item => item.InstrumentName == ConversionTelemetry.AttemptMetricName),
            "attempt counter가 기록되어야 합니다.");
        True(measurements.Any(item =>
                item.InstrumentName == ConversionTelemetry.CompletionMetricName &&
                Equals(item.Tags["conversion.outcome"], "succeeded")),
            "completion counter에 성공 outcome이 있어야 합니다.");
        True(measurements.Any(item =>
                item.InstrumentName == ConversionTelemetry.DurationMetricName && item.Value >= 0),
            "0 이상의 duration이 기록되어야 합니다.");

        // Metric backend 비용은 "값의 개수"에 크게 좌우됩니다.
        // 고유 job ID나 본문은 trace/log에는 정책적으로 쓸 수 있어도 metric label에는 넣지 않습니다.
        True(measurements.All(item => !item.Tags.Keys.Any(key =>
                key.Contains("job", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("content", StringComparison.OrdinalIgnoreCase))),
            "metric에는 고유 ID나 본문 태그를 넣으면 안 됩니다.");

        var allObservableText = BuildObservableText(logs, activities, measurements);
        True(!allObservableText.Contains(secret, StringComparison.Ordinal),
            "어떤 관측 신호에도 원문이 노출되면 안 됩니다.");
    }

    /// <summary>
    /// 예상 가능한 실패 Result가 예외로 바뀌지 않고 Warning 로그와 Error Activity로 분류되는지 검사합니다.
    /// 파라미터와 반환값은 없으며 assertion 실패 시 예외를 던집니다.
    /// </summary>
    /// <returns>거절 흐름의 관측 검증이 끝날 때 완료되는 Task입니다.</returns>
    private static async Task RejectionIsObservedWithoutThrowingAsync()
    {
        var request = CreateRequest("observed-reject", "[[unsupported]]", "plain");
        var inner = new StubWorkflow(
            (_, _) => Task.FromResult(
                OperationResult.Failure<ConversionReceipt>(
                    "conversion.syntax_unsupported",
                    "지원하지 않는 문법입니다.")));

        var instrumentationName = CreateInstrumentationName("reject");
        using var signals = new SignalCollector(instrumentationName);
        using var telemetry = new ConversionTelemetry(instrumentationName);
        var logger = new CollectingLogger<ObservedConversionWorkflow>();
        var decorator = new ObservedConversionWorkflow(inner, telemetry, logger);

        var result = await decorator
            .ExecuteAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        True(!result.IsSuccess, "예상 거절 Result를 그대로 반환해야 합니다.");
        Equal("conversion.syntax_unsupported", result.Error.Code, "오류 코드를 바꾸면 안 됩니다.");
        True(logger.GetEntries().Any(entry =>
                entry.EventId.Id == 1003 && entry.Level == LogLevel.Warning),
            "예상 거절은 Warning으로 남겨야 합니다.");

        var activity = Single(signals.GetActivities(), "거절 Activity");
        Equal(ActivityStatusCode.Error, activity.Status, "거절 Activity는 Error 상태여야 합니다.");
        Equal("rejected", activity.Tags["conversion.outcome"], "거절 outcome이 필요합니다.");
        Equal("conversion.syntax_unsupported", activity.Tags["error.type"],
            "안정된 오류 코드를 trace에 기록해야 합니다.");

        True(signals.GetMeasurements().Any(item =>
                Equals(item.Tags.GetValueOrDefault("conversion.outcome"), "rejected")),
            "metric에도 rejected 분류가 있어야 합니다.");
    }

    /// <summary>
    /// 호출자가 취소한 OperationCanceledException을 canceled로 관측한 뒤 다시 던지는지 검사합니다.
    /// 파라미터와 반환값은 없고, 취소가 Result나 fault로 잘못 바뀌면 테스트를 실패시킵니다.
    /// </summary>
    /// <returns>취소 예외와 signal을 모두 확인할 때 완료되는 Task입니다.</returns>
    private static async Task CancellationIsObservedAndRethrownAsync()
    {
        var request = CreateRequest("observed-cancel", "cancel me", "upper");
        var inner = new StubWorkflow(
            (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(
                    OperationResult.Success(
                        new ConversionReceipt("unreachable", "upper", 0)));
            });

        var instrumentationName = CreateInstrumentationName("cancel");
        using var signals = new SignalCollector(instrumentationName);
        using var telemetry = new ConversionTelemetry(instrumentationName);
        var logger = new CollectingLogger<ObservedConversionWorkflow>();
        var decorator = new ObservedConversionWorkflow(inner, telemetry, logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await ThrowsAsync<OperationCanceledException>(
                () => decorator.ExecuteAsync(request, cancellation.Token),
                "호출자 취소는 상위 계층으로 다시 전달해야 합니다.")
            .ConfigureAwait(false);

        True(logger.GetEntries().Any(entry =>
                entry.EventId.Id == 1004 && entry.Level == LogLevel.Information),
            "호출자 취소는 Information 로그여야 합니다.");
        var activity = Single(signals.GetActivities(), "취소 Activity");
        Equal(ActivityStatusCode.Unset, activity.Status,
            "예상된 호출자 취소는 서버 장애가 아니므로 Activity 상태를 Error로 만들면 안 됩니다.");
        Equal("canceled", activity.Tags["conversion.outcome"], "취소 outcome이 필요합니다.");
        True(signals.GetMeasurements().Any(item =>
                Equals(item.Tags.GetValueOrDefault("conversion.outcome"), "canceled")),
            "metric에도 canceled 분류가 있어야 합니다.");

        // 호출자 토큰과 무관한 timeout 토큰이 취소된 순간 caller도 취소된 경합 상황을 재현합니다.
        // 예외가 가진 토큰까지 비교하지 않으면 이 기술 장애를 정상 사용자 취소로 잘못 셀 수 있습니다.
        var unrelatedToken = new CancellationToken(canceled: true);
        var timeoutInner = new StubWorkflow(
            (_, _) => Task.FromException<OperationResult<ConversionReceipt>>(
                new OperationCanceledException(unrelatedToken)));
        var timeoutInstrumentationName = CreateInstrumentationName("unrelated-cancellation");
        using var timeoutSignals = new SignalCollector(timeoutInstrumentationName);
        using var timeoutTelemetry = new ConversionTelemetry(timeoutInstrumentationName);
        var timeoutLogger = new CollectingLogger<ObservedConversionWorkflow>();
        var timeoutDecorator = new ObservedConversionWorkflow(
            timeoutInner,
            timeoutTelemetry,
            timeoutLogger);

        await ThrowsAsync<OperationCanceledException>(
                () => timeoutDecorator.ExecuteAsync(request, cancellation.Token),
                "다른 토큰의 취소 예외도 원래 형식으로 다시 전달해야 합니다.")
            .ConfigureAwait(false);

        True(timeoutLogger.GetEntries().Any(entry =>
                entry.EventId.Id == 1005 && entry.Level == LogLevel.Error),
            "다른 토큰의 취소 예외는 기술 장애로 관측해야 합니다.");
        True(timeoutLogger.GetEntries().All(entry => entry.EventId.Id != 1004),
            "다른 토큰의 취소 예외를 호출자 취소 이벤트로 기록하면 안 됩니다.");
        var timeoutActivity = Single(timeoutSignals.GetActivities(), "다른 토큰 취소 Activity");
        Equal(ActivityStatusCode.Error, timeoutActivity.Status,
            "다른 토큰의 취소 예외 Activity는 Error여야 합니다.");
        Equal("faulted", timeoutActivity.Tags["conversion.outcome"],
            "다른 토큰의 취소 예외는 faulted outcome이어야 합니다.");
    }

    /// <summary>
    /// 예상하지 못한 예외를 faulted로 관측하고 원래 예외를 보존하며 Message를 숨기는지 검사합니다.
    /// 파라미터와 반환값은 없고, 민감 문자열이 signal에 나타나면 실패합니다.
    /// </summary>
    /// <returns>예외 재전파와 개인정보 보호 검증이 끝날 때 완료되는 Task입니다.</returns>
    private static async Task FaultIsObservedAndRethrownSafelyAsync()
    {
        const string secretExceptionMessage = "SECRET-PATH-CUSTOMER-A.TXT";
        var request = CreateRequest("observed-fault", "private source", "plain");
        var inner = new StubWorkflow(
            (_, _) => Task.FromException<OperationResult<ConversionReceipt>>(
                new InvalidOperationException(secretExceptionMessage)));

        var instrumentationName = CreateInstrumentationName("fault");
        using var signals = new SignalCollector(instrumentationName);
        using var telemetry = new ConversionTelemetry(instrumentationName);
        var logger = new CollectingLogger<ObservedConversionWorkflow>();
        var decorator = new ObservedConversionWorkflow(inner, telemetry, logger);

        await ThrowsAsync<InvalidOperationException>(
                () => decorator.ExecuteAsync(request, CancellationToken.None),
                "기술 예외는 Result로 숨기지 않고 다시 던져야 합니다.")
            .ConfigureAwait(false);

        var logs = logger.GetEntries();
        True(logs.Any(entry => entry.EventId.Id == 1005 && entry.Level == LogLevel.Error),
            "기술 장애는 Error 로그가 필요합니다.");
        // FullName 뒤의 `!`는 이 런타임 형식에는 값이 있음을 컴파일러에 알려 주는 null-forgiving 연산자입니다.
        var expectedExceptionType = typeof(InvalidOperationException).FullName!;
        True(logs.SelectMany(entry => entry.State).Any(pair =>
                pair.Key == "ExceptionType" && Equals(pair.Value, expectedExceptionType)),
            "안전한 예외 형식은 구조화 속성으로 남겨야 합니다.");

        var activity = Single(signals.GetActivities(), "fault Activity");
        Equal(ActivityStatusCode.Error, activity.Status, "기술 장애 Activity는 Error여야 합니다.");
        Equal("faulted", activity.Tags["conversion.outcome"], "faulted outcome이 필요합니다.");
        Equal(expectedExceptionType, activity.Tags["error.type"],
            "trace에는 예외 형식만 기록해야 합니다.");

        var observableText = BuildObservableText(logs, new[] { activity }, signals.GetMeasurements());
        True(!observableText.Contains(secretExceptionMessage, StringComparison.Ordinal),
            "예외 Message의 민감정보가 관측 신호에 노출되면 안 됩니다.");
    }

    /// <summary>
    /// logger·metric·Activity listener가 예외를 던져도 업무 결과와 ambient trace 부모가 보존되는지 검사합니다.
    /// 파라미터와 반환값은 없으며, 관측 장애가 업무 의미나 다음 trace를 바꾸면 assertion 예외로 실패를 알립니다.
    /// </summary>
    /// <returns>성공 경로와 예외 경로의 fail-open 검증이 끝날 때 완료되는 Task입니다.</returns>
    private static async Task TelemetryFailureDoesNotChangeBusinessOutcomeAsync()
    {
        var request = CreateRequest("provider-failure", "private source", "plain");
        var receipt = new ConversionReceipt(request.JobId, request.TargetFormat, 17);
        var instrumentationName = CreateInstrumentationName("provider-failure");

        // 정상 listener를 먼저 등록해 각 독립 기록 시도가 도달했는지 관찰하고, 뒤의 listener가 예외를 던지게 합니다.
        // 선언의 역순으로 Dispose되므로 telemetry가 먼저 닫힌 뒤 두 전역 listener가 해제됩니다.
        using var healthySignals = new SignalCollector(instrumentationName);
        using var throwingSignals = new ThrowingMetricListener(instrumentationName);
        using var telemetry = new ConversionTelemetry(instrumentationName);
        var throwingLogger = new ThrowingLogger<ObservedConversionWorkflow>();

        var successfulInner = new StubWorkflow(
            (_, _) => Task.FromResult(OperationResult.Success(receipt)));
        var successfulDecorator = new ObservedConversionWorkflow(
            successfulInner,
            telemetry,
            throwingLogger);

        var result = await successfulDecorator
            .ExecuteAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        True(result.IsSuccess, "관측 실패가 업무 성공을 실패로 바꾸면 안 됩니다.");
        Equal(receipt, result.Value, "관측 실패 뒤에도 안쪽 성공값을 그대로 반환해야 합니다.");
        Equal(1, successfulInner.CallCount, "관측 실패 뒤에도 안쪽 Workflow를 정확히 한 번 호출해야 합니다.");
        Equal(1, healthySignals.GetActivities().Length,
            "metric 수집기 실패가 독립적인 Activity 생성을 막으면 안 됩니다.");
        True(healthySignals.GetMeasurements().Any(measurement =>
                measurement.InstrumentName == ConversionTelemetry.DurationMetricName),
            "completion counter 수집 실패 뒤에도 duration 기록을 독립적으로 시도해야 합니다.");

        var originalException = new InvalidOperationException("원래 업무 예외");
        var faultingInner = new StubWorkflow(
            (_, _) => Task.FromException<OperationResult<ConversionReceipt>>(originalException));
        var faultingDecorator = new ObservedConversionWorkflow(
            faultingInner,
            telemetry,
            throwingLogger);

        Exception? caughtException = null;
        try
        {
            _ = await faultingDecorator
                .ExecuteAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            caughtException = exception;
        }

        True(ReferenceEquals(originalException, caughtException),
            "관측 실패가 원래 업무 예외를 가리거나 다른 예외로 바꾸면 안 됩니다.");
        Equal(2, healthySignals.GetActivities().Length,
            "성공과 예외 경로 모두 metric 장애와 독립적으로 Activity를 끝내야 합니다.");

        var startFailureName = CreateInstrumentationName("activity-start-failure");
        using (var startFailureListener = new ThrowingActivityListener(
                   startFailureName,
                   throwWhenStarted: true,
                   throwWhenStopped: false))
        using (var startFailureTelemetry = new ConversionTelemetry(startFailureName))
        using (var startParent = new Activity("self-test.parent.start").Start())
        {
            var startFailureInner = new StubWorkflow(
                (_, _) => Task.FromResult(OperationResult.Success(receipt)));
            var startFailureDecorator = new ObservedConversionWorkflow(
                startFailureInner,
                startFailureTelemetry,
                new CollectingLogger<ObservedConversionWorkflow>());

            var startFailureResult = await startFailureDecorator
                .ExecuteAsync(request, CancellationToken.None)
                .ConfigureAwait(false);

            True(startFailureResult.IsSuccess,
                "Activity 시작 callback 실패가 업무 성공을 바꾸면 안 됩니다.");
            Equal(1, startFailureListener.StartCallbackCount,
                "ActivityStarted 실패 callback이 실제로 한 번 실행되어야 합니다.");
            True(ReferenceEquals(startParent, Activity.Current),
                "Activity 시작 callback 실패 뒤 기존 ambient 부모를 복원해야 합니다.");
        }

        var stopFailureName = CreateInstrumentationName("activity-stop-failure");
        using (var stopFailureListener = new ThrowingActivityListener(
                   stopFailureName,
                   throwWhenStarted: false,
                   throwWhenStopped: true))
        using (var stopFailureTelemetry = new ConversionTelemetry(stopFailureName))
        using (var stopParent = new Activity("self-test.parent.stop").Start())
        {
            var stopFailureInner = new StubWorkflow(
                (_, _) => Task.FromResult(OperationResult.Success(receipt)));
            var stopFailureDecorator = new ObservedConversionWorkflow(
                stopFailureInner,
                stopFailureTelemetry,
                new CollectingLogger<ObservedConversionWorkflow>());

            var stopFailureResult = await stopFailureDecorator
                .ExecuteAsync(request, CancellationToken.None)
                .ConfigureAwait(false);

            True(stopFailureResult.IsSuccess,
                "Activity 종료 callback 실패가 업무 성공을 바꾸면 안 됩니다.");
            Equal(1, stopFailureListener.StopCallbackCount,
                "ActivityStopped 실패 callback이 실제로 한 번 실행되어야 합니다.");
            True(ReferenceEquals(stopParent, Activity.Current),
                "Activity 종료 callback 실패 뒤 기존 ambient 부모를 복원해야 합니다.");
        }
    }

    /// <summary>
    /// 반복되는 테스트용 입력 생성을 한곳에 모으고 성공 값을 꺼냅니다.
    /// jobId·source·target은 원시 입력이며, 검증된 ConversionRequest를 반환합니다.
    /// </summary>
    /// <param name="jobId">테스트에서 사용할 작업 ID입니다.</param>
    /// <param name="source">테스트에서 사용할 원문입니다.</param>
    /// <param name="target">테스트에서 사용할 출력 형식입니다.</param>
    /// <returns>검증 성공이 보장된 요청입니다.</returns>
    private static ConversionRequest CreateRequest(string jobId, string source, string target)
    {
        var result = ConversionRequest.Create(jobId, source, target);
        True(result.IsSuccess, $"테스트 준비 입력이 유효해야 합니다: {result.ErrorOrEmpty()}");
        return result.Value;
    }

    /// <summary>
    /// 동시에 실행될 listener들이 서로의 신호를 받지 않도록 테스트별 고유 생산자 이름을 만듭니다.
    /// suffix는 사람이 읽는 사례 이름이며, 충돌하지 않는 instrumentation 이름을 반환합니다.
    /// </summary>
    /// <param name="suffix">success, reject처럼 테스트 목적을 나타내는 짧은 이름입니다.</param>
    /// <returns>GUID가 포함된 고유 instrumentation 이름입니다.</returns>
    private static string CreateInstrumentationName(string suffix)
    {
        return $"{ConversionTelemetry.DefaultInstrumentationName}.{suffix}.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 수집한 log·activity·metric의 key와 value를 하나의 감사 문자열로 합칩니다.
    /// 세 목록은 관측 결과이며, 민감 문자열 포함 여부를 검사할 결합 문자열을 반환합니다.
    /// </summary>
    /// <param name="logs">구조화 로그 snapshot입니다.</param>
    /// <param name="activities">종료된 Activity snapshot입니다.</param>
    /// <param name="measurements">metric 측정 snapshot입니다.</param>
    /// <returns>모든 signal의 공개된 텍스트를 합친 문자열입니다.</returns>
    private static string BuildObservableText(
        IReadOnlyList<CapturedLog> logs,
        IReadOnlyList<ActivitySnapshot> activities,
        IReadOnlyList<MetricSnapshot> measurements)
    {
        var logText = logs.SelectMany(entry =>
            new[]
            {
                entry.Level.ToString(),
                entry.EventId.Id.ToString(CultureInfo.InvariantCulture),
                entry.Message,
            }
                .Concat(entry.State.Select(pair => $"{pair.Key}={pair.Value}")));
        var activityText = activities.SelectMany(activity =>
            new[] { activity.Name, activity.TraceId, activity.Status.ToString() }
                .Concat(activity.Tags.Select(pair => $"{pair.Key}={pair.Value}")));
        var metricText = measurements.SelectMany(measurement =>
            new[]
            {
                measurement.InstrumentName,
                measurement.Value.ToString(CultureInfo.InvariantCulture),
            }
                .Concat(measurement.Tags.Select(pair => $"{pair.Key}={pair.Value}")));

        return string.Join("|", logText.Concat(activityText).Concat(metricText));
    }

    /// <summary>
    /// 목록에 정확히 한 항목이 있는지 검사하고 그 항목을 돌려줍니다.
    /// items는 확인할 목록, description은 실패 설명이며, 유일한 항목을 반환합니다.
    /// </summary>
    /// <typeparam name="T">목록 항목의 형식입니다.</typeparam>
    /// <param name="items">정확히 한 항목이어야 하는 읽기 전용 목록입니다.</param>
    /// <param name="description">assertion 실패 메시지에 넣을 대상 이름입니다.</param>
    /// <returns>목록의 유일한 항목입니다.</returns>
    private static T Single<T>(IReadOnlyList<T> items, string description)
    {
        Equal(1, items.Count, $"{description}는 정확히 하나여야 합니다.");
        return items[0];
    }

    /// <summary>
    /// 조건이 true인지 검사합니다.
    /// condition은 검사 결과, message는 실패 이유이며 반환값은 없고 false일 때 예외를 던집니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 조건입니다.</param>
    /// <param name="message">조건이 false일 때 보여 줄 설명입니다.</param>
    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// expected와 actual이 기본 동등성 규칙으로 같은지 검사합니다.
    /// 두 값과 실패 설명을 받고 반환값은 없으며, 다르면 구체적인 값을 포함한 예외를 던집니다.
    /// </summary>
    /// <typeparam name="T">비교할 두 값의 공통 형식입니다.</typeparam>
    /// <param name="expected">기대하는 값입니다.</param>
    /// <param name="actual">실제로 얻은 값입니다.</param>
    /// <param name="message">값이 다를 때 보여 줄 설명입니다.</param>
    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected={expected}, Actual={actual}");
        }
    }

    /// <summary>
    /// 비동기 action이 지정한 예외 형식을 던지는지 검사합니다.
    /// action은 실행할 함수, message는 실패 설명이며, 기대한 예외가 나오면 정상 완료 Task를 반환합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 전파되어야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외가 발생해야 하는 비동기 작업입니다.</param>
    /// <param name="message">예외가 없거나 다른 예외일 때 보여 줄 설명입니다.</param>
    /// <returns>기대한 예외를 확인한 뒤 완료되는 Task입니다.</returns>
    private static async Task ThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{message} Expected={typeof(TException).Name}, Actual={exception.GetType().Name}",
                exception);
        }

        throw new InvalidOperationException(
            $"{message} Expected={typeof(TException).Name}, Actual=no exception");
    }

    /// <summary>
    /// Converter Port의 성공값과 실패를 테스트가 직접 정하도록 감싸는 test double입니다.
    /// </summary>
    private sealed class StubDocumentConverter : IDocumentConverter
    {
        private readonly Func<
            ConversionRequest,
            CancellationToken,
            Task<OperationResult<ConvertedDocument>>> _handler;

        /// <summary>
        /// 변환 호출 때 실행할 handler를 받습니다.
        /// handler는 요청과 토큰을 받아 문서 Result Task를 만들며 Stub 객체를 초기화합니다.
        /// </summary>
        /// <param name="handler">각 테스트가 정한 Converter 동작을 수행할 delegate입니다.</param>
        public StubDocumentConverter(
            Func<
                ConversionRequest,
                CancellationToken,
                Task<OperationResult<ConvertedDocument>>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 요청과 취소 토큰을 테스트가 주입한 handler에 그대로 전달합니다.
        /// 두 파라미터는 실제 Port 계약과 같으며 handler가 만든 문서 Result Task를 반환합니다.
        /// </summary>
        /// <param name="request">Application Service가 전달한 검증된 요청입니다.</param>
        /// <param name="cancellationToken">Application Service가 전달한 취소 신호입니다.</param>
        /// <returns>테스트가 준비한 변환 성공 또는 예상 실패입니다.</returns>
        public Task<OperationResult<ConvertedDocument>> ConvertAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    /// <summary>
    /// 테스트 double이 원하는 성공·실패·예외·취소를 반환하도록 감싸는 Workflow입니다.
    /// </summary>
    private sealed class StubWorkflow : IConversionWorkflow
    {
        private readonly Func<
            ConversionRequest,
            CancellationToken,
            Task<OperationResult<ConversionReceipt>>> _handler;

        /// <summary>
        /// 호출될 때 실행할 handler를 받습니다.
        /// handler는 요청과 토큰을 받아 Result Task를 만드는 함수이며, Stub 객체를 초기화합니다.
        /// </summary>
        /// <param name="handler">각 테스트가 정한 동작을 수행할 delegate입니다.</param>
        public StubWorkflow(
            Func<
                ConversionRequest,
                CancellationToken,
                Task<OperationResult<ConversionReceipt>>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public int CallCount { get; private set; }

        /// <summary>
        /// 호출 횟수를 세고 테스트가 주입한 handler에 제어를 넘깁니다.
        /// request와 cancellationToken은 실제 계약과 같으며 handler가 만든 Result Task를 반환합니다.
        /// </summary>
        /// <param name="request">Decorator가 전달한 변환 요청입니다.</param>
        /// <param name="cancellationToken">Decorator가 전달한 취소 신호입니다.</param>
        /// <returns>테스트 사례가 준비한 성공, 실패, 취소 또는 예외 Task입니다.</returns>
        public Task<OperationResult<ConversionReceipt>> ExecuteAsync(
            ConversionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _handler(request, cancellationToken);
        }
    }

    /// <summary>
    /// ILogger가 받은 렌더링 메시지와 구조화 state를 메모리에 모으는 테스트 logger입니다.
    /// </summary>
    /// <typeparam name="T">실제 ILogger 카테고리와 같은 형식입니다.</typeparam>
    private sealed class CollectingLogger<T> : ILogger<T>
    {
        private readonly List<CapturedLog> _entries = new();
        private readonly object _gate = new();

        /// <summary>
        /// 이 예제에서는 scope 자체를 검증하지 않으므로 아무 일도 하지 않는 disposable을 돌려줍니다.
        /// state는 scope 메타데이터이며, 반환값은 using 문에서 안전하게 정리할 빈 객체입니다.
        /// </summary>
        /// <typeparam name="TState">호출자가 scope에 넣은 상태 형식입니다.</typeparam>
        /// <param name="state">이번 테스트에서는 사용하지 않는 scope 상태입니다.</param>
        /// <returns>Dispose해도 부작용이 없는 빈 scope입니다.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoopScope.Instance;
        }

        /// <summary>
        /// 모든 로그 레벨을 수집하도록 true를 반환합니다.
        /// logLevel은 질문받은 수준이며, 테스트가 전체 신호를 봐야 하므로 항상 true를 반환합니다.
        /// </summary>
        /// <param name="logLevel">호출자가 기록하려는 로그 수준입니다.</param>
        /// <returns>항상 true입니다.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// ILogger 호출의 level, event ID, 렌더링 메시지, 구조화 state를 snapshot으로 저장합니다.
        /// exception은 이 실습 logger에서 문자열로 만들지 않아 민감 Message 노출을 피하고, 반환값은 없습니다.
        /// </summary>
        /// <typeparam name="TState">LoggerMessage source generator가 만든 구조화 state 형식입니다.</typeparam>
        /// <param name="logLevel">로그 심각도입니다.</param>
        /// <param name="eventId">안정된 이벤트 번호입니다.</param>
        /// <param name="state">템플릿 속성이 들어 있는 구조화 상태입니다.</param>
        /// <param name="exception">logger에 함께 전달될 수 있는 예외입니다.</param>
        /// <param name="formatter">state와 exception을 사람이 읽는 문자열로 바꾸는 함수입니다.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var structuredState = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            var entry = new CapturedLog(
                logLevel,
                eventId,
                formatter(state, exception),
                structuredState);

            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        /// <summary>
        /// 현재까지 받은 로그를 복사해 반환합니다.
        /// 파라미터는 없고 호출 이후 내부 변경과 분리된 읽기 전용 snapshot을 반환합니다.
        /// </summary>
        /// <returns>수집 순서를 보존한 로그 배열입니다.</returns>
        public CapturedLog[] GetEntries()
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>
    /// 모든 기록 시도에서 예외를 던져 logger 공급자 장애를 재현하는 테스트 double입니다.
    /// </summary>
    /// <typeparam name="T">실제 Decorator가 요청하는 logger 카테고리 형식입니다.</typeparam>
    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        /// <summary>
        /// scope 생성은 이번 장애 시나리오의 대상이 아니므로 빈 disposable을 반환합니다.
        /// state는 사용하지 않는 scope 값이며, 안전하게 정리할 빈 scope를 반환합니다.
        /// </summary>
        /// <typeparam name="TState">호출자가 scope에 넣은 상태 형식입니다.</typeparam>
        /// <param name="state">이번 테스트에서는 사용하지 않는 scope 상태입니다.</param>
        /// <returns>Dispose해도 부작용이 없는 빈 scope입니다.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoopScope.Instance;
        }

        /// <summary>
        /// 모든 수준의 로그가 실제 Log 메서드까지 도달하도록 true를 반환합니다.
        /// logLevel은 질문받은 심각도이며 항상 true를 반환합니다.
        /// </summary>
        /// <param name="logLevel">호출자가 기록하려는 로그 수준입니다.</param>
        /// <returns>logger 장애 경로를 실행하기 위해 항상 true입니다.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// logger 공급자가 실패하는 상황을 재현하기 위해 의도적으로 예외를 던집니다.
        /// 모든 파라미터는 ILogger 계약을 맞추기 위한 값이며 정상 반환하지 않습니다.
        /// </summary>
        /// <typeparam name="TState">구조화 로그 상태의 형식입니다.</typeparam>
        /// <param name="logLevel">기록하려던 로그 수준입니다.</param>
        /// <param name="eventId">기록하려던 안정된 이벤트 번호입니다.</param>
        /// <param name="state">기록하려던 구조화 상태입니다.</param>
        /// <param name="exception">함께 전달된 예외입니다.</param>
        /// <param name="formatter">메시지를 만들 formatter입니다.</param>
        /// <exception cref="InvalidOperationException">테스트용 logger 장애를 항상 나타냅니다.</exception>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("테스트용 logger 공급자 장애");
        }
    }

    /// <summary>
    /// Activity 시작 또는 종료 observer callback의 예외를 선택적으로 재현하는 test listener입니다.
    /// </summary>
    private sealed class ThrowingActivityListener : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly bool _throwWhenStarted;
        private readonly bool _throwWhenStopped;

        /// <summary>
        /// 지정한 ActivitySource만 구독하고 선택한 lifecycle callback에서 예외를 던지도록 구성합니다.
        /// instrumentationName은 구독 경계, 두 bool은 시작·종료 실패 선택이며 listener 등록을 완료합니다.
        /// </summary>
        /// <param name="instrumentationName">이 테스트가 소유한 ActivitySource 이름입니다.</param>
        /// <param name="throwWhenStarted">ActivityStarted callback에서 예외를 던질지 여부입니다.</param>
        /// <param name="throwWhenStopped">ActivityStopped callback에서 예외를 던질지 여부입니다.</param>
        public ThrowingActivityListener(
            string instrumentationName,
            bool throwWhenStarted,
            bool throwWhenStopped)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instrumentationName);

            _throwWhenStarted = throwWhenStarted;
            _throwWhenStopped = throwWhenStopped;
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == instrumentationName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = OnActivityStarted,
                ActivityStopped = OnActivityStopped,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public int StartCallbackCount { get; private set; }

        public int StopCallbackCount { get; private set; }

        /// <summary>
        /// 시작 callback 호출 횟수를 세고 설정된 경우 테스트용 예외를 던집니다.
        /// activity는 방금 시작된 span이며 정상 모드에서는 반환값 없이 끝납니다.
        /// </summary>
        /// <param name="activity">ActivitySource가 시작한 문서 변환 Activity입니다.</param>
        /// <exception cref="InvalidOperationException">시작 실패 모드에서 observer 장애를 나타냅니다.</exception>
        private void OnActivityStarted(Activity activity)
        {
            StartCallbackCount++;

            if (_throwWhenStarted)
            {
                throw new InvalidOperationException("테스트용 ActivityStarted observer 장애");
            }
        }

        /// <summary>
        /// 종료 callback 호출 횟수를 세고 설정된 경우 테스트용 예외를 던집니다.
        /// activity는 방금 끝난 span이며 정상 모드에서는 반환값 없이 끝납니다.
        /// </summary>
        /// <param name="activity">Dispose로 종료 중인 문서 변환 Activity입니다.</param>
        /// <exception cref="InvalidOperationException">종료 실패 모드에서 observer 장애를 나타냅니다.</exception>
        private void OnActivityStopped(Activity activity)
        {
            StopCallbackCount++;

            if (_throwWhenStopped)
            {
                throw new InvalidOperationException("테스트용 ActivityStopped observer 장애");
            }
        }

        /// <summary>
        /// 전역 ActivityListener 등록을 해제해 다음 테스트의 trace와 섞이지 않게 합니다.
        /// 파라미터와 반환값은 없으며 using 블록이 끝날 때 호출됩니다.
        /// </summary>
        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    /// <summary>
    /// 구독한 metric 측정 callback에서 예외를 던져 수집기 장애를 재현합니다.
    /// </summary>
    private sealed class ThrowingMetricListener : IDisposable
    {
        private readonly MeterListener _listener;

        /// <summary>
        /// 지정한 Meter만 구독하고 long·double 측정 모두에 실패 callback을 연결합니다.
        /// instrumentationName은 격리된 생산자 이름이며 listener 등록과 수집 시작을 완료합니다.
        /// </summary>
        /// <param name="instrumentationName">이 테스트의 ConversionTelemetry가 사용할 고유 이름입니다.</param>
        public ThrowingMetricListener(string instrumentationName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instrumentationName);

            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == instrumentationName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>(ThrowLongMeasurement);
            _listener.SetMeasurementEventCallback<double>(ThrowDoubleMeasurement);
            _listener.Start();
        }

        /// <summary>
        /// counter 측정 수집기가 실패하는 상황을 재현해 예외를 던집니다.
        /// instrument·measurement·tags·state는 callback 계약용 값이며 정상 반환하지 않습니다.
        /// </summary>
        /// <param name="instrument">측정을 발행한 counter입니다.</param>
        /// <param name="measurement">발행된 정수 측정값입니다.</param>
        /// <param name="tags">측정에 붙은 분류 태그입니다.</param>
        /// <param name="state">listener가 선택적으로 연결한 상태입니다.</param>
        /// <exception cref="InvalidOperationException">테스트용 metric 수집기 장애를 항상 나타냅니다.</exception>
        private static void ThrowLongMeasurement(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            throw new InvalidOperationException("테스트용 long metric 수집기 장애");
        }

        /// <summary>
        /// histogram 측정 수집기가 실패하는 상황을 재현해 예외를 던집니다.
        /// instrument·measurement·tags·state는 callback 계약용 값이며 정상 반환하지 않습니다.
        /// </summary>
        /// <param name="instrument">측정을 발행한 histogram입니다.</param>
        /// <param name="measurement">발행된 실수 측정값입니다.</param>
        /// <param name="tags">측정에 붙은 분류 태그입니다.</param>
        /// <param name="state">listener가 선택적으로 연결한 상태입니다.</param>
        /// <exception cref="InvalidOperationException">테스트용 metric 수집기 장애를 항상 나타냅니다.</exception>
        private static void ThrowDoubleMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            throw new InvalidOperationException("테스트용 double metric 수집기 장애");
        }

        /// <summary>
        /// 전역 MeterListener 등록을 해제해 다른 테스트의 측정과 섞이지 않게 합니다.
        /// 파라미터와 반환값은 없으며 using 블록이 끝날 때 호출됩니다.
        /// </summary>
        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    /// <summary>
    /// ActivityListener와 MeterListener를 함께 등록해 테스트 대상 signal만 메모리에 모읍니다.
    /// </summary>
    private sealed class SignalCollector : IDisposable
    {
        private readonly ActivityListener _activityListener;
        private readonly MeterListener _meterListener;
        private readonly List<ActivitySnapshot> _activities = new();
        private readonly List<MetricSnapshot> _measurements = new();
        private readonly object _gate = new();

        /// <summary>
        /// instrumentationName과 정확히 같은 ActivitySource·Meter만 구독합니다.
        /// 이름은 테스트 격리 경계이며, listener 두 개를 등록하고 측정 수신을 시작합니다.
        /// </summary>
        /// <param name="instrumentationName">이 테스트가 소유한 고유 생산자 이름입니다.</param>
        public SignalCollector(string instrumentationName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instrumentationName);

            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == instrumentationName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = CaptureActivity,
            };
            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == instrumentationName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _meterListener.SetMeasurementEventCallback<long>(CaptureLongMeasurement);
            _meterListener.SetMeasurementEventCallback<double>(CaptureDoubleMeasurement);
            _meterListener.Start();
        }

        /// <summary>
        /// 끝난 Activity의 이름, 상태, 태그를 복사해 저장합니다.
        /// activity는 listener가 전달한 작업이며 반환값은 없습니다.
        /// </summary>
        /// <param name="activity">Dispose되어 종료된 문서 변환 Activity입니다.</param>
        private void CaptureActivity(Activity activity)
        {
            var tags = activity.TagObjects
                .ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.Ordinal);
            var snapshot = new ActivitySnapshot(
                activity.OperationName,
                activity.TraceId.ToString(),
                activity.Status,
                tags);

            lock (_gate)
            {
                _activities.Add(snapshot);
            }
        }

        /// <summary>
        /// long counter 측정을 double 공통 snapshot으로 바꾸어 저장합니다.
        /// instrument는 metric 정의, measurement는 값, tags는 분류, state는 사용하지 않는 listener 상태이며 반환값은 없습니다.
        /// </summary>
        /// <param name="instrument">값을 만든 counter입니다.</param>
        /// <param name="measurement">이번에 더해진 정수 값입니다.</param>
        /// <param name="tags">이번 측정의 낮은 카디널리티 태그입니다.</param>
        /// <param name="state">EnableMeasurementEvents에서 선택적으로 전달할 수 있는 상태입니다.</param>
        private void CaptureLongMeasurement(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            AddMeasurement(instrument.Name, measurement, tags);
        }

        /// <summary>
        /// double histogram 측정을 공통 snapshot으로 저장합니다.
        /// instrument는 metric 정의, measurement는 시간 값, tags는 분류, state는 미사용 상태이며 반환값은 없습니다.
        /// </summary>
        /// <param name="instrument">값을 만든 histogram입니다.</param>
        /// <param name="measurement">밀리초 단위 처리 시간입니다.</param>
        /// <param name="tags">이번 측정의 낮은 카디널리티 태그입니다.</param>
        /// <param name="state">EnableMeasurementEvents에서 선택적으로 전달할 수 있는 상태입니다.</param>
        private void CaptureDoubleMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            AddMeasurement(instrument.Name, measurement, tags);
        }

        /// <summary>
        /// span 태그를 독립 Dictionary로 복사하고 metric snapshot을 저장합니다.
        /// instrumentName·value·tags는 한 측정을 구성하며 반환값은 없습니다.
        /// callback이 끝난 뒤 span을 보관할 수 없으므로 즉시 복사해야 합니다.
        /// </summary>
        /// <param name="instrumentName">counter 또는 histogram의 안정된 이름입니다.</param>
        /// <param name="value">수집된 숫자 값입니다.</param>
        /// <param name="tags">callback 수명 동안만 유효한 태그 span입니다.</param>
        private void AddMeasurement(
            string instrumentName,
            double value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copiedTags = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                copiedTags[tag.Key] = tag.Value;
            }

            var snapshot = new MetricSnapshot(instrumentName, value, copiedTags);
            lock (_gate)
            {
                _measurements.Add(snapshot);
            }
        }

        /// <summary>
        /// 현재까지 종료된 Activity를 복사해 반환합니다.
        /// 파라미터는 없고 수집 순서를 보존한 snapshot을 반환합니다.
        /// </summary>
        /// <returns>종료된 Activity snapshot 배열입니다.</returns>
        public ActivitySnapshot[] GetActivities()
        {
            lock (_gate)
            {
                return _activities.ToArray();
            }
        }

        /// <summary>
        /// 현재까지 받은 metric 측정을 복사해 반환합니다.
        /// 파라미터는 없고 수집 순서를 보존한 snapshot을 반환합니다.
        /// </summary>
        /// <returns>counter와 histogram 측정 snapshot 배열입니다.</returns>
        public MetricSnapshot[] GetMeasurements()
        {
            lock (_gate)
            {
                return _measurements.ToArray();
            }
        }

        /// <summary>
        /// 두 listener의 전역 등록을 해제해 다음 테스트와 신호가 섞이지 않게 합니다.
        /// 파라미터와 반환값은 없으며 테스트가 끝날 때 using이 자동 호출합니다.
        /// </summary>
        public void Dispose()
        {
            _meterListener.Dispose();
            _activityListener.Dispose();
        }
    }

    /// <summary>
    /// BeginScope 계약을 만족하기 위한 부작용 없는 disposable singleton입니다.
    /// </summary>
    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        /// <summary>
        /// 외부 생성을 막고 하나의 빈 scope를 재사용합니다.
        /// 파라미터와 반환값은 없으며 singleton 객체를 초기화합니다.
        /// </summary>
        private NoopScope()
        {
        }

        /// <summary>
        /// 빈 scope에는 정리할 자원이 없으므로 아무 일도 하지 않습니다.
        /// 파라미터와 반환값은 없습니다.
        /// </summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// logger가 공개한 한 이벤트의 읽기 전용 snapshot입니다.
    /// </summary>
    private sealed record CapturedLog(
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> State);

    /// <summary>
    /// 종료된 Activity의 검증에 필요한 최소 정보입니다.
    /// </summary>
    private sealed record ActivitySnapshot(
        string Name,
        string TraceId,
        ActivityStatusCode Status,
        IReadOnlyDictionary<string, string?> Tags);

    /// <summary>
    /// counter와 histogram을 같은 방식으로 검증하기 위한 metric snapshot입니다.
    /// </summary>
    private sealed record MetricSnapshot(
        string InstrumentName,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);
}

/// <summary>
/// 실패 Result의 오류를 테스트 준비 메시지에서 안전하게 표시하는 작은 확장 메서드입니다.
/// </summary>
internal static class OperationResultTestExtensions
{
    /// <summary>
    /// 실패 Result면 코드와 설명을, 성공 Result면 빈 문자열을 반환합니다.
    /// result는 검사할 값이며 테스트 준비가 실패했을 때만 진단 문구로 사용합니다.
    /// </summary>
    /// <typeparam name="T">Result 성공 값의 null이 아닌 형식입니다.</typeparam>
    /// <param name="result">성공 또는 실패 상태의 Result입니다.</param>
    /// <returns>실패 진단 문자열 또는 빈 문자열입니다.</returns>
    public static string ErrorOrEmpty<T>(this OperationResult<T> result)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? string.Empty : $"{result.Error.Code}: {result.Error.Message}";
    }
}
