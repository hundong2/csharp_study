using System.Collections.Concurrent;
using SupplierQuoteExercise.Application;
using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;
using SupplierQuoteExercise.Infrastructure;

namespace SupplierQuoteExercise.SelfTesting;

/// <summary>
/// Domain 검증, 완료 순서, 동시 시작, 부분 실패, 정책, 예외, 취소, 저장 경계를 자동 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    // target-typed new는 왼쪽 DateOnly 형식이 분명해 생성자 형식 이름을 생략하는 문법입니다.
    private static readonly DateOnly NeedBy = new(2026, 11, 5);

    /// <summary>
    /// 서로 독립된 동기·비동기 self-test를 모두 실행하고 process exit code를 반환합니다.
    /// </summary>
    /// <returns>모든 검사가 통과하면 0, 하나라도 실패하면 1을 담은 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        var checks = new Checkbook();
        TestDomainValidation(checks);
        TestResultContract(checks);
        TestSelectionPolicy(checks);
        TestConstructorGuards(checks);
        TestReportConsistencyGuards(checks);
        await TestCompletionOrderAndConcurrentStartAsync(checks);
        await TestAllFailuresAsync(checks);
        await TestSupplierIdMismatchAsync(checks);
        await TestGatewayFaultCancelsSiblingsAsync(checks);
        await TestCallbackFaultCancelsSiblingsAsync(checks);
        await TestUnexpectedExceptionAsync(checks);
        await TestCancellationPreventsSaveAsync(checks);
        return checks.Finish();
    }

    /// <summary>
    /// 요청 정규화와 수량·단가 경계가 유효하지 않은 Domain 객체 생성을 막는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestDomainValidation(Checkbook checks)
    {
        var validRequest = QuoteRequest.Create("  gear-100  ", 5, NeedBy);
        checks.True(validRequest.IsSuccess, "정상 요청은 생성된다");
        checks.Equal("GEAR-100", validRequest.Value.Sku, "SKU는 trim 후 대문자로 정규화된다");
        checks.Equal(5, validRequest.Value.Quantity, "요청 수량은 보존된다");

        var missingSku = QuoteRequest.Create(null, 1, NeedBy);
        checks.True(!missingSku.IsSuccess, "null SKU는 실패한다");
        checks.Equal("quote_request.sku_required", missingSku.Error.Code, "SKU 오류 코드는 안정적이다");

        var zeroQuantity = QuoteRequest.Create("GEAR", 0, NeedBy);
        checks.True(!zeroQuantity.IsSuccess, "0개 요청은 실패한다");
        checks.Equal(
            "quote_request.quantity_out_of_range",
            zeroQuantity.Error.Code,
            "수량 오류 코드는 안정적이다");

        var validQuote = SupplierQuote.Create("  Alpha  ", 125.50m, NeedBy);
        checks.True(validQuote.IsSuccess, "정상 공급업체 견적은 생성된다");
        checks.Equal("Alpha", validQuote.Value.SupplierId, "공급업체 ID는 trim된다");
        checks.Equal(627.50m, validQuote.Value.TotalFor(5), "총액은 decimal로 계산된다");

        var invalidPrice = SupplierQuote.Create("Alpha", 0m, NeedBy);
        checks.True(!invalidPrice.IsSuccess, "0원 단가는 외부 응답 오류다");
        checks.Equal(ErrorKind.External, invalidPrice.Error.Kind, "잘못된 공급업체 값은 External 실패다");

        var maximumPrice = SupplierQuote.Create(
            "Maximum",
            SupplierQuote.MaximumUnitPrice,
            NeedBy);
        checks.True(maximumPrice.IsSuccess, "공개된 최대 단가 경계는 허용한다");
        checks.Equal(
            10_000_000_000_000m,
            maximumPrice.Value.TotalFor(10_000),
            "최대 단가와 최대 수량의 총액은 decimal 범위에서 안전하다");

        var tooLargePrice = SupplierQuote.Create(
            "TooLarge",
            SupplierQuote.MaximumUnitPrice + 0.01m,
            NeedBy);
        checks.True(!tooLargePrice.IsSuccess, "최대 단가를 조금이라도 넘으면 실패한다");
        checks.Equal(
            "supplier_quote.price_too_large",
            tooLargePrice.Error.Code,
            "단가 상한 오류 코드는 안정적이다");
        // () => 식은 나중에 실행할 동작을 Action으로 전달하는 parameter 없는 lambda입니다.
        checks.Throws<ArgumentOutOfRangeException>(
            () => validQuote.Value.TotalFor(0),
            "총액 계산에 0개를 넘기면 programmer 오류 예외가 난다");
    }

    /// <summary>
    /// Result가 성공 값과 실패 오류 중 정확히 한쪽만 공개하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestResultContract(Checkbook checks)
    {
        var success = Result<string>.Success("ok");
        var failure = Result<string>.Failure(Error.Business("test.failure", "예상 실패"));

        checks.Equal("ok", success.Value, "성공 Result는 값을 공개한다");
        checks.Equal("test.failure", failure.Error.Code, "실패 Result는 오류를 공개한다");
        checks.Throws<InvalidOperationException>(
            () => _ = success.Error,
            "성공 Result의 Error 접근은 계약 위반이다");
        checks.Throws<InvalidOperationException>(
            () => _ = failure.Value,
            "실패 Result의 Value 접근은 계약 위반이다");
    }

    /// <summary>
    /// 최저 총액 정책이 늦은 납기를 제외하고 동률을 날짜와 ID로 결정적으로 푸는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestSelectionPolicy(Checkbook checks)
    {
        var request = RequireRequest("gear", 10, NeedBy);
        IReadOnlyCollection<SupplierQuote> quotes =
        [
            RequireQuote("Zulu", 100m, new DateOnly(2026, 11, 4)),
            RequireQuote("Beta", 100m, new DateOnly(2026, 11, 3)),
            RequireQuote("Alpha", 100m, new DateOnly(2026, 11, 3)),
            RequireQuote("LateCheap", 1m, new DateOnly(2026, 11, 6))
        ];
        var policy = new LowestTotalCostPolicy();

        var selected = policy.Select(request, quotes);
        checks.Equal("Alpha", selected?.SupplierId, "동률은 빠른 납기 뒤 ID 순서로 선택된다");
        checks.Equal(
            null,
            policy.Select(request, Array.Empty<SupplierQuote>()),
            "성공 견적이 없으면 선택 결과는 null이다");
        checks.Equal(
            null,
            policy.Select(
                request,
                [RequireQuote("OnlyLate", 1m, new DateOnly(2026, 11, 7))]),
            "모든 견적이 늦으면 선택 결과는 null이다");
    }

    /// <summary>
    /// 잘못 조립된 빈 Gateway와 중복 공급업체 ID를 생성자에서 즉시 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestConstructorGuards(Checkbook checks)
    {
        var repository = new InMemoryQuoteRoundRepository();
        var policy = new LowestTotalCostPolicy();
        checks.Throws<ArgumentException>(
            () => _ = new CollectSupplierQuotesService([], policy, repository),
            "빈 Gateway 목록은 조립 오류다");

        ISupplierQuoteGateway first = new ScriptedSupplierQuoteGateway(
            "Same",
            TimeSpan.Zero,
            Result<SupplierQuote>.Success(RequireQuote("Same", 10m, NeedBy)));
        ISupplierQuoteGateway second = new ScriptedSupplierQuoteGateway(
            "Same",
            TimeSpan.Zero,
            Result<SupplierQuote>.Success(RequireQuote("Same", 20m, NeedBy)));
        checks.Throws<ArgumentException>(
            () => _ = new CollectSupplierQuotesService([first, second], policy, repository),
            "중복 공급업체 ID는 완료 결과를 모호하게 하므로 거절한다");

        var blankId = new ControlledGateway("   ");
        checks.Throws<ArgumentException>(
            () => _ = new CollectSupplierQuotesService([blankId], policy, repository),
            "빈 공급업체 ID는 조립 시점에 거절한다");
    }

    /// <summary>
    /// 최종 report factory가 수집 흐름으로는 생기면 안 되는 모순된 snapshot을 방어하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestReportConsistencyGuards(Checkbook checks)
    {
        var request = RequireRequest("GEAR", 1, NeedBy);
        var alpha = RequireQuote("Alpha", 100m, NeedBy);
        var beta = RequireQuote("Beta", 90m, NeedBy);
        var success = Result<SupplierQuote>.Success(alpha);
        var failure = Result<SupplierQuote>.Failure(
            Error.External("supplier.unavailable", "일시 장애"));

        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(request, [], null),
            "빈 완료 목록은 report가 될 수 없다");
        // postfix !는 일부러 잘못된 runtime 입력을 만드는 이 방어 테스트에서만 nullable 경고를 억제합니다.
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(request, [null!], null),
            "null 완료 항목은 report가 될 수 없다");
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(
                request,
                [new QuoteAttempt(null!, failure)],
                null),
            "null 공급업체 ID는 report가 될 수 없다");
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(
                request,
                [new QuoteAttempt("   ", failure)],
                null),
            "공백 공급업체 ID는 report가 될 수 없다");
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(
                request,
                [new QuoteAttempt("Alpha", failure), new QuoteAttempt("Alpha", failure)],
                null),
            "같은 공급업체 완료 결과가 둘이면 report가 될 수 없다");
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(
                request,
                [new QuoteAttempt("Wrong", success)],
                null),
            "완료 ID와 성공 견적 ID가 다르면 report가 될 수 없다");
        checks.Throws<ArgumentException>(
            () => _ = QuoteRoundReport.Create(
                request,
                [new QuoteAttempt("Alpha", success)],
                beta),
            "성공 목록에 없는 선택 견적은 report가 될 수 없다");
    }

    /// <summary>
    /// 모든 Gateway가 첫 완료 전에 시작되고, Task.WhenEach가 인위적으로 완료한 순서를 보존하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 완료 순서 검사가 모두 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestCompletionOrderAndConcurrentStartAsync(Checkbook checks)
    {
        var slow = new ControlledGateway("SlowCheap");
        var fast = new ControlledGateway("Fast");
        var failure = new ControlledGateway("NoStock");
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            [slow, fast, failure],
            new LowestTotalCostPolicy(),
            repository);
        var completionOrder = new ConcurrentQueue<string>();

        // callback lambda는 service가 완료를 관찰한 바로 그 순서를 thread-safe queue에 기록합니다.
        var collectTask = service.CollectAsync(
            RequireRequest("GEAR", 2, NeedBy),
            attempt => completionOrder.Enqueue(attempt.SupplierId));

        await Task.WhenAll(slow.Started, fast.Started, failure.Started)
            .WaitAsync(TimeSpan.FromSeconds(2));
        checks.Equal(1, slow.CallCount, "느린 Gateway도 첫 완료 전에 시작된다");
        checks.Equal(1, fast.CallCount, "빠른 Gateway도 첫 완료 전에 시작된다");
        checks.Equal(1, failure.CallCount, "실패 Gateway도 첫 완료 전에 시작된다");

        fast.Complete(Result<SupplierQuote>.Success(RequireQuote("Fast", 120m, NeedBy)));
        await WaitUntilAsync(() => completionOrder.Count == 1);
        failure.Complete(
            Result<SupplierQuote>.Failure(
                Error.External("supplier.out_of_stock", "재고 없음")));
        await WaitUntilAsync(() => completionOrder.Count == 2);
        slow.Complete(Result<SupplierQuote>.Success(RequireQuote("SlowCheap", 90m, NeedBy)));

        var report = await collectTask;
        checks.SequenceEqual(
            ["Fast", "NoStock", "SlowCheap"],
            completionOrder,
            "진행 callback은 입력 순서가 아닌 완료 순서다");
        checks.SequenceEqual(
            ["Fast", "NoStock", "SlowCheap"],
            report.CompletionOrder.Select(attempt => attempt.SupplierId),
            "report도 완료 순서를 snapshot으로 보존한다");
        checks.Equal(2, report.CountSuccesses(), "부분 실패 뒤에도 두 성공을 보존한다");
        checks.Equal(1, report.CountFailures(), "예상 실패 하나를 report에 보존한다");
        checks.Equal("SlowCheap", report.SelectedQuote?.SupplierId, "늦게 끝난 저렴한 견적도 선택 후보로 남는다");
        checks.Equal(1, repository.Count, "모든 Task가 끝난 report는 한 번 저장된다");
        checks.True(
            ReferenceEquals(report, repository.Snapshot()[0]),
            "Repository는 최종 불변 report를 저장한다");
    }

    /// <summary>
    /// 모든 공급업체가 예상 실패여도 전체 수집은 정상 완료되고 선택만 null인지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>모두 실패하는 비동기 수집이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestAllFailuresAsync(Checkbook checks)
    {
        var failure = Result<SupplierQuote>.Failure(
            Error.External("supplier.unavailable", "공급업체 일시 장애"));
        ISupplierQuoteGateway[] gateways =
        [
            new ScriptedSupplierQuoteGateway("A", TimeSpan.Zero, failure),
            new ScriptedSupplierQuoteGateway("B", TimeSpan.Zero, failure)
        ];
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            gateways,
            new LowestTotalCostPolicy(),
            repository);

        var report = await service.CollectAsync(RequireRequest("GEAR", 1, NeedBy));
        checks.Equal(0, report.CountSuccesses(), "모두 실패하면 성공 수는 0이다");
        checks.Equal(2, report.CountFailures(), "모든 예상 실패를 잃지 않는다");
        checks.Equal(null, report.SelectedQuote, "선택할 성공 견적이 없으면 null이다");
        checks.Equal(1, repository.Count, "모두 예상 실패인 완성 report도 감사용으로 저장한다");
    }

    /// <summary>
    /// Gateway가 자기 ID와 다른 성공 견적을 반환하면 신뢰하지 않고 External 실패로 바꾸는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>ID 불일치 수집이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestSupplierIdMismatchAsync(Checkbook checks)
    {
        var repository = new InMemoryQuoteRoundRepository();
        var mismatchedGateway = new ScriptedSupplierQuoteGateway(
            "ExpectedSupplier",
            TimeSpan.Zero,
            Result<SupplierQuote>.Success(RequireQuote("OtherSupplier", 10m, NeedBy)));
        var service = new CollectSupplierQuotesService(
            [mismatchedGateway],
            new LowestTotalCostPolicy(),
            repository);

        var report = await service.CollectAsync(RequireRequest("GEAR", 1, NeedBy));
        var attempt = report.CompletionOrder[0];
        checks.True(!attempt.Outcome.IsSuccess, "공급업체 ID 불일치는 성공으로 받아들이지 않는다");
        checks.Equal(
            "supplier_quote.supplier_id_mismatch",
            attempt.Outcome.Error.Code,
            "ID 불일치 오류 코드는 안정적이다");
        checks.Equal(ErrorKind.External, attempt.Outcome.Error.Kind, "ID 불일치는 외부 응답 실패다");
        checks.Equal(null, report.SelectedQuote, "ID가 불일치한 견적은 선택 대상이 아니다");
        checks.Equal(1, repository.Count, "예상 가능한 ID 불일치 report는 저장한다");
    }

    /// <summary>
    /// 한 Gateway Task의 예상 밖 fault가 blocked sibling을 취소하고 관찰한 뒤 원래 예외를 보존하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>sibling 취소와 예외 전파 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestGatewayFaultCancelsSiblingsAsync(Checkbook checks)
    {
        var broken = new ControlledGateway("Broken");
        var blocked = new ControlledGateway("Blocked");
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            [broken, blocked],
            new LowestTotalCostPolicy(),
            repository);
        var collectTask = service.CollectAsync(RequireRequest("GEAR", 1, NeedBy));

        await Task.WhenAll(broken.Started, blocked.Started).WaitAsync(TimeSpan.FromSeconds(2));
        broken.Fail(new InvalidOperationException("gateway boom"));

        try
        {
            await collectTask;
            checks.True(false, "Gateway fault는 호출자에게 전파돼야 한다");
        }
        catch (InvalidOperationException exception)
        {
            checks.Equal("gateway boom", exception.Message, "sibling 취소가 최초 Gateway 예외를 가리지 않는다");
        }

        await blocked.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(2));
        checks.Equal(0, repository.Count, "Gateway fault 뒤 불완전 report는 저장하지 않는다");
    }

    /// <summary>
    /// 진행 callback의 fault도 blocked sibling을 취소하고 관찰한 뒤 원래 callback 예외를 보존하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>sibling 취소와 callback 예외 전파 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestCallbackFaultCancelsSiblingsAsync(Checkbook checks)
    {
        var first = new ControlledGateway("First");
        var blocked = new ControlledGateway("Blocked");
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            [first, blocked],
            new LowestTotalCostPolicy(),
            repository);
        var collectTask = service.CollectAsync(
            RequireRequest("GEAR", 1, NeedBy),
            _ => throw new ApplicationException("callback boom"));

        await Task.WhenAll(first.Started, blocked.Started).WaitAsync(TimeSpan.FromSeconds(2));
        first.Complete(Result<SupplierQuote>.Success(RequireQuote("First", 10m, NeedBy)));

        try
        {
            await collectTask;
            checks.True(false, "callback fault는 호출자에게 전파돼야 한다");
        }
        catch (ApplicationException exception)
        {
            checks.Equal("callback boom", exception.Message, "sibling 취소가 최초 callback 예외를 가리지 않는다");
        }

        await blocked.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(2));
        checks.Equal(0, repository.Count, "callback fault 뒤 불완전 report는 저장하지 않는다");
    }

    /// <summary>
    /// programmer bug나 network library 결함 같은 예상 밖 예외를 업무 Result로 삼키지 않는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>예외 전파 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestUnexpectedExceptionAsync(Checkbook checks)
    {
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            [new ThrowingGateway("Broken")],
            new LowestTotalCostPolicy(),
            repository);

        // async lambda는 Task를 반환하는 짧은 비동기 동작을 assertion 도구에 전달합니다.
        await checks.ThrowsAsync<InvalidOperationException>(
            async () => await service.CollectAsync(RequireRequest("GEAR", 1, NeedBy)),
            "예상 밖 Gateway 예외는 호출자에게 전파된다");
        checks.Equal(0, repository.Count, "예상 밖 예외가 나면 불완전 report를 저장하지 않는다");
    }

    /// <summary>
    /// 취소가 업무 실패로 바뀌지 않고 전파되며 최종 Repository 저장을 막는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>취소 전파 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestCancellationPreventsSaveAsync(Checkbook checks)
    {
        var blocked = new ControlledGateway("Blocked");
        var repository = new InMemoryQuoteRoundRepository();
        var service = new CollectSupplierQuotesService(
            [blocked],
            new LowestTotalCostPolicy(),
            repository);
        // using var는 test가 성공하거나 예외가 나도 CancellationTokenSource를 반드시 Dispose합니다.
        using var cancellationSource = new CancellationTokenSource();

        var collectTask = service.CollectAsync(
            RequireRequest("GEAR", 1, NeedBy),
            cancellationToken: cancellationSource.Token);
        await blocked.Started.WaitAsync(TimeSpan.FromSeconds(2));
        cancellationSource.Cancel();

        await checks.ThrowsAsync<OperationCanceledException>(
            async () => await collectTask,
            "취소는 Result 실패가 아니라 OperationCanceledException이다");
        checks.Equal(0, repository.Count, "취소된 수집은 최종 report를 저장하지 않는다");
    }

    /// <summary>
    /// 비동기 callback이 기대한 조건을 만들 때까지 짧게 양보하며 기다리고 무한 대기를 제한합니다.
    /// </summary>
    /// <param name="condition">기다림을 끝낼 조건을 계산하는 함수입니다.</param>
    /// <returns>조건이 2초 안에 true가 되면 완료되는 Task를 반환합니다.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        // target-typed new는 왼쪽 CancellationTokenSource 형식이 분명해 형식 이름을 생략합니다.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    /// <summary>
    /// 테스트 상수로 만든 요청이 유효하다고 확인하고 성공 값을 꺼냅니다.
    /// </summary>
    /// <param name="sku">테스트 상품 코드입니다.</param>
    /// <param name="quantity">테스트 수량입니다.</param>
    /// <param name="needBy">테스트 최종 납기일입니다.</param>
    /// <returns>검증이 끝난 QuoteRequest를 반환합니다.</returns>
    private static QuoteRequest RequireRequest(string sku, int quantity, DateOnly needBy)
    {
        var result = QuoteRequest.Create(sku, quantity, needBy);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    /// <summary>
    /// 테스트 상수로 만든 견적이 유효하다고 확인하고 성공 값을 꺼냅니다.
    /// </summary>
    /// <param name="supplierId">테스트 공급업체 ID입니다.</param>
    /// <param name="unitPrice">테스트 개당 가격입니다.</param>
    /// <param name="deliveryDate">테스트 약속 납기일입니다.</param>
    /// <returns>검증이 끝난 SupplierQuote를 반환합니다.</returns>
    private static SupplierQuote RequireQuote(
        string supplierId,
        decimal unitPrice,
        DateOnly deliveryDate)
    {
        var result = SupplierQuote.Create(supplierId, unitPrice, deliveryDate);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    /// <summary>
    /// 테스트가 원하는 시점에 완료시켜 Task.WhenEach 순서를 시간 지연 없이 제어하는 fake Gateway입니다.
    /// </summary>
    private sealed class ControlledGateway : ISupplierQuoteGateway
    {
        // RunContinuationsAsynchronously는 Complete를 호출한 test thread에서 service 연속 동작이 즉시 겹치지 않게 합니다.
        private readonly TaskCompletionSource<Result<SupplierQuote>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _cancellationObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        /// <summary>
        /// 수동 완료할 공급업체 fake를 만듭니다.
        /// supplierId 한 파라미터로 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="supplierId">완료 순서에서 식별할 공급업체 ID입니다.</param>
        public ControlledGateway(string supplierId)
        {
            SupplierId = supplierId;
        }

        /// <summary>이 fake의 공급업체 ID를 반환합니다.</summary>
        public string SupplierId { get; }

        /// <summary>RequestQuoteAsync가 시작되면 완료되는 관찰용 Task를 반환합니다.</summary>
        // =>는 getter 한 식의 결과를 바로 반환하는 expression-bodied member 문법입니다.
        public Task Started => _started.Task;

        /// <summary>RequestQuoteAsync가 취소를 관찰하면 완료되는 검증용 Task를 반환합니다.</summary>
        public Task CancellationObserved => _cancellationObserved.Task;

        /// <summary>이 fake가 호출된 횟수를 원자적으로 읽어 반환합니다.</summary>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 호출 시작을 기록하고 test가 Complete를 호출하거나 취소할 때까지 비동기로 기다립니다.
        /// </summary>
        /// <param name="request">service가 모든 공급업체에 보낸 동일한 요청입니다.</param>
        /// <param name="cancellationToken">기다림을 중단할 수 있는 호출자 취소 신호입니다.</param>
        /// <returns>test가 지정한 Result를 담은 Task를 반환합니다.</returns>
        public async Task<Result<SupplierQuote>> RequestQuoteAsync(
            QuoteRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            Interlocked.Increment(ref _callCount);
            _started.TrySetResult(true);
            try
            {
                return await _completion.Task.WaitAsync(cancellationToken);
            }
            // catch (...) when (...)은 예외 필터입니다. OperationCanceledException 중에서도 이 메서드가 받은
            // cancellationToken이 실제로 취소된 경우만 잡아, Gateway가 취소를 관찰했다는 신호를 기록합니다.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _cancellationObserved.TrySetResult(true);
                throw;
            }
        }

        /// <summary>
        /// 대기 중인 Gateway Task를 지정한 성공 또는 실패 Result로 완료합니다.
        /// </summary>
        /// <param name="outcome">service가 관찰해야 할 성공 견적 또는 예상 실패입니다.</param>
        /// <returns>완료 신호만 보내므로 반환값은 없습니다.</returns>
        public void Complete(Result<SupplierQuote> outcome)
        {
            if (!_completion.TrySetResult(outcome))
            {
                throw new InvalidOperationException("ControlledGateway는 한 번만 완료할 수 있습니다.");
            }
        }

        /// <summary>
        /// 대기 중인 Gateway Task를 예상 밖 예외 상태로 완료합니다.
        /// </summary>
        /// <param name="exception">Application Service가 Result로 숨기지 않고 전파해야 할 최초 예외입니다.</param>
        /// <returns>fault 신호만 보내므로 반환값은 없습니다.</returns>
        public void Fail(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (!_completion.TrySetException(exception))
            {
                throw new InvalidOperationException("ControlledGateway는 한 번만 완료할 수 있습니다.");
            }
        }
    }

    /// <summary>
    /// 예상 밖 결함이 Result로 바뀌지 않고 전파되는지 확인하는 예외 발생 fake Gateway입니다.
    /// </summary>
    private sealed class ThrowingGateway : ISupplierQuoteGateway
    {
        /// <summary>
        /// 항상 예외를 낼 공급업체 fake를 만듭니다.
        /// supplierId 한 파라미터로 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="supplierId">오류가 난 공급업체를 식별할 ID입니다.</param>
        public ThrowingGateway(string supplierId)
        {
            SupplierId = supplierId;
        }

        /// <summary>이 fake의 공급업체 ID를 반환합니다.</summary>
        public string SupplierId { get; }

        /// <summary>
        /// 비동기 경계를 한 번 지난 뒤 예상 밖 InvalidOperationException을 발생시킵니다.
        /// </summary>
        /// <param name="request">호출 계약을 만족하기 위한 견적 요청입니다.</param>
        /// <param name="cancellationToken">호출 계약을 만족하기 위한 취소 신호입니다.</param>
        /// <returns>정상적으로 값을 반환하지 않고 예외 상태가 되는 Task입니다.</returns>
        public async Task<Result<SupplierQuote>> RequestQuoteAsync(
            QuoteRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            throw new InvalidOperationException("예상 밖 gateway 결함");
        }
    }
}
