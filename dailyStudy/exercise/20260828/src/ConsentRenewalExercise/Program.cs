// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryConsentRepository([
    new("CUSTOMER-101", "email", now.AddDays(-370), true, 1),
    new("CUSTOMER-102", "sms", now.AddDays(-330), true, 2),
    new("CUSTOMER-103", null, now.AddDays(-400), true, 1),
    new("CUSTOMER-104", "email", now.AddDays(-500), false, 3)
]);

// Composition Root는 실제 구현을 한곳에서 조립합니다. 서비스가 구체 저장소를 직접 만들지 않아 테스트 대역으로 교체하기 쉽습니다(DI/DIP).
IConsentRenewalPolicy policy = new ExpiryBasedConsentRenewalPolicy();
var service = new PlanConsentRenewalsService(repository, policy, new PrivacySafeAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(now, CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"처리 실패: {result.Error}");
    return;
}

// 성공 여부를 먼저 확인했으므로 Value가 null이 아님을 null-forgiving 연산자(!)로 컴파일러에 알립니다.
var summary = result.Value!;
Console.WriteLine($"발송 {summary.SendCount}건, 대기 {summary.WaitCount}건, 제외 {summary.SkipCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.CustomerId}: {item.Decision} ({item.Reason})");

// enum은 가능한 결정을 제한하여 문자열 오타와 정의되지 않은 상태를 막습니다.
enum RenewalDecision { SendReminder, Wait, Skip }

// record는 값 중심 불변 데이터에 적합합니다. nullable string?은 연락 채널이 없을 수 있다는 업무 사실을 형식에 드러냅니다.
sealed record ConsentSnapshot(string CustomerId, string? PreferredChannel, DateTimeOffset ConsentedAt, bool IsAccountActive, int ExpectedVersion);
sealed record RenewalPlan(string CustomerId, string? Channel, RenewalDecision Decision, string Reason, int ExpectedVersion);
sealed record RenewalSummary(IReadOnlyList<RenewalPlan> Items)
{
    // LINQ Count는 조건별 집계라는 의도를 직접 표현해 수동 반복문의 상태 변경을 줄입니다.
    public int SendCount => Items.Count(x => x.Decision == RenewalDecision.SendReminder);
    public int WaitCount => Items.Count(x => x.Decision == RenewalDecision.Wait);
    public int SkipCount => Items.Count(x => x.Decision == RenewalDecision.Skip);
}

// 예상 가능한 입력·업무 실패는 Result로 반환하고, DB 단절이나 버그 같은 뜻밖의 장애는 예외로 전파합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IConsentRepository
{
    Task<IReadOnlyList<ConsentSnapshot>> GetCandidatesAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(RenewalPlan plan, CancellationToken cancellationToken);
}

// Strategy는 변경하기 쉬운 갱신 기준을 계약 뒤에 숨겨 새 정책을 추가해도 서비스 흐름을 수정하지 않게 합니다(OCP).
interface IConsentRenewalPolicy { RenewalPlan Decide(ConsentSnapshot snapshot, DateTimeOffset now); }
interface IAuditLog { void Planned(RenewalPlan plan); }

// Application Service는 조회→판단→저장의 사용 사례 순서만 맡고, 규칙과 저장 세부사항은 협력 객체에 위임합니다(SRP).
sealed class PlanConsentRenewalsService(IConsentRepository repository, IConsentRenewalPolicy policy, IAuditLog auditLog)
{
    public async Task<Result<RenewalSummary>> ExecuteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await repository.GetCandidatesAsync(cancellationToken);
        if (candidates.Count == 0)
            return Result<RenewalSummary>.Failure("검토할 동의 정보가 없습니다.");

