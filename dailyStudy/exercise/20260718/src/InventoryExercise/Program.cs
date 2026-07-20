using System.Diagnostics.CodeAnalysis;
using System.Text;

// 학습 순서: ① BasicSyntaxTour → ② InventoryDemo → ③ Application Service
// → ④ Domain Model → ⑤ Repository/Generator → ⑥ SelfTest입니다.
// [고급 관점] 중복 요청을 같은 결과로 처리하는 멱등성과 불변 record 갱신을 살펴보세요.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

BasicSyntaxTour.Run();
await InventoryDemo.RunAsync();

public static class BasicSyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== 1. 기본 문법 둘러보기 ==");

        string productName = "기계식 키보드";
        int stock = 7;
        bool isActive = true;
        decimal? optionalDiscountRate = null;

        string stockState = stock switch
        {
            0 => "품절",
            <= 3 => "재고 부족",
            _ => "판매 가능"
        };

        string[] warehouses = ["서울", "부산"];
        List<int> requests = [2, 1, 3];
        Dictionary<string, int> stockByWarehouse = new()
        {
            ["서울"] = 5,
            ["부산"] = 2
        };

        foreach (string warehouse in warehouses)
        {
            Console.WriteLine($"{warehouse} 창고 재고: {stockByWarehouse[warehouse]}개");
        }

        int requestedTotal = requests.Sum();
        string activeText = isActive ? "판매 중" : "판매 중지";
        decimal discountRate = optionalDiscountRate ?? 0m;
        Console.WriteLine($"{productName}: {stockState}, {activeText}");
        Console.WriteLine($"요청 합계: {requestedTotal}개, 할인율: {discountRate:P0}");
        Console.WriteLine();
    }
}

public static class InventoryDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== 2. 재고 예약 흐름 ==");

        InventoryApp app = CompositionRoot.Build();
        ReserveStockCommand command = new("REQ-1001", "SKU-KEYBOARD", 3);
        Result<Reservation> result = await app.Service.ReserveAsync(command);

        if (result.IsSuccess)
        {
            Reservation reservation = result.Value;
            Console.WriteLine($"예약 번호: {reservation.Id}");
            Console.WriteLine($"상품: {reservation.Sku}");
            Console.WriteLine($"예약 수량: {reservation.Quantity}개");
            Console.WriteLine($"남은 재고: {reservation.RemainingStock}개");
        }
        else
        {
            Console.WriteLine($"예약 실패: {result.Error}");
        }

        Console.WriteLine("초보자 검증은 --self-test 옵션으로 실행하세요.");
    }
}

// Composition Root: 실제 구현을 고르고 객체를 연결하는 한 장소입니다.
public static class CompositionRoot
{
    public static InventoryApp Build()
    {
        IInventoryRepository inventory = new InMemoryInventoryRepository(
            [new InventoryItem("SKU-KEYBOARD", "기계식 키보드", 7, true)]);
        IReservationRepository reservations = new InMemoryReservationRepository();
        IReservationIdGenerator ids = new SequentialReservationIdGenerator();
        InventoryService service = new(inventory, reservations, ids);
        return new InventoryApp(service, inventory, reservations);
    }
}

public sealed record InventoryApp(
    InventoryService Service,
    IInventoryRepository Inventory,
    IReservationRepository Reservations);

// Application Service: 검증, 조회, 도메인 변경, 저장의 순서를 조정합니다.
public sealed class InventoryService(
    IInventoryRepository inventory,
    IReservationRepository reservations,
    IReservationIdGenerator ids)
{
    // [실무] RequestId로 기존 예약을 먼저 찾는 부분이 멱등성입니다.
    // 같은 네트워크 요청이 재시도돼도 재고를 두 번 차감하지 않습니다.
    public async ValueTask<Result<Reservation>> ReserveAsync(
        ReserveStockCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RequestId))
        {
            return Result<Reservation>.Fail("요청 ID는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(command.Sku))
        {
            return Result<Reservation>.Fail("상품 SKU는 필수입니다.");
        }

        if (command.Quantity <= 0)
        {
            return Result<Reservation>.Fail("예약 수량은 1개 이상이어야 합니다.");
        }

        Reservation? duplicate = await reservations.FindByRequestIdAsync(
            command.RequestId.Trim(), cancellationToken);
        if (duplicate is not null)
        {
            return Result<Reservation>.Ok(duplicate);
        }

        InventoryItem? item = await inventory.FindAsync(command.Sku.Trim(), cancellationToken);
        if (item is null)
        {
            return Result<Reservation>.Fail("상품을 찾을 수 없습니다.");
        }

        Result<InventoryItem> stockResult = item.Reserve(command.Quantity);
        if (!stockResult.IsSuccess)
        {
            return Result<Reservation>.Fail(stockResult.Error);
        }

        InventoryItem updatedItem = stockResult.Value;
        Reservation reservation = new(
            ids.Next(), command.RequestId.Trim(), updatedItem.Sku,
            command.Quantity, updatedItem.Stock);

        await inventory.SaveAsync(updatedItem, cancellationToken);
        await reservations.SaveAsync(reservation, cancellationToken);
        return Result<Reservation>.Ok(reservation);
    }
}

