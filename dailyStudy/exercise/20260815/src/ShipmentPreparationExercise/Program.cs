// 오늘 예제는 출고 가능한 주문을 골라 운송사를 선택하고 출고 계획을 저장하는 작은 실무 프로그램입니다.
// 위에서 아래로 읽으면 데이터 모델 → 업무 규칙 → 실행 흐름 → 테스트의 책임 분리를 자연스럽게 볼 수 있습니다.

var orders = new[]
{
    new Order("ORD-101", "서울", 2.2m, false, "서울시 중구"),
    new Order("ORD-102", "제주", 7.5m, true, "제주시 연동"),
    new Order("ORD-103", "부산", 0m, false, "부산시 해운대구"),
    new Order("ORD-101", "서울", 1.0m, false, "서울시 종로구"),
    new Order("ORD-104", "대전", 18.0m, false, null)
};

// Composition Root는 프로그램 시작점에서 구현 객체를 조립합니다. 업무 코드는 구체 클래스 생성법을 몰라 테스트 대역으로 교체하기 쉽습니다.
IShipmentRepository repository = new InMemoryShipmentRepository();
IShippingStrategy strategy = new StandardShippingStrategy();
var service = new PrepareShipmentsService(repository, strategy, new ConsoleShipmentLogger());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var summary = await service.ExecuteAsync(orders, CancellationToken.None);
Console.WriteLine($"처리 완료: 출고 계획 {summary.PreparedCount}건, 제외 {summary.Errors.Count}건");
foreach (var error in summary.Errors)
{
    Console.WriteLine($"- {error.OrderId}: {error.Message}");
}

// record는 값 중심 데이터를 간결하게 표현합니다. init 전용 속성 덕분에 생성 후 뜻밖의 변경을 줄여 안전한 전달 객체가 됩니다.
// string?는 값이 없을 수 있음을 컴파일러에 알려 주며, 사용 전 null 검사를 강제해 운영 중 NullReferenceException을 줄입니다.
sealed record Order(string OrderId, string Region, decimal WeightKg, bool IsExpress, string? Address);
sealed record ShipmentPlan(string OrderId, Carrier Carrier, decimal Fee, string Address);
sealed record PreparationError(string OrderId, string Message);
sealed record PreparationSummary(int PreparedCount, IReadOnlyList<PreparationError> Errors);

// enum은 허용된 운송사만 표현해 "fast-company" 같은 오타가 업무 상태로 들어오는 것을 막습니다.
enum Carrier { Quick, Economy, Island }

// 예상 가능한 검증 실패는 Result로 돌려 호출자가 분기하게 합니다. 장애·취소처럼 정상 흐름 밖의 문제는 예외로 전파합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Repository는 저장 기술을 감춘 계약입니다. 메모리를 DB로 바꿔도 Application Service의 업무 순서는 변하지 않습니다(DIP).
interface IShipmentRepository
{
    Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken);
    Task SaveAsync(ShipmentPlan plan, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShipmentPlan>> GetAllAsync(CancellationToken cancellationToken);
}

// Strategy는 자주 바뀌는 운송사 선택 규칙만 분리합니다. 새 정책을 추가해도 실행 흐름을 고치지 않는 OCP를 연습합니다.
interface IShippingStrategy
{
    Result<ShipmentPlan> CreatePlan(Order order);
}

interface IShipmentLogger
{
    void Prepared(ShipmentPlan plan);
}

sealed class StandardShippingStrategy : IShippingStrategy
{
    public Result<ShipmentPlan> CreatePlan(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            return Result<ShipmentPlan>.Failure("주문 ID는 필수입니다.");
        }

