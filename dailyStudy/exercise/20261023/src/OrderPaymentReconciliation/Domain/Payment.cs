namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 결제 시스템에서 수신한 실제 정산 금액과 연결할 주문 ID를 담는 불변 Domain Model입니다.
/// </summary>
public sealed record Payment
{
    /// <summary>
    /// 검증된 결제 값만 저장하는 내부 생성자입니다.
    /// </summary>
    /// <param name="paymentId">정규화된 결제 식별자입니다.</param>
    /// <param name="orderId">결제가 주장하는 주문 식별자입니다.</param>
    /// <param name="settledAmount">실제로 정산된 금액과 통화입니다.</param>
    /// <param name="paidAtUtc">UTC로 정규화한 결제 시각입니다.</param>
    private Payment(
        string paymentId,
        string orderId,
        Money settledAmount,
        DateTimeOffset paidAtUtc)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        SettledAmount = settledAmount;
        PaidAtUtc = paidAtUtc;
    }

    /// <summary>중복 결제 데이터를 찾는 안정적인 결제 식별자입니다.</summary>
    public string PaymentId { get; }

    /// <summary>주문과 결제를 연결하는 외래 식별자 역할의 값입니다.</summary>
    public string OrderId { get; }

    /// <summary>결제 시스템이 실제로 정산했다고 기록한 금액입니다.</summary>
    public Money SettledAmount { get; }

    /// <summary>서버 지역 시간과 관계없이 비교 가능한 UTC 결제 시각입니다.</summary>
    public DateTimeOffset PaidAtUtc { get; }

    /// <summary>
    /// 외부 결제 값을 검증하고 식별자 공백과 시각을 정규화합니다.
    /// </summary>
    /// <param name="paymentId">비어 있지 않아야 하는 결제 식별자입니다.</param>
    /// <param name="orderId">결제가 연결될 주문 식별자입니다.</param>
    /// <param name="settledAmount">이미 검증된 실제 정산 금액입니다.</param>
    /// <param name="paidAt">어떤 offset으로 와도 UTC로 저장할 결제 시각입니다.</param>
    /// <returns>유효하면 Payment, 잘못되면 해당 필드를 가리키는 Error를 반환합니다.</returns>
    public static Result<Payment> Create(
        string? paymentId,
        string? orderId,
        Money? settledAmount,
        DateTimeOffset paidAt)
    {
        var normalizedPaymentId = paymentId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPaymentId))
        {
            return Result<Payment>.Failure(
                new Error("payment.id_required", "결제 ID는 필수입니다.", "paymentId"));
        }

        var normalizedOrderId = orderId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrderId))
        {
            return Result<Payment>.Failure(
                new Error("payment.order_required", "연결할 주문 ID는 필수입니다.", "orderId"));
        }

        if (settledAmount is null)
        {
            return Result<Payment>.Failure(
                new Error("payment.amount_required", "결제 금액은 필수입니다.", "settledAmount"));
        }

        return Result<Payment>.Success(
            new Payment(
                normalizedPaymentId,
                normalizedOrderId,
                settledAmount,
                paidAt.ToUniversalTime()));
    }
}
