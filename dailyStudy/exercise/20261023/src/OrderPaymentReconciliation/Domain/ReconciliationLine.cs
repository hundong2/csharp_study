namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 한 주문 또는 고아 결제에 대한 판정 근거를 모두 보존하는 불변 보고서 행입니다.
/// private 생성자와 상태별 factory가 "불변이지만 모순된 행"도 만들어지지 않게 보호합니다.
/// </summary>
public sealed record ReconciliationLine
{
    /// <summary>
    /// 상태별 factory가 검증한 값만 실제 record로 저장합니다.
    /// </summary>
    /// <param name="orderId">주문 쪽 또는 결제가 주장한 주문 식별자입니다.</param>
    /// <param name="paymentId">연결된 결제가 없으면 null인 결제 식별자입니다.</param>
    /// <param name="expectedAmount">고아 결제라 주문이 없으면 null인 기대 금액입니다.</param>
    /// <param name="actualAmount">결제가 없으면 null인 실제 정산 금액입니다.</param>
    /// <param name="difference">같은 통화일 때 실제 금액에서 기대 금액을 뺀 값이며 비교할 수 없으면 null입니다.</param>
    /// <param name="status">업무 규칙이 결정한 대사 상태입니다.</param>
    /// <param name="reason">사람이 검토할 수 있는 판정 이유입니다.</param>
    private ReconciliationLine(
        string orderId,
        string? paymentId,
        Money? expectedAmount,
        Money? actualAmount,
        decimal? difference,
        ReconciliationStatus status,
        string reason)
    {
        OrderId = orderId;
        PaymentId = paymentId;
        ExpectedAmount = expectedAmount;
        ActualAmount = actualAmount;
        Difference = difference;
        Status = status;
        Reason = reason;
    }

    /// <summary>주문 쪽 또는 결제가 주장한 주문 식별자입니다.</summary>
    public string OrderId { get; }

    /// <summary>연결된 결제가 없으면 null인 결제 식별자입니다.</summary>
    public string? PaymentId { get; }

    /// <summary>고아 결제라 주문이 없으면 null인 기대 금액입니다.</summary>
    public Money? ExpectedAmount { get; }

    /// <summary>결제가 없으면 null인 실제 정산 금액입니다.</summary>
    public Money? ActualAmount { get; }

    /// <summary>같은 통화일 때 actual - expected이며 비교할 수 없으면 null입니다.</summary>
    public decimal? Difference { get; }

    /// <summary>업무 규칙이 결정한 다섯 상태 중 하나입니다.</summary>
    public ReconciliationStatus Status { get; }

    /// <summary>사람이 검토할 수 있는 판정 이유입니다.</summary>
    public string Reason { get; }

    /// <summary>
    /// Strategy가 업무상 일치라고 판정한 같은 통화의 주문·결제를 일치 행으로 만듭니다.
    /// 금액 차이 허용 범위는 Strategy의 책임이므로 이 factory는 정확한 금액 일치를 다시 강제하지 않습니다.
    /// </summary>
    /// <param name="order">기대 금액을 가진 기준 주문입니다.</param>
    /// <param name="payment">같은 주문 ID와 통화를 가지며 Strategy가 허용한 결제입니다.</param>
    /// <param name="reason">Strategy가 남긴 일치 판정 이유입니다.</param>
    /// <returns>실제 차액을 보존한 Matched 행을 반환합니다.</returns>
    internal static ReconciliationLine ForPolicyMatch(
        Order order,
        Payment payment,
        string reason)
    {
        ValidatePair(order, payment, reason);
        if (!string.Equals(order.Total.Currency, payment.SettledAmount.Currency, StringComparison.Ordinal))
        {
            throw new ArgumentException("Matched 행은 차액을 계산할 수 있도록 통화가 같아야 합니다.", nameof(payment));
        }

        var difference = payment.SettledAmount.Amount - order.Total.Amount;
        return new ReconciliationLine(
            order.OrderId,
            payment.PaymentId,
            order.Total,
            payment.SettledAmount,
            difference,
            ReconciliationStatus.Matched,
            reason);
    }

    /// <summary>
    /// 연결된 결제가 없는 주문을 누락 행으로 만듭니다.
    /// </summary>
    /// <param name="order">결제를 찾지 못한 기준 주문입니다.</param>
    /// <param name="reason">누락 판정 이유입니다.</param>
    /// <returns>PaymentId와 실제 금액이 null인 MissingPayment 행을 반환합니다.</returns>
    internal static ReconciliationLine ForMissing(Order order, string reason)
    {
        ArgumentNullException.ThrowIfNull(order);
        ValidateReason(reason);
        return new ReconciliationLine(
            order.OrderId,
            null,
            order.Total,
            null,
            null,
            ReconciliationStatus.MissingPayment,
            reason);
    }

