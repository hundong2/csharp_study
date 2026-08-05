// 오늘 예제는 재고 부족 상품의 발주 제안을 만드는 작은 업무 프로그램입니다.
// 한 파일에 모았지만, 인터페이스와 클래스의 책임을 나눠 실제 .NET 프로젝트의 구조를 연습합니다.

var repository = new InMemoryStockRepository(
[
    new StockItem("PEN-01", "검은 펜", 3, 10, 20, true),
    new StockItem("NOTE-02", "줄 노트", 12, 10, 20, true),
    new StockItem("BAG-03", "종이 봉투", 0, 5, 15, false)
]);

// Composition Root는 프로그램 시작점에서 구현 객체를 조립합니다.
// 업무 로직이 new와 구체 타입을 몰라도 되어 테스트용 구현으로 바꾸기 쉽습니다.
IReorderPolicy policy = new TargetLevelReorderPolicy();
var service = new CreateReorderPlanService(repository, policy, new ConsoleReorderNotifier());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"발주 제안 {result.Value.Count}건, 총 {result.Value.TotalQuantity}개"
    : $"처리 실패: {result.Error}");

// record는 값 중심 데이터를 간결하게 표현하고 init 전용 속성처럼 생성 후 변경을 제한합니다.
// string?은 이름이 없을 수 있음을 타입에 드러내며, 사용 전에 null 검사를 강제합니다.
public sealed record StockItem(
    string Sku,
    string? Name,
    int OnHand,
    int ReorderPoint,
    int TargetLevel,
    bool IsActive);

public sealed record ReorderProposal(string Sku, string Name, int Quantity);
public sealed record ReorderSummary(int Count, int TotalQuantity);

// 예상 가능한 업무 실패는 Result로 반환하면 호출자가 성공과 실패를 명시적으로 처리할 수 있습니다.
// 반면 네트워크 단절 같은 예상 밖 기술 장애는 예외로 받고 경계에서 Result로 번역합니다.
public sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default!, error);
}

public interface IStockRepository
{
    Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken);
}

public interface IReorderPolicy
{
    Result<ReorderProposal> Evaluate(StockItem item);
}

public interface IReorderNotifier
{
    Task NotifyAsync(IReadOnlyList<ReorderProposal> proposals, CancellationToken cancellationToken);
}

// Strategy 패턴은 변할 가능성이 큰 발주 규칙을 인터페이스 뒤에 둡니다.
// 거래처별 정책이 추가되어도 Application Service를 수정하지 않아 개방-폐쇄 원칙에 가깝습니다.
public sealed class TargetLevelReorderPolicy : IReorderPolicy
{
    public Result<ReorderProposal> Evaluate(StockItem item)
    {
        if (!item.IsActive || item.OnHand > item.ReorderPoint)
            return Result<ReorderProposal>.Failure("발주 대상이 아닙니다.");

        if (string.IsNullOrWhiteSpace(item.Name))
            return Result<ReorderProposal>.Failure("상품명이 필요합니다.");

        if (item.OnHand < 0 || item.ReorderPoint < 0 || item.TargetLevel <= item.ReorderPoint)
            return Result<ReorderProposal>.Failure("재고 설정값이 올바르지 않습니다.");

        var quantity = item.TargetLevel - item.OnHand;
        return Result<ReorderProposal>.Success(new(item.Sku, item.Name, quantity));
    }
}

// Application Service는 조회, 판단, 알림의 순서만 조정하고 세부 규칙은 협력 객체에 맡깁니다.
// 생성자 주입은 의존성을 숨기지 않아 테스트 가능성과 단일 책임 원칙을 높입니다.
public sealed class CreateReorderPlanService(
    IStockRepository repository,
    IReorderPolicy policy,
    IReorderNotifier notifier)
{
    public async Task<Result<ReorderSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // await는 I/O가 끝날 때까지 스레드를 점유하지 않으며, 토큰은 운영 중 취소 요청을 전달합니다.
            var items = await repository.GetAllAsync(cancellationToken);

            // LINQ는 원본을 바꾸지 않고 필터와 변환 의도를 이어 씁니다.
            // ToArray에서 한 번 실행해 이후 열거 중 데이터가 달라지는 위험을 줄입니다.
            var proposals = items
                .Select(policy.Evaluate)
                .Where(result => result.IsSuccess)
                .Select(result => result.Value)
                .OrderByDescending(proposal => proposal.Quantity)
                .ToArray();

            await notifier.NotifyAsync(proposals, cancellationToken);
            return Result<ReorderSummary>.Success(
                new(proposals.Length, proposals.Sum(proposal => proposal.Quantity)));
        }
        catch (OperationCanceledException)
        {
            // 취소는 장애가 아니라 상위 흐름의 제어 신호이므로 삼키지 않습니다.
            throw;
        }
        catch (Exception ex)
        {
            // 실제 서비스에서는 원문 예외를 구조화 로그에 남기고 사용자에게는 안전한 메시지를 반환합니다.
            return Result<ReorderSummary>.Failure($"발주 계획 생성 중 기술 오류: {ex.Message}");
        }
    }
}

public sealed class InMemoryStockRepository(IEnumerable<StockItem> seed) : IStockRepository
{
    private readonly IReadOnlyList<StockItem> _items = seed.ToArray();

    public Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items);
    }
}

public sealed class ConsoleReorderNotifier : IReorderNotifier
{
    public Task NotifyAsync(IReadOnlyList<ReorderProposal> proposals, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var proposal in proposals)
            Console.WriteLine($"{proposal.Sku} {proposal.Name}: {proposal.Quantity}개 발주");
        return Task.CompletedTask;
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new TargetLevelReorderPolicy();

        Check(policy.Evaluate(new("A", "상품", 3, 5, 10, true)).Value.Quantity == 7, "부족 수량", ref passed);
        Check(!policy.Evaluate(new("B", "상품", 6, 5, 10, true)).IsSuccess, "충분한 재고 제외", ref passed);
        Check(!policy.Evaluate(new("C", null, 1, 5, 10, true)).IsSuccess, "null 상품명", ref passed);

        var notifier = new CollectingNotifier();
        var repository = new InMemoryStockRepository([new("D", "상품", 0, 2, 5, true)]);
        var service = new CreateReorderPlanService(repository, policy, notifier);
        var result = await service.ExecuteAsync(CancellationToken.None);
        Check(result.Value.Count == 1 && notifier.Count == 1, "서비스 흐름", ref passed);

        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name, ref int passed)
    {
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }

    private sealed class CollectingNotifier : IReorderNotifier
    {
        public int Count { get; private set; }

        public Task NotifyAsync(IReadOnlyList<ReorderProposal> proposals, CancellationToken cancellationToken)
        {
            Count += proposals.Count;
            return Task.CompletedTask;
        }
    }
}
