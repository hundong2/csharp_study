using OrderPaymentReconciliation.Application.Ports;
using OrderPaymentReconciliation.Application;
using OrderPaymentReconciliation.Domain;
using OrderPaymentReconciliation.Infrastructure;

namespace OrderPaymentReconciliation.SelfTesting;

/// <summary>
/// Domain 불변식, LeftJoin 보존성, 다섯 상태, 중복 데이터 방어, 취소 전달을 자동 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    // new(...)는 왼쪽 DateTimeOffset 형식이 분명할 때 형식 이름을 생략하는 target-typed new 문법입니다.
    private static readonly DateTimeOffset BaseTime =
        new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 모든 self-test를 독립된 단계로 실행하고 process exit code를 반환합니다.
    /// </summary>
    /// <returns>모든 검사가 통과하면 0, 하나라도 실패하면 1을 담은 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        var checks = new Checkbook();
        TestMoneyValidation(checks);
        TestEntityNormalization(checks);
        await TestFiveStatusesAsync(checks);
        TestReportSnapshot(checks);
        TestPolicyMatchBoundary(checks);
        await TestDuplicateInputAsync(checks);
        await TestCancellationAsync(checks);
        return checks.Finish();
    }

    /// <summary>
    /// Money가 통화를 정규화하고 음수·과도한 소수 자리·잘못된 코드를 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestMoneyValidation(Checkbook checks)
    {
        var normalized = Money.Create(12.30m, " krw ");
        checks.True(normalized.IsSuccess, "Money는 유효한 금액을 받아들인다");
        checks.Equal(
            "KRW",
            normalized.IsSuccess ? normalized.Value.Currency : "<failure>",
            "통화 코드를 대문자로 정규화한다");
        checks.Equal(
            12.30m,
            normalized.IsSuccess ? normalized.Value.Amount : decimal.MinValue,
            "decimal 금액을 정확히 보존한다");

        var negative = Money.Create(-1m, "KRW");
        checks.True(!negative.IsSuccess, "음수 금액을 거절한다");
        checks.Equal(
            "money.amount_negative",
            negative.IsSuccess ? "<success>" : negative.Error.Code,
            "음수 금액 오류 코드는 안정적이다");

        var excessiveScale = Money.Create(1.001m, "KRW");
        checks.Equal(
            "money.amount_scale",
            excessiveScale.IsSuccess ? "<success>" : excessiveScale.Error.Code,
            "소수 셋째 자리 금액을 거절한다");

        var invalidCurrency = Money.Create(1m, "K1W");
        checks.Equal(
            "money.currency_invalid",
            invalidCurrency.IsSuccess ? "<success>" : invalidCurrency.Error.Code,
            "영문 세 글자가 아닌 통화를 거절한다");
    }

    /// <summary>
    /// Order와 Payment가 식별자 공백을 제거하고 DateTimeOffset을 UTC로 바꾸는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestEntityNormalization(Checkbook checks)
    {
        var localTime = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.FromHours(9));
        var order = Order.Create(" ORD-TRIM ", " CUS-TRIM ", CreateMoney(1_000m, "KRW"), localTime);
        checks.Equal(
            "ORD-TRIM",
            order.IsSuccess ? order.Value.OrderId : "<failure>",
            "주문 ID 앞뒤 공백을 제거한다");
        checks.Equal(
            TimeSpan.Zero,
            order.IsSuccess ? order.Value.PlacedAtUtc.Offset : TimeSpan.MinValue,
            "주문 시각을 UTC로 정규화한다");

        var payment = Payment.Create(" ", "ORD-TRIM", CreateMoney(1_000m, "KRW"), localTime);
        checks.True(!payment.IsSuccess, "빈 결제 ID를 거절한다");
        checks.Equal(
            "payment.id_required",
            payment.IsSuccess ? "<success>" : payment.Error.Code,
            "빈 결제 ID 오류 코드는 안정적이다");
    }

    /// <summary>
    /// LeftJoin이 결제 없는 주문을 남기고 Strategy가 다섯 대사 상태와 차액을 올바르게 만드는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 대사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestFiveStatusesAsync(Checkbook checks)
    {
        IReadOnlyList<Order> orders =
        [
            CreateOrder("ORD-1", 10_000m, "KRW"),
            CreateOrder("ORD-2", 20_000m, "KRW"),
            CreateOrder("ORD-3", 30_000m, "KRW"),
            CreateOrder("ORD-4", 40m, "USD")
        ];
        IReadOnlyList<Payment> payments =
        [
            CreatePayment("PAY-1", "ORD-1", 10_000m, "KRW"),
            CreatePayment("PAY-3", "ORD-3", 25_000m, "KRW"),
            CreatePayment("PAY-4", "ORD-4", 40m, "EUR"),
            CreatePayment("PAY-X", "ORD-X", 5_000m, "KRW")
        ];

        var result = await BuildService(orders, payments)
            .ReconcileAsync(CancellationToken.None);

        checks.True(result.IsSuccess, "유효한 대사 입력은 성공한다");
        if (!result.IsSuccess)
        {
            return;
        }

        var report = result.Value;
        checks.Equal(5, report.Lines.Length, "네 주문과 한 고아 결제를 모두 보고한다");
        checks.Equal(1, report.Count(ReconciliationStatus.Matched), "일치 한 건을 센다");
        checks.Equal(1, report.Count(ReconciliationStatus.MissingPayment), "누락 한 건을 센다");
        checks.Equal(1, report.Count(ReconciliationStatus.AmountMismatch), "금액 불일치 한 건을 센다");
        checks.Equal(1, report.Count(ReconciliationStatus.CurrencyMismatch), "통화 불일치 한 건을 센다");
        checks.Equal(1, report.Count(ReconciliationStatus.OrphanPayment), "고아 결제 한 건을 센다");
        checks.Equal(4, report.ReviewCount, "일치 외 네 건을 검토 대상으로 센다");

        if (report.Lines.Length != 5
            || Enum.GetValues<ReconciliationStatus>()
                .Any(status => report.Count(status) != 1))
        {
            return;
        }

        var matched = report.Lines.Single(line => line.Status == ReconciliationStatus.Matched);
        var missing = report.Lines.Single(line => line.Status == ReconciliationStatus.MissingPayment);
        var amountMismatch = report.Lines.Single(line => line.Status == ReconciliationStatus.AmountMismatch);
        var currencyMismatch = report.Lines.Single(line => line.Status == ReconciliationStatus.CurrencyMismatch);
        var orphan = report.Lines.Single(line => line.Status == ReconciliationStatus.OrphanPayment);

        checks.True(!matched.NeedsReview, "C# 14 확장 속성은 일치 행을 검토 대상에서 제외한다");
        checks.True(missing.ActualAmount is null, "결제 누락은 실제 금액을 null로 보존한다");
        checks.Equal(-5_000m, amountMismatch.Difference, "부족 결제의 차액은 actual - expected이다");
        checks.True(currencyMismatch.Difference is null, "통화가 다르면 의미 없는 차액을 만들지 않는다");
        checks.True(orphan.ExpectedAmount is null, "고아 결제는 존재하지 않는 기대 금액을 null로 둔다");
        checks.Equal(
            "ORD-1,ORD-2,ORD-3,ORD-4,ORD-X",
            string.Join(',', report.Lines.Select(line => line.OrderId)),
            "보고서는 OrderId 순서로 결정적으로 정렬된다");
    }

    /// <summary>
    /// 보고서가 원본 List의 이후 변경에 영향받지 않는 불변 snapshot인지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestReportSnapshot(Checkbook checks)
    {
        var policy = new AmountAndCurrencyPolicy();
        var mutableLines = new List<ReconciliationLine>
        {
            policy.MarkOrphan(CreatePayment("PAY-Z", "ORD-Z", 1m, "KRW")),
            policy.Compare(CreateOrder("ORD-A", 1m, "KRW"), null)
        };
        var report = ReconciliationReport.Create(mutableLines);
        mutableLines.Add(policy.MarkOrphan(CreatePayment("PAY-LATE", "ORD-LATE", 1m, "KRW")));

        checks.Equal(2, report.Lines.Length, "보고서는 원본 List 변경과 분리된 snapshot이다");
        checks.Equal("ORD-A", report.Lines[0].OrderId, "Report factory가 정렬 불변식을 직접 보장한다");

    }

    /// <summary>
    /// Matched 상태의 허용 범위는 Strategy가 결정하고 Domain factory는 실제 차액을 잃지 않는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestPolicyMatchBoundary(Checkbook checks)
    {
        var toleratedMatch = ReconciliationLine.ForPolicyMatch(
            CreateOrder("ORD-TOLERATED", 10m, "KRW"),
            CreatePayment("PAY-TOLERATED", "ORD-TOLERATED", 9m, "KRW"),
            "Strategy가 1원 차이를 허용했습니다.");
        checks.Equal(
            -1m,
            toleratedMatch.Difference,
            "Matched factory는 Strategy의 결론을 존중하고 실제 차액을 보존한다");
    }

    /// <summary>
    /// 중복 주문 ID, 중복 결제 ID, 한 주문의 복수 결제를 각각 안정적인 Result 오류로 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>세 가지 비동기 대사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestDuplicateInputAsync(Checkbook checks)
    {
        var firstOrder = CreateOrder("ORD-A", 100m, "KRW");
        var secondOrder = CreateOrder("ORD-B", 200m, "KRW");

        var duplicateOrderResult = await BuildService(
                [firstOrder, firstOrder],
                [])
            .ReconcileAsync(CancellationToken.None);
        checks.Equal(
            "data.duplicate_order_id",
            duplicateOrderResult.IsSuccess ? "<success>" : duplicateOrderResult.Error.Code,
            "중복 주문 ID를 명시적 오류로 거절한다");

        var duplicatePaymentResult = await BuildService(
                [firstOrder, secondOrder],
                [
                    CreatePayment("PAY-DUP", "ORD-A", 100m, "KRW"),
                    CreatePayment("PAY-DUP", "ORD-B", 200m, "KRW")
                ])
            .ReconcileAsync(CancellationToken.None);
        checks.Equal(
            "data.duplicate_payment_id",
            duplicatePaymentResult.IsSuccess ? "<success>" : duplicatePaymentResult.Error.Code,
            "중복 결제 ID를 명시적 오류로 거절한다");

        var multiplePaymentsResult = await BuildService(
                [firstOrder],
                [
                    CreatePayment("PAY-A", "ORD-A", 40m, "KRW"),
                    CreatePayment("PAY-B", "ORD-A", 60m, "KRW")
                ])
            .ReconcileAsync(CancellationToken.None);
        checks.Equal(
            "data.multiple_payments_for_order",
            multiplePaymentsResult.IsSuccess ? "<success>" : multiplePaymentsResult.Error.Code,
            "한 주문의 복수 결제를 조용히 합치지 않는다");
    }

    /// <summary>
    /// 호출자가 보낸 취소 신호를 Result 실패로 삼키지 않고 OperationCanceledException으로 전파하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>취소 예외 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestCancellationAsync(Checkbook checks)
    {
        // using var는 method가 끝날 때 CancellationTokenSource.Dispose를 자동 호출하는 using declaration입니다.
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var service = BuildService([], []);

        // lambda는 나중에 실행할 비동기 호출을 값으로 Checks 도구에 전달합니다.
        await checks.ThrowsAsync<OperationCanceledException>(
            () => service.ReconcileAsync(cancellationSource.Token),
            "취소는 예상 업무 오류와 구분해 호출자에게 전파한다");

        using var afterReadCancellationSource = new CancellationTokenSource();
        var cancelAfterReadService = new ReconcileOrdersService(
            new InMemoryOrderRepository([]),
            new CancelingPaymentRepository(afterReadCancellationSource),
            new AmountAndCurrencyPolicy());
        await checks.ThrowsAsync<OperationCanceledException>(
            () => cancelAfterReadService.ReconcileAsync(afterReadCancellationSource.Token),
            "Repository 조회 직후 취소도 CPU 대사 전에 확인한다");
    }

    /// <summary>
    /// 테스트가 원하는 주문·결제 snapshot으로 Port와 Adapter를 조립합니다.
    /// </summary>
    /// <param name="orders">테스트 저장소에 넣을 주문입니다.</param>
    /// <param name="payments">테스트 저장소에 넣을 결제입니다.</param>
    /// <returns>외부 자원 없이 실행 가능한 Application Service를 반환합니다.</returns>
    private static ReconcileOrdersService BuildService(
        IEnumerable<Order> orders,
        IEnumerable<Payment> payments)
    {
        return new ReconcileOrdersService(
            new InMemoryOrderRepository(orders),
            new InMemoryPaymentRepository(payments),
            new AmountAndCurrencyPolicy());
    }

    /// <summary>
    /// 테스트 의도를 흐리는 반복 생성 코드를 감추고 항상 유효한 주문을 만듭니다.
    /// </summary>
    /// <param name="orderId">테스트에서 구분할 주문 ID입니다.</param>
    /// <param name="amount">주문의 기대 금액입니다.</param>
    /// <param name="currency">주문의 기대 통화입니다.</param>
    /// <returns>검증을 통과한 Order를 반환합니다.</returns>
    private static Order CreateOrder(string orderId, decimal amount, string currency)
    {
        return Must(Order.Create(orderId, "CUS-TEST", CreateMoney(amount, currency), BaseTime));
    }

    /// <summary>
    /// 테스트 의도를 흐리는 반복 생성 코드를 감추고 항상 유효한 결제를 만듭니다.
    /// </summary>
    /// <param name="paymentId">테스트에서 구분할 결제 ID입니다.</param>
    /// <param name="orderId">결제가 연결될 주문 ID입니다.</param>
    /// <param name="amount">실제 정산 금액입니다.</param>
    /// <param name="currency">실제 정산 통화입니다.</param>
    /// <returns>검증을 통과한 Payment를 반환합니다.</returns>
    private static Payment CreatePayment(
        string paymentId,
        string orderId,
        decimal amount,
        string currency)
    {
        return Must(
            Payment.Create(paymentId, orderId, CreateMoney(amount, currency), BaseTime.AddMinutes(1)));
    }

    /// <summary>
    /// 테스트용 숫자와 통화를 검증된 Money 값 객체로 만듭니다.
    /// </summary>
    /// <param name="amount">테스트에서 사용할 금액입니다.</param>
    /// <param name="currency">테스트에서 사용할 통화 코드입니다.</param>
    /// <returns>검증을 통과한 Money를 반환합니다.</returns>
    private static Money CreateMoney(decimal amount, string currency)
    {
        return Must(Money.Create(amount, currency));
    }

    /// <summary>
    /// 테스트 fixture 자체의 오류를 즉시 예외로 바꿔 실제 검증 실패와 구분합니다.
    /// </summary>
    /// <typeparam name="T">성공 시 꺼낼 null이 아닌 값의 형식입니다.</typeparam>
    /// <param name="result">항상 성공해야 하는 테스트 데이터 생성 결과입니다.</param>
    /// <returns>Result 안의 성공 값을 반환합니다.</returns>
    /// <exception cref="InvalidOperationException">테스트 fixture가 도메인 규칙을 어겼을 때 발생합니다.</exception>
    private static T Must<T>(Result<T> result)
        where T : notnull
    {
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"테스트 fixture 오류: {result.Error.Code} - {result.Error.Message}");
    }

    /// <summary>
    /// 결제 snapshot을 반환하는 순간 취소 신호를 보내 조회 이후 checkpoint를 검증하는 test double입니다.
    /// </summary>
    private sealed class CancelingPaymentRepository : IPaymentRepository
    {
        private readonly CancellationTokenSource _cancellationSource;

        /// <summary>
        /// 취소 시점을 test가 제어하도록 사용할 source를 받습니다.
        /// </summary>
        /// <param name="cancellationSource">ListAsync가 값을 반환하기 직전에 취소할 source입니다.</param>
        public CancelingPaymentRepository(CancellationTokenSource cancellationSource)
        {
            _cancellationSource = cancellationSource
                ?? throw new ArgumentNullException(nameof(cancellationSource));
        }

        /// <summary>
        /// token source를 취소한 뒤 빈 결제 목록을 반환해 service의 조회 이후 확인 지점을 자극합니다.
        /// </summary>
        /// <param name="cancellationToken">호출 시점에 이미 취소됐는지 먼저 확인할 신호입니다.</param>
        /// <returns>빈 결제 목록을 담은 완료된 Task를 반환합니다.</returns>
        public Task<IReadOnlyList<Payment>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cancellationSource.Cancel();
            return Task.FromResult<IReadOnlyList<Payment>>([]);
        }
    }
}
