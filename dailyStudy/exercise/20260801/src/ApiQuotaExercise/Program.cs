// 읽는 순서: 실행 코드 → Application Service → Domain Model → Strategy → Repository → Composition Root → SelfTests
// 한 파일에 모은 이유는 초보자가 파일 이동 없이 요청의 전체 흐름을 먼저 볼 수 있게 하기 위해서입니다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var service = CompositionRoot.Create();
var result = await service.CheckAsync(
    new QuotaCommand("CLIENT-1001", "/orders", RequestPlan.Standard),
    CancellationToken.None);

Console.WriteLine(result.IsSuccess
    ? $"허용 여부: {result.Value!.Allowed}, 남은 요청: {result.Value.Remaining}"
    : $"검사 실패: {result.Error}");

// record는 값 중심 입력을 짧게 표현하고 생성 후 바꾸지 않아, 비동기 흐름에서도 상태 추적을 쉽게 합니다.
// string?처럼 물음표가 붙은 참조 형식은 null 가능성을 컴파일러가 추적한다는 뜻입니다.
public sealed record QuotaCommand(string ClientId, string Endpoint, RequestPlan Plan);
public sealed record QuotaSnapshot(string ClientId, int Used, DateTimeOffset WindowStartedAt);
public sealed record QuotaDecision(bool Allowed, int Remaining, string PolicyName);
public enum RequestPlan { Standard, Premium }

// 예상 가능한 입력·업무 실패는 Result로 반환하면 호출자가 예외 없이 성공과 실패를 명시적으로 분기할 수 있습니다.
// 반면 저장소 연결 끊김 같은 예상 밖 장애는 예외로 남겨 상위 경계에서 기록·재시도하는 편이 낫습니다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

// Application Service는 조회→정책 선택→도메인 판단→저장의 유스케이스 순서만 조율합니다.
// 생성자로 추상화를 받는 DI/DIP 덕분에 실제 DB 없이도 메모리 저장소로 빠르게 테스트할 수 있습니다.
public sealed class QuotaService(
    IQuotaRepository repository,
    IEnumerable<IQuotaPolicy> policies,
    IAuditSink audit)
{
    public async Task<Result<QuotaDecision>> CheckAsync(QuotaCommand command, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(command.ClientId))
            return Result<QuotaDecision>.Failure("클라이언트 ID는 필수입니다.");
        if (string.IsNullOrWhiteSpace(command.Endpoint))
            return Result<QuotaDecision>.Failure("엔드포인트는 필수입니다.");

        var snapshot = await repository.FindAsync(command.ClientId, token)
            ?? new QuotaSnapshot(command.ClientId, 0, DateTimeOffset.UtcNow);

        // FirstOrDefault는 조건에 맞는 항목이 없으면 null이므로 반드시 nullable 검사를 합니다.
        var policy = policies.FirstOrDefault(candidate => candidate.CanHandle(command.Plan));
        if (policy is null)
            return Result<QuotaDecision>.Failure("적용 가능한 할당량 정책이 없습니다.");

        var bucket = QuotaBucket.Restore(snapshot);
        var decision = bucket.TryConsume(policy.Limit);
        await repository.SaveAsync(bucket.ToSnapshot(), token);
        await audit.WriteAsync($"client={command.ClientId}, allowed={decision.Allowed}, policy={policy.Name}", token);
        return Result<QuotaDecision>.Success(decision with { PolicyName = policy.Name });
    }
}

// Domain Model은 사용량이 음수가 되거나 한 요청에서 두 번 증가하는 등의 잘못된 상태 변경을 한곳에서 막습니다.
public sealed class QuotaBucket
{
    public string ClientId { get; }
    public int Used { get; private set; }
    public DateTimeOffset WindowStartedAt { get; }

    private QuotaBucket(string clientId, int used, DateTimeOffset startedAt) =>
        (ClientId, Used, WindowStartedAt) = (clientId, used, startedAt);

    public static QuotaBucket Restore(QuotaSnapshot snapshot) =>
        new(snapshot.ClientId, Math.Max(0, snapshot.Used), snapshot.WindowStartedAt);

    public QuotaDecision TryConsume(int limit)
    {
        if (Used >= limit)
            return new(false, 0, string.Empty);

        Used++;
        return new(true, limit - Used, string.Empty);
    }

    public QuotaSnapshot ToSnapshot() => new(ClientId, Used, WindowStartedAt);
}

