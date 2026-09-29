namespace OrderIntakeApi.Domain;

/// <summary>
/// 생성과 조회에서 같은 규칙을 재사용하는 정규화된 주문 식별자 Value Object입니다.
/// </summary>
// record는 값이 같으면 같은 식별자로 비교되는 불변 데이터 모양을 간결하게 선언합니다.
public sealed record OrderIdentifier
{
    /// <summary>
    /// 이미 검증한 문자열을 주문 식별자로 보관합니다.
    /// </summary>
    /// <param name="value">대문자로 정규화된 1~40자 식별자입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private OrderIdentifier(string value)
    {
        Value = value;
    }

    /// <summary>
    /// 저장과 비교에 사용할 정규화 문자열을 가져옵니다.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 외부 주문 ID를 trim·대문자화하고 길이와 허용 문자를 검사합니다.
    /// </summary>
    /// <param name="value">JSON 본문이나 URL 경로에서 받은 주문 식별자입니다.</param>
    /// <returns>유효하면 OrderId, 아니면 같은 `order.id.invalid` 실패 Result를 반환합니다.</returns>
    public static Result<OrderIdentifier> Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 40)
        {
            return Invalid();
        }

        var allCharactersAllowed = normalized.All(
            character => char.IsAsciiLetterOrDigit(character) || character == '-');
        return allCharactersAllowed
            ? Result<OrderIdentifier>.Success(new OrderIdentifier(normalized))
            : Invalid();
    }

    /// <summary>
    /// 생성과 조회가 공유할 주문 ID 오류 Result를 한곳에서 만듭니다.
    /// </summary>
    /// <returns>안정적인 `order.id.invalid` 코드가 든 실패 Result를 반환합니다.</returns>
    private static Result<OrderIdentifier> Invalid()
    {
        return Result<OrderIdentifier>.Failure(
            new Error("order.id.invalid", "orderId는 1~40자의 영문자, 숫자, 하이픈이어야 합니다."));
    }
}

/// <summary>
/// 가격 조회 전 사용자가 요청한 한 상품 줄을 검증된 형태로 표현합니다.
/// </summary>
public sealed record RequestedOrderLine
{
    /// <summary>
    /// 이미 검증하고 정규화한 SKU와 수량으로 요청 줄을 만듭니다.
    /// </summary>
    /// <param name="sku">대문자로 정규화한 상품 식별자입니다.</param>
    /// <param name="quantity">1~100 범위의 주문 수량입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private RequestedOrderLine(string sku, int quantity)
    {
        Sku = sku;
        Quantity = quantity;
    }

    /// <summary>
    /// 정규화한 상품 식별자를 가져옵니다.
    /// </summary>
    public string Sku { get; }

    /// <summary>
    /// 검증된 주문 수량을 가져옵니다.
    /// </summary>
    public int Quantity { get; }

    /// <summary>
    /// 외부 문자열과 수량을 검사해 안전한 요청 줄로 바꿉니다.
    /// </summary>
    /// <param name="sku">공백이 섞일 수 있는 외부 SKU입니다.</param>
    /// <param name="quantity">외부에서 받은 주문 수량입니다.</param>
    /// <returns>검증 성공 시 RequestedOrderLine, 실패 시 안정적인 오류 코드를 반환합니다.</returns>
    public static Result<RequestedOrderLine> Create(string? sku, int quantity)
    {
        // string?의 ?는 외부 JSON에서 값이 빠져 null이 들어올 수 있음을 nullable 분석기에 알립니다.
        var normalizedSku = sku?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSku))
        {
            return Result<RequestedOrderLine>.Failure(
                new Error("order.sku.required", "각 상품의 sku를 입력하세요."));
        }

        if (normalizedSku.Length > 32)
        {
            return Result<RequestedOrderLine>.Failure(
                new Error("order.sku.too_long", "sku는 32자 이하여야 합니다."));
        }

        // Any와 lambda는 각 문자를 검사해 하나라도 허용되지 않은 문자가 있는지 묻는 LINQ 표현입니다.
        var hasInvalidCharacter = normalizedSku.Any(
            character => !char.IsAsciiLetterOrDigit(character) && character != '-');
        if (hasInvalidCharacter)
        {
            return Result<RequestedOrderLine>.Failure(
                new Error("order.sku.invalid", "sku에는 영문자, 숫자, 하이픈만 사용할 수 있습니다."));
        }

        // is < 1 or > 100은 두 관계 pattern을 묶어 허용 범위 밖인지 읽기 쉽게 표현합니다.
        if (quantity is < 1 or > 100)
        {
            return Result<RequestedOrderLine>.Failure(
                new Error("order.quantity.out_of_range", "상품 수량은 1~100이어야 합니다."));
        }

        return Result<RequestedOrderLine>.Success(new RequestedOrderLine(normalizedSku, quantity));
    }
}

