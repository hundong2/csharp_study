// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var now = new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryCouponRepository([
    new("REQ-101", "CUS-101", "WELCOME", 0m, 10, true, null, 1),
    new("REQ-102", "CUS-102", "LOYALTY", 320_000m, 400, true, "VIP", 1),
    new("REQ-103", "CUS-103", "LOYALTY", 80_000m, 30, true, null, 2),
    new("REQ-104", "CUS-104", "WELCOME", 0m, 5, false, null, 1)
]);

// Composition Root는 구체 구현을 한곳에서 조립합니다. 서비스가 new를 직접 하지 않아 DI와 테스트 대역 교체가 쉽습니다.
ICouponPolicy policy = new StandardCouponPolicy(now);
var service = new IssueCouponsService(repository, policy, new PrivacySafeAuditLog());

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

// 성공 여부를 확인했으므로 Value가 null이 아님을 !로 nullable 분석기에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"발급 {summary.IssuedCount}건, 검토 {summary.ReviewCount}건, 제외 {summary.SkippedCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.RequestId}: {item.Decision} ({item.Reason})");

// enum은 가능한 결과를 제한하여 문자열 오타와 정의되지 않은 상태를 막습니다.
enum CouponDecision { Issue, ManualReview, Skip }

// record는 값 중심 불변 데이터에 적합합니다. string?은 등급이 없을 수 있음을 형식에 드러냅니다.
sealed record CouponRequest(string RequestId, string CustomerId, string CampaignCode, decimal PurchaseTotal, int AccountAgeDays, bool MarketingConsent, string? Grade, int ExpectedVersion);
sealed record CouponPlan(string RequestId, CouponDecision Decision, string Reason, int ExpectedVersion);
sealed record CouponSummary(IReadOnlyList<CouponPlan> Items)
{
    // LINQ Count는 조건별 집계 의도를 직접 표현하여 변경 가능한 카운터를 줄입니다.
    public int IssuedCount => Items.Count(x => x.Decision == CouponDecision.Issue);
    public int ReviewCount => Items.Count(x => x.Decision == CouponDecision.ManualReview);
    public int SkippedCount => Items.Count(x => x.Decision == CouponDecision.Skip);
}

// 예상 가능한 업무 실패는 Result로 돌려주고, DB 단절이나 버그 같은 뜻밖의 장애는 예외로 전파합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface ICouponRepository
{
    Task<IReadOnlyList<CouponRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(CouponPlan plan, CancellationToken cancellationToken);
}

// Strategy는 자주 바뀌는 캠페인 규칙을 계약 뒤에 숨겨 정책 추가 시 처리 흐름을 수정하지 않게 합니다(OCP).
interface ICouponPolicy { CouponPlan Decide(CouponRequest request); }
interface IAuditLog { void Planned(CouponPlan plan); }

// Application Service는 조회→판단→저장 순서만 맡고 규칙과 저장 세부사항을 협력 객체에 위임합니다(SRP).
sealed class IssueCouponsService(ICouponRepository repository, ICouponPolicy policy, IAuditLog auditLog)
{
    public async Task<Result<CouponSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pending = await repository.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
            return Result<CouponSummary>.Failure("처리할 쿠폰 요청이 없습니다.");

        var plans = new List<CouponPlan>();
        // 명시적 정렬은 저장소 반환 순서가 달라도 실행과 테스트 결과를 재현 가능하게 합니다.
        foreach (var request in pending.OrderBy(x => x.CampaignCode).ThenBy(x => x.RequestId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(request);
            var saved = await repository.SaveAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<CouponSummary>.Failure($"{request.RequestId} 저장 실패: {saved.Error}");
            plans.Add(plan);
            auditLog.Planned(plan);
        }
        return Result<CouponSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class StandardCouponPolicy(DateTimeOffset evaluatedAt) : ICouponPolicy
{
    public CouponPlan Decide(CouponRequest request)
    {
        // 시스템 경계 검증은 잘못된 식별자·금액·버전이 핵심 처리로 퍼지는 것을 막습니다.
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.PurchaseTotal < 0 || request.ExpectedVersion < 1)
            return new(request.RequestId, CouponDecision.Skip, "요청 데이터가 올바르지 않습니다.", request.ExpectedVersion);
        if (!request.MarketingConsent)
            return new(request.RequestId, CouponDecision.Skip, "마케팅 수신 동의가 없습니다.", request.ExpectedVersion);

        var campaign = request.CampaignCode.ToUpperInvariant();
        var grade = request.Grade?.ToUpperInvariant();
        // 튜플 switch 식은 캠페인·가입 기간·금액·등급을 한 결과로 매핑해 규칙 우선순위를 읽기 쉽게 합니다.
        return (campaign, request.AccountAgeDays, request.PurchaseTotal, grade) switch
        {
            ("WELCOME", <= 30, _, _) => new(request.RequestId, CouponDecision.Issue, $"{evaluatedAt:yyyy-MM-dd} 신규 고객 쿠폰 대상입니다.", request.ExpectedVersion),
            ("LOYALTY", _, >= 300_000m, "VIP") => new(request.RequestId, CouponDecision.Issue, "VIP 우수 고객 쿠폰 대상입니다.", request.ExpectedVersion),
            ("LOYALTY", _, >= 100_000m, _) => new(request.RequestId, CouponDecision.ManualReview, "등급 확인이 필요합니다.", request.ExpectedVersion),
            _ => new(request.RequestId, CouponDecision.Skip, "캠페인 발급 조건을 충족하지 않습니다.", request.ExpectedVersion)
        };
    }
}

sealed class InMemoryCouponRepository(IEnumerable<CouponRequest> seed) : ICouponRepository
{
    private readonly List<CouponRequest> _pending = [.. seed];
    private readonly Dictionary<string, CouponPlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<CouponRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본은 호출자가 저장소 내부 목록을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<CouponRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveAsync(CouponPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 결과의 재저장은 성공 처리하여 재시도가 쿠폰을 중복 발급하지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.RequestId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 계획이 저장되었습니다."));
        _saved.Add(plan.RequestId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class PrivacySafeAuditLog : IAuditLog
{
    public void Planned(CouponPlan plan)
    {
        // 고객 ID와 구매 내역 원문은 빼고 요청 ID·결정·버전만 기록해 개인정보 노출을 줄입니다.
        Console.WriteLine($"[audit] request={plan.RequestId} decision={plan.Decision} version={plan.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new StandardCouponPolicy(now);
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("신규 고객은 발급", () => AssertDecisionAsync(policy, new("T-1", "C-1", "WELCOME", 0m, 7, true, null, 1), CouponDecision.Issue)),
            ("동의 없으면 제외", () => AssertDecisionAsync(policy, new("T-2", "C-2", "WELCOME", 0m, 7, false, null, 1), CouponDecision.Skip)),
            ("일반 고액 구매는 검토", () => AssertDecisionAsync(policy, new("T-3", "C-3", "LOYALTY", 150_000m, 100, true, null, 1), CouponDecision.ManualReview)),
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

    private static Task AssertDecisionAsync(ICouponPolicy policy, CouponRequest request, CouponDecision expected)
    {
        var actual = policy.Decide(request).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryCouponRepository([new("T-4", "C-4", "WELCOME", 0m, 1, true, null, 1)]);
        var result = await new IssueCouponsService(repository, new StandardCouponPolicy(now), new SilentLog()).ExecuteAsync(CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 후보가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Planned(CouponPlan plan) { }
    }
}
