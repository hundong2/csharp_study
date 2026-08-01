// 읽는 순서: 실행 코드 → Application Service → Domain Model → Strategy → Repository → Composition Root → 테스트.
// 초보자가 한 요청의 흐름을 잃지 않도록 오늘 학습용 타입은 한 파일에 모았다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var service = CompositionRoot.Create();
var result = await service.AssignAsync(
    new AssignmentCommand("new-checkout", "USER-1001", "beginner@example.com"),
    CancellationToken.None);

Console.WriteLine(result.IsSuccess
    ? $"배정 성공: {result.Value!.UserId} → {result.Value.Variant}"
    : $"배정 실패: {result.Error}");

// record는 값이 같은 명령을 같은 데이터로 다루며, init 전용 불변 데이터를 간결하게 만든다.
// string?는 값이 없을 수 있음을 타입에 표시해 null 실수를 컴파일 단계에서 찾게 한다.
public sealed record AssignmentCommand(string FlagKey, string UserId, string? Email);
public enum Variant { Control, Treatment }

// 예상 가능한 업무 실패는 Result로 반환하면 호출자가 실패를 빠뜨리지 않고 분기할 수 있다.
// 반대로 DB 단절 같은 예상 밖 인프라 장애나 프로그래밍 버그는 예외로 처리한다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

// Application Service는 유스케이스 순서만 조율하고 배정 규칙은 Domain Model과 Strategy에 위임한다.
// 생성자에서 인터페이스를 받는 DI/DIP 설계는 저장소와 정책을 가짜 구현으로 바꿔 테스트하기 쉽게 한다.
public sealed class RolloutService(
    IFlagRepository flags,
    IAssignmentRepository assignments,
    IEnumerable<IAssignmentStrategy> strategies,
    IAuditSink audit)
{
    public async Task<Result<Assignment>> AssignAsync(
        AssignmentCommand command,
        CancellationToken token)
    {
        var input = Assignment.Create(command.FlagKey, command.UserId);
        if (!input.IsSuccess)
            return input;

        var flag = await flags.FindAsync(command.FlagKey, token);
        if (flag is null)
            return Result<Assignment>.Failure("등록되지 않은 기능 플래그입니다.");

        // FirstOrDefault는 항목이 없으면 null이므로 nullable 검사가 반드시 필요하다.
        var strategy = strategies.FirstOrDefault(x => x.CanHandle(flag));
        if (strategy is null)
            return Result<Assignment>.Failure("적용 가능한 배정 정책이 없습니다.");

        var assigned = strategy.Decide(input.Value!, flag);
        await assignments.SaveAsync(assigned, token);
        await audit.WriteAsync($"flag={flag.Key}, user={assigned.UserId}, variant={assigned.Variant}", token);

        // 이메일은 선택값이므로 실제 사용 직전에 빈 문자열까지 함께 검사한다.
        if (!string.IsNullOrWhiteSpace(command.Email))
            Console.WriteLine($"안내 대상: {command.Email}");

        return Result<Assignment>.Success(assigned);
    }
}

// Domain Model은 유효한 상태만 만들도록 생성 규칙과 상태 변경을 한곳에서 보호한다.
public sealed class Assignment
{
    public string FlagKey { get; }
    public string UserId { get; }
    public Variant Variant { get; private set; } = Variant.Control;
    public DateTimeOffset AssignedAt { get; private set; }

    private Assignment(string flagKey, string userId) =>
        (FlagKey, UserId, AssignedAt) = (flagKey, userId, DateTimeOffset.UtcNow);

    public static Result<Assignment> Create(string flagKey, string userId)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
            return Result<Assignment>.Failure("플래그 키는 필수입니다.");
        if (string.IsNullOrWhiteSpace(userId))
            return Result<Assignment>.Failure("사용자 ID는 필수입니다.");
        return Result<Assignment>.Success(new Assignment(flagKey, userId));
    }

    public void Choose(Variant variant)
    {
        Variant = variant;
        AssignedAt = DateTimeOffset.UtcNow;
    }
}

// record는 설정처럼 값 중심인 불변 데이터에 적합하며 with 식으로 안전한 복사도 가능하다.
public sealed record FeatureFlag(string Key, bool Enabled, int TreatmentPercentage);

// Strategy는 정책을 교체하거나 추가할 때 서비스 코드를 수정하지 않게 해 SOLID의 OCP와 SRP를 돕는다.
public interface IAssignmentStrategy
{
    bool CanHandle(FeatureFlag flag);
    Assignment Decide(Assignment assignment, FeatureFlag flag);
}

public sealed class DisabledStrategy : IAssignmentStrategy
{
    public bool CanHandle(FeatureFlag flag) => !flag.Enabled;
    public Assignment Decide(Assignment assignment, FeatureFlag flag)
    {
        assignment.Choose(Variant.Control);
        return assignment;
    }
}