/// <summary>
/// 주문 식별자, 고객, 검증된 요청 줄을 가격 조회 전에 묶은 불변 초안입니다.
/// </summary>
public sealed class OrderDraft
{
    /// <summary>
    /// 검증된 주문 정보를 복사해 외부 목록 변경의 영향을 받지 않게 보관합니다.
    /// </summary>
    /// <param name="orderId">검증된 주문 식별자입니다.</param>
    /// <param name="customerId">검증된 고객 식별자입니다.</param>
    /// <param name="lines">하나 이상인 검증된 요청 줄입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private OrderDraft(string orderId, string customerId, RequestedOrderLine[] lines)
    {
        OrderId = orderId;
        CustomerId = customerId;
        // Array.AsReadOnly은 내부 배열을 수정 메서드가 없는 읽기 전용 view로 감싸 불변 경계를 강화합니다.
        Lines = Array.AsReadOnly(lines);
    }

    /// <summary>
    /// 검증된 주문 식별자를 가져옵니다.
    /// </summary>
    public string OrderId { get; }

    /// <summary>
    /// 검증된 고객 식별자를 가져옵니다.
    /// </summary>
    public string CustomerId { get; }

    /// <summary>
    /// 외부에서 수정할 수 없는 요청 줄 목록을 가져옵니다.
    /// </summary>
    public IReadOnlyList<RequestedOrderLine> Lines { get; }

    /// <summary>
    /// 주문의 기본 필드와 줄 목록을 검증해 가격 조회에 사용할 초안을 만듭니다.
    /// </summary>
    /// <param name="orderId">클라이언트가 재시도에도 동일하게 보낼 주문 식별자입니다.</param>
    /// <param name="customerId">주문 소유 고객 식별자입니다.</param>
    /// <param name="lines">각 항목을 이미 검증한 요청 줄 모음입니다.</param>
    /// <returns>성공 시 불변 OrderDraft, 실패 시 입력 오류 Result를 반환합니다.</returns>
    public static Result<OrderDraft> Create(
        string? orderId,
        string? customerId,
        IEnumerable<RequestedOrderLine>? lines)
    {
        var orderIdResult = OrderIdentifier.Create(orderId);
        if (!orderIdResult.IsSuccess)
        {
            return Result<OrderDraft>.Failure(orderIdResult.Error!);
        }

        var normalizedCustomerId = NormalizeCustomerId(customerId);
        if (normalizedCustomerId is null)
        {
            return Result<OrderDraft>.Failure(
                new Error("order.customer_id.invalid", "customerId는 1~40자의 영문자, 숫자, 하이픈이어야 합니다."));
        }

        // ?.는 null이 아닐 때만 호출하며, ?? 뒤의 []는 빈 배열을 만드는 C# collection expression입니다.
        var lineArray = lines?.ToArray() ?? [];
        if (lineArray.Length == 0)
        {
            return Result<OrderDraft>.Failure(
                new Error("order.items.required", "상품을 하나 이상 입력하세요."));
        }

        if (lineArray.Length > 20)
        {
            return Result<OrderDraft>.Failure(
                new Error("order.items.too_many", "한 주문에는 상품을 최대 20개까지 담을 수 있습니다."));
        }

        // GroupBy는 같은 SKU를 묶고 FirstOrDefault는 중복 묶음이 없으면 null을 반환합니다.
        var duplicateSku = lineArray
            .GroupBy(line => line.Sku, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateSku is not null)
        {
            return Result<OrderDraft>.Failure(
                new Error("order.sku.duplicate", $"같은 sku를 두 번 보낼 수 없습니다: {duplicateSku}"));
        }

        return Result<OrderDraft>.Success(
            new OrderDraft(orderIdResult.Value!.Value, normalizedCustomerId, lineArray));
    }