    /// <summary>
    /// 같은 통화지만 금액이 다른 주문·결제를 불일치 행으로 만듭니다.
    /// </summary>
    /// <param name="order">기대 금액을 가진 기준 주문입니다.</param>
    /// <param name="payment">같은 주문과 통화지만 금액이 다른 결제입니다.</param>
    /// <param name="reason">금액 불일치 판정 이유입니다.</param>
    /// <returns>actual - expected 차액을 가진 AmountMismatch 행을 반환합니다.</returns>
    internal static ReconciliationLine ForAmountMismatch(
        Order order,
        Payment payment,
        string reason)
    {
        ValidatePair(order, payment, reason);
        if (!string.Equals(order.Total.Currency, payment.SettledAmount.Currency, StringComparison.Ordinal))
        {
            throw new ArgumentException("AmountMismatch 행은 통화가 같아야 합니다.", nameof(payment));
        }

        var difference = payment.SettledAmount.Amount - order.Total.Amount;
        if (difference == 0m)
        {
            throw new ArgumentException("AmountMismatch 행은 금액이 달라야 합니다.", nameof(payment));
        }

        return new ReconciliationLine(
            order.OrderId,
            payment.PaymentId,
            order.Total,
            payment.SettledAmount,
            difference,
            ReconciliationStatus.AmountMismatch,
            reason);
    }

    /// <summary>
    /// 통화가 서로 달라 차액을 계산할 수 없는 주문·결제를 불일치 행으로 만듭니다.
    /// </summary>
    /// <param name="order">기대 통화를 가진 기준 주문입니다.</param>
    /// <param name="payment">다른 통화로 정산된 결제입니다.</param>
    /// <param name="reason">통화 불일치 판정 이유입니다.</param>
    /// <returns>Difference가 null인 CurrencyMismatch 행을 반환합니다.</returns>
    internal static ReconciliationLine ForCurrencyMismatch(
        Order order,
        Payment payment,
        string reason)
    {
        ValidatePair(order, payment, reason);
        if (string.Equals(order.Total.Currency, payment.SettledAmount.Currency, StringComparison.Ordinal))
        {
            throw new ArgumentException("CurrencyMismatch 행은 통화가 달라야 합니다.", nameof(payment));
        }

        return new ReconciliationLine(
            order.OrderId,
            payment.PaymentId,
            order.Total,
            payment.SettledAmount,
            null,
            ReconciliationStatus.CurrencyMismatch,
            reason);
    }

    /// <summary>
    /// 주문을 찾지 못한 결제를 기대 금액 없는 고아 결제 행으로 만듭니다.
    /// </summary>
    /// <param name="payment">존재하지 않는 주문 ID를 가리키는 결제입니다.</param>
    /// <param name="reason">고아 판정 이유입니다.</param>
    /// <returns>ExpectedAmount가 null인 OrphanPayment 행을 반환합니다.</returns>
    internal static ReconciliationLine ForOrphan(Payment payment, string reason)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ValidateReason(reason);
        return new ReconciliationLine(
            payment.OrderId,
            payment.PaymentId,
            null,
            payment.SettledAmount,
            null,
            ReconciliationStatus.OrphanPayment,
            reason);
    }

    /// <summary>
    /// 두 데이터가 실제 같은 주문을 가리키고 판정 이유가 비어 있지 않은지 공통 검증합니다.
    /// </summary>
    /// <param name="order">기준 주문입니다.</param>
    /// <param name="payment">주문과 비교할 결제입니다.</param>
    /// <param name="reason">보고서에 기록할 판정 이유입니다.</param>
    /// <returns>유효하지 않으면 예외를 던지고, 유효하면 반환값 없이 끝납니다.</returns>
    private static void ValidatePair(Order order, Payment payment, string reason)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(payment);
        ValidateReason(reason);
        if (!string.Equals(order.OrderId, payment.OrderId, StringComparison.Ordinal))
        {
            throw new ArgumentException("주문과 결제의 OrderId가 같아야 합니다.", nameof(payment));
        }
    }

    /// <summary>
    /// 사람이 읽을 판정 이유가 공백이 아닌지 검증합니다.
    /// </summary>
    /// <param name="reason">보고서에 기록할 판정 이유입니다.</param>
    /// <returns>공백이면 예외를 던지고, 유효하면 반환값 없이 끝납니다.</returns>
    private static void ValidateReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    }
}
