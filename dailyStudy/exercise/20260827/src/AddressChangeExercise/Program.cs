// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var now = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryOrderRepository([
    new("ORDER-101", "서울시 중구 새길 10", OrderStatus.Paid, now.AddMinutes(-30), 1),
    new("ORDER-102", "부산시 해운대구 바다로 20", OrderStatus.Packing, now.AddMinutes(-20), 2),
    new("ORDER-103", "", OrderStatus.Paid, now.AddMinutes(-10), 1),
    new("ORDER-104", "대전시 서구 과학로 30", OrderStatus.Shipped, now.AddMinutes(-5), 4)
]);

// Composition Root에서 구현을 한 번만 조립하면 업무 코드는 저장 기술과 분리되고 테스트 대역으로 바꾸기 쉽습니다(DI/DIP).
IAddressChangePolicy policy = new StatusBasedAddressChangePolicy();
var service = new ChangeDeliveryAddressService(repository, policy, new ConsoleAuditLog());

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

// 성공을 확인한 뒤 Value가 null이 아님을 null-forgiving 연산자(!)로 컴파일러에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"승인 {summary.ApprovedCount}건, 수동 검토 {summary.ReviewCount}건, 거절 {summary.RejectedCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.OrderId}: {item.Decision} ({item.Reason})");

// enum은 가능한 상태와 결정을 제한하여 문자열 오타나 정의되지 않은 상태를 막습니다.
enum OrderStatus { Paid, Packing, Shipped, Cancelled }
enum AddressChangeDecision { Approved, ManualReview, Rejected }

// record는 값 중심의 불변 데이터에 적합하여 처리 도중 요청이 뜻밖에 바뀌는 위험을 줄입니다.
sealed record AddressChangeRequest(string OrderId, string NewAddress, OrderStatus Status, DateTimeOffset RequestedAt, int ExpectedVersion);
sealed record AddressChangePlan(string OrderId, string NewAddress, AddressChangeDecision Decision, string Reason, int ExpectedVersion);
sealed record AddressChangeSummary(IReadOnlyList<AddressChangePlan> Items)
{
    // LINQ Count는 조건별 집계라는 의도를 반복문보다 직접적으로 표현합니다.
    public int ApprovedCount => Items.Count(x => x.Decision == AddressChangeDecision.Approved);
    public int ReviewCount => Items.Count(x => x.Decision == AddressChangeDecision.ManualReview);
    public int RejectedCount => Items.Count(x => x.Decision == AddressChangeDecision.Rejected);
}

// 예상 가능한 입력 오류와 업무 거절은 Result로 반환하고, DB 단절이나 버그 같은 예기치 못한 장애는 예외로 남깁니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IOrderRepository
{
    Task<IReadOnlyList<AddressChangeRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(AddressChangePlan plan, CancellationToken cancellationToken);
}

// Strategy 계약은 승인 기준을 숨겨 정책이 바뀌어도 Application Service를 수정하지 않게 합니다(OCP).
interface IAddressChangePolicy { AddressChangePlan Decide(AddressChangeRequest request, DateTimeOffset now); }
interface IAuditLog { void Saved(AddressChangePlan plan); }

// Application Service는 조회→판단→저장의 사용 사례 흐름만 맡고 세부 규칙은 협력 객체에 위임합니다(SRP).
sealed class ChangeDeliveryAddressService(IOrderRepository repository, IAddressChangePolicy policy, IAuditLog auditLog)
{
    public async Task<Result<AddressChangeSummary>> ExecuteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await repository.GetPendingAsync(cancellationToken);
        if (requests.Count == 0)
            return Result<AddressChangeSummary>.Failure("처리할 주소 변경 요청이 없습니다.");

        var plans = new List<AddressChangePlan>();
        // 명시적 정렬은 저장소 반환 순서가 달라도 실행과 테스트 결과를 일정하게 만듭니다.
        foreach (var request in requests.OrderBy(x => x.RequestedAt).ThenBy(x => x.OrderId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(request, now);
            var saved = await repository.SaveAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<AddressChangeSummary>.Failure($"{request.OrderId} 저장 실패: {saved.Error}");

            plans.Add(plan);
            auditLog.Saved(plan);
        }
        return Result<AddressChangeSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class StatusBasedAddressChangePolicy : IAddressChangePolicy
{
    public AddressChangePlan Decide(AddressChangeRequest request, DateTimeOffset now)
    {
        // 경계에서 필수값과 버전을 검증하면 잘못된 데이터가 핵심 로직과 저장소로 퍼지지 않습니다.
        if (string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.NewAddress))
            return new(request.OrderId, request.NewAddress, AddressChangeDecision.Rejected, "주문 ID와 새 주소가 필요합니다.", request.ExpectedVersion);
        if (request.ExpectedVersion < 1 || request.RequestedAt > now.AddMinutes(1))
            return new(request.OrderId, request.NewAddress, AddressChangeDecision.Rejected, "버전 또는 요청 시각이 올바르지 않습니다.", request.ExpectedVersion);

        return request.Status switch
        {
            OrderStatus.Paid => new(request.OrderId, request.NewAddress, AddressChangeDecision.Approved, "포장 전이라 즉시 변경할 수 있습니다.", request.ExpectedVersion),
            OrderStatus.Packing => new(request.OrderId, request.NewAddress, AddressChangeDecision.ManualReview, "포장 진행 여부를 작업자에게 확인해야 합니다.", request.ExpectedVersion),
            _ => new(request.OrderId, request.NewAddress, AddressChangeDecision.Rejected, "발송 또는 취소된 주문은 주소를 변경할 수 없습니다.", request.ExpectedVersion)
        };
    }
}

sealed class InMemoryOrderRepository(IEnumerable<AddressChangeRequest> seed) : IOrderRepository
{
    private readonly List<AddressChangeRequest> _pending = [.. seed];
    private readonly Dictionary<string, AddressChangePlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<AddressChangeRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 호출자가 저장소 내부 목록을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<AddressChangeRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveAsync(AddressChangePlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 동일 결과의 재저장은 성공으로 보아 재시도가 주소 변경을 두 번 만들지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.OrderId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 변경 결과가 저장되었습니다."));
        _saved.Add(plan.OrderId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void Saved(AddressChangePlan plan)
    {
        // 주소는 개인정보이므로 감사 로그에는 주문 ID·결정·버전만 남기고 실제 주소는 기록하지 않습니다.
        Console.WriteLine($"[audit] order={plan.OrderId} decision={plan.Decision} version={plan.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new StatusBasedAddressChangePolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("결제 완료 주문은 승인", () => AssertDecisionAsync(policy, new("T-1", "서울", OrderStatus.Paid, now, 1), now, AddressChangeDecision.Approved)),
            ("포장 중 주문은 검토", () => AssertDecisionAsync(policy, new("T-2", "부산", OrderStatus.Packing, now, 1), now, AddressChangeDecision.ManualReview)),
            ("발송 주문은 거절", () => AssertDecisionAsync(policy, new("T-3", "대전", OrderStatus.Shipped, now, 1), now, AddressChangeDecision.Rejected)),
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

    private static Task AssertDecisionAsync(IAddressChangePolicy policy, AddressChangeRequest request, DateTimeOffset now, AddressChangeDecision expected)
    {
        var actual = policy.Decide(request, now).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryOrderRepository([new("T-4", "광주", OrderStatus.Paid, now, 1)]);
        var result = await new ChangeDeliveryAddressService(repository, new StatusBasedAddressChangePolicy(), new SilentLog()).ExecuteAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 요청이 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Saved(AddressChangePlan plan) { }
    }
}
