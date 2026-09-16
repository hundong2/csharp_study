using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace DocumentObservabilityExercise;

/// <summary>
/// 이 예제가 내보내는 trace와 metric 도구를 한 수명으로 묶습니다.
/// 실제 Generic Host에서는 ActivitySource를 singleton으로, Meter는 IMeterFactory로 관리하는 방식을 권장합니다.
/// </summary>
public sealed class ConversionTelemetry : IDisposable
{
    public const string DefaultInstrumentationName = "CSharpStudy.DocumentConversion";
    public const string ActivityName = "document.convert";
    public const string AttemptMetricName = "csharpstudy.document_conversion.attempts";
    public const string CompletionMetricName = "csharpstudy.document_conversion.completions";
    public const string DurationMetricName = "csharpstudy.document_conversion.duration";

    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _attempts;
    private readonly Counter<long> _completions;
    private readonly Histogram<double> _duration;

    /// <summary>
    /// 하나의 이름으로 ActivitySource와 Meter를 만들고 세 metric instrument를 준비합니다.
    /// instrumentationName은 수집기가 구독할 고유 이름이며, 완성된 telemetry 묶음을 초기화합니다.
    /// 선택 파라미터는 생략하면 학습 예제의 고정 이름을 사용한다는 뜻입니다.
    /// </summary>
    /// <param name="instrumentationName">trace와 metric 생산자를 구분하는 고유 이름입니다.</param>
    public ConversionTelemetry(string instrumentationName = DefaultInstrumentationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentationName);

        InstrumentationName = instrumentationName;
        _activitySource = new ActivitySource(instrumentationName);
        _meter = new Meter(instrumentationName);

        // Counter는 누적 건수처럼 절대 줄지 않는 측정에 사용합니다.
        _attempts = _meter.CreateCounter<long>(
            AttemptMetricName,
            unit: "{conversion}",
            description: "시작한 문서 변환 수");

        _completions = _meter.CreateCounter<long>(
            CompletionMetricName,
            unit: "{conversion}",
            description: "outcome별로 끝난 문서 변환 수");

