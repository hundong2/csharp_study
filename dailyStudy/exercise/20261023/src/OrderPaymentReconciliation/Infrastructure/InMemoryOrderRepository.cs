using System.Collections.Immutable;
using OrderPaymentReconciliation.Application.Ports;
using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Infrastructure;

/// <summary>
/// 학습 예제와 테스트가 외부 데이터베이스 없이 실행되도록 주문을 메모리 snapshot에 보관하는 Adapter입니다.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ImmutableArray<Order> _orders;

    /// <summary>
    /// 호출자가 가진 collection의 이후 변경이 저장소에 새어 들어오지 않도록 즉시 복사합니다.
    /// </summary>
    /// <param name="orders">저장소의 초기 주문 sequence입니다.</param>
    public InMemoryOrderRepository(IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        _orders = orders.ToImmutableArray();
    }

    /// <summary>
    /// 취소 신호를 먼저 확인한 뒤 불변 주문 snapshot을 반환합니다.
    /// </summary>
    /// <param name="cancellationToken">이미 취소되었다면 OperationCanceledException을 발생시킬 신호입니다.</param>
    /// <returns>호출자가 원소를 바꿀 수 없는 주문 목록을 완료된 Task로 반환합니다.</returns>
    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Order>>(_orders);
    }
}
