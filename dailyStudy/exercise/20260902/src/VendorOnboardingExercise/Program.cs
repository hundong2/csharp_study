// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var repository = new InMemoryVendorRepository([
    new("REQ-101", "새봄소프트", "KR", 12_000_000m, true, "contact@saebom.example", 1),
    new("REQ-102", "글로벌부품", "US", 85_000_000m, true, null, 1),
    new("REQ-103", "빠른물류", "KR", 6_000_000m, false, "ops@fast.example", 2),
    new("REQ-104", "신규컨설팅", "XZ", 3_000_000m, true, "hello@new.example", 1)
]);

// Composition Root는 구체 구현을 한곳에서 조립합니다. 서비스가 구현을 직접 만들지 않아 테스트 대역 교체가 쉽습니다.
IVendorRiskPolicy policy = new StandardVendorRiskPolicy();
var service = new ReviewVendorsService(repository, policy, new PrivacySafeAuditLog());

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

// 성공 분기를 확인했으므로 Value가 null이 아님을 !로 nullable 분석기에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"승인 {summary.ApprovedCount}건, 수동 검토 {summary.ReviewCount}건, 거절 {summary.RejectedCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.RequestId}: {item.Decision} ({item.Reason})");

// enum은 가능한 결과를 제한하여 문자열 오타와 정의되지 않은 상태를 막습니다.
enum VendorDecision { Approve, ManualReview, Reject }

// record는 값 중심 불변 데이터에 적합합니다. decimal은 금액을 정확히 다루고 string?은 이메일이 없을 수 있음을 표현합니다.
sealed record VendorApplication(string RequestId, string CompanyName, string CountryCode, decimal AnnualContractAmount, bool TaxIdVerified, string? ContactEmail, int ExpectedVersion);
sealed record VendorReview(string RequestId, VendorDecision Decision, string Reason, int ExpectedVersion);
sealed record ReviewSummary(IReadOnlyList<VendorReview> Items)
{
    // LINQ Count는 조건별 집계 의도를 직접 표현하고 변경 가능한 카운터를 줄입니다.
    public int ApprovedCount => Items.Count(x => x.Decision == VendorDecision.Approve);
    public int ReviewCount => Items.Count(x => x.Decision == VendorDecision.ManualReview);
    public int RejectedCount => Items.Count(x => x.Decision == VendorDecision.Reject);
}

// 예상 가능한 업무 실패는 Result로 돌려 호출자가 분기하게 하고, DB 단절이나 버그 같은 뜻밖의 장애는 예외로 전파합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IVendorRepository
{
    Task<IReadOnlyList<VendorApplication>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(VendorReview review, CancellationToken cancellationToken);
}

// Strategy는 자주 바뀌는 심사 규칙을 계약 뒤에 숨겨 정책 추가 시 처리 흐름을 수정하지 않게 합니다(OCP).
interface IVendorRiskPolicy { VendorReview Decide(VendorApplication application); }
interface IAuditLog { void Reviewed(VendorReview review); }

// Application Service는 조회→판단→저장 순서만 맡고 규칙과 저장 세부사항은 협력 객체에 위임합니다(SRP).
sealed class ReviewVendorsService(IVendorRepository repository, IVendorRiskPolicy policy, IAuditLog auditLog)
{
    public async Task<Result<ReviewSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pending = await repository.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
            return Result<ReviewSummary>.Failure("심사할 공급업체 신청이 없습니다.");

        var reviews = new List<VendorReview>();
        // 명시적 정렬은 저장소 반환 순서가 달라도 실행과 테스트 결과를 재현 가능하게 합니다.
        foreach (var application in pending.OrderBy(x => x.RequestId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var review = policy.Decide(application);
            var saved = await repository.SaveAsync(review, cancellationToken);
            if (!saved.IsSuccess)
                return Result<ReviewSummary>.Failure($"{application.RequestId} 저장 실패: {saved.Error}");
            reviews.Add(review);
            auditLog.Reviewed(review);
        }
        return Result<ReviewSummary>.Success(new(reviews.AsReadOnly()));
    }
}

