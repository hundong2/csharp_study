namespace CursorPaginationExercise;

public static class Program
{
    // 이 메서드는 args(명령줄 옵션)에 따라 예제 또는 자체 검증을 실행하고 종료 코드를 반환합니다.
    // async/await는 비동기 조회가 끝날 때까지 기다리되 스레드를 붙잡아 두지 않는 문법입니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTests.RunAsync();
        }

        // 이곳이 Composition Root입니다. 구체 저장소와 필터를 조립하고 서비스에는 포트로 전달합니다.
        IOrderRepository repository = new InMemoryOrderRepository(CreateSampleOrders());
        IOrderFilter filter = new StatusOrderFilter();
        OrderSearchService service = new(repository, filter);

        OrderCursor? after = null;
        int pageNumber = 1;
        do
        {
            Result<OrderPage> result = await service.SearchAsync(new OrderQuery(2, after, OrderStatus.Paid));
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.Error);
                return 1;
            }

            // null 허용 속성은 성공 여부를 확인한 뒤에도 컴파일러가 자동으로 연결하지 못하므로 !로 의도를 알립니다.
            OrderPage page = result.Value!;
            // 문자열 앞의 $는 중괄호 안 값을 문자열에 넣습니다. Select는 표시용 ID만 고르는 LINQ 연산입니다.
            Console.WriteLine($"{pageNumber}페이지: {string.Join(", ", page.Items.Select(order => $"#{order.Id}"))}");
            after = page.NextCursor;
            pageNumber++;
        }
        while (after.HasValue);

        return 0;
    }

    // 이 메서드는 파라미터 없이, 같은 생성 시각을 가진 주문이 포함된 예제 목록을 반환합니다.
    // 시각을 UTC로 고정하면 실행하는 컴퓨터의 시간대에 따라 결과가 달라지지 않습니다.
    private static IReadOnlyList<Order> CreateSampleOrders()
    {
        DateTimeOffset first = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = first.AddMinutes(1);
        // 대괄호 컬렉션 식은 반환 타입에 맞는 목록을 간결하게 만듭니다. 입력 순서는 일부러 섞어 두었습니다.
        return
        [
            new Order(4, second, "C-02", OrderStatus.Paid, 18_000m),
            new Order(2, first, "C-01", OrderStatus.Paid, 12_000m),
            new Order(5, second, "C-03", OrderStatus.Paid, 32_000m),
            new Order(1, first, "C-01", OrderStatus.Pending, 8_000m),
            new Order(3, first, "C-02", OrderStatus.Paid, 17_000m)
        ];
    }
}
