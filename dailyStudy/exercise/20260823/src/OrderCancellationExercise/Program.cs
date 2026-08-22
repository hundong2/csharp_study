// 읽는 순서: 조립(Composition Root) → 실행 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var now = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryCancellationRepository([
    new("C-101", "ORDER-1", 45_000m, OrderState.Paid, now.AddMinutes(-20)),
    new("C-102", "ORDER-2", 19_000m, OrderState.Shipped, now.AddMinutes(-10)),
    new("C-103", "ORDER-3", 0m, OrderState.Paid, now.AddMinutes(-5)),
    new("C-104", "ORDER-4", 82_000m, OrderState.Preparing, now)
]);

// Composition Root 한 곳에서 구현을 조립하면 업무 코드는 구체 클래스가 아닌 계약에 의존해 교체와 테스트가 쉽습니다(DI/DIP).
ICancellationPolicy policy = new StandardCancellationPolicy(50_000m);
var service = new PlanCancellationsService(repository, policy, new ConsoleOperationsLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.PlanAsync(now, CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"처리 실패: {result.Error}");
    return;
}

// Result 성공을 확인했으므로 Value가 null이 아님을 !로 컴파일러에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"즉시 취소 {summary.ImmediateCount}건, 수동 검토 {summary.ReviewCount}건, 거절 {summary.RejectCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.RequestId}: {item.Decision} / {item.Compensation} ({item.Reason})");

// enum은 가능한 상태를 제한하여 임의 문자열 오타와 불가능한 상태를 줄입니다.
enum OrderState { Paid, Preparing, Shipped }
enum CancellationDecision { CancelImmediately, ManualReview, Reject }
enum CompensationKind { RefundOnly, RefundAndRestock, None }

// record는 값으로 비교되는 불변 데이터에 적합합니다. decimal은 금액의 10진 계산 오차를 피하려고 사용합니다.
sealed record CancellationRequest(string Id, string OrderId, decimal PaidAmount, OrderState State, DateTimeOffset RequestedAt);
sealed record CancellationPlan(string RequestId, CancellationDecision Decision, CompensationKind Compensation, string Reason);
sealed record CancellationSummary(IReadOnlyList<CancellationPlan> Items)
{
    // LINQ Count는 조건에 맞는 개수를 센다는 의도를 반복문보다 직접 표현합니다.
    public int ImmediateCount => Items.Count(x => x.Decision == CancellationDecision.CancelImmediately);
    public int ReviewCount => Items.Count(x => x.Decision == CancellationDecision.ManualReview);
    public int RejectCount => Items.Count(x => x.Decision == CancellationDecision.Reject);
}

// 예상 가능한 업무 실패는 Result로 반환하고, DB 장애·취소·코드 결함은 예외로 남겨 운영 장애 신호를 보존합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface ICancellationRepository
{
    Task<IReadOnlyList<CancellationRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SavePlanAsync(CancellationPlan plan, CancellationToken cancellationToken);
}

// Strategy는 취소 정책을 계약 뒤에 숨겨 프로모션·국가별 정책을 Application Service 수정 없이 추가하게 합니다(OCP).
interface ICancellationPolicy
{
    CancellationPlan Decide(CancellationRequest request, DateTimeOffset now);
}

interface IOperationsLog { void PlanSaved(CancellationPlan plan); }

// Application Service는 조회→판단→저장이라는 사용 사례 흐름만 조정하고 규칙과 저장 기술은 협력 객체에 맡깁니다(SRP).
sealed class PlanCancellationsService(ICancellationRepository repository, ICancellationPolicy policy, IOperationsLog log)
{
    public async Task<Result<CancellationSummary>> PlanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await repository.GetPendingAsync(cancellationToken);
        if (requests.Count == 0)
            return Result<CancellationSummary>.Failure("처리할 취소 요청이 없습니다.");

