// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var receivedAt = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryDisputeRepository([
    new("DSP-101", 190_000m, "fraud", receivedAt.AddHours(-2), "order-101", 1),
    new("DSP-102", 25_000m, "duplicate", receivedAt.AddHours(-5), "order-102", 1),
    new("DSP-103", 0m, "fraud", receivedAt.AddHours(-1), "order-103", 2),
    new("DSP-104", 80_000m, null, receivedAt.AddHours(-30), "order-104", 3)
]);

// Composition Root는 실제 구현을 한곳에서 조립합니다. 서비스가 구체 구현을 만들지 않아 테스트 대역으로 쉽게 교체됩니다(DI/DIP).
IDisputePolicy policy = new RiskBasedDisputePolicy();
var service = new TriageDisputesService(repository, policy, new PrivacySafeAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"처리 실패: {result.Error}");
    return;
}

// 성공 여부를 확인한 뒤 Value를 쓰므로 !로 nullable 분석기에 null이 아님을 알립니다.
var summary = result.Value!;
Console.WriteLine($"긴급 {summary.UrgentCount}건, 일반 {summary.StandardCount}건, 거절 {summary.RejectedCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.DisputeId}: {item.Decision} ({item.Reason})");

// enum은 가능한 상태를 제한해 문자열 오타와 정의되지 않은 결정을 막습니다.
enum TriageDecision { UrgentReview, StandardReview, Reject }

// record는 값 중심 불변 데이터에 적합합니다. string?은 사유 코드가 없을 수 있다는 사실을 형식에 드러냅니다.
sealed record DisputeSnapshot(string DisputeId, decimal Amount, string? ReasonCode, DateTimeOffset ReportedAt, string PaymentReference, int ExpectedVersion);
sealed record TriagePlan(string DisputeId, TriageDecision Decision, string Reason, int ExpectedVersion);
sealed record TriageSummary(IReadOnlyList<TriagePlan> Items)
{
    // LINQ Count는 조건별 집계 의도를 직접 표현해 변경 가능한 카운터를 줄입니다.
    public int UrgentCount => Items.Count(x => x.Decision == TriageDecision.UrgentReview);
    public int StandardCount => Items.Count(x => x.Decision == TriageDecision.StandardReview);
    public int RejectedCount => Items.Count(x => x.Decision == TriageDecision.Reject);
}

// 예상 가능한 업무 실패는 Result로 반환하고, DB 단절이나 버그 같은 뜻밖의 장애는 예외로 전파합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IDisputeRepository
{
    Task<IReadOnlyList<DisputeSnapshot>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(TriagePlan plan, CancellationToken cancellationToken);
}

// Strategy는 자주 바뀌는 분류 기준을 계약 뒤에 숨겨 새 정책 추가 시 서비스 흐름을 수정하지 않게 합니다(OCP).
interface IDisputePolicy { TriagePlan Decide(DisputeSnapshot dispute); }
interface IAuditLog { void Planned(TriagePlan plan); }

// Application Service는 조회→판단→저장 순서만 맡고 규칙과 저장 세부사항을 협력 객체에 위임합니다(SRP).
sealed class TriageDisputesService(IDisputeRepository repository, IDisputePolicy policy, IAuditLog auditLog)
{
    public async Task<Result<TriageSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pending = await repository.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
            return Result<TriageSummary>.Failure("분류할 결제 분쟁이 없습니다.");

        var plans = new List<TriagePlan>();
        // 명시적 정렬은 DB 반환 순서가 달라도 결과와 테스트를 재현 가능하게 만듭니다.
        foreach (var dispute in pending.OrderBy(x => x.ReportedAt).ThenBy(x => x.DisputeId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(dispute);
            var saved = await repository.SaveAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<TriageSummary>.Failure($"{dispute.DisputeId} 저장 실패: {saved.Error}");

            plans.Add(plan);
            auditLog.Planned(plan);
        }
        return Result<TriageSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class RiskBasedDisputePolicy : IDisputePolicy
{
    public TriagePlan Decide(DisputeSnapshot dispute)
    {
        // 시스템 경계에서 식별자·금액·버전을 검사하면 잘못된 데이터가 핵심 처리로 퍼지지 않습니다.
        if (string.IsNullOrWhiteSpace(dispute.DisputeId) || dispute.Amount <= 0 || dispute.ExpectedVersion < 1)
            return new(dispute.DisputeId, TriageDecision.Reject, "분쟁 데이터가 올바르지 않습니다.", dispute.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(dispute.ReasonCode))
            return new(dispute.DisputeId, TriageDecision.Reject, "분쟁 사유가 필요합니다.", dispute.ExpectedVersion);

        // switch 식은 입력을 한 결과로 매핑합니다. or 패턴은 여러 사유를 같은 규칙으로 묶습니다.
        return (dispute.ReasonCode.ToLowerInvariant(), dispute.Amount) switch
        {
            ("fraud", >= 100_000m) => new(dispute.DisputeId, TriageDecision.UrgentReview, "고액 부정 사용 의심 건입니다.", dispute.ExpectedVersion),
            ("fraud" or "duplicate", _) => new(dispute.DisputeId, TriageDecision.StandardReview, "증빙 확인이 필요한 지원 사유입니다.", dispute.ExpectedVersion),
            _ => new(dispute.DisputeId, TriageDecision.Reject, "지원하지 않는 분쟁 사유입니다.", dispute.ExpectedVersion)
        };
    }
}

sealed class InMemoryDisputeRepository(IEnumerable<DisputeSnapshot> seed) : IDisputeRepository
{
    private readonly List<DisputeSnapshot> _pending = [.. seed];
    private readonly Dictionary<string, TriagePlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<DisputeSnapshot>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본은 호출자가 저장소 내부 목록을 우연히 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<DisputeSnapshot>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveAsync(TriagePlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 결과의 재저장은 성공 처리하여 재시도가 중복 업무를 만들지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.DisputeId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 분류 결과가 저장되었습니다."));
        _saved.Add(plan.DisputeId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class PrivacySafeAuditLog : IAuditLog
{
    public void Planned(TriagePlan plan)
    {
        // 결제 참조와 고객 정보는 민감하므로 로그에는 분쟁 ID·결정·버전만 남깁니다.
        Console.WriteLine($"[audit] dispute={plan.DisputeId} decision={plan.Decision} version={plan.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new RiskBasedDisputePolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("고액 부정 사용은 긴급", () => AssertDecisionAsync(policy, new("T-1", 100_000m, "fraud", now, "p-1", 1), TriageDecision.UrgentReview)),
            ("중복 결제는 일반", () => AssertDecisionAsync(policy, new("T-2", 30_000m, "duplicate", now, "p-2", 1), TriageDecision.StandardReview)),
            ("0원 분쟁은 거절", () => AssertDecisionAsync(policy, new("T-3", 0m, "fraud", now, "p-3", 1), TriageDecision.Reject)),
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

    private static Task AssertDecisionAsync(IDisputePolicy policy, DisputeSnapshot dispute, TriageDecision expected)
    {
        var actual = policy.Decide(dispute).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryDisputeRepository([new("T-4", 50_000m, "fraud", now, "p-4", 1)]);
        var result = await new TriageDisputesService(repository, new RiskBasedDisputePolicy(), new SilentLog()).ExecuteAsync(CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 후보가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Planned(TriagePlan plan) { }
    }
}
