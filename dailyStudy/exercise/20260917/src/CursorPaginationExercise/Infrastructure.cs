namespace CursorPaginationExercise;

// Repository 포트는 서비스가 저장소의 종류를 몰라도 주문을 읽게 하는 경계입니다.
public interface IOrderRepository
{
    // 이 메서드는 after(제외할 마지막 키)보다 뒤에 있는 주문을 take개까지 읽습니다.
    // status와 filter는 개수 제한 전에 적용할 필터이고, cancellationToken은 중단 신호입니다.
    // 반환값은 (CreatedAtUtc, Id) 오름차순으로 정렬된 주문 목록입니다.
    Task<IReadOnlyList<Order>> ReadAfterAsync(
        OrderCursor? after,
        int take,
        OrderStatus? status,
        IOrderFilter filter,
        CancellationToken cancellationToken);
}

// Strategy 포트는 어떤 주문이 요청에 맞는지 판정하는 규칙을 저장소에서 교체 가능하게 합니다.
public interface IOrderFilter
{
    // 이 메서드는 order(검사할 주문)와 status(요청한 상태)를 받아 포함 여부를 반환합니다.
    bool Matches(Order order, OrderStatus? status);
}

public sealed class StatusOrderFilter : IOrderFilter
{
    // 이 메서드는 order(검사할 주문)의 상태가 status(선택적 요청 상태)와 맞는지 반환합니다.
    // null이면 상태 제한이 없으므로 모든 주문을 허용합니다.
    public bool Matches(Order order, OrderStatus? status)
    {
        return !status.HasValue || order.Status == status.Value;
    }
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Order[] _orderedOrders;

    // 생성자는 orders(저장해 둘 주문들)를 받아 메모리 저장소를 반환합니다.
    // LINQ의 OrderBy/ThenBy는 생성 시각이 같을 때 ID까지 비교해 순서를 완전히 고정합니다.
    public InMemoryOrderRepository(IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        Order[] snapshot = orders.ToArray();
        // HashSet은 이미 본 복합 키를 빠르게 찾습니다. 키 중복을 허용하면 페이지 경계가 모호해집니다.
        // []는 비어 있는 컬렉션 식입니다. 아직 본 키가 없으므로 빈 집합에서 검사를 시작합니다.
        HashSet<OrderCursor> keys = [];
        foreach (Order order in snapshot)
        {
            if (order.Id <= 0 || order.CreatedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("주문은 양수 ID와 UTC 생성 시각을 가져야 합니다.", nameof(orders));
            }

            if (!keys.Add(new OrderCursor(order.CreatedAtUtc, order.Id)))
            {
                throw new ArgumentException("주문 정렬 키는 중복될 수 없습니다.", nameof(orders));
            }
        }

        // =>는 한 주문에서 정렬 키를 꺼내는 짧은 함수 문법입니다.
        _orderedOrders = snapshot.OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id)
            .ToArray();
    }

    // 이 메서드는 after(이전 페이지의 끝) 다음 주문을 take개까지 찾습니다.
    // status와 filter는 선택적 상태 규칙이며, cancellationToken은 순회 중에도 중단을 확인합니다.
    // 반환값은 읽기 전용 주문 목록을 담은 Task입니다. 메모리 작업이라 Task.FromResult로 즉시 완료합니다.
    public Task<IReadOnlyList<Order>> ReadAfterAsync(
        OrderCursor? after,
        int take,
        OrderStatus? status,
        IOrderFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        // `new(take)`는 왼쪽 List<Order> 타입을 재사용해 초기 용량을 지정하는 문법입니다.
        List<Order> selected = new(take);

        foreach (Order order in _orderedOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 두 번째 비교가 없으면 시각이 같은 주문을 다음 페이지에서 누락할 수 있습니다.
            bool isAfter = !after.HasValue
                || order.CreatedAtUtc > after.Value.CreatedAtUtc
                || (order.CreatedAtUtc == after.Value.CreatedAtUtc && order.Id > after.Value.Id);
            if (!isAfter || !filter.Matches(order, status))
            {
                continue;
            }

            selected.Add(order);
            if (selected.Count == take)
            {
                break;
            }
        }

        IReadOnlyList<Order> result = selected.AsReadOnly();
        return Task.FromResult(result);
    }
}