        // Histogram은 개별 처리 시간을 기록하고 수집기가 분포와 백분위수를 계산하게 합니다.
        _duration = _meter.CreateHistogram<double>(
            DurationMetricName,
            unit: "ms",
            description: "문서 변환 전체 처리 시간");
    }

    public string InstrumentationName { get; }

    /// <summary>
    /// 문서 변환 시도 counter를 1 올립니다.
    /// targetFormat은 값 종류가 제한된 출력 형식이며 반환값은 없습니다.
    /// Activity 시작과 경계를 분리해 metric listener 장애가 trace 생성을 막지 않게 합니다.
    /// </summary>
    /// <param name="targetFormat">plain 또는 upper 중 하나인 낮은 카디널리티 출력 형식입니다.</param>
    internal void RecordAttempt(string targetFormat)
    {
        ValidateTargetFormat(targetFormat);

        var metricTags = new TagList
        {
            { "conversion.target_format", targetFormat },
        };

        _attempts.Add(1, metricTags);
    }

    /// <summary>
    /// 문서 변환 Activity를 시작하고 정책상 승인된 correlation 메타데이터를 붙입니다.
    /// request는 구문 검증과 상위 개인정보 정책을 통과했다고 가정한 입력이며,
    /// listener가 있으면 Activity를, 없으면 null을 반환합니다.
    /// </summary>
    /// <param name="request">추적할 변환 요청입니다. JobId는 승인된 불투명 키여야 하며 SourceText는 복사하지 않습니다.</param>
    /// <returns>수집기가 구독 중이면 시작된 Activity, 아니면 null입니다.</returns>
    internal Activity? StartConversion(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ActivitySource는 수집 listener가 없을 때 null을 반환해 불필요한 객체 생성을 피합니다.
        // `?`는 Activity가 null일 때 SetTag 호출을 건너뛰는 null 조건부 연산자입니다.
        var activity = _activitySource.StartActivity(ActivityName, ActivityKind.Internal);
        activity?.SetTag("conversion.job_id", request.JobId);
        activity?.SetTag("conversion.target_format", request.TargetFormat);

        return activity;
    }

    /// <summary>
    /// 변환의 최종 outcome을 completion counter에 기록합니다.
    /// targetFormat과 outcome은 작은 고정 집합이며 반환값은 없습니다.
    /// duration과 경계를 분리해 counter 수집기 장애가 histogram 기록 시도를 막지 않게 합니다.
    /// </summary>
    /// <param name="targetFormat">plain 또는 upper 중 하나인 출력 형식입니다.</param>
    /// <param name="outcome">succeeded, rejected, canceled, faulted 중 하나인 종료 분류입니다.</param>
    internal void RecordCompletion(
        string targetFormat,
        string outcome)
    {
        ValidateMetricDimensions(targetFormat, outcome);

        var metricTags = new TagList
        {
            { "conversion.target_format", targetFormat },
            { "conversion.outcome", outcome },
        };

        _completions.Add(1, metricTags);
    }

    /// <summary>
    /// 변환의 최종 outcome과 전체 처리 시간을 duration histogram에 기록합니다.
    /// targetFormat과 outcome은 제한된 분류, elapsedMilliseconds는 경과 시간이며 반환값은 없습니다.
    /// </summary>
    /// <param name="targetFormat">plain 또는 upper 중 하나인 출력 형식입니다.</param>
    /// <param name="outcome">succeeded, rejected, canceled, faulted 중 하나인 종료 분류입니다.</param>
    /// <param name="elapsedMilliseconds">Stopwatch로 측정한 0 이상의 전체 처리 시간입니다.</param>
    internal void RecordDuration(
        string targetFormat,
        string outcome,
        double elapsedMilliseconds)
    {
        ValidateMetricDimensions(targetFormat, outcome);

        if (elapsedMilliseconds < 0 || !double.IsFinite(elapsedMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedMilliseconds),
                "처리 시간은 0 이상의 유한한 값이어야 합니다.");
        }

        var metricTags = new TagList
        {
            { "conversion.target_format", targetFormat },
            { "conversion.outcome", outcome },
        };

        _duration.Record(elapsedMilliseconds, metricTags);
    }

    /// <summary>
    /// metric에 사용할 target format과 outcome이 고정 허용 목록에 속하는지 검사합니다.
    /// 두 문자열은 metric 차원을 구성하며 반환값은 없고, 허용 목록 밖이면 예외를 던집니다.
    /// </summary>
    /// <param name="targetFormat">plain 또는 upper 중 하나여야 하는 출력 형식입니다.</param>
    /// <param name="outcome">네 종료 분류 중 하나여야 하는 결과입니다.</param>
    private static void ValidateMetricDimensions(string targetFormat, string outcome)
    {
        ValidateTargetFormat(targetFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        if (outcome is not ("succeeded" or "rejected" or "canceled" or "faulted"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                "종료 분류가 고정된 허용 목록에 없습니다.");
        }
    }

    /// <summary>
    /// metric과 trace에 사용할 출력 형식이 고정 허용 목록에 속하는지 검사합니다.
    /// targetFormat은 검사할 문자열이며 반환값은 없고, plain·upper가 아니면 예외를 던집니다.
    /// </summary>
    /// <param name="targetFormat">검사할 출력 형식입니다.</param>
    private static void ValidateTargetFormat(string targetFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFormat);

        if (targetFormat is not ("plain" or "upper"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFormat),
                "출력 형식은 plain 또는 upper여야 합니다.");
        }
    }

    /// <summary>
    /// ActivitySource와 Meter가 가진 수집 연결 자원을 정리합니다.
    /// 파라미터와 반환값은 없으며 Composition Root가 애플리케이션 종료 시 한 번 호출합니다.
    /// </summary>
    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}

/// <summary>
/// 기존 IConversionWorkflow 앞에 로그·trace·metric 책임을 덧붙이는 Decorator입니다.
/// 핵심 Service를 수정하지 않으므로 관측 기술 교체와 단위 테스트가 쉬워집니다.
/// </summary>
public sealed partial class ObservedConversionWorkflow : IConversionWorkflow
{
    private readonly IConversionWorkflow _inner;
    private readonly ConversionTelemetry _telemetry;
    private readonly ILogger<ObservedConversionWorkflow> _logger;