        var plans = new List<CancellationPlan>();
        // 정렬 기준을 명시하면 저장 순서와 테스트 결과가 실행 환경에 따라 흔들리지 않습니다.
        foreach (var request in requests.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(request, now);
            var saved = await repository.SavePlanAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<CancellationSummary>.Failure($"{request.Id} 저장 실패: {saved.Error}");

            plans.Add(plan);
            log.PlanSaved(plan);
        }

        return Result<CancellationSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class StandardCancellationPolicy(decimal highValueThreshold) : ICancellationPolicy
{
    public CancellationPlan Decide(CancellationRequest request, DateTimeOffset now)
    {
        // 시스템 경계에서 잘못된 식별자·금액·미래 시각을 차단해 잘못된 상태가 안쪽으로 퍼지지 않게 합니다.
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.OrderId) || request.PaidAmount <= 0)
            return new(request.Id, CancellationDecision.Reject, CompensationKind.None, "필수 입력과 결제 금액을 확인해야 합니다.");
        if (request.RequestedAt > now.AddMinutes(1))
            return new(request.Id, CancellationDecision.Reject, CompensationKind.None, "서버 시간보다 미래인 요청입니다.");
        if (request.State == OrderState.Shipped)
            return new(request.Id, CancellationDecision.Reject, CompensationKind.None, "출고 후에는 반품 절차가 필요합니다.");
        if (request.PaidAmount >= highValueThreshold)
            return new(request.Id, CancellationDecision.ManualReview, CompensationKind.RefundAndRestock, "고액 주문은 담당자가 검토합니다.");

        var compensation = request.State == OrderState.Preparing ? CompensationKind.RefundAndRestock : CompensationKind.RefundOnly;
        return new(request.Id, CancellationDecision.CancelImmediately, compensation, "자동 취소 조건을 충족했습니다.");
    }
}

sealed class InMemoryCancellationRepository(IEnumerable<CancellationRequest> seed) : ICancellationRepository
{
    private readonly List<CancellationRequest> _pending = [.. seed];
    private readonly Dictionary<string, CancellationPlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<CancellationRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 복사본을 반환하여 호출자가 저장소 내부 컬렉션을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<CancellationRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SavePlanAsync(CancellationPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 요청의 같은 결과 재저장은 성공으로 보아 재시도에도 중복 환불 작업이 생기지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.RequestId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 계획이 저장되었습니다."));
        _saved.Add(plan.RequestId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleOperationsLog : IOperationsLog
{
    public void PlanSaved(CancellationPlan plan)
    {
        // 운영 로그에는 추적용 요청 ID와 결정만 남기고 결제수단·개인정보는 남기지 않습니다.
        Console.WriteLine($"[operations] request={plan.RequestId} decision={plan.Decision}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new StandardCancellationPolicy(50_000m);
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("결제 완료 소액 주문은 즉시 취소", () => AssertDecisionAsync(policy, new("T-1", "O-1", 10_000m, OrderState.Paid, now), now, CancellationDecision.CancelImmediately)),
            ("고액 주문은 수동 검토", () => AssertDecisionAsync(policy, new("T-2", "O-2", 50_000m, OrderState.Preparing, now), now, CancellationDecision.ManualReview)),
            ("출고 주문은 취소 거절", () => AssertDecisionAsync(policy, new("T-3", "O-3", 10_000m, OrderState.Shipped, now), now, CancellationDecision.Reject)),
            ("서비스는 전체 요청 처리", ServiceProcessesAllAsync)
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

    private static Task AssertDecisionAsync(ICancellationPolicy policy, CancellationRequest request, DateTimeOffset now, CancellationDecision expected)
    {
        var actual = policy.Decide(request, now).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryCancellationRepository([new("T-4", "O-4", 10_000m, OrderState.Paid, now)]);
        var result = await new PlanCancellationsService(repository, new StandardCancellationPolicy(50_000m), new SilentLog()).PlanAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 요청이 처리되어야 합니다.");
    }

    private sealed class SilentLog : IOperationsLog
    {
        // 테스트는 반환값에 집중하므로 콘솔 부작용이 없는 대역을 사용합니다.
        public void PlanSaved(CancellationPlan plan) { }
    }
}
