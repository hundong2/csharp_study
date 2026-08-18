// 이 파일은 작은 콘솔 앱 하나로 문법부터 실무 구조까지 따라가도록 구성했습니다.
// 위에서 아래로 읽으면 조립(Composition Root) → 실행 → 업무 모델 → 저장소와 정책 순서가 보입니다.

var repository = new InMemoryLeaveRequestRepository(
[
    new("LEAVE-100", "EMP-01", LeaveType.Annual, 3, 8, null),
    new("LEAVE-101", "EMP-02", LeaveType.Sick, 1, 2, "진료 예정"),
    new("LEAVE-102", "EMP-03", LeaveType.Annual, 5, 8, "가족 여행"),
    new("LEAVE-103", "EMP-04", LeaveType.Annual, 1, 0, null)
]);

// 구체 클래스는 시작 지점에서만 조립합니다. 업무 서비스는 인터페이스에 의존하므로 테스트 대역으로 바꾸기 쉽습니다.
ILeaveApprovalPolicy policy = new BalancedLeaveApprovalPolicy();
var service = new ReviewLeaveRequestsService(repository, policy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var summary = await service.ReviewPendingAsync(CancellationToken.None);
Console.WriteLine($"승인 {summary.ApprovedCount}건, 거절 {summary.RejectedCount}건, 관리자 검토 {summary.ManualReviewCount}건");
foreach (var item in summary.Results)
{
    Console.WriteLine($"- {item.RequestId}: {item.Decision} ({item.Reason})");
}

// enum은 허용되는 값의 목록을 이름으로 제한해 잘못된 문자열 입력을 줄입니다.
enum LeaveType { Annual, Sick }
enum ApprovalDecision { Approved, Rejected, ManualReview }

// record는 값 중심 데이터를 간결하게 표현합니다. init 전용 값이라 처리 도중 원본 요청이 몰래 바뀌지 않습니다.
sealed record LeaveRequest(
    string Id,
    string EmployeeId,
    LeaveType Type,
    int RequestedDays,
    int RemainingDays,
    string? Note);

sealed record ReviewResult(string RequestId, ApprovalDecision Decision, string Reason);
sealed record ReviewSummary(IReadOnlyList<ReviewResult> Results)
{
    // 식 본문 속성은 단순 계산을 짧게 표현합니다. LINQ Count의 조건은 각 결과를 한 번씩 검사합니다.
    public int ApprovedCount => Results.Count(x => x.Decision == ApprovalDecision.Approved);
    public int RejectedCount => Results.Count(x => x.Decision == ApprovalDecision.Rejected);
    public int ManualReviewCount => Results.Count(x => x.Decision == ApprovalDecision.ManualReview);
}

// 예상 가능한 업무 실패는 예외 대신 Result로 돌려 호출자가 분기를 빠뜨리지 않게 합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface ILeaveRequestRepository
{
    Task<IReadOnlyList<LeaveRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveDecisionAsync(ReviewResult result, CancellationToken cancellationToken);
}

// Strategy 계약 덕분에 보수적 정책, 팀별 정책 등을 서비스 수정 없이 추가할 수 있습니다(OCP).
interface ILeaveApprovalPolicy
{
    ReviewResult Review(LeaveRequest request);
}

interface IAuditLog
{
    void DecisionSaved(ReviewResult result);
}

// Application Service는 조회→판단→저장의 사용 사례 순서를 담당하고 세부 정책은 협력 객체에 맡깁니다(SRP).
sealed class ReviewLeaveRequestsService(
    ILeaveRequestRepository repository,
    ILeaveApprovalPolicy policy,
    IAuditLog auditLog)
{
    public async Task<ReviewSummary> ReviewPendingAsync(CancellationToken cancellationToken)
    {
        var requests = await repository.GetPendingAsync(cancellationToken);
        var results = new List<ReviewResult>();

        // OrderBy로 처리 순서를 고정하면 실행마다 로그와 테스트 결과가 달라지는 일을 줄일 수 있습니다.
        foreach (var request in requests.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = policy.Review(request);
            var saved = await repository.SaveDecisionAsync(result, cancellationToken);

            // 저장 실패는 정상 업무 결과가 아니므로 조용히 삼키지 않고 운영자가 발견할 예외로 바꿉니다.
            if (!saved.IsSuccess)
            {
                throw new InvalidOperationException($"{request.Id} 저장 실패: {saved.Error}");
            }

            results.Add(result);
            auditLog.DecisionSaved(result);
        }

        return new ReviewSummary(results.AsReadOnly());
    }
}

sealed class BalancedLeaveApprovalPolicy : ILeaveApprovalPolicy
{
    public ReviewResult Review(LeaveRequest request)
    {
        // 먼저 입력 불변 조건을 검사하면 뒤 정책은 유효한 값만 다룰 수 있습니다.
        if (string.IsNullOrWhiteSpace(request.Id) || request.RequestedDays <= 0)
            return new(request.Id, ApprovalDecision.Rejected, "요청 값이 올바르지 않습니다.");

        if (request.RequestedDays > request.RemainingDays)
            return new(request.Id, ApprovalDecision.Rejected, "남은 휴가가 부족합니다.");

        // 병가는 짧은 사유가 없더라도 자동 거절하지 않고 사람의 판단으로 넘깁니다.
        if (request.Type == LeaveType.Sick && string.IsNullOrWhiteSpace(request.Note))
            return new(request.Id, ApprovalDecision.ManualReview, "병가 사유를 확인해야 합니다.");

        if (request.RequestedDays >= 5)
            return new(request.Id, ApprovalDecision.ManualReview, "장기 휴가는 관리자 확인이 필요합니다.");

        return new(request.Id, ApprovalDecision.Approved, "자동 승인 기준을 충족했습니다.");
    }
}

sealed class InMemoryLeaveRequestRepository(IEnumerable<LeaveRequest> seed) : ILeaveRequestRepository
{
    private readonly List<LeaveRequest> _pending = [.. seed];
    private readonly Dictionary<string, ReviewResult> _decisions = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<LeaveRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 복사본을 반환해 호출자가 저장소 내부 컬렉션을 직접 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<LeaveRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveDecisionAsync(ReviewResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 동일 요청 재처리는 같은 결과일 때만 성공시켜 단순한 멱등성을 보여 줍니다.
        if (_decisions.TryGetValue(result.RequestId, out var existing))
        {
            return Task.FromResult(existing == result
                ? Result<bool>.Success(true)
                : Result<bool>.Failure("이미 다른 결정이 저장되었습니다."));
        }

        _decisions.Add(result.RequestId, result);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void DecisionSaved(ReviewResult result)
    {
        // 실제 로그에는 추적용 요청 ID와 결과를 남기되 병가 사유 같은 민감 정보는 남기지 않습니다.
        Console.WriteLine($"[audit] request={result.RequestId} decision={result.Decision}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var policy = new BalancedLeaveApprovalPolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("잔여 일수 안의 짧은 휴가는 승인", () => AssertDecisionAsync(policy, new("T-1", "E", LeaveType.Annual, 2, 3, null), ApprovalDecision.Approved)),
            ("잔여 일수 초과는 거절", () => AssertDecisionAsync(policy, new("T-2", "E", LeaveType.Annual, 4, 3, null), ApprovalDecision.Rejected)),
            ("장기 휴가는 관리자 검토", () => AssertDecisionAsync(policy, new("T-3", "E", LeaveType.Annual, 5, 8, "여행"), ApprovalDecision.ManualReview)),
            ("서비스는 모든 요청을 처리", ServiceProcessesAllAsync)
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

    private static Task AssertDecisionAsync(ILeaveApprovalPolicy policy, LeaveRequest request, ApprovalDecision expected)
    {
        var actual = policy.Review(request).Decision;
        if (actual != expected)
            throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var repository = new InMemoryLeaveRequestRepository([
            new("T-4", "E", LeaveType.Annual, 1, 2, null),
            new("T-5", "E", LeaveType.Annual, 3, 1, null)
        ]);
        var service = new ReviewLeaveRequestsService(repository, new BalancedLeaveApprovalPolicy(), new SilentAuditLog());
        var summary = await service.ReviewPendingAsync(CancellationToken.None);
        if (summary.Results.Count != 2)
            throw new InvalidOperationException("모든 요청이 처리되어야 합니다.");
    }

    private sealed class SilentAuditLog : IAuditLog
    {
        // 테스트에서는 콘솔 출력이 필요 없으므로 아무 일도 하지 않는 대역을 사용합니다.
        public void DecisionSaved(ReviewResult result) { }
    }
}
