using OrderIntakeApi.Domain;

namespace OrderIntakeApi.Application.Ports;

/// <summary>
/// Application이 메모리나 DB 세부 구현을 모르고 주문을 저장·조회하게 하는 Repository Port입니다.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// 같은 주문 ID가 아직 없을 때만 주문을 원자적으로 추가합니다.
    /// </summary>
    /// <param name="order">저장할 불변 Domain 주문입니다.</param>
    /// <param name="cancellationToken">호출자가 작업을 더 이상 원하지 않을 때의 취소 신호입니다.</param>
    /// <returns>추가했으면 true, 이미 같은 ID가 있으면 false를 반환합니다.</returns>
    Task<bool> TryAddAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// 주문 ID로 저장된 주문 snapshot을 찾습니다.
    /// </summary>
    /// <param name="orderId">정규화할 주문 식별자입니다.</param>
    /// <param name="cancellationToken">조회 중단 신호입니다.</param>
    /// <returns>찾으면 주문, 없으면 null을 반환합니다.</returns>
    Task<Order?> FindAsync(string orderId, CancellationToken cancellationToken);
}
