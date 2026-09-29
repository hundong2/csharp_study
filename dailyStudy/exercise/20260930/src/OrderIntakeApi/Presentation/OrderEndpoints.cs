using OrderIntakeApi.Application;

namespace OrderIntakeApi.Presentation;

/// <summary>
/// 주문 HTTP 계약을 Application 명령과 Result mapper에 연결합니다.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// POST 주문 접수와 GET 주문 조회 endpoint를 route table에 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>다른 endpoint도 연달아 등록할 수 있도록 같은 builder를 반환합니다.</returns>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/orders");
        // 메서드 그룹은 같은 모양의 lambda를 다시 쓰지 않고 handler 메서드 자체를 delegate로 전달합니다.
        group.MapPost("/", HandleCreateAsync);
        group.MapGet("/{orderId}", HandleReadAsync);
        return endpoints;
    }

    /// <summary>
    /// JSON 요청을 Application 명령으로 바꾸고 주문 접수 use case를 실행합니다.
    /// </summary>
    /// <param name="request">본문에서 binding된 주문 입력이며 JSON null일 수 있습니다.</param>
    /// <param name="service">DI가 제공한 주문 Application Service입니다.</param>
    /// <param name="cancellationToken">연결 종료 시 자동으로 취소되는 RequestAborted 토큰입니다.</param>
    /// <returns>201 또는 예상 가능한 4xx Problem Details를 실행할 IResult를 반환합니다.</returns>
    private static async Task<IResult> HandleCreateAsync(
        PlaceOrderRequest? request,
        PlaceOrderService service,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return OrderHttpMapper.ToProblemResult(
                new Domain.Error("order.body.required", "JSON 주문 본문을 입력하세요."));
        }

        // 큰 배열을 Application DTO로 한 번 더 복사하기 전에 공개 한도 20개를 빠르게 적용합니다.
        if (request.Items is { Count: > 20 })
        {
            return OrderHttpMapper.ToProblemResult(
                new Domain.Error("order.items.too_many", "한 주문에는 상품을 최대 20개까지 담을 수 있습니다."));
        }

        // ?.Select는 Items가 있을 때만 각 HTTP DTO를 Application item으로 투영하고, 없으면 null을 유지합니다.
        // JSON 배열의 null 원소도 Application Result 경계가 검증하도록 그대로 null로 전달합니다.
        var items = request.Items?
            .Select(item => item is null ? null : new PlaceOrderItem(item.Sku, item.Quantity))
            .ToArray();
        var command = new PlaceOrderCommand(request.OrderId, request.CustomerId, items);
        var result = await service.PlaceAsync(command, cancellationToken);
        return OrderHttpMapper.ToCreateResult(result);
    }

    /// <summary>
    /// 경로의 주문 ID를 Application 조회 use case에 전달합니다.
    /// </summary>
    /// <param name="orderId">URL 경로에서 binding된 주문 식별자입니다.</param>
    /// <param name="service">DI가 제공한 주문 Application Service입니다.</param>
    /// <param name="cancellationToken">조회 중 클라이언트가 떠났음을 알리는 토큰입니다.</param>
    /// <returns>200 영수증 또는 404 Problem Details를 실행할 IResult를 반환합니다.</returns>
    private static async Task<IResult> HandleReadAsync(
        string orderId,
        PlaceOrderService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FindAsync(orderId, cancellationToken);
        return OrderHttpMapper.ToReadResult(result);
    }
}

/// <summary>
/// POST /orders JSON 본문 모양입니다. null 가능성은 외부 입력의 불완전함을 그대로 나타냅니다.
/// </summary>
/// <param name="OrderId">재시도에도 유지할 주문 식별자입니다.</param>
/// <param name="CustomerId">주문 소유 고객 식별자입니다.</param>
/// <param name="Items">배열 자체나 개별 원소가 null일 수 있는 주문 상품 배열입니다.</param>
public sealed record PlaceOrderRequest(
    string? OrderId,
    string? CustomerId,
    IReadOnlyList<PlaceOrderItemRequest?>? Items);

/// <summary>
/// 주문 JSON 배열 안의 상품 한 줄 모양입니다.
/// </summary>
/// <param name="Sku">상품 식별자입니다.</param>
/// <param name="Quantity">주문 수량입니다.</param>
public sealed record PlaceOrderItemRequest(string? Sku, int Quantity);