        if (order.WeightKg <= 0)
        {
            return Result<ShipmentPlan>.Failure("무게는 0보다 커야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(order.Address))
        {
            return Result<ShipmentPlan>.Failure("배송 주소가 없습니다.");
        }

        // switch 식은 조건에 맞는 하나의 값을 만듭니다. 구체적인 제주 규칙을 먼저 두어 넓은 특급 규칙에 가려지지 않게 합니다.
        var carrier = (order.Region, order.IsExpress, order.WeightKg) switch
        {
            ("제주", _, _) => Carrier.Island,
            (_, true, <= 10m) => Carrier.Quick,
            _ => Carrier.Economy
        };

        var fee = carrier switch
        {
            Carrier.Quick => 8_000m + order.WeightKg * 900m,
            Carrier.Island => 10_000m + order.WeightKg * 1_200m,
            _ => 3_000m + order.WeightKg * 500m
        };

        return Result<ShipmentPlan>.Success(new(order.OrderId, carrier, decimal.Round(fee), order.Address));
    }
}

// Application Service는 중복 확인 → 규칙 적용 → 저장의 유스케이스 순서만 조정하고, 세부 정책은 협력 객체에 맡깁니다(SRP).
sealed class PrepareShipmentsService(
    IShipmentRepository repository,
    IShippingStrategy strategy,
    IShipmentLogger logger)
{
    public async Task<PreparationSummary> ExecuteAsync(
        IEnumerable<Order> orders,
        CancellationToken cancellationToken)
    {
        var errors = new List<PreparationError>();
        var prepared = new List<ShipmentPlan>();

        foreach (var order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 주문 ID 중복 확인은 같은 요청이 재시도되어도 한 번만 저장하려는 멱등성 경계입니다.
            if (await repository.ExistsAsync(order.OrderId, cancellationToken))
            {
                errors.Add(new(order.OrderId, "이미 출고 준비된 주문입니다."));
                continue;
            }

            var result = strategy.CreatePlan(order);
            if (!result.IsSuccess || result.Value is null)
            {
                errors.Add(new(order.OrderId, result.Error ?? "알 수 없는 검증 오류"));
                continue;
            }

            await repository.SaveAsync(result.Value, cancellationToken);
            logger.Prepared(result.Value);
            prepared.Add(result.Value);
        }

        // LINQ는 저장된 객체를 다시 순회하지 않고 운송사별 건수를 선언적으로 집계합니다.
        var counts = prepared.GroupBy(plan => plan.Carrier)
            .Select(group => $"{group.Key}={group.Count()}");
        Console.WriteLine($"운송사별 건수: {string.Join(", ", counts)}");

        return new(prepared.Count, errors);
    }
}

sealed class InMemoryShipmentRepository : IShipmentRepository
{
    private readonly List<ShipmentPlan> _plans = [];

    public Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_plans.Any(plan => plan.OrderId == orderId));
    }

    public Task SaveAsync(ShipmentPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _plans.Add(plan);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShipmentPlan>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 복사본을 반환해 호출자가 Repository 내부 컬렉션을 몰래 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ShipmentPlan>>(_plans.ToArray());
    }
}

sealed class ConsoleShipmentLogger : IShipmentLogger
{
    public void Prepared(ShipmentPlan plan) =>
        Console.WriteLine($"[출고 준비] order={plan.OrderId}, carrier={plan.Carrier}, fee={plan.Fee:N0}원");
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var tests = new List<(string Name, Func<Task> Run)>
        {
            ("일반 배송은 Economy를 선택한다", TestEconomyAsync),
            ("제주 배송은 Island를 선택한다", TestIslandAsync),
            ("주소가 없으면 저장하지 않는다", TestMissingAddressAsync),
            ("중복 주문은 한 번만 저장한다", TestDuplicateAsync)
        };

        var passed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                passed++;
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FAIL: {test.Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"self-test: {passed}/{tests.Count}");
        if (passed != tests.Count) Environment.ExitCode = 1;
    }

    private static async Task TestEconomyAsync()
    {
        var plans = await ExecuteOneAsync(new("T-1", "서울", 2m, false, "서울"));
        Assert(plans.Single().Carrier == Carrier.Economy, "일반 운송사가 아닙니다.");
    }

    private static async Task TestIslandAsync()
    {
        var plans = await ExecuteOneAsync(new("T-2", "제주", 2m, true, "제주"));
        Assert(plans.Single().Carrier == Carrier.Island, "제주 운송사가 우선되어야 합니다.");
    }

    private static async Task TestMissingAddressAsync()
    {
        var plans = await ExecuteOneAsync(new("T-3", "서울", 2m, false, null));
        Assert(plans.Count == 0, "주소가 없는 주문이 저장되었습니다.");
    }

    private static async Task TestDuplicateAsync()
    {
        var repository = new InMemoryShipmentRepository();
        var service = new PrepareShipmentsService(repository, new StandardShippingStrategy(), new SilentLogger());
        var order = new Order("T-4", "서울", 2m, false, "서울");
        await service.ExecuteAsync([order, order], CancellationToken.None);
        Assert((await repository.GetAllAsync(CancellationToken.None)).Count == 1, "중복 주문이 저장되었습니다.");
    }

    private static async Task<IReadOnlyList<ShipmentPlan>> ExecuteOneAsync(Order order)
    {
        var repository = new InMemoryShipmentRepository();
        var service = new PrepareShipmentsService(repository, new StandardShippingStrategy(), new SilentLogger());
        await service.ExecuteAsync([order], CancellationToken.None);
        return await repository.GetAllAsync(CancellationToken.None);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // 테스트에서는 출력이라는 부수 효과를 없애 검증 결과에만 집중합니다. 인터페이스 기반 DI가 이런 교체를 가능하게 합니다.
    private sealed class SilentLogger : IShipmentLogger
    {
        public void Prepared(ShipmentPlan plan) { }
    }
}
