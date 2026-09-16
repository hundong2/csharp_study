namespace CursorPaginationExercise;

// Application Service는 요청 검증과 페이지 조립을 맡습니다. 저장 방식은 포트(interface)에만 의존합니다.
public sealed class OrderSearchService
{
    private readonly IOrderRepository _repository;
    private readonly IOrderFilter _filter;

    // 생성자는 repository(주문을 읽는 구현)와 filter(상태 판정 전략)를 받아 서비스를 반환합니다.
    // 의존성을 밖에서 넣으면 실제 DB 대신 메모리 구현으로 같은 로직을 검증할 수 있습니다.
    // `?? throw`는 전달된 객체가 null이면 즉시 예외를 내고, nameof는 오류에 파라미터 이름을 정확히 적습니다.
    public OrderSearchService(IOrderRepository repository, IOrderFilter filter)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    // 이 메서드는 query(페이지 크기, 커서, 선택적 상태)로 주문 한 페이지를 조회합니다.
    // cancellationToken은 호출자가 작업 중단을 알릴 때 사용하고, 반환값은 성공 페이지 또는 입력 오류입니다.
    // async는 내부에서 await로 저장소 작업을 기다리는 메서드임을 뜻합니다.
    public async Task<Result<OrderPage>> SearchAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        // 패턴 문법 `is < 1 or > 100`은 허용 범위 밖의 크기를 한 번에 읽기 쉽게 표현합니다.
        if (query.PageSize is < 1 or > 100)
        {
            return Result<OrderPage>.Failure("PageSize는 1부터 100 사이여야 합니다.");
        }

        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            return Result<OrderPage>.Failure("알 수 없는 주문 상태입니다.");
        }

        // `is OrderCursor cursor`는 선택적 커서에 값이 있을 때 그 값을 꺼내 검사하는 패턴입니다.
        if (query.After is OrderCursor cursor && (cursor.Id <= 0 || cursor.CreatedAtUtc.Offset != TimeSpan.Zero))
        {
            return Result<OrderPage>.Failure("커서는 양수 주문 ID와 UTC 시각을 포함해야 합니다.");
        }

        // await는 저장소 작업이 끝날 때까지 현재 메서드를 잠시 양보합니다. 취소 토큰도 저장소까지 그대로 전달합니다.
        IReadOnlyList<Order> fetched = await _repository.ReadAfterAsync(
            query.After, query.PageSize + 1, query.Status, _filter, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        bool hasMore = fetched.Count > query.PageSize;
        // LINQ의 Take는 표시할 개수만 고릅니다. 여분 한 건은 다음 페이지 존재 여부에만 사용합니다.
        Order[] items = fetched.Take(query.PageSize).ToArray();
        // [^1]은 배열의 마지막 항목을 뜻합니다. 커서는 표시한 마지막 주문에서 만들어야 건너뛰는 주문이 없습니다.
        OrderCursor? nextCursor = hasMore
            ? new OrderCursor(items[^1].CreatedAtUtc, items[^1].Id)
            : null;
        return Result<OrderPage>.Success(new OrderPage(Array.AsReadOnly(items), nextCursor));
    }
}
