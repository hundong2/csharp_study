namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 한 공급업체가 제안한 단가와 납기일을 불변 값으로 나타냅니다.
/// decimal은 이진 부동소수점 오차를 피해야 하는 금액 계산에 적합합니다.
/// </summary>
public sealed record SupplierQuote
{
    /// <summary>
    /// 학습 예제에서 허용하는 개당 가격 상한입니다. 최대 수량과 곱해도 decimal 범위에 넉넉히 들어옵니다.
    /// </summary>
    public const decimal MaximumUnitPrice = 1_000_000_000m;

    /// <summary>
    /// factory 검증을 통과한 공급업체 견적 값을 보관합니다.
    /// 세 파라미터로 속성을 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="supplierId">공급업체를 안정적으로 식별하는 ID입니다.</param>
    /// <param name="unitPrice">상품 한 개의 가격입니다.</param>
    /// <param name="deliveryDate">공급업체가 약속한 도착 날짜입니다.</param>
    private SupplierQuote(string supplierId, decimal unitPrice, DateOnly deliveryDate)
    {
        SupplierId = supplierId;
        UnitPrice = unitPrice;
        DeliveryDate = deliveryDate;
    }

    /// <summary>공급업체 ID를 반환합니다.</summary>
    public string SupplierId { get; }

    /// <summary>상품 하나의 가격을 반환합니다.</summary>
    public decimal UnitPrice { get; }

    /// <summary>공급업체가 약속한 도착 날짜를 반환합니다.</summary>
    public DateOnly DeliveryDate { get; }

    /// <summary>
    /// 외부 공급업체 응답을 검증하고 유효한 SupplierQuote를 만듭니다.
    /// </summary>
    /// <param name="supplierId">공급업체 ID이며 앞뒤 공백은 제거됩니다. `string?`는 null 입력도 검증 대상임을 뜻합니다.</param>
    /// <param name="unitPrice">0보다 큰 개당 가격이어야 합니다.</param>
    /// <param name="deliveryDate">공급업체가 약속한 도착 날짜입니다.</param>
    /// <returns>검증 성공 시 SupplierQuote, 실패 시 외부 응답 오류를 반환합니다.</returns>
    public static Result<SupplierQuote> Create(
        string? supplierId,
        decimal unitPrice,
        DateOnly deliveryDate)
    {
        // ?.는 supplierId가 null이면 Trim을 호출하지 않고 null을 만드는 null-conditional 문법입니다.
        var normalizedSupplierId = supplierId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSupplierId))
        {
            return Result<SupplierQuote>.Failure(
                Error.External("supplier_quote.id_required", "공급업체 ID가 비어 있습니다."));
        }

        if (normalizedSupplierId.Length > 64)
        {
            return Result<SupplierQuote>.Failure(
                Error.External("supplier_quote.id_too_long", "공급업체 ID는 64자 이하여야 합니다."));
        }

        if (unitPrice <= 0m)
        {
            return Result<SupplierQuote>.Failure(
                Error.External("supplier_quote.price_not_positive", "단가는 0보다 커야 합니다."));
        }

        if (unitPrice > MaximumUnitPrice)
        {
            return Result<SupplierQuote>.Failure(
                Error.External(
                    "supplier_quote.price_too_large",
                    $"단가는 {MaximumUnitPrice:N0} 이하여야 합니다."));
        }

        return Result<SupplierQuote>.Success(
            new SupplierQuote(normalizedSupplierId, unitPrice, deliveryDate));
    }

    /// <summary>
    /// 이 견적의 단가에 요청 수량을 곱해 총액을 계산합니다.
    /// </summary>
    /// <param name="quantity">구매할 양수 수량입니다.</param>
    /// <returns>단가와 수량을 곱한 decimal 총액을 반환합니다.</returns>
    public decimal TotalFor(int quantity)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "수량은 양수여야 합니다.");
        }

        // checked는 정수·decimal 계산이 표현 가능한 범위를 넘으면 조용히 잘못된 값으로 넘어가지 않고
        // OverflowException을 발생시키는 문법입니다. 생성 단계의 상한 검증이 바뀌더라도 안전망을 남기기 위해 사용합니다.
        return checked(UnitPrice * quantity);
    }
}