// Domain Model: 재고가 줄어들 수 있는 조건을 데이터와 가까운 곳에 둡니다.
public sealed record InventoryItem(string Sku, string Name, int Stock, bool IsActive)
{
    public Result<InventoryItem> Reserve(int quantity)
    {
        if (!IsActive)
        {
            return Result<InventoryItem>.Fail("판매 중지 상품은 예약할 수 없습니다.");
        }

        if (quantity > Stock)
        {
            return Result<InventoryItem>.Fail(
                $"재고가 부족합니다. 현재 {Stock}개, 요청 {quantity}개입니다.");
        }

        // [중급] record의 with는 원본을 바꾸지 않고 일부 값만 바꾼 새 객체를 만듭니다.
        return Result<InventoryItem>.Ok(this with { Stock = Stock - quantity });
    }
}

public sealed record ReserveStockCommand(string RequestId, string Sku, int Quantity);
public sealed record Reservation(
    string Id, string RequestId, string Sku, int Quantity, int RemainingStock);

public sealed record Result<T>(T? Value, string? Error)
{
    // [고급] 재고 부족 같은 예상 가능한 실패는 Result로 반환해 호출자가 명시적으로 분기합니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

// Repository: 저장 방식을 인터페이스 뒤로 숨겨 서비스가 DB 기술에 묶이지 않게 합니다.
public interface IInventoryRepository
{
    ValueTask<InventoryItem?> FindAsync(string sku, CancellationToken cancellationToken);
    ValueTask SaveAsync(InventoryItem item, CancellationToken cancellationToken);
}

public sealed class InMemoryInventoryRepository(IEnumerable<InventoryItem> items)
    : IInventoryRepository
{
    private readonly Dictionary<string, InventoryItem> _items = items.ToDictionary(
        item => item.Sku, StringComparer.OrdinalIgnoreCase);

    public ValueTask<InventoryItem?> FindAsync(
        string sku, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryGetValue(sku, out InventoryItem? item);
        return ValueTask.FromResult(item);
    }

    public ValueTask SaveAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[item.Sku] = item;
        return ValueTask.CompletedTask;
    }
}

public interface IReservationRepository
{
    ValueTask<Reservation?> FindByRequestIdAsync(
        string requestId, CancellationToken cancellationToken);
    ValueTask SaveAsync(Reservation reservation, CancellationToken cancellationToken);
}

public sealed class InMemoryReservationRepository : IReservationRepository
{
    private readonly Dictionary<string, Reservation> _reservations =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<Reservation?> FindByRequestIdAsync(
        string requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reservations.TryGetValue(requestId, out Reservation? reservation);
        return ValueTask.FromResult(reservation);
    }

    public ValueTask SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reservations[reservation.RequestId] = reservation;
        return ValueTask.CompletedTask;
    }
}

// Generator도 계약으로 분리하면 테스트에서 예측 가능한 값을 쓸 수 있습니다.
public interface IReservationIdGenerator { string Next(); }

public sealed class SequentialReservationIdGenerator : IReservationIdGenerator
{
    private int _sequence;
    public string Next() => $"RSV-{++_sequence:0000}";
}

public static class SelfTest
{
    // [검증] 정상·중복·재고 부족·잘못된 수량과 실패 후 상태 보존을 함께 확인합니다.
    public static async Task RunAsync()
    {
        InventoryApp app = CompositionRoot.Build();

        Result<Reservation> success = await ReserveAsync(app, "REQ-1", 3);
        Assert(success.IsSuccess, "재고 안의 수량은 예약에 성공해야 합니다.");
        Assert(success.Value.RemainingStock == 4, "7개에서 3개를 예약하면 4개가 남아야 합니다.");

        Result<Reservation> duplicate = await ReserveAsync(app, "REQ-1", 3);
        Assert(duplicate.IsSuccess, "같은 요청을 재시도해도 성공 결과를 돌려줘야 합니다.");
        Assert(duplicate.Value.Id == success.Value.Id, "재시도는 새 예약을 만들면 안 됩니다.");

        Result<Reservation> tooMany = await ReserveAsync(app, "REQ-2", 5);
        Assert(!tooMany.IsSuccess, "남은 재고보다 많은 수량은 실패해야 합니다.");
        Assert(tooMany.Error.Contains("재고가 부족", StringComparison.Ordinal),
            "오류는 초보자도 원인을 알 수 있어야 합니다.");

        Result<Reservation> invalid = await ReserveAsync(app, "REQ-3", 0);
        Assert(!invalid.IsSuccess, "0개 예약은 실패해야 합니다.");

        InventoryItem? item = await app.Inventory.FindAsync("SKU-KEYBOARD", CancellationToken.None);
        Assert(item?.Stock == 4, "실패한 요청은 재고를 바꾸면 안 됩니다.");

        Console.WriteLine("초보자 검증 통과 (5/5)");
        Console.WriteLine("✓ 정상 예약으로 재고가 7개 → 4개가 됨");
        Console.WriteLine("✓ 같은 요청 재시도는 같은 예약을 반환함");
        Console.WriteLine("✓ 재고 부족과 0개 요청은 이해하기 쉬운 오류로 실패함");
        Console.WriteLine("✓ 실패한 요청은 저장된 재고를 바꾸지 않음");
    }

    private static ValueTask<Result<Reservation>> ReserveAsync(
        InventoryApp app, string requestId, int quantity) =>
        app.Service.ReserveAsync(new(requestId, "SKU-KEYBOARD", quantity));

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }
    }
}