    /// <summary>
    /// 외부 식별자를 trim·대문자화하고 허용 문자와 길이를 확인합니다.
    /// </summary>
    /// <param name="value">검증할 외부 문자열입니다.</param>
    /// <returns>유효하면 정규화 문자열, 유효하지 않으면 null을 반환합니다.</returns>
    private static string? NormalizeCustomerId(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 40)
        {
            return null;
        }

        return normalized.All(
            character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? normalized
            : null;
    }
}

/// <summary>
/// 카탈로그 가격을 붙인 주문 한 줄입니다.
/// </summary>
public sealed record OrderLine
{
    /// <summary>
    /// 검증된 요청 줄과 0 이상 가격을 최종 주문 줄 값으로 보관합니다.
    /// </summary>
    /// <param name="sku">정규화된 상품 식별자입니다.</param>
    /// <param name="quantity">1~100 범위의 주문 수량입니다.</param>
    /// <param name="unitPrice">0 이상인 개당 가격입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private OrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// 정규화된 상품 식별자를 가져옵니다.
    /// </summary>
    public string Sku { get; }

    /// <summary>
    /// 검증된 주문 수량을 가져옵니다.
    /// </summary>
    public int Quantity { get; }

    /// <summary>
    /// 카탈로그가 제공한 0 이상 개당 가격을 가져옵니다.
    /// </summary>
    public decimal UnitPrice { get; }

    /// <summary>
    /// 수량과 개당 가격을 곱한 줄 금액을 계산합니다.
    /// </summary>
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>
    /// 검증된 요청 줄에 카탈로그 가격을 붙이고 Adapter 계약 위반은 예외로 드러냅니다.
    /// </summary>
    /// <param name="requestedLine">Domain 검증을 통과한 SKU와 수량입니다.</param>
    /// <param name="unitPrice">카탈로그 Adapter가 반환한 개당 가격입니다.</param>
    /// <returns>모든 불변 조건이 맞는 OrderLine을 반환합니다.</returns>
    public static OrderLine Create(RequestedOrderLine requestedLine, decimal unitPrice)
    {
        ArgumentNullException.ThrowIfNull(requestedLine);
        if (unitPrice < 0)
        {
            throw new InvalidOperationException("상품 카탈로그가 음수 가격을 반환했습니다.");
        }

        return new OrderLine(requestedLine.Sku, requestedLine.Quantity, unitPrice);
    }
}

/// <summary>
/// 저장 가능한 최종 주문과 금액 계산 결과를 표현하는 불변 Domain Model입니다.
/// </summary>
public sealed class Order
{
    /// <summary>
    /// 검증된 초안과 가격 줄, 배송비를 최종 주문 상태로 보관합니다.
    /// </summary>
    /// <param name="draft">식별자와 요청 줄 검증이 끝난 초안입니다.</param>
    /// <param name="lines">카탈로그 가격이 붙은 줄 배열입니다.</param>
    /// <param name="shippingFee">Strategy가 계산한 0 이상 배송비입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private Order(OrderDraft draft, OrderLine[] lines, decimal shippingFee)
    {
        OrderId = draft.OrderId;
        CustomerId = draft.CustomerId;
        Lines = Array.AsReadOnly(lines);
        Subtotal = lines.Sum(line => line.LineTotal);
        ShippingFee = shippingFee;
        Total = Subtotal + ShippingFee;
    }