public sealed class PercentageStrategy : IAssignmentStrategy
{
    public bool CanHandle(FeatureFlag flag) => flag.Enabled;
    public Assignment Decide(Assignment assignment, FeatureFlag flag)
    {
        // GetHashCode는 프로세스마다 달라질 수 있어 영구 배정에 부적합하다.
        // 학습 예제는 글자 코드 합으로 같은 사용자를 늘 같은 그룹에 넣는 결정적 계산을 보여 준다.
        var bucket = assignment.UserId.Sum(character => (int)character) % 100;
        assignment.Choose(bucket < flag.TreatmentPercentage ? Variant.Treatment : Variant.Control);
        return assignment;
    }
}

// Repository는 저장 기술을 업무 로직에서 분리해 DB 교체와 빠른 단위 테스트를 가능하게 한다.
public interface IFlagRepository
{
    Task<FeatureFlag?> FindAsync(string key, CancellationToken token);
}

public interface IAssignmentRepository
{
    Task SaveAsync(Assignment assignment, CancellationToken token);
    Task<IReadOnlyList<Assignment>> GetAllAsync(CancellationToken token);
}

public sealed class MemoryFlagRepository : IFlagRepository
{
    private readonly IReadOnlyDictionary<string, FeatureFlag> _flags =
        new Dictionary<string, FeatureFlag>
        {
            ["new-checkout"] = new("new-checkout", true, 50),
            ["disabled-search"] = new("disabled-search", false, 100)
        };

    public Task<FeatureFlag?> FindAsync(string key, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _flags.TryGetValue(key, out var flag);
        return Task.FromResult(flag);
    }
}

public sealed class MemoryAssignmentRepository : IAssignmentRepository
{
    private readonly List<Assignment> _items = [];

    public Task SaveAsync(Assignment assignment, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _items.Add(assignment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Assignment>> GetAllAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // 복사본을 반환해 호출자가 저장소 내부 컬렉션을 몰래 변경하지 못하게 한다.
        return Task.FromResult<IReadOnlyList<Assignment>>(_items.ToList());
    }
}

public interface IAuditSink
{
    Task WriteAsync(string message, CancellationToken token);
}

public sealed class ConsoleAuditSink : IAuditSink
{
    public Task WriteAsync(string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Console.WriteLine($"감사 로그: {message}");
        return Task.CompletedTask;
    }
}

// Composition Root는 구체 구현 선택과 객체 조립을 프로그램 경계 한곳에 모은다.
public static class CompositionRoot
{
    public static RolloutService Create(
        IFlagRepository? flags = null,
        IAssignmentRepository? assignments = null) => new(
            flags ?? new MemoryFlagRepository(),
            assignments ?? new MemoryAssignmentRepository(),
            [new DisabledStrategy(), new PercentageStrategy()],
            new ConsoleAuditSink());
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("활성 플래그 배정", ActiveFlagAsync),
            ("비활성 플래그는 대조군", DisabledFlagAsync),
            ("빈 사용자 실패", InvalidUserAsync),
            ("LINQ 그룹 집계", LinqSummaryAsync)
        };

        var passed = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                await run();
                Console.WriteLine($"[PASS] {name}");
                passed++;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[FAIL] {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{passed}/{tests.Length} 통과");
        if (passed != tests.Length)
            Environment.ExitCode = 1;
    }

    private static async Task ActiveFlagAsync()
    {
        var result = await CompositionRoot.Create().AssignAsync(
            new("new-checkout", "USER-1001", null), default);
        Assert(result.IsSuccess, "활성 플래그는 배정에 성공해야 합니다.");
    }

    private static async Task DisabledFlagAsync()
    {
        var result = await CompositionRoot.Create().AssignAsync(
            new("disabled-search", "USER-1002", null), default);
        Assert(result.Value?.Variant == Variant.Control, "비활성 플래그는 대조군이어야 합니다.");
    }

    private static async Task InvalidUserAsync()
    {
        var result = await CompositionRoot.Create().AssignAsync(
            new("new-checkout", "", null), default);
        Assert(!result.IsSuccess, "빈 사용자 ID는 실패해야 합니다.");
    }

    private static async Task LinqSummaryAsync()
    {
        var repository = new MemoryAssignmentRepository();
        var service = CompositionRoot.Create(assignments: repository);
        await service.AssignAsync(new("new-checkout", "A", null), default);
        await service.AssignAsync(new("new-checkout", "Z", null), default);
        var items = await repository.GetAllAsync(default);

        // LINQ는 그룹화와 개수 계산이라는 의도를 반복문보다 직접 드러낸다.
        var count = items.GroupBy(x => x.Variant).Sum(group => group.Count());
        Assert(count == 2, "저장된 배정은 두 건이어야 합니다.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