sealed class StandardVendorRiskPolicy : IVendorRiskPolicy
{
    private static readonly HashSet<string> SupportedCountries = new(StringComparer.OrdinalIgnoreCase) { "KR", "US", "JP" };

    public VendorReview Decide(VendorApplication application)
    {
        // 시스템 경계에서 필수값과 범위를 검증하면 잘못된 데이터가 핵심 규칙과 저장소로 퍼지지 않습니다.
        if (string.IsNullOrWhiteSpace(application.RequestId) || string.IsNullOrWhiteSpace(application.CompanyName) || application.AnnualContractAmount < 0 || application.ExpectedVersion < 1)
            return new(application.RequestId, VendorDecision.Reject, "신청 데이터가 올바르지 않습니다.", application.ExpectedVersion);
        if (!SupportedCountries.Contains(application.CountryCode))
            return new(application.RequestId, VendorDecision.Reject, "지원하지 않는 국가입니다.", application.ExpectedVersion);

        var hasEmail = !string.IsNullOrWhiteSpace(application.ContactEmail);
        // 튜플 switch 식은 여러 조건과 결과를 한눈에 보여 주며, 위쪽 규칙이 먼저 적용되어 우선순위도 분명합니다.
        return (application.TaxIdVerified, application.AnnualContractAmount, hasEmail) switch
        {
            (false, _, _) => new(application.RequestId, VendorDecision.Reject, "사업자 번호 확인이 필요합니다.", application.ExpectedVersion),
            (true, >= 50_000_000m, _) => new(application.RequestId, VendorDecision.ManualReview, "고액 계약은 담당자 검토가 필요합니다.", application.ExpectedVersion),
            (true, _, false) => new(application.RequestId, VendorDecision.ManualReview, "연락 이메일을 확인해야 합니다.", application.ExpectedVersion),
            _ => new(application.RequestId, VendorDecision.Approve, "기본 등록 조건을 충족했습니다.", application.ExpectedVersion)
        };
    }
}

sealed class InMemoryVendorRepository(IEnumerable<VendorApplication> seed) : IVendorRepository
{
    private readonly List<VendorApplication> _pending = [.. seed];
    private readonly Dictionary<string, VendorReview> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<VendorApplication>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본은 호출자가 저장소 내부 목록을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<VendorApplication>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveAsync(VendorReview review, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 결과의 재저장은 성공 처리하여 재시도가 중복 등록을 만들지 않게 합니다(멱등성).
        if (_saved.TryGetValue(review.RequestId, out var existing))
            return Task.FromResult(existing == review ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 심사 결과가 저장되었습니다."));
        _saved.Add(review.RequestId, review);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class PrivacySafeAuditLog : IAuditLog
{
    public void Reviewed(VendorReview review)
    {
        // 회사명과 이메일 원문은 빼고 요청 ID·결정·버전만 기록하여 개인정보와 거래정보 노출을 줄입니다.
        Console.WriteLine($"[audit] request={review.RequestId} decision={review.Decision} version={review.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var policy = new StandardVendorRiskPolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("기본 조건은 승인", () => AssertDecisionAsync(policy, new("T-1", "테스트", "KR", 1_000_000m, true, "a@b.example", 1), VendorDecision.Approve)),
            ("미확인 사업자는 거절", () => AssertDecisionAsync(policy, new("T-2", "테스트", "KR", 1_000_000m, false, null, 1), VendorDecision.Reject)),
            ("고액 계약은 검토", () => AssertDecisionAsync(policy, new("T-3", "테스트", "US", 50_000_000m, true, "a@b.example", 1), VendorDecision.ManualReview)),
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

    private static Task AssertDecisionAsync(IVendorRiskPolicy policy, VendorApplication application, VendorDecision expected)
    {
        var actual = policy.Decide(application).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var repository = new InMemoryVendorRepository([new("T-4", "테스트", "JP", 1m, true, "a@b.example", 1)]);
        var result = await new ReviewVendorsService(repository, new StandardVendorRiskPolicy(), new SilentLog()).ExecuteAsync(CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 후보가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Reviewed(VendorReview review) { }
    }
}
