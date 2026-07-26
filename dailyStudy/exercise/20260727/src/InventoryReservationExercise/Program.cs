// 오늘의 읽기 순서: 실행부 → 기본 타입/record → Result → 도메인 → Strategy → Repository → Application Service.
// 한 파일에 모은 이유는 초보자가 파일 이동보다 객체가 협력하는 흐름에 먼저 집중하도록 하기 위해서입니다.

var repository = new InMemoryInventoryRepository(
[
    new InventoryItem("BOOK-001", "C# 입문서", 5),
    new InventoryItem("MOUSE-002", "무선 마우스", 1)
]);
IReservationPolicy policy = new StandardReservationPolicy(maximumQuantity: 3);
var service = new ReservationApplicationService(repository, policy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync(service);
    return;
}

var requests = new[]
{
    new ReservationRequest("ORDER-100", "BOOK-001", 2),
    new ReservationRequest("ORDER-101", "MOUSE-002", 2),
    new ReservationRequest("ORDER-102", "UNKNOWN", 1)
};

foreach (var request in requests)
{
    var result = await service.ReserveAsync(request, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"성공: {result.Value!.OrderId} / 남은 수량 {result.Value.RemainingQuantity}"
        : $"실패: {result.Error}");
}

var lowStockNames = (await repository.GetAllAsync(CancellationToken.None))
    .Where(item => item.AvailableQuantity <= 2)
    .OrderBy(item => item.AvailableQuantity)
    .Select(item => $"{item.Name}({item.AvailableQuantity})");
Console.WriteLine($"재고 부족: {string.Join(", ", lowStockNames)}");

// enum은 허용할 상태를 제한하여 잘못된 문자열 상태가 퍼지는 일을 막습니다.
enum ReservationStatus { Confirmed }

// record는 값 중심 데이터를 간결하게 표현하며, init 전용 속성은 생성 뒤 우발적 변경을 줄입니다.
sealed record ReservationRequest(string OrderId, string Sku, int Quantity);
sealed record ReservationReceipt(string OrderId, string Sku, int RemainingQuantity, ReservationStatus Status);

// Result는 재고 부족처럼 예상 가능한 업무 실패를 예외와 구분하여 호출자가 분기하도록 만듭니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

sealed class InventoryItem
{
    public string Sku { get; }
    public string Name { get; }
    public int AvailableQuantity { get; private set; }

    public InventoryItem(string sku, string name, int availableQuantity)
    {
        // ArgumentException은 잘못된 객체 자체를 만들 수 없게 하는 개발/입력 계약 위반에 사용합니다.
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(availableQuantity);
        Sku = sku;
        Name = name;
        AvailableQuantity = availableQuantity;
    }

    public Result<int> Reserve(int quantity)
    {
        if (quantity <= 0)
            return Result<int>.Failure("예약 수량은 1개 이상이어야 합니다.");
        if (quantity > AvailableQuantity)
            return Result<int>.Failure($"재고가 부족합니다. 현재 {AvailableQuantity}개");

        AvailableQuantity -= quantity;
        return Result<int>.Success(AvailableQuantity);
    }
}

// Strategy 인터페이스는 주문별 제한 정책이 바뀌어도 서비스 코드를 수정하지 않게 합니다(OCP).
interface IReservationPolicy
{
    Result<bool> Validate(ReservationRequest request);
}

sealed class StandardReservationPolicy(int maximumQuantity) : IReservationPolicy
{
    public Result<bool> Validate(ReservationRequest request) =>
        request.Quantity <= maximumQuantity
            ? Result<bool>.Success(true)
            : Result<bool>.Failure($"한 주문은 최대 {maximumQuantity}개까지 예약할 수 있습니다.");
}

// Repository는 저장 기술을 숨겨 서비스가 메모리, SQL 같은 구현 세부사항에 의존하지 않게 합니다(DIP).
interface IInventoryRepository
{
    Task<InventoryItem?> FindAsync(string sku, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAsync(InventoryItem item, CancellationToken cancellationToken);
}

sealed class InMemoryInventoryRepository(IEnumerable<InventoryItem> seed) : IInventoryRepository
{
    private readonly Dictionary<string, InventoryItem> _items =
        seed.ToDictionary(item => item.Sku, StringComparer.OrdinalIgnoreCase);

    public Task<InventoryItem?> FindAsync(string sku, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryGetValue(sku, out var item); // nullable 반환은 "없음"이 정상 결과임을 타입으로 드러냅니다.
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<InventoryItem>>(_items.Values.ToArray());
    }

    public Task SaveAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[item.Sku] = item;
        return Task.CompletedTask;
    }
}

interface IAuditLog
{
    void ReservationCreated(ReservationReceipt receipt);
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void ReservationCreated(ReservationReceipt receipt) =>
        Console.WriteLine($"감사 로그: {receipt.OrderId}, {receipt.Sku}, UTC={DateTimeOffset.UtcNow:O}");
}

// Application Service는 조회→정책 검사→도메인 실행→저장 순서를 조정하고 업무 규칙은 각 객체에 맡깁니다(SRP).
sealed class ReservationApplicationService(
    IInventoryRepository repository,
    IReservationPolicy policy,
    IAuditLog auditLog)
{
    public async Task<Result<ReservationReceipt>> ReserveAsync(
        ReservationRequest request,
        CancellationToken cancellationToken)
    {
        var policyResult = policy.Validate(request);
        if (!policyResult.IsSuccess)
            return Result<ReservationReceipt>.Failure(policyResult.Error!);

        var item = await repository.FindAsync(request.Sku, cancellationToken);
        if (item is null)
            return Result<ReservationReceipt>.Failure("상품을 찾을 수 없습니다.");

        var reserveResult = item.Reserve(request.Quantity);
        if (!reserveResult.IsSuccess)
            return Result<ReservationReceipt>.Failure(reserveResult.Error!);

        await repository.SaveAsync(item, cancellationToken);
        var receipt = new ReservationReceipt(
            request.OrderId, request.Sku, reserveResult.Value, ReservationStatus.Confirmed);
        auditLog.ReservationCreated(receipt);
        return Result<ReservationReceipt>.Success(receipt);
    }
}

static class SelfTest
{
    public static async Task RunAsync(ReservationApplicationService service)
    {
        var cases = new[]
        {
            ("정상 예약", await service.ReserveAsync(new("TEST-1", "BOOK-001", 2), default), true),
            ("재고 부족", await service.ReserveAsync(new("TEST-2", "MOUSE-002", 2), default), false),
            ("없는 상품", await service.ReserveAsync(new("TEST-3", "NONE", 1), default), false),
            ("정책 초과", await service.ReserveAsync(new("TEST-4", "BOOK-001", 4), default), false)
        };

        foreach (var (name, result, expected) in cases)
        {
            if (result.IsSuccess != expected)
                throw new InvalidOperationException($"{name} 검증 실패");
            Console.WriteLine($"PASS: {name}");
        }
    }
}
