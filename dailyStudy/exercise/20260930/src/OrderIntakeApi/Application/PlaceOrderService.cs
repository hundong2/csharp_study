using OrderIntakeApi.Application.Ports;
using OrderIntakeApi.Domain;

namespace OrderIntakeApi.Application;

/// <summary>
/// 검증, 가격 조회, 배송비 정책, 저장 순서를 조율하는 주문 Application Service입니다.
/// </summary>
public sealed class PlaceOrderService
{
    private readonly IProductCatalog _catalog;
    private readonly IOrderRepository _orders;
    private readonly IShippingFeePolicy _shippingFeePolicy;

    /// <summary>
    /// use case가 의존할 Port와 Strategy를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="catalog">상품 가격을 제공하는 외부 시스템 Port입니다.</param>
    /// <param name="orders">주문 저장소 Port입니다.</param>
    /// <param name="shippingFeePolicy">배송비를 결정하는 Strategy입니다.</param>
    /// <returns>생성자는 서비스를 초기화하므로 별도 반환값은 없습니다.</returns>
    public PlaceOrderService(
        IProductCatalog catalog,
        IOrderRepository orders,
        IShippingFeePolicy shippingFeePolicy)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _shippingFeePolicy = shippingFeePolicy ?? throw new ArgumentNullException(nameof(shippingFeePolicy));
    }

    /// <summary>
    /// 외부 주문 입력을 검증하고 가격·배송비를 계산한 뒤 중복 없이 저장합니다.
    /// </summary>
    /// <param name="command">HTTP와 분리된 주문 접수 입력입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 가격 조회와 저장까지 전파합니다.</param>
    /// <returns>성공 시 영수증, 예상 가능한 실패 시 Error가 든 Result를 반환합니다.</returns>
    // async는 이 메서드가 await를 사용하며 완료 결과를 Task<Result<OrderReceipt>>로 돌려준다는 뜻입니다.
    public async Task<Result<OrderReceipt>> PlaceAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items is { Count: > 20 })
        {
            return Result<OrderReceipt>.Failure(
                new Error("order.items.too_many", "한 주문에는 상품을 최대 20개까지 담을 수 있습니다."));
        }

        var requestedLines = new List<RequestedOrderLine>();
        // ??는 Items가 null일 때 빈 배열을 선택해 같은 검증 흐름으로 합칩니다.
        foreach (var item in command.Items ?? [])
        {
            // nullable element를 명시하면 JSON의 [null]이나 다른 adapter의 잘못된 명령도 NullReferenceException 대신 400 Result가 됩니다.
            if (item is null)
            {
                return Result<OrderReceipt>.Failure(
                    new Error("order.item.required", "상품 배열의 각 항목은 객체여야 합니다."));
            }

            var lineResult = RequestedOrderLine.Create(item.Sku, item.Quantity);
            if (!lineResult.IsSuccess)
            {
                // 앞 조건으로 Error가 null이 아님을 알지만 컴파일러는 추론하지 못하므로 !로 nullable 경고만 제거합니다.
                return Result<OrderReceipt>.Failure(lineResult.Error!);
            }

            requestedLines.Add(lineResult.Value!);
        }

        var draftResult = OrderDraft.Create(command.OrderId, command.CustomerId, requestedLines);
        if (!draftResult.IsSuccess)
        {
            return Result<OrderReceipt>.Failure(draftResult.Error!);
        }

        var draft = draftResult.Value!;
        // ToHashSet은 중복 SKU 조회를 막고 Ordinal 비교로 문화권에 따른 식별자 차이를 없앱니다.
        var requestedSkus = draft.Lines
            .Select(line => line.Sku)
            .ToHashSet(StringComparer.Ordinal);

        // await는 외부 가격 I/O를 기다리는 동안 요청 thread를 붙잡지 않고, 같은 취소 토큰을 Adapter까지 전달합니다.
        var prices = await _catalog.GetUnitPricesAsync(requestedSkus, cancellationToken);
        var missingSku = requestedSkus.FirstOrDefault(sku => !prices.ContainsKey(sku));
        if (missingSku is not null)
        {
            return Result<OrderReceipt>.Failure(
                new Error("catalog.item.not_found", $"가격을 찾을 수 없는 상품입니다: {missingSku}"));
        }

        var pricedLines = new List<OrderLine>(draft.Lines.Count);
        foreach (var requestedLine in draft.Lines)
        {
            var unitPrice = prices[requestedLine.Sku];
            // factory는 음수 가격 같은 Adapter 계약 위반을 400 Result로 위장하지 않고 예외로 드러냅니다.
            pricedLines.Add(OrderLine.Create(requestedLine, unitPrice));
        }

        var subtotal = pricedLines.Sum(line => line.LineTotal);
        var totalQuantity = pricedLines.Sum(line => line.Quantity);
        var shippingFee = _shippingFeePolicy.Calculate(subtotal, totalQuantity);
        var order = Order.Create(draft, pricedLines, shippingFee);

        if (!await _orders.TryAddAsync(order, cancellationToken))
        {
            return Result<OrderReceipt>.Failure(
                new Error("order.duplicate", "이미 같은 orderId로 접수된 주문이 있습니다."));
        }

        return Result<OrderReceipt>.Success(order.ToReceipt());
    }

    /// <summary>
    /// 저장된 주문을 식별자로 찾아 외부 응답용 영수증으로 바꿉니다.
    /// </summary>
    /// <param name="orderId">경로에서 받은 주문 식별자입니다.</param>
    /// <param name="cancellationToken">조회 중단 신호입니다.</param>
    /// <returns>찾으면 영수증 성공 Result, 없거나 식별자가 잘못되면 실패 Result를 반환합니다.</returns>
    public async Task<Result<OrderReceipt>> FindAsync(
        string? orderId,
        CancellationToken cancellationToken)
    {
        var orderIdResult = OrderIdentifier.Create(orderId);
        if (!orderIdResult.IsSuccess)
        {
            return Result<OrderReceipt>.Failure(orderIdResult.Error!);
        }

        var order = await _orders.FindAsync(orderIdResult.Value!.Value, cancellationToken);
        return order is null
            ? Result<OrderReceipt>.Failure(
                new Error("order.not_found", "주문을 찾을 수 없습니다."))
            : Result<OrderReceipt>.Success(order.ToReceipt());
    }
}
