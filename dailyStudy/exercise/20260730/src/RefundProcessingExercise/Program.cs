// 읽는 순서: 실행 코드 → Application Service → Domain Model → 인터페이스/구현 → 테스트.
// 초보자가 요청 흐름에 집중하도록 오늘은 관련 타입을 한 파일에 모았다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var service = CompositionRoot.Create();
var result = await service.RequestAsync(
    new RefundCommand("ORDER-1001", "beginner@example.com", 35_000m, "상품 파손"),
    CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"환불 접수 성공: {result.Value!.Id} / {result.Value.Status}"
    : $"환불 접수 실패: {result.Error}");

// record는 명령처럼 값 중심인 불변 데이터를 간결하게 표현한다. string?는 null 가능성을 명시한다.
public sealed record RefundCommand(string OrderId, string? Email, decimal Amount, string Reason);
public enum RefundStatus { Requested, Approved }

// 예상 가능한 업무 실패는 Result, 인프라 장애나 버그는 예외로 구분한다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

// Application Service는 유스케이스의 순서만 조율하고 규칙은 도메인과 Strategy에 위임한다.
// 인터페이스 생성자 주입(DI/DIP)은 테스트 대역을 쉽게 넣게 한다.
public sealed class RefundService(
    IRefundRepository repository,
    IEnumerable<IRefundPolicy> policies,
    INotifier notifier)
{
    public async Task<Result<Refund>> RequestAsync(RefundCommand command, CancellationToken token)
    {
        var created = Refund.Create(command.OrderId, command.Amount, command.Reason);
        if (!created.IsSuccess) return created;

        // FirstOrDefault는 없으면 null이므로 nullable 검사가 필요하다.
        var policy = policies.FirstOrDefault(x => x.CanHandle(command.Amount));
        if (policy is null) return Result<Refund>.Failure("적용할 환불 정책이 없습니다.");

        var evaluated = policy.Evaluate(created.Value!);
        await repository.SaveAsync(evaluated.Value!, token);
        if (!string.IsNullOrWhiteSpace(command.Email))
            await notifier.SendAsync(command.Email, evaluated.Value!, token);
        return evaluated;
    }
}

// Domain Model은 상태와 생성 규칙을 함께 보호한다. private setter는 외부의 무단 변경을 막는다.
public sealed class Refund
{
    public Guid Id { get; } = Guid.NewGuid();
    public string OrderId { get; }
    public decimal Amount { get; }
    public RefundStatus Status { get; private set; } = RefundStatus.Requested;

    private Refund(string orderId, decimal amount) => (OrderId, Amount) = (orderId, amount);

    public static Result<Refund> Create(string orderId, decimal amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return Result<Refund>.Failure("주문 번호는 필수입니다.");
        if (amount <= 0) return Result<Refund>.Failure("환불 금액은 0보다 커야 합니다.");
        if (string.IsNullOrWhiteSpace(reason)) return Result<Refund>.Failure("환불 사유는 필수입니다.");
        return Result<Refund>.Success(new Refund(orderId, amount));
    }

    public void Approve() => Status = RefundStatus.Approved;
}

// Strategy는 새 승인 정책을 추가할 때 서비스를 수정하지 않게 하여 SOLID의 OCP를 돕는다.
public interface IRefundPolicy
{
    bool CanHandle(decimal amount);
    Result<Refund> Evaluate(Refund refund);
}

public sealed class AutomaticPolicy : IRefundPolicy
{
    public bool CanHandle(decimal amount) => amount <= 50_000m;
    public Result<Refund> Evaluate(Refund refund)
    {
        refund.Approve();
        return Result<Refund>.Success(refund);
    }
}

public sealed class ManualPolicy : IRefundPolicy
{
    public bool CanHandle(decimal amount) => amount > 50_000m;
    // 고액 환불은 Requested 상태로 남겨 사람의 검토를 기다리게 한다.
    public Result<Refund> Evaluate(Refund refund) => Result<Refund>.Success(refund);
}

// Repository는 저장 기술을 업무 로직에서 분리해 DB 교체와 빠른 테스트를 가능하게 한다.
public interface IRefundRepository
{
    Task SaveAsync(Refund refund, CancellationToken token);
    Task<IReadOnlyList<Refund>> GetAllAsync(CancellationToken token);
}

public sealed class MemoryRepository : IRefundRepository
{
    private readonly List<Refund> _items = [];
    public Task SaveAsync(Refund refund, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _items.Add(refund);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Refund>> GetAllAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // 복사본을 반환해 내부 컬렉션의 캡슐화를 지킨다.
        return Task.FromResult<IReadOnlyList<Refund>>(_items.ToList());
    }
}

public interface INotifier { Task SendAsync(string email, Refund refund, CancellationToken token); }
public sealed class ConsoleNotifier : INotifier
{
    public Task SendAsync(string email, Refund refund, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Console.WriteLine($"알림 전송: {email} / {refund.Status}");
        return Task.CompletedTask;
    }
}

// Composition Root는 구체 구현 선택과 객체 조립을 프로그램 경계 한곳에 모은다.
public static class CompositionRoot
{
    public static RefundService Create(IRefundRepository? repository = null) => new(
        repository ?? new MemoryRepository(),
        [new AutomaticPolicy(), new ManualPolicy()],
        new ConsoleNotifier());
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var tests = new (string, Func<Task>)[]
        {
            ("소액 자동 승인", () => StatusAsync(1_000m, RefundStatus.Approved)),
            ("고액 검토 대기", () => StatusAsync(100_000m, RefundStatus.Requested)),
            ("0원 실패", InvalidAsync),
            ("LINQ 승인액 집계", LinqAsync)
        };
        var passed = 0;
        foreach (var (name, test) in tests)
        {
            try { await test(); Console.WriteLine($"[PASS] {name}"); passed++; }
            catch (Exception e) { Console.WriteLine($"[FAIL] {name}: {e.Message}"); }
        }
        Console.WriteLine($"{passed}/{tests.Length} 통과");
        if (passed != tests.Length) Environment.ExitCode = 1;
    }

    private static async Task StatusAsync(decimal amount, RefundStatus expected)
    {
        var result = await CompositionRoot.Create().RequestAsync(new("O-1", null, amount, "파손"), default);
        Assert(result.Value?.Status == expected, $"상태는 {expected}여야 합니다.");
    }
    private static async Task InvalidAsync()
    {
        var result = await CompositionRoot.Create().RequestAsync(new("O-2", null, 0m, "취소"), default);
        Assert(!result.IsSuccess, "0원은 실패해야 합니다.");
    }
    private static async Task LinqAsync()
    {
        var repository = new MemoryRepository();
        var service = CompositionRoot.Create(repository);
        await service.RequestAsync(new("O-3", null, 10_000m, "불량"), default);
        await service.RequestAsync(new("O-4", null, 70_000m, "파손"), default);
        var items = await repository.GetAllAsync(default);
        // LINQ는 필터링과 합계라는 의도를 반복문보다 직접 표현한다.
        var total = items.Where(x => x.Status == RefundStatus.Approved).Sum(x => x.Amount);
        Assert(total == 10_000m, "승인 금액 합계가 맞아야 합니다.");
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
