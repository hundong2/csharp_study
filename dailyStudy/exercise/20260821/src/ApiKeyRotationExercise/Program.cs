// 이 파일은 위에서 아래로 조립 → 실행 → 모델 → 계약 → 구현 → 테스트 순서로 읽도록 구성했습니다.
// 작은 콘솔 앱 하나로 기본 문법과 실무 아키텍처가 어떻게 연결되는지 확인하세요.

var today = new DateOnly(2026, 8, 21);
var repository = new InMemoryApiKeyRepository(
[
    new("KEY-101", "billing-api", today.AddDays(-120), today.AddDays(2), true, "payments"),
    new("KEY-102", "search-worker", today.AddDays(-20), today.AddDays(40), false, null),
    new("KEY-103", "report-export", today.AddDays(-100), today.AddDays(20), false, "data"),
    new("KEY-104", "legacy-sync", today.AddDays(-200), today.AddDays(-1), false, "integration")
]);

// 구체 구현은 시작 지점(Composition Root)에서 조립합니다. 서비스가 인터페이스에 의존하므로 테스트 대역으로 바꾸기 쉽습니다(DI/DIP).
IApiKeyRotationPolicy policy = new RiskBasedRotationPolicy();
var service = new PlanApiKeyRotationsService(repository, policy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.PlanAsync(today, CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"계획 실패: {result.Error}");
    return;
}

var summary = result.Value!; // 성공을 확인했으므로 값이 null이 아님을 컴파일러에 알려 줍니다.
Console.WriteLine($"즉시 {summary.ImmediateCount}건, 예약 {summary.ScheduledCount}건, 유지 {summary.KeepCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.KeyId}: {item.Action} / {item.OwnerTeam} ({item.Reason})");

// enum은 허용되는 상태를 이름으로 제한해 임의 문자열과 오타를 막습니다.
enum RotationAction { Keep, Scheduled, Immediate }

// record는 값 중심의 불변 데이터에 적합합니다. Secret 자체는 모델에 넣지 않아 실수로 출력되는 위험도 줄입니다.
sealed record ApiKeyMetadata(string Id, string Application, DateOnly CreatedOn, DateOnly ExpiresOn, bool SuspectedLeak, string? OwnerHint);
sealed record RotationPlanItem(string KeyId, RotationAction Action, string OwnerTeam, string Reason);
sealed record RotationSummary(IReadOnlyList<RotationPlanItem> Items)
{
    // LINQ Count는 조건에 맞는 항목 수를 구한다는 의도를 반복문보다 직접 표현합니다.
    public int ImmediateCount => Items.Count(x => x.Action == RotationAction.Immediate);
    public int ScheduledCount => Items.Count(x => x.Action == RotationAction.Scheduled);
    public int KeepCount => Items.Count(x => x.Action == RotationAction.Keep);
}

// 예상 가능한 업무 실패는 Result로 표현해 호출자가 성공과 실패를 명시적으로 분기하게 합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKeyMetadata>> GetActiveAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SavePlanAsync(RotationPlanItem item, CancellationToken cancellationToken);
}

// Strategy 계약을 두면 위험 판단 규칙을 서비스 수정 없이 새 구현으로 확장할 수 있습니다(OCP).
interface IApiKeyRotationPolicy
{
    RotationPlanItem Decide(ApiKeyMetadata key, DateOnly today);
}

interface IAuditLog
{
    void PlanSaved(RotationPlanItem item);
}

// Application Service는 조회 → 판단 → 저장의 사용 사례 순서만 맡고 세부 규칙과 저장 기술은 협력 객체에 위임합니다(SRP).
sealed class PlanApiKeyRotationsService(IApiKeyRepository repository, IApiKeyRotationPolicy policy, IAuditLog auditLog)
{
    public async Task<Result<RotationSummary>> PlanAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var keys = await repository.GetActiveAsync(cancellationToken);
        if (keys.Count == 0)
            return Result<RotationSummary>.Failure("검토할 활성 API 키가 없습니다.");

        var plans = new List<RotationPlanItem>();
        // OrderBy로 순서를 고정하면 실행과 테스트 결과가 매번 달라지는 혼란을 줄입니다.
        foreach (var key in keys.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = policy.Decide(key, today);
            var saved = await repository.SavePlanAsync(item, cancellationToken);
            if (!saved.IsSuccess)
                return Result<RotationSummary>.Failure($"{key.Id} 저장 실패: {saved.Error}");

            plans.Add(item);
            auditLog.PlanSaved(item);
        }

