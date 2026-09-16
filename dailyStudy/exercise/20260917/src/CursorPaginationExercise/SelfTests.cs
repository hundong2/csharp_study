namespace CursorPaginationExercise;

// 외부 테스트 패키지 없이 실행할 수 있는 작은 검증 모음입니다. 실패하면 프로세스 종료 코드를 1로 돌려줍니다.
internal static class SelfTests
{
    // 이 메서드는 파라미터 없이 모든 검증을 실행하고 성공이면 0, 실패면 1을 반환합니다.
    public static async Task<int> RunAsync()
    {
        try
        {
            await PagesDoNotDuplicateOrSkipTiedTimestampsAsync();
            await KeysetBoundaryAndFilterAsync();
            await InvalidInputReturnsResultAsync();
            await CancellationReachesRepositoryAsync();
            await CancellationDuringRepositoryScanAsync();
            RepositoryRejectsAmbiguousKeys();
            Console.WriteLine("자체 검증 6개 통과");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"자체 검증 실패: {exception.Message}");
            return 1;
        }
    }

    // 이 메서드는 파라미터 없이 같은 시각의 주문을 여러 페이지에 걸쳐 읽고, 중복·누락이 없는지 확인합니다.
    // 성공하면 완료된 Task를 반환하고, 조건이 깨지면 예외를 던집니다.
    private static async Task PagesDoNotDuplicateOrSkipTiedTimestampsAsync()
    {
        OrderSearchService service = CreateService();
        // []는 빈 목록을 만드는 컬렉션 식입니다. List<long> 타입은 왼쪽 선언에서 결정됩니다.
        List<long> seenIds = [];
        OrderCursor? cursor = null;

        do
        {
            Result<OrderPage> result = await service.SearchAsync(new OrderQuery(2, cursor));
            Require(result.IsSuccess, "정상적인 페이지 요청이 실패했습니다.");
            OrderPage page = result.Value!;
            seenIds.AddRange(page.Items.Select(order => order.Id));
            cursor = page.NextCursor;
        }
        while (cursor.HasValue);

        Require(seenIds.SequenceEqual([1L, 2L, 3L, 4L, 5L, 6L]), "같은 시각의 주문에 중복 또는 누락이 있습니다.");
        Require(seenIds.Distinct().Count() == seenIds.Count, "한 주문을 두 번 읽었습니다.");
    }

    // 이 메서드는 파라미터 없이 복합 커서의 경계와 필터 적용 순서를 확인하고 완료된 Task를 반환합니다.
    private static async Task KeysetBoundaryAndFilterAsync()
    {
        OrderSearchService service = CreateService();
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        Result<OrderPage> boundary = await service.SearchAsync(new OrderQuery(2, new OrderCursor(first, 2)));
        Require(boundary.IsSuccess, "커서 경계 요청이 실패했습니다.");
        Require(boundary.Value!.Items.Select(order => order.Id).SequenceEqual([3L, 4L]),
            "동일 시각에서 ID가 큰 주문부터 이어져야 합니다.");
        Require(boundary.Value.NextCursor == new OrderCursor(first.AddMinutes(1), 4),
            "다음 커서는 실제로 표시한 마지막 주문을 가리켜야 합니다.");

        // 필터를 take 뒤에 적용하면 첫 페이지가 비거나 너무 짧아질 수 있으므로 결과 ID까지 확인합니다.
        Result<OrderPage> filtered = await service.SearchAsync(new OrderQuery(2, null, OrderStatus.Paid));
        Require(filtered.IsSuccess, "상태 필터 요청이 실패했습니다.");
        Require(filtered.Value!.Items.Select(order => order.Id).SequenceEqual([2L, 4L]),
            "상태 필터는 개수 제한보다 먼저 적용되어야 합니다.");
        Require(filtered.Value.NextCursor == new OrderCursor(first.AddMinutes(1), 4),
            "필터된 페이지의 커서가 잘못되었습니다.");

        Result<OrderPage> filteredNext = await service.SearchAsync(
            new OrderQuery(2, filtered.Value.NextCursor, OrderStatus.Paid));
        Require(filteredNext.IsSuccess, "필터된 다음 페이지 요청이 실패했습니다.");
        Require(filteredNext.Value!.Items.Select(order => order.Id).SequenceEqual([6L]),
            "필터된 다음 페이지는 남은 유료 주문만 포함해야 합니다.");
        Require(filteredNext.Value.NextCursor == null,
            "더 읽을 주문이 없을 때 NextCursor는 없어야 합니다.");

        Result<OrderPage> beyondEnd = await service.SearchAsync(
            new OrderQuery(2, new OrderCursor(first.AddMinutes(2), 6)));
        Require(beyondEnd.IsSuccess && beyondEnd.Value!.Items.Count == 0 && beyondEnd.Value.NextCursor == null,
            "마지막 키 뒤 요청은 빈 종료 페이지를 반환해야 합니다.");
    }

    // 이 메서드는 파라미터 없이 잘못된 크기·상태·커서가 예외가 아닌 실패 Result인지 확인하고 완료된 Task를 반환합니다.
    private static async Task InvalidInputReturnsResultAsync()
    {
        OrderSearchService service = CreateService();
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        Require(!(await service.SearchAsync(new OrderQuery(0))).IsSuccess, "0개 페이지는 거부해야 합니다.");
        Require(!(await service.SearchAsync(new OrderQuery(101))).IsSuccess, "101개 페이지는 거부해야 합니다.");
        Require(!(await service.SearchAsync(new OrderQuery(2, new OrderCursor(first, 0)))).IsSuccess,
            "0인 커서 ID는 거부해야 합니다.");
        Require(!(await service.SearchAsync(new OrderQuery(2, new OrderCursor(first.ToOffset(TimeSpan.FromHours(9)), 1)))).IsSuccess,
            "UTC가 아닌 커서 시각은 거부해야 합니다.");
        Require(!(await service.SearchAsync(new OrderQuery(2, null, (OrderStatus)999))).IsSuccess,
            "정의되지 않은 상태는 거부해야 합니다.");
    }

    // 이 메서드는 파라미터 없이 서비스가 같은 취소 토큰을 저장소에 전달하고 취소를 전파하는지 확인합니다.
    // 성공하면 완료된 Task를 반환하고, 취소가 무시되면 예외를 던집니다.
    private static async Task CancellationReachesRepositoryAsync()
    {
        TokenCheckingRepository repository = new();
        OrderSearchService service = new(repository, new StatusOrderFilter());
        using CancellationTokenSource source = new();
        repository.ExpectedToken = source.Token;
        Result<OrderPage> normal = await service.SearchAsync(new OrderQuery(1), source.Token);
        Require(normal.IsSuccess && repository.WasCalled, "취소 토큰이 저장소까지 전달되지 않았습니다.");

        source.Cancel();
        try
        {
            await service.SearchAsync(new OrderQuery(1), source.Token);
            throw new InvalidOperationException("취소된 요청이 계속 실행되었습니다.");
        }
        catch (OperationCanceledException)
        {
            // 취소는 잘못된 입력과 달리 호출자에게 그대로 전파되는 것이 정상입니다.
        }
    }

    // 이 메서드는 파라미터 없이 저장소가 주문을 순회하는 도중 취소되면 즉시 멈추는지 확인합니다.
    // 성공하면 완료된 Task를 반환하고, 취소를 놓치면 검증 실패 예외를 던집니다.
    private static async Task CancellationDuringRepositoryScanAsync()
    {
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        IOrderRepository repository = new InMemoryOrderRepository(
        [
            new Order(1, first, "C-1", OrderStatus.Paid, 10m),
            new Order(2, first, "C-2", OrderStatus.Paid, 20m)
        ]);
        using CancellationTokenSource source = new();
        CancelOnFirstMatchFilter filter = new(source);

        try
        {
            await repository.ReadAfterAsync(null, 2, null, filter, source.Token);
            throw new InvalidOperationException("저장소가 순회 중 취소를 놓쳤습니다.");
        }
        catch (OperationCanceledException)
        {
            Require(filter.WasCalled, "순회 중 취소를 시작하지 못했습니다.");
        }
    }

    // 이 메서드는 파라미터 없이 중복된 복합 키와 UTC가 아닌 주문을 저장소가 거부하는지 확인합니다.
    // 반환값은 없으며, 잘못된 데이터가 허용되면 검증 실패 예외를 던집니다.
    private static void RepositoryRejectsAmbiguousKeys()
    {
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        Order order = new(1, first, "C-1", OrderStatus.Paid, 10m);
        try
        {
            _ = new InMemoryOrderRepository([order, order]);
            throw new InvalidOperationException("중복 정렬 키가 허용되었습니다.");
        }
        catch (ArgumentException)
        {
            // 생성 시점에 모호한 정렬 키를 막으면 이후 페이지 결과를 신뢰할 수 있습니다.
        }

        // with는 원본 record를 바꾸지 않고 일부 속성만 바꾼 복사본을 만듭니다.
        try
        {
            _ = new InMemoryOrderRepository([order with { CreatedAtUtc = first.ToOffset(TimeSpan.FromHours(9)) }]);
            throw new InvalidOperationException("UTC가 아닌 주문 시각이 허용되었습니다.");
        }
        catch (ArgumentException)
        {
            // 데이터 원본이 UTC라는 계약도 저장소가 확인합니다.
        }
    }

    // 이 메서드는 파라미터 없이 순서를 섞은 여섯 주문으로 서비스를 만들고 반환합니다.
    private static OrderSearchService CreateService()
    {
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        Order[] orders =
        [
            new(5, first.AddMinutes(1), "C-3", OrderStatus.Shipped, 50m),
            new(3, first, "C-2", OrderStatus.Pending, 30m),
            new(1, first, "C-1", OrderStatus.Pending, 10m),
            new(6, first.AddMinutes(2), "C-3", OrderStatus.Paid, 60m),
            new(4, first.AddMinutes(1), "C-2", OrderStatus.Paid, 40m),
            new(2, first, "C-1", OrderStatus.Paid, 20m)
        ];
        return new OrderSearchService(new InMemoryOrderRepository(orders), new StatusOrderFilter());
    }

    // 이 메서드는 condition(기대 조건)과 message(실패 설명)를 받아 검증합니다.
    // 반환값은 없으며 조건이 거짓이면 검사 실패를 예외로 알립니다.
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TokenCheckingRepository : IOrderRepository
    {
        public CancellationToken ExpectedToken { get; set; }
        public bool WasCalled { get; private set; }

        // 이 메서드는 after(페이지 경계), take(최대 수), status(상태), filter(필터 전략)를 받습니다.
        // cancellationToken이 예상한 토큰인지 확인하고 빈 읽기 전용 목록을 Task로 반환합니다.
        public Task<IReadOnlyList<Order>> ReadAfterAsync(
            OrderCursor? after,
            int take,
            OrderStatus? status,
            IOrderFilter filter,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            Require(cancellationToken == ExpectedToken, "서비스가 다른 취소 토큰을 전달했습니다.");
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Order> empty = Array.Empty<Order>();
            return Task.FromResult(empty);
        }
    }

    private sealed class CancelOnFirstMatchFilter : IOrderFilter
    {
        private readonly CancellationTokenSource _source;
        public bool WasCalled { get; private set; }

        // 생성자는 source(첫 주문을 검사할 때 취소할 토큰의 출처)를 받아 테스트 필터를 반환합니다.
        public CancelOnFirstMatchFilter(CancellationTokenSource source)
        {
            _source = source;
        }

        // 이 메서드는 order(현재 주문)와 status(선택적 상태)를 받아 true를 반환합니다.
        // 첫 호출에서 취소 신호를 보내, 저장소의 다음 순회가 중단 신호를 검사하는지 시험합니다.
        public bool Matches(Order order, OrderStatus? status)
        {
            WasCalled = true;
            _source.Cancel();
            return true;
        }
    }
}