        var plans = new List<RenewalPlan>();
        // 명시적 정렬은 DB 반환 순서가 달라도 실행 결과와 테스트를 재현 가능하게 만듭니다.
        foreach (var candidate in candidates.OrderBy(x => x.ConsentedAt).ThenBy(x => x.CustomerId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(candidate, now);
            var saved = await repository.SaveAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<RenewalSummary>.Failure($"{candidate.CustomerId} 저장 실패: {saved.Error}");

            plans.Add(plan);
            auditLog.Planned(plan);
        }
        return Result<RenewalSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class ExpiryBasedConsentRenewalPolicy : IConsentRenewalPolicy
{
    public RenewalPlan Decide(ConsentSnapshot snapshot, DateTimeOffset now)
    {
        // 경계에서 식별자·버전·미래 시각을 검증하면 잘못된 데이터가 핵심 로직과 저장소로 퍼지지 않습니다.
        if (string.IsNullOrWhiteSpace(snapshot.CustomerId) || snapshot.ExpectedVersion < 1 || snapshot.ConsentedAt > now)
            return new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.Skip, "동의 데이터가 올바르지 않습니다.", snapshot.ExpectedVersion);
        if (!snapshot.IsAccountActive)
            return new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.Skip, "비활성 계정에는 안내하지 않습니다.", snapshot.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(snapshot.PreferredChannel))
            return new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.Skip, "사용 가능한 연락 채널이 없습니다.", snapshot.ExpectedVersion);

        var age = now - snapshot.ConsentedAt;
        return age.TotalDays switch
        {
            >= 365 => new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.SendReminder, "동의 후 365일이 지나 갱신 안내가 필요합니다.", snapshot.ExpectedVersion),
            >= 335 => new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.Wait, "갱신 시점이 가까워 대기 목록에 둡니다.", snapshot.ExpectedVersion),
            _ => new(snapshot.CustomerId, snapshot.PreferredChannel, RenewalDecision.Skip, "아직 갱신 시점이 아닙니다.", snapshot.ExpectedVersion)
        };
    }
}

sealed class InMemoryConsentRepository(IEnumerable<ConsentSnapshot> seed) : IConsentRepository
{
    private readonly List<ConsentSnapshot> _candidates = [.. seed];
    private readonly Dictionary<string, RenewalPlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ConsentSnapshot>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 호출자가 저장소 내부 목록을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ConsentSnapshot>>(_candidates.ToArray());
    }

    public Task<Result<bool>> SaveAsync(RenewalPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 결과의 재저장은 성공 처리하여 재시도가 동일 안내 계획을 중복 생성하지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.CustomerId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 갱신 계획이 저장되었습니다."));
        _saved.Add(plan.CustomerId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class PrivacySafeAuditLog : IAuditLog
{
    public void Planned(RenewalPlan plan)
    {
        // 연락처 원문은 개인정보이므로 로그에 남기지 않고 고객 식별자·결정·버전만 기록합니다.
        Console.WriteLine($"[audit] customer={plan.CustomerId} decision={plan.Decision} version={plan.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new ExpiryBasedConsentRenewalPolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("365일 경과는 발송", () => AssertDecisionAsync(policy, new("T-1", "email", now.AddDays(-365), true, 1), now, RenewalDecision.SendReminder)),
            ("갱신 임박은 대기", () => AssertDecisionAsync(policy, new("T-2", "sms", now.AddDays(-340), true, 1), now, RenewalDecision.Wait)),
            ("연락 채널 없음은 제외", () => AssertDecisionAsync(policy, new("T-3", null, now.AddDays(-400), true, 1), now, RenewalDecision.Skip)),
            ("서비스는 전체 후보 처리", ServiceProcessesAllAsync)
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

    private static Task AssertDecisionAsync(IConsentRenewalPolicy policy, ConsentSnapshot snapshot, DateTimeOffset now, RenewalDecision expected)
    {
        var actual = policy.Decide(snapshot, now).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryConsentRepository([new("T-4", "email", now.AddDays(-370), true, 1)]);
        var result = await new PlanConsentRenewalsService(repository, new ExpiryBasedConsentRenewalPolicy(), new SilentLog()).ExecuteAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 후보가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Planned(RenewalPlan plan) { }
    }
}