        return Result<RotationSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class RiskBasedRotationPolicy : IApiKeyRotationPolicy
{
    public RotationPlanItem Decide(ApiKeyMetadata key, DateOnly today)
    {
        // 잘못된 입력을 먼저 막으면 이후 업무 규칙은 유효한 값만 다룰 수 있습니다.
        if (string.IsNullOrWhiteSpace(key.Id) || string.IsNullOrWhiteSpace(key.Application) || key.ExpiresOn < key.CreatedOn)
            return new(key.Id, RotationAction.Immediate, "security", "메타데이터 오류를 보안 담당자가 확인해야 합니다.");

        var owner = string.IsNullOrWhiteSpace(key.OwnerHint) ? "platform" : key.OwnerHint;
        var daysUntilExpiry = key.ExpiresOn.DayNumber - today.DayNumber;
        var ageDays = today.DayNumber - key.CreatedOn.DayNumber;

        if (key.SuspectedLeak || daysUntilExpiry <= 0)
            return new(key.Id, RotationAction.Immediate, owner, "유출 의심 또는 만료 상태입니다.");

        if (daysUntilExpiry <= 7 || ageDays >= 90)
            return new(key.Id, RotationAction.Scheduled, owner, "7일 이내 만료 또는 생성 후 90일이 지났습니다.");

        return new(key.Id, RotationAction.Keep, owner, "현재 교체 기준에 해당하지 않습니다.");
    }
}

sealed class InMemoryApiKeyRepository(IEnumerable<ApiKeyMetadata> seed) : IApiKeyRepository
{
    private readonly List<ApiKeyMetadata> _active = [.. seed];
    private readonly Dictionary<string, RotationPlanItem> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ApiKeyMetadata>> GetActiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 복사본을 반환해 호출자가 저장소 내부 목록을 직접 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ApiKeyMetadata>>(_active.ToArray());
    }

    public Task<Result<bool>> SavePlanAsync(RotationPlanItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 계획의 재저장은 성공으로 보는 멱등성이 재시도 때 중복 작업을 막습니다.
        if (_saved.TryGetValue(item.KeyId, out var existing))
            return Task.FromResult(existing == item
                ? Result<bool>.Success(true)
                : Result<bool>.Failure("이미 다른 교체 계획이 저장되었습니다."));

        _saved.Add(item.KeyId, item);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void PlanSaved(RotationPlanItem item)
    {
        // 로그에는 추적용 ID와 결정만 남깁니다. 실제 API 키 값이나 인증 헤더는 절대 기록하면 안 됩니다.
        Console.WriteLine($"[audit] key={item.KeyId} action={item.Action} owner={item.OwnerTeam}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var today = new DateOnly(2026, 8, 21);
        var policy = new RiskBasedRotationPolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("유출 의심 키는 즉시 교체", () => AssertActionAsync(policy, new("T-1", "api", today.AddDays(-1), today.AddDays(30), true, null), today, RotationAction.Immediate)),
            ("7일 이내 만료 키는 예약", () => AssertActionAsync(policy, new("T-2", "api", today.AddDays(-10), today.AddDays(7), false, "team-a"), today, RotationAction.Scheduled)),
            ("안전한 새 키는 유지", () => AssertActionAsync(policy, new("T-3", "api", today.AddDays(-10), today.AddDays(50), false, null), today, RotationAction.Keep)),
            ("서비스는 모든 키를 처리", ServiceProcessesAllAsync)
        };

        var passed = 0;
        foreach (var test in tests)
        {
            await test.Run();
            passed++;
            Console.WriteLine($"PASS: {test.Name}");
        }
        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
    }

    private static Task AssertActionAsync(IApiKeyRotationPolicy policy, ApiKeyMetadata key, DateOnly today, RotationAction expected)
    {
        var actual = policy.Decide(key, today).Action;
        if (actual != expected)
            throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var today = new DateOnly(2026, 8, 21);
        var repository = new InMemoryApiKeyRepository([
            new("T-4", "a", today.AddDays(-1), today.AddDays(30), false, null),
            new("T-5", "b", today.AddDays(-100), today.AddDays(30), false, "ops")
        ]);
        var service = new PlanApiKeyRotationsService(repository, new RiskBasedRotationPolicy(), new SilentLog());
        var result = await service.PlanAsync(today, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 2)
            throw new InvalidOperationException("모든 키가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트에서는 콘솔 출력 대신 반환 결과만 검증하므로 아무 작업도 하지 않는 대역을 사용합니다.
        public void PlanSaved(RotationPlanItem item) { }
    }
}
