namespace OrderIntakeApi.Application;

/// <summary>
/// HTTP 형식과 분리해 Application Service에 전달할 상품 입력입니다.
/// </summary>
/// <param name="Sku">가격을 조회할 상품 식별자입니다.</param>
/// <param name="Quantity">주문 수량입니다.</param>
public sealed record PlaceOrderItem(string? Sku, int Quantity);

/// <summary>
/// 주문 접수 use case에 필요한 외부 입력만 묶은 명령입니다.
/// </summary>
/// <param name="OrderId">클라이언트가 정한 주문 식별자입니다.</param>
/// <param name="CustomerId">주문 소유 고객 식별자입니다.</param>
/// <param name="Items">목록 자체나 JSON 배열 원소가 null일 수 있어 Application에서 검증할 상품 모음입니다.</param>
public sealed record PlaceOrderCommand(
    string? OrderId,
    string? CustomerId,
    IReadOnlyList<PlaceOrderItem?>? Items);
