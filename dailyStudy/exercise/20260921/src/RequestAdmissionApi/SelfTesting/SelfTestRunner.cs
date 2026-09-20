using System.Threading.RateLimiting;
using RequestAdmissionApi.Application;
using RequestAdmissionApi.Application.Ports;
using RequestAdmissionApi.Domain;
using RequestAdmissionApi.Infrastructure;
using RequestAdmissionApi.RateLimiting;

namespace RequestAdmissionApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 핵심 도메인, 서비스, partition limiter 결정을 검증하는 실행기입니다.
/// 각 테스트는 새 상태를 사용해 실행 순서나 네트워크 시간에 의존하지 않습니다.
/// </summary>
public static class SelfTestRunner
{
    // 모든 비동기 검증에 같은 상한을 사용해 회귀가 생겨도 self-test가 무기한 멈추지 않게 합니다.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 모든 자체 테스트를 순서대로 실행하고 사람이 읽을 수 있는 결과를 콘솔에 표시합니다.
    /// 파라미터는 없으며 모두 성공하면 0, 하나라도 실패하면 1을 반환합니다.
    /// </summary>
    public static async Task<int> RunAsync()
    {
        var failures = 0;
        failures += await RunCaseAsync("known clients normalize", TestKnownClientNormalizationAsync);
        failures += await RunCaseAsync("unknown clients share anonymous", TestUnknownClientNormalizationAsync);
        failures += await RunCaseAsync("valid domain request is immutable", TestValidDomainRequestAsync);
        failures += await RunCaseAsync("blank title is rejected", TestBlankTitleAsync);
        failures += await RunCaseAsync("unsupported format is rejected", TestUnsupportedFormatAsync);
        failures += await RunCaseAsync("invalid rows are rejected", TestInvalidRowsAsync);
        failures += await RunCaseAsync("null row is rejected", TestNullRowAsync);
        failures += await RunCaseAsync("one row reports every error", TestMultipleErrorsInOneRowAsync);
        failures += await RunCaseAsync("application service renders and saves", TestApplicationServiceAsync);
        failures += await RunCaseAsync("invalid renderer configuration fails fast", TestRendererConfigurationAsync);
        failures += await RunCaseAsync("application service propagates cancellation", TestApplicationServiceCancellationAsync);
        failures += await RunCaseAsync("application service finds saved reports", TestApplicationServiceLookupAsync);
        failures += await RunCaseAsync("first partition lease succeeds", TestFirstLeaseAsync);
        failures += await RunCaseAsync("same partition request queues", TestSamePartitionQueuesAsync);
        failures += await RunCaseAsync("third same-partition request rejects", TestThirdRequestRejectsAsync);
        failures += await RunCaseAsync("different partition stays independent", TestDifferentPartitionAsync);
        failures += await RunCaseAsync("disposing lease advances queue", TestDisposeAdvancesQueueAsync);
        failures += await RunCaseAsync("queued acquisition observes cancellation", TestQueuedCancellationAsync);

        Console.WriteLine($"Self-test: {18 - failures}/18 passed");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 테스트 하나를 실행하고 성공/실패를 격리해 보고합니다.
    /// <paramref name="name"/>은 출력 이름, <paramref name="test"/>는 실행할 비동기 함수이며 성공 시 0, 실패 시 1을 반환합니다.
    /// </summary>
    private static async Task<int> RunCaseAsync(string name, Func<Task> test)
    {
        try
        {
            // WaitAsync는 실제 테스트를 바꾸지 않고 최대 대기 시간만 감싸, 멈춤을 명시적인 실패로 바꿉니다.
            await test().WaitAsync(TestTimeout);
            Console.WriteLine($"[PASS] {name}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[FAIL] {name}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// alpha와 beta가 대소문자 및 바깥 공백과 무관하게 각각의 제한된 키로 정규화되는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환하고 어긋나면 예외를 발생시킵니다.
    /// </summary>
    private static Task TestKnownClientNormalizationAsync()
    {
        Assert(ReportRateLimitPolicy.NormalizeClient(" Alpha ") == "alpha", "alpha 정규화 실패");
        Assert(ReportRateLimitPolicy.NormalizeClient("BETA") == "beta", "beta 정규화 실패");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 누락값과 임의 문자열이 모두 하나의 anonymous 키로 모여 partition 수가 제한되는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestUnknownClientNormalizationAsync()
    {
        Assert(ReportRateLimitPolicy.NormalizeClient(null) == "anonymous", "null은 anonymous여야 합니다.");
        Assert(ReportRateLimitPolicy.NormalizeClient("attacker-123") == "anonymous", "알 수 없는 키는 anonymous여야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 유효한 도메인 입력이 공백과 형식을 정규화하고 원본 목록 변경의 영향을 받지 않는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestValidDomainRequestAsync()
    {
        var source = new List<ReportRowDraft> { new(" Seoul ", 1200m) };
        var result = ReportRequest.Create(" Monthly Sales ", "TEXT", source);
        source.Clear();

        Assert(result.IsSuccess, "유효한 요청이 실패했습니다.");
        // !는 직전 성공 검증으로 Request가 null이 아님을 사람이 확인했다는 사실을 Nullable 분석기에 알립니다.
        Assert(result.Request!.Title == "Monthly Sales", "제목 공백이 정리되지 않았습니다.");
        Assert(result.Request.Rows.Count == 1 && result.Request.Rows[0].Label == "Seoul", "행이 방어적으로 복사되지 않았습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 공백 제목이 도메인 검증 오류로 반환되는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestBlankTitleAsync()
    {
        // [ ... ] 컬렉션 식은 필요한 원소를 대괄호 안에 적어 짧은 일회용 컬렉션을 만드는 문법입니다.
        var result = ReportRequest.Create("   ", "text", [new ReportRowDraft("row", 1m)]);
        // => 람다는 각 오류 문자열을 받아 title 포함 여부를 계산하는 이름 없는 짧은 함수입니다.
        Assert(!result.IsSuccess && result.Errors.Any(error => error.Contains("title", StringComparison.Ordinal)), "공백 제목 오류가 없습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 지원하지 않는 렌더링 형식이 명시적 오류로 반환되는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestUnsupportedFormatAsync()
    {
        var result = ReportRequest.Create("report", "pdf", [new ReportRowDraft("row", 1m)]);
        Assert(!result.IsSuccess && result.Errors.Any(error => error.Contains("format", StringComparison.Ordinal)), "형식 오류가 없습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 빈 행 목록과 음수 값이 도메인 규칙으로 거절되는지 검증합니다.
    /// 파라미터는 없으며 성공 시 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestInvalidRowsAsync()
    {
        var empty = ReportRequest.Create("report", "text", Array.Empty<ReportRowDraft>());
        var negative = ReportRequest.Create("report", "text", [new ReportRowDraft("row", -1m)]);
        Assert(!empty.IsSuccess, "빈 rows가 허용되었습니다.");
        Assert(!negative.IsSuccess, "음수 value가 허용되었습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// rows 배열 안의 null 요소가 예외가 아니라 예상 가능한 검증 실패가 되는지 확인합니다.
    /// 파라미터는 없으며 Application Service의 오류 Result를 확인한 뒤 완료된 Task를 반환합니다.
    /// </summary>
    private static async Task TestNullRowAsync()
    {
        var repository = new InMemoryReportRepository();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var service = new ReportApplicationService([new ImmediateTextRenderer()], repository, clock);
        var command = new CreateReportCommand("report", "text", [null]);

        var result = await service.CreateAsync(command, CancellationToken.None);
        Assert(!result.IsSuccess, "null row가 허용되었습니다.");
        Assert(
            result.Error?.Details.Any(error => error.Contains("rows[0]", StringComparison.Ordinal)) == true,
            "null row의 위치를 알려 주는 검증 오류가 없습니다.");
    }

    /// <summary>
    /// 같은 행의 잘못된 label과 음수 value를 한 번의 검증에서 모두 알려 주는지 확인합니다.
    /// 파라미터는 없으며 두 오류를 확인한 뒤 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestMultipleErrorsInOneRowAsync()
    {
        var result = ReportRequest.Create("report", "text", [new ReportRowDraft(" ", -1m)]);

        Assert(!result.IsSuccess, "label과 value가 모두 잘못된 행이 허용되었습니다.");
        Assert(
            result.Errors.Any(error => error.Contains("rows[0].label", StringComparison.Ordinal)),
            "같은 행의 label 오류가 누락되었습니다.");
        Assert(
            result.Errors.Any(error => error.Contains("rows[0].value", StringComparison.Ordinal)),
            "같은 행의 value 오류가 누락되었습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Application Service가 주입된 즉시 렌더러를 선택하고 결과를 저장하는지 검증합니다.
    /// 파라미터는 없으며 저장된 값까지 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestApplicationServiceAsync()
    {
        var repository = new InMemoryReportRepository();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var service = new ReportApplicationService([new ImmediateTextRenderer()], repository, clock);
        var command = new CreateReportCommand(
            "sales",
            "text",
            [new ReportRowDraft("Seoul", 10m)]);

        var result = await service.CreateAsync(command, CancellationToken.None);
        Assert(result.IsSuccess, "서비스가 유효한 명령에 실패했습니다.");
        Assert(result.Value.Content == "fast:Seoul", "주입한 Strategy가 사용되지 않았습니다.");
        Assert(result.Value.CreatedAtUtc == clock.GetUtcNow(), "주입한 시계가 사용되지 않았습니다.");
        var stored = await repository.FindByIdAsync(result.Value.Id, CancellationToken.None);
        Assert(stored == result.Value, "Repository 포트에 보고서가 저장되지 않았습니다.");
    }

    /// <summary>
    /// renderer가 빠졌거나 같은 형식으로 중복되거나 미정의 enum 형식을 내놓으면 생성자에서 즉시 실패하는지 확인합니다.
    /// 파라미터는 없으며 세 잘못된 구성에서 ArgumentException을 확인한 뒤 완료된 Task를 반환합니다.
    /// </summary>
    private static Task TestRendererConfigurationAsync()
    {
        var repository = new InMemoryReportRepository();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));

        AssertArgumentException(
            () =>
            {
                _ = new ReportApplicationService(Array.Empty<IReportRenderer>(), repository, clock);
            },
            "renderer 누락 구성을 생성자가 허용했습니다.");

        var duplicates = new IReportRenderer[]
        {
            new ImmediateTextRenderer(),
            new ImmediateTextRenderer(),
        };
        AssertArgumentException(
            () =>
            {
                _ = new ReportApplicationService(duplicates, repository, clock);
            },
            "renderer 중복 구성을 생성자가 허용했습니다.");

        var undefinedFormatRenderers = new IReportRenderer[]
        {
            new ImmediateTextRenderer(),
            new UndefinedFormatRenderer(),
        };
        AssertArgumentException(
            () =>
            {
                _ = new ReportApplicationService(undefinedFormatRenderers, repository, clock);
            },
            "정의되지 않은 enum 형식의 renderer를 생성자가 허용했습니다.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Application Service가 받은 CancellationToken을 같은 값으로 renderer에 전달하고 이미 취소된 호출도 중단하는지 확인합니다.
    /// 파라미터는 없으며 전달된 토큰과 취소 예외를 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestApplicationServiceCancellationAsync()
    {
        var repository = new InMemoryReportRepository();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var renderer = new TokenRecordingRenderer();
        var service = new ReportApplicationService([renderer], repository, clock);
        var command = new CreateReportCommand("report", "text", [new ReportRowDraft("row", 1m)]);

        // using var는 현재 scope가 끝날 때 Dispose를 자동 호출해 CancellationTokenSource 자원을 빠뜨리지 않는 문법입니다.
        using var cancellation = new CancellationTokenSource();
        var result = await service.CreateAsync(command, cancellation.Token);
        Assert(result.IsSuccess, "취소되지 않은 서비스 호출이 실패했습니다.");
        Assert(renderer.ObservedCancellationToken == cancellation.Token, "renderer에 같은 취소 토큰이 전달되지 않았습니다.");

        cancellation.Cancel();
        await AssertCanceledAsync(service.CreateAsync(command, cancellation.Token), cancellation.Token);
    }

    /// <summary>
    /// 저장된 보고서를 Application Service를 통해 다시 찾고 없는 ID는 null로 구분하는지 확인합니다.
    /// 파라미터는 없으며 존재/부재 조회 결과를 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestApplicationServiceLookupAsync()
    {
        var repository = new InMemoryReportRepository();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var service = new ReportApplicationService([new ImmediateTextRenderer()], repository, clock);
        var command = new CreateReportCommand("report", "text", [new ReportRowDraft("row", 1m)]);

        var created = await service.CreateAsync(command, CancellationToken.None);
        Assert(created.IsSuccess, "조회 준비용 보고서 생성이 실패했습니다.");

        var found = await service.FindByIdAsync(created.Value.Id, CancellationToken.None);
        var missing = await service.FindByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert(found == created.Value, "Application Service 조회가 저장된 보고서를 반환하지 않았습니다.");
        Assert(missing is null, "없는 보고서 ID가 null로 반환되지 않았습니다.");
    }

    /// <summary>
    /// 비어 있는 새 partition의 첫 요청이 즉시 permit을 얻는지 검증합니다.
    /// 파라미터는 없으며 검증 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestFirstLeaseAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        using var lease = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        Assert(lease.IsAcquired, "첫 lease가 허용되지 않았습니다.");
    }

    /// <summary>
    /// 같은 partition의 두 번째 요청이 첫 lease가 사용 중일 때 완료되지 않고 대기하는지 검증합니다.
    /// 파라미터는 없으며 대기 작업을 취소해 정리한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestSamePartitionQueuesAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        using var first = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        // AsTask는 ValueTask를 완료 여부 확인과 timeout 조합이 쉬운 Task 형태로 바꿉니다.
        var queued = limiter.AcquireAsync("alpha", 1, cancellation.Token).AsTask();

        Assert(!queued.IsCompleted, "두 번째 요청이 queue에서 기다리지 않았습니다.");
        cancellation.Cancel();
        await AssertCanceledAsync(queued, cancellation.Token);
    }

    /// <summary>
    /// 실행 1개와 대기 1개가 찬 뒤 같은 partition의 세 번째 요청이 즉시 거절되는지 검증합니다.
    /// 파라미터는 없으며 queue를 정상적으로 비운 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestThirdRequestRejectsAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        var first = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        var queuedTask = limiter.AcquireAsync("alpha", 1, CancellationToken.None).AsTask();
        using var third = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);

        Assert(!third.IsAcquired, "세 번째 요청이 거절되지 않았습니다.");
        first.Dispose();
        using var queued = await queuedTask.WaitAsync(TestTimeout);
        Assert(queued.IsAcquired, "정리 중 대기 요청이 permit을 얻지 못했습니다.");
    }

    /// <summary>
    /// alpha가 사용 중이어도 beta partition은 독립적인 permit을 얻는지 검증합니다.
    /// 파라미터는 없으며 두 lease를 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestDifferentPartitionAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        using var alpha = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        using var beta = await limiter.AcquireAsync("beta", 1, CancellationToken.None);
        Assert(alpha.IsAcquired && beta.IsAcquired, "서로 다른 partition이 독립적이지 않습니다.");
    }

    /// <summary>
    /// 실행 중 lease를 Dispose하면 가장 오래 기다린 요청이 permit을 이어받는지 검증합니다.
    /// 파라미터는 없으며 대기 요청의 성공을 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestDisposeAdvancesQueueAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        var first = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        var queuedTask = limiter.AcquireAsync("alpha", 1, CancellationToken.None).AsTask();
        Assert(!queuedTask.IsCompleted, "요청이 queue에 들어가지 않았습니다.");

        first.Dispose();
        using var queued = await queuedTask.WaitAsync(TestTimeout);
        Assert(queued.IsAcquired, "Dispose 뒤 대기 요청이 진행되지 않았습니다.");
    }

    /// <summary>
    /// queue에서 기다리는 요청의 CancellationToken이 취소되면 취소 예외로 완료되는지 검증합니다.
    /// 파라미터는 없으며 취소를 확인한 뒤 완료되는 Task를 반환합니다.
    /// </summary>
    private static async Task TestQueuedCancellationAsync()
    {
        using var limiter = ReportRateLimitPolicy.CreatePartitionedLimiter();
        using var first = await limiter.AcquireAsync("alpha", 1, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var queued = limiter.AcquireAsync("alpha", 1, cancellation.Token).AsTask();

        cancellation.Cancel();
        await AssertCanceledAsync(queued, cancellation.Token);
    }

    /// <summary>
    /// 비동기 lease 요청이 OperationCanceledException으로 끝나는지 검사합니다.
    /// <paramref name="task"/>는 취소되어야 할 lease 작업, <paramref name="expectedToken"/>은 원래 취소 신호이며
    /// 기대한 토큰의 취소이면 정상 완료하고 아니면 검증 예외를 발생시킵니다.
    /// </summary>
    private static async Task AssertCanceledAsync(
        Task<RateLimitLease> task,
        CancellationToken expectedToken)
    {
        try
        {
            using var unexpectedLease = await task.WaitAsync(TestTimeout);
            throw new InvalidOperationException("대기 요청이 취소되지 않았습니다.");
        }
        catch (OperationCanceledException exception)
        {
            Assert(exception.CancellationToken == expectedToken, "다른 CancellationToken으로 취소되었습니다.");
        }
    }

    /// <summary>
    /// 일반 비동기 작업이 지정한 CancellationToken의 OperationCanceledException으로 끝나는지 검사합니다.
    /// <paramref name="task"/>는 취소되어야 할 작업, <paramref name="expectedToken"/>은 원래 취소 신호이며
    /// 기대한 취소이면 정상 완료하고 아니면 검증 예외를 발생시킵니다.
    /// </summary>
    private static async Task AssertCanceledAsync(Task task, CancellationToken expectedToken)
    {
        try
        {
            await task.WaitAsync(TestTimeout);
            throw new InvalidOperationException("비동기 작업이 취소되지 않았습니다.");
        }
        catch (OperationCanceledException exception)
        {
            Assert(exception.CancellationToken == expectedToken, "다른 CancellationToken으로 취소되었습니다.");
        }
    }

    /// <summary>
    /// 조건이 거짓이면 읽기 쉬운 자체 테스트 실패를 발생시킵니다.
    /// <paramref name="condition"/>은 기대 조건, <paramref name="message"/>는 실패 이유이며 반환값은 없습니다.
    /// </summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 잘못된 renderer 구성 코드가 ArgumentException으로 즉시 실패하는지 검사합니다.
    /// <paramref name="action"/>은 실행할 구성 코드, <paramref name="message"/>는 예외가 없을 때의 실패 설명이며 반환값은 없습니다.
    /// </summary>
    private static void AssertArgumentException(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 전달받은 CancellationToken을 기록해 Application Service의 취소 신호 전달을 검사하는 테스트 Strategy입니다.
    /// </summary>
    private sealed class TokenRecordingRenderer : IReportRenderer
    {
        // =>는 단일 식의 결과를 바로 반환하는 expression-bodied 멤버이며, 이 Strategy의 형식은 바뀌지 않습니다.
        public ReportFormat Format => ReportFormat.PlainText;

        public CancellationToken ObservedCancellationToken { get; private set; }

        /// <summary>
        /// 전달된 취소 토큰을 기록하고 즉시 예측 가능한 본문을 반환합니다.
        /// <paramref name="request"/>는 검증된 요청, <paramref name="cancellationToken"/>은 기록할 중단 신호이며
        /// 즉시 완료된 문자열을 반환합니다.
        /// </summary>
        public ValueTask<string> RenderAsync(
            ReportRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ObservedCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult($"observed:{request.Rows[0].Label}");
        }
    }

    /// <summary>
    /// enum에 선언되지 않은 형식을 노출해 renderer 구성 검증을 확인하는 테스트 Strategy입니다.
    /// 정상적인 Application Service 생성에서는 거절되므로 렌더링 메서드가 호출되면 안 됩니다.
    /// </summary>
    private sealed class UndefinedFormatRenderer : IReportRenderer
    {
        // 명시적 형 변환은 정수를 enum으로 바꿀 수 있지만, 그 값이 실제 멤버로 선언되었다는 보장은 없습니다.
        public ReportFormat Format => (ReportFormat)999;

        /// <summary>
        /// 구성 검증이 실패했다면 호출될 수 없는 방어용 메서드입니다.
        /// <paramref name="request"/>와 <paramref name="cancellationToken"/>은 interface 계약을 위한 값이며
        /// 반환값 없이 InvalidOperationException을 발생시킵니다.
        /// </summary>
        public ValueTask<string> RenderAsync(
            ReportRequest request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("미정의 형식 renderer는 실행되면 안 됩니다.");
        }
    }

    /// <summary>
    /// 지연 없이 고정된 문자열을 만드는 self-test 전용 Strategy입니다.
    /// 운영 렌더러의 의도적인 I/O 지연 때문에 테스트가 느려지거나 시간에 의존하지 않게 합니다.
    /// </summary>
    private sealed class ImmediateTextRenderer : IReportRenderer
    {
        public ReportFormat Format => ReportFormat.PlainText;

        /// <summary>
        /// 첫 행의 이름을 사용해 즉시 예측 가능한 본문을 만듭니다.
        /// <paramref name="request"/>는 검증된 요청, <paramref name="cancellationToken"/>은 중단 신호이며 즉시 완료된 문자열을 반환합니다.
        /// </summary>
        public ValueTask<string> RenderAsync(ReportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult($"fast:{request.Rows[0].Label}");
        }
    }

    /// <summary>
    /// 테스트가 정한 UTC 시각만 반환하는 교체 가능한 시계입니다.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        /// <summary>
        /// 시계가 항상 반환할 시각을 저장합니다.
        /// <paramref name="utcNow"/>는 UTC 기준 고정 시각이며 새 시계를 초기화합니다.
        /// </summary>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        /// <summary>
        /// 생성 때 받은 고정 UTC 시각을 반환합니다.
        /// 파라미터는 없으며 테스트용 현재 시각을 반환합니다.
        /// </summary>
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
