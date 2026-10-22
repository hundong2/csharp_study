namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 결제가 반드시 맞춰야 하는 주문의 기대 금액과 식별 정보를 담는 불변 Domain Model입니다.
/// </summary>
public sealed record Order
{
    /// <summary>
    /// 검증된 주문 값만 저장하는 내부 생성자입니다.
    /// </summary>
    /// <param name="orderId">정규화된 주문 식별자입니다.</param>
    /// <param name="customerId">정규화된 고객 식별자입니다.</param>
    /// <param name="total">주문이 기대하는 총액과 통화입니다.</param>
    /// <param name="placedAtUtc">정렬과 감사를 위한 UTC 주문 시각입니다.</param>
    private Order(string orderId, string customerId, Money total, DateTimeOffset placedAtUtc)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Total = total;
        PlacedAtUtc = placedAtUtc;
    }

    /// <summary>대사에서 주문과 결제를 연결하는 안정적인 식별자입니다.</summary>
    public string OrderId { get; }

    /// <summary>주문 소유자를 식별하는 값입니다.</summary>
    public string CustomerId { get; }

    /// <summary>결제와 비교할 주문의 기대 금액입니다.</summary>
    public Money Total { get; }

    /// <summary>서버 지역 시간에 흔들리지 않도록 UTC로 정규화한 주문 시각입니다.</summary>
    public DateTimeOffset PlacedAtUtc { get; }

    /// <summary>
    /// 외부에서 받은 주문 값을 검증하고 공백과 시각을 정규화합니다.
    /// </summary>
    /// <param name="orderId">비어 있지 않아야 하는 주문 식별자입니다.</param>
    /// <param name="customerId">비어 있지 않아야 하는 고객 식별자입니다.</param>
    /// <param name="total">이미 검증된 주문 총액입니다.</param>
    /// <param name="placedAt">어떤 offset으로 와도 UTC로 저장할 주문 시각입니다.</param>
    /// <returns>유효하면 Order, 잘못되면 해당 필드를 가리키는 Error를 반환합니다.</returns>
    public static Result<Order> Create(
        string? orderId,
        string? customerId,
        Money? total,
        DateTimeOffset placedAt)
    {
        var normalizedOrderId = orderId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrderId))
        {
            return Result<Order>.Failure(
                new Error("order.id_required", "주문 ID는 필수입니다.", "orderId"));
        }

        var normalizedCustomerId = customerId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return Result<Order>.Failure(
                new Error("order.customer_required", "고객 ID는 필수입니다.", "customerId"));
        }

        // is null은 overloaded equality의 영향을 받지 않고 값이 실제 null인지 확인하는 pattern 문법입니다.
        if (total is null)
        {
            return Result<Order>.Failure(
                new Error("order.total_required", "주문 총액은 필수입니다.", "total"));
        }

        return Result<Order>.Success(
            new Order(
                normalizedOrderId,
                normalizedCustomerId,
                total,
                placedAt.ToUniversalTime()));
    }
}
