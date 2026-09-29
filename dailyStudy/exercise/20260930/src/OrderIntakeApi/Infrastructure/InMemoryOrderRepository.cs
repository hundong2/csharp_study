using System.Collections.Concurrent;
using OrderIntakeApi.Application.Ports;
using OrderIntakeApi.Domain;

namespace OrderIntakeApi.Infrastructure;

/// <summary>
/// ConcurrentDictionary로 같은 ID의 동시 주문 추가를 원자적으로 막는 Repository Adapter입니다.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 같은 ID가 없을 때만 불변 주문을 원자적으로 저장합니다.
    /// </summary>
    /// <param name="order">저장할 Domain 주문입니다.</param>
    /// <param name="cancellationToken">저장 시작 전 확인할 취소 신호입니다.</param>
    /// <returns>추가 성공 여부가 든 완료된 Task를 반환합니다.</returns>
    public Task<bool> TryAddAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        cancellationToken.ThrowIfCancellationRequested();
        var added = _orders.TryAdd(order.OrderId, order);
        return Task.FromResult(added);
    }

    /// <summary>
    /// 대소문자 차이를 제거한 주문 ID로 저장된 주문을 찾습니다.
    /// </summary>
    /// <param name="orderId">외부 경로에서 받은 주문 식별자입니다.</param>
    /// <param name="cancellationToken">조회 시작 전 확인할 취소 신호입니다.</param>
    /// <returns>찾은 주문 또는 null이 든 완료된 Task를 반환합니다.</returns>
    public Task<Order?> FindAsync(string orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        cancellationToken.ThrowIfCancellationRequested();
        _orders.TryGetValue(orderId.Trim().ToUpperInvariant(), out var order);
        return Task.FromResult(order);
    }
}