// Strategy는 요금제별 제한 계산을 교체 가능하게 해 SRP와 OCP를 지키며, 새 요금제가 서비스 수정을 강요하지 않게 합니다.
public interface IQuotaPolicy
{
    string Name { get; }
    int Limit { get; }
    bool CanHandle(RequestPlan plan);
}

public sealed class StandardQuotaPolicy : IQuotaPolicy
{
    public string Name => "standard-3";
    public int Limit => 3;
    public bool CanHandle(RequestPlan plan) => plan == RequestPlan.Standard;
}

public sealed class PremiumQuotaPolicy : IQuotaPolicy
{
    public string Name => "premium-10";
    public int Limit => 10;
    public bool CanHandle(RequestPlan plan) => plan == RequestPlan.Premium;
}

// Repository는 저장 기술을 업무 코드에서 분리합니다. 운영에서는 원자적 증가가 가능한 Redis나 DB 구현으로 교체해야 합니다.
public interface IQuotaRepository
{
    Task<QuotaSnapshot?> FindAsync(string clientId, CancellationToken token);
    Task SaveAsync(QuotaSnapshot snapshot, CancellationToken token);
    Task<IReadOnlyList<QuotaSnapshot>> GetAllAsync(CancellationToken token);
}

public sealed class MemoryQuotaRepository : IQuotaRepository
{
    private readonly Dictionary<string, QuotaSnapshot> _items = [];

    public Task<QuotaSnapshot?> FindAsync(string clientId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _items.TryGetValue(clientId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task SaveAsync(QuotaSnapshot snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _items[snapshot.ClientId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QuotaSnapshot>> GetAllAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // 복사본을 반환해 호출자가 저장소 내부 컬렉션을 몰래 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<QuotaSnapshot>>(_items.Values.ToList());
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

// Composition Root는 구체 구현 선택을 프로그램 경계 한곳에 모아 나머지 코드가 조립 방법을 모르도록 합니다.
public static class CompositionRoot
{
    public static QuotaService Create(IQuotaRepository? repository = null) => new(
        repository ?? new MemoryQuotaRepository(),
        [new StandardQuotaPolicy(), new PremiumQuotaPolicy()],
        new ConsoleAuditSink());
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("첫 요청 허용", FirstRequestAsync),
            ("제한 초과 거부", LimitExceededAsync),
            ("빈 ID 검증", EmptyClientAsync),
            ("LINQ 사용량 합계", LinqSummaryAsync)
        };

        var passed = 0;
        foreach (var (name, run) in tests)
        {
            try { await run(); Console.WriteLine($"[PASS] {name}"); passed++; }
            catch (Exception exception) { Console.WriteLine($"[FAIL] {name}: {exception.Message}"); }
        }

        Console.WriteLine($"{passed}/{tests.Length} 통과");
        if (passed != tests.Length) Environment.ExitCode = 1;
    }

    private static async Task FirstRequestAsync()
    {
        var result = await CompositionRoot.Create().CheckAsync(new("A", "/orders", RequestPlan.Standard), default);
        Assert(result.Value?.Allowed == true && result.Value.Remaining == 2, "첫 요청은 허용되고 두 번 남아야 합니다.");
    }

    private static async Task LimitExceededAsync()
    {
        var service = CompositionRoot.Create();
        for (var count = 0; count < 3; count++)
            await service.CheckAsync(new("B", "/orders", RequestPlan.Standard), default);
        var result = await service.CheckAsync(new("B", "/orders", RequestPlan.Standard), default);
        Assert(result.Value?.Allowed == false, "네 번째 표준 요청은 거부되어야 합니다.");
    }

    private static async Task EmptyClientAsync()
    {
        var result = await CompositionRoot.Create().CheckAsync(new("", "/orders", RequestPlan.Standard), default);
        Assert(!result.IsSuccess, "빈 ID는 Result 실패여야 합니다.");
    }

    private static async Task LinqSummaryAsync()
    {
        var repository = new MemoryQuotaRepository();
        var service = CompositionRoot.Create(repository);
        await service.CheckAsync(new("A", "/orders", RequestPlan.Standard), default);
        await service.CheckAsync(new("B", "/orders", RequestPlan.Standard), default);
        var snapshots = await repository.GetAllAsync(default);
        // LINQ의 Where와 Sum은 필터링과 합계를 의도를 드러내는 연속 단계로 표현합니다.
        var total = snapshots.Where(item => item.Used > 0).Sum(item => item.Used);
        Assert(total == 2, "두 클라이언트의 총 사용량은 2여야 합니다.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