    /// <summary>
    /// 감쌀 핵심 유스케이스와 관측 도구를 생성자 주입으로 연결합니다.
    /// inner는 실제 업무 흐름, telemetry는 trace·metric 생산자, logger는 구조화 로그 생산자이며,
    /// 완성된 Decorator를 초기화합니다.
    /// </summary>
    /// <param name="inner">관측 책임 없이 업무를 수행하는 안쪽 Workflow입니다.</param>
    /// <param name="telemetry">Activity와 metric을 만드는 도구입니다.</param>
    /// <param name="logger">구조화된 이벤트를 기록할 ILogger입니다.</param>
    public ObservedConversionWorkflow(
        IConversionWorkflow inner,
        ConversionTelemetry telemetry,
        ILogger<ObservedConversionWorkflow> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 안쪽 Workflow 실행 전후에 구조화 로그, Activity 상태, counter와 duration을 남깁니다.
    /// request는 원문을 가진 입력, cancellationToken은 취소 신호이며, 안쪽 Workflow의 Result를 그대로 반환합니다.
    /// 예상 실패·취소·예외를 서로 다른 outcome으로 관측하되 원문과 예외 메시지는 기록하지 않습니다.
    /// 관측 공급자나 listener가 실패해도 업무 결과를 바꾸지 않는 fail-open 정책을 사용합니다.
    /// </summary>
    /// <param name="request">원문을 포함하지만 관측 데이터에는 정책상 승인된 필드만 사용할 요청입니다.</param>
    /// <param name="cancellationToken">안쪽 흐름에 전달하고 취소 분류에도 사용할 신호입니다.</param>
    /// <returns>안쪽 Workflow가 만든 완료 영수증 또는 예상 실패입니다.</returns>
    public async Task<OperationResult<ConversionReceipt>> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedTimestamp = Stopwatch.GetTimestamp();
        var parentActivity = Activity.Current;

        // Activity listener와 metric callback은 서로 다른 외부 확장 지점이므로 경계도 분리합니다.
        // 한 signal의 관측 장애가 변환이나 다른 signal 생성을 막지 않게 합니다.
        TryObserve(() => _telemetry.RecordAttempt(request.TargetFormat));
        var activity = TryStartActivity(
            () => _telemetry.StartConversion(request),
            parentActivity);
        var outcome = "faulted";
        var traceId = activity?.TraceId.ToString() ?? "not-recorded";

        TryObserve(() =>
            LogConversionStarted(_logger, request.JobId, request.TargetFormat, traceId));

        try
        {
            var result = await _inner
                .ExecuteAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                outcome = "succeeded";
                TryObserve(() => activity?.SetTag("conversion.outcome", outcome));
                TryObserve(() => activity?.SetStatus(ActivityStatusCode.Ok));
                TryObserve(() =>
                    LogConversionSucceeded(_logger, request.JobId, result.Value.OutputLength));
            }
            else
            {
                outcome = "rejected";
                TryObserve(() => activity?.SetTag("conversion.outcome", outcome));
                TryObserve(() => activity?.SetTag("error.type", result.Error.Code));
                TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error));
                TryObserve(() =>
                    LogConversionRejected(_logger, request.JobId, result.Error.Code));
            }

            return result;
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested &&
                  exception.CancellationToken == cancellationToken)
        {
            // catch filter의 when은 "전달한 바로 그 토큰으로 호출자가 취소한 경우"만 분류합니다.
            // timeout의 다른 토큰과 호출자 취소가 우연히 겹쳐도 정상 취소로 숨기지 않습니다.
            outcome = "canceled";
            TryObserve(() => activity?.SetTag("conversion.outcome", outcome));

            // 예상된 호출자 취소는 서버 결함이 아니므로 Activity 기본 상태인 Unset을 유지합니다.
            TryObserve(() => LogConversionCanceled(_logger, request.JobId));
            throw;
        }
        catch (Exception exception)
        {
            outcome = "faulted";
            var exceptionType = GetExceptionTypeName(exception);
            TryObserve(() => activity?.SetTag("conversion.outcome", outcome));
            TryObserve(() => activity?.SetTag("error.type", exceptionType));
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error));

            // 예외 Message에는 파일 경로나 외부 응답이 들어갈 수 있어 여기서는 형식 이름만 기록합니다.
            // 운영에서는 승인된 필드만 선별하는 중앙 redaction 정책을 함께 사용해야 합니다.
            TryObserve(() => LogConversionFaulted(_logger, request.JobId, exceptionType));
            throw;
        }
        finally
        {
            // finally는 성공, return, 예외, 취소 어느 경로에서도 한 번 실행됩니다.
            // 그래서 completion counter와 duration을 경로별로 빠뜨리거나 두 번 시도하는 문제를 줄일 수 있습니다.
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            TryObserve(() => _telemetry.RecordCompletion(request.TargetFormat, outcome));
            TryObserve(() =>
                _telemetry.RecordDuration(request.TargetFormat, outcome, elapsedMilliseconds));

            // Dispose의 listener callback이 실패해도 캡처한 부모 ambient context를 반드시 복원합니다.
            TryStopActivity(activity, parentActivity);
        }
    }

    /// <summary>
    /// Activity 시작 관측을 실행하되 관측 공급자의 예외가 업무 흐름으로 새지 않게 격리합니다.
    /// start는 Activity를 만들 함수, parentActivity는 시작 전 ambient 부모이며,
    /// 성공하면 Activity를, 관측 실패나 비구독 상태면 null을 반환합니다.
    /// 이 helper 안에서는 재귀 장애를 피하기 위해 실패 자체를 같은 logger로 다시 기록하지 않습니다.
    /// </summary>
    /// <param name="start">Activity를 만드는 관측 함수입니다.</param>
    /// <param name="parentActivity">시작 실패 시 복원할 기존 Activity.Current입니다.</param>
    /// <returns>시작된 Activity 또는 관측 불가를 뜻하는 null입니다.</returns>
    private static Activity? TryStartActivity(
        Func<Activity?> start,
        Activity? parentActivity)
    {
        ArgumentNullException.ThrowIfNull(start);

        try
        {
            return start();
        }
        catch (Exception)
        {
            // ActivityStarted callback이 던지면 StartActivity가 객체를 반환하지 않은 채 Current만 바꿀 수 있습니다.
            // 이때 Current에서 실패한 Activity를 찾아 종료를 시도하고 원래 부모를 명시적으로 되돌립니다.
            var failedActivity = Activity.Current;
            if (!ReferenceEquals(failedActivity, parentActivity))
            {
                TryObserve(() => failedActivity?.Dispose());
            }

            RestoreActivityCurrent(parentActivity);

            // 운영에서는 업무 logger와 분리된 공급자 health 신호로 이 장애를 감시합니다.
            return null;
        }
    }

    /// <summary>
    /// Activity 종료 callback 실패를 격리하고 시작 전에 캡처한 ambient 부모를 복원합니다.
    /// activity는 종료할 현재 span, parentActivity는 복원 대상이며 반환값은 없습니다.
    /// </summary>
    /// <param name="activity">listener가 있을 때 만들어진 문서 변환 Activity입니다.</param>
    /// <param name="parentActivity">문서 변환 Activity를 시작하기 전의 Activity.Current입니다.</param>
    private static void TryStopActivity(Activity? activity, Activity? parentActivity)
    {
        TryObserve(() => activity?.Dispose());
        RestoreActivityCurrent(parentActivity);
    }

    /// <summary>
    /// Activity.Current 복원 과정 자체의 observer callback 예외도 업무 흐름에서 격리합니다.
    /// parentActivity는 되돌릴 ambient 값이며 반환값은 없습니다.
    /// </summary>
    /// <param name="parentActivity">복원할 부모 Activity 또는 부모가 없음을 뜻하는 null입니다.</param>
    private static void RestoreActivityCurrent(Activity? parentActivity)
    {
        TryObserve(() => Activity.Current = parentActivity);
    }

    /// <summary>
    /// 로그·태그·metric·Activity 종료 같은 관측 부작용을 fail-open 경계 안에서 실행합니다.
    /// observation은 업무 결과에 영향을 주면 안 되는 동작이며 반환값은 없습니다.
    /// 관측 실패를 같은 logger로 기록하면 재귀 실패할 수 있으므로 여기서는 의도적으로 삼킵니다.
    /// </summary>
    /// <param name="observation">실패하더라도 업무 성공·실패·예외를 바꾸지 않을 관측 동작입니다.</param>
    private static void TryObserve(Action observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        try
        {
            observation();
        }
        catch (Exception)
        {
            // 운영 배포에서는 별도 provider-health 경보로 보완해야 합니다.
        }
    }

    /// <summary>
    /// 예외 Message를 제외하고 namespace까지 포함한 안정적인 형식 이름을 얻습니다.
    /// exception은 분류할 기술 예외이며, FullName이 없으면 단순 Name을 대신 반환합니다.
    /// </summary>
    /// <param name="exception">관측 분류에 사용할 예외 객체입니다.</param>
    /// <returns>민감한 Message를 포함하지 않는 예외 형식 이름입니다.</returns>
    private static string GetExceptionTypeName(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.GetType().FullName ?? exception.GetType().Name;
    }

    /// <summary>
    /// 변환 시작 로그의 고정 템플릿을 컴파일 시 생성합니다.
    /// logger는 기록 대상, jobId·targetFormat·traceId는 정책상 승인된 검색 필드이며 반환값은 없습니다.
    /// LoggerMessage를 쓰면 매 호출마다 템플릿을 다시 해석하는 비용을 줄일 수 있습니다.
    /// </summary>
    /// <param name="logger">로그를 받을 ILogger입니다.</param>
    /// <param name="jobId">검증된 작업 식별자입니다.</param>
    /// <param name="targetFormat">낮은 카디널리티 출력 형식입니다.</param>
    /// <param name="traceId">수집 중인 trace ID 또는 not-recorded입니다.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Conversion started. JobId={JobId}, TargetFormat={TargetFormat}, TraceId={TraceId}")]
    private static partial void LogConversionStarted(
        ILogger logger,
        string jobId,
        string targetFormat,
        string traceId);

    /// <summary>
    /// 성공 로그를 구조화 필드와 함께 기록합니다.
    /// logger는 기록 대상, jobId는 작업 키, outputLength는 결과 크기이며 반환값은 없습니다.
    /// </summary>
    /// <param name="logger">로그를 받을 ILogger입니다.</param>
    /// <param name="jobId">성공한 작업 식별자입니다.</param>
    /// <param name="outputLength">저장된 결과 문자의 수입니다.</param>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Conversion succeeded. JobId={JobId}, OutputLength={OutputLength}")]
    private static partial void LogConversionSucceeded(
        ILogger logger,
        string jobId,
        int outputLength);

    /// <summary>
    /// 예상 가능한 정책 거절을 Warning 로그로 기록합니다.
    /// logger는 기록 대상, jobId는 작업 키, errorCode는 안정된 분류이며 반환값은 없습니다.
    /// </summary>
    /// <param name="logger">로그를 받을 ILogger입니다.</param>
    /// <param name="jobId">거절된 작업 식별자입니다.</param>
    /// <param name="errorCode">집계 가능한 안정된 오류 코드입니다.</param>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Conversion rejected. JobId={JobId}, ErrorCode={ErrorCode}")]
    private static partial void LogConversionRejected(
        ILogger logger,
        string jobId,
        string errorCode);

    /// <summary>
    /// 호출자 취소를 Information 로그로 기록합니다.
    /// logger는 기록 대상, jobId는 취소된 작업 키이며 반환값은 없습니다.
    /// 취소를 서버 결함과 같은 Error 로그로 세지 않기 위해 별도 이벤트로 둡니다.
    /// </summary>
    /// <param name="logger">로그를 받을 ILogger입니다.</param>
    /// <param name="jobId">취소된 작업 식별자입니다.</param>
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Conversion canceled by caller. JobId={JobId}")]
    private static partial void LogConversionCanceled(ILogger logger, string jobId);

    /// <summary>
    /// 예상하지 못한 기술 장애를 예외 형식만 사용해 Error 로그로 기록합니다.
    /// logger는 기록 대상, jobId는 작업 키, exceptionType은 예외 종류이며 반환값은 없습니다.
    /// </summary>
    /// <param name="logger">로그를 받을 ILogger입니다.</param>
    /// <param name="jobId">실패한 작업 식별자입니다.</param>
    /// <param name="exceptionType">민감한 Message를 제외한 예외 형식 이름입니다.</param>
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "Conversion faulted. JobId={JobId}, ExceptionType={ExceptionType}")]
    private static partial void LogConversionFaulted(
        ILogger logger,
        string jobId,
        string exceptionType);
}