    /// <summary>
    /// 주문 식별자를 가져옵니다.
    /// </summary>
    public string OrderId { get; }

    /// <summary>
    /// 고객 식별자를 가져옵니다.
    /// </summary>
    public string CustomerId { get; }

    /// <summary>
    /// 가격이 붙은 읽기 전용 주문 줄을 가져옵니다.
    /// </summary>
    public IReadOnlyList<OrderLine> Lines { get; }

    /// <summary>
    /// 배송비 전 상품 합계를 가져옵니다.
    /// </summary>
    public decimal Subtotal { get; }

    /// <summary>
    /// 배송비 정책이 계산한 금액을 가져옵니다.
    /// </summary>
    public decimal ShippingFee { get; }

    /// <summary>
    /// 상품 합계와 배송비를 더한 최종 금액을 가져옵니다.
    /// </summary>
    public decimal Total { get; }

    /// <summary>
    /// Application 내부 계약이 모두 맞는지 확인하고 최종 주문을 만듭니다.
    /// </summary>
    /// <param name="draft">외부 입력 검증이 끝난 주문 초안입니다.</param>
    /// <param name="lines">초안과 같은 개수의 가격 적용 줄입니다.</param>
    /// <param name="shippingFee">배송비 Strategy의 계산값입니다.</param>
    /// <returns>저장 가능한 불변 Order를 반환합니다.</returns>
    public static Order Create(OrderDraft draft, IEnumerable<OrderLine> lines, decimal shippingFee)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(lines);

        var lineArray = lines.ToArray();
        if (lineArray.Length != draft.Lines.Count)
        {
            // 외부 입력 실패가 아니라 Application 조립 오류이므로 Result로 숨기지 않고 예외로 빨리 드러냅니다.
            throw new InvalidOperationException("가격 줄 수가 주문 초안과 일치하지 않습니다.");
        }

        for (var index = 0; index < lineArray.Length; index++)
        {
            var requestedLine = draft.Lines[index];
            var pricedLine = lineArray[index];
            if (pricedLine.Sku != requestedLine.Sku || pricedLine.Quantity != requestedLine.Quantity)
            {
                // 가격 결합 중 SKU나 수량을 바꾸는 것은 사용자 오류가 아니라 Application 조립 버그입니다.
                throw new InvalidOperationException("가격 줄이 주문 초안의 SKU와 수량 순서를 보존하지 않았습니다.");
            }
        }

        if (shippingFee < 0)
        {
            throw new InvalidOperationException("배송비 Strategy는 음수를 반환할 수 없습니다.");
        }

        return new Order(draft, lineArray, shippingFee);
    }

    /// <summary>
    /// API 응답과 조회에 사용할 불변 영수증 snapshot을 만듭니다.
    /// </summary>
    /// <returns>현재 주문 값만 담은 OrderReceipt를 반환합니다.</returns>
    public OrderReceipt ToReceipt()
    {
        return new OrderReceipt(OrderId, CustomerId, Lines, Subtotal, ShippingFee, Total);
    }
}

/// <summary>
/// 외부에 공개할 저장 완료 주문 snapshot입니다.
/// </summary>
/// <param name="OrderId">주문 식별자입니다.</param>
/// <param name="CustomerId">고객 식별자입니다.</param>
/// <param name="Lines">가격 적용 주문 줄입니다.</param>
/// <param name="Subtotal">배송비 전 상품 합계입니다.</param>
/// <param name="ShippingFee">배송비입니다.</param>
/// <param name="Total">최종 결제 예정 금액입니다.</param>
public sealed record OrderReceipt(
    string OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total);
