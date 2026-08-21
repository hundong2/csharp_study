// 이 파일은 조립 → 실행 → 모델 → 계약 → 구현 → 테스트 순서입니다. 위에서 아래로 읽으면 요청이 처리되는 흐름을 따라갈 수 있습니다.
var now = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryResetRequestRepository(
[
    new("REQ-101", "kim@example.com", true, 1, now.AddMinutes(-30)),
    new("REQ-102", "lee@example.com", true, 5, now.AddMinutes(-10)),
    new("REQ-103", "unknown@example.com", false, 0, now.AddMinutes(-5)),
    new("REQ-104", null, true, 0, now)
]);

// Composition Root에서 구체 구현을 한 번만 조립합니다. 서비스는 인터페이스에 의존하므로 운영 구현이나 테스트 대역으로 교체하기 쉽습니다(DI/DIP).
IResetPolicy policy = new SafeResetPolicy(maxRecentRequests: 3);
var service = new PlanPasswordResetsService(repository, policy, new ConsoleSecurityLog());

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

// 성공 여부를 확인했으므로 null 아님 연산자(!)로 Value가 존재한다는 사실을 컴파일러에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"발송 {summary.SendCount}건, 동일 응답 {summary.GenericCount}건, 차단 {summary.BlockCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.RequestId}: {item.Action} ({item.Reason})");

// enum은 가능한 상태를 제한해 문자열 오타와 잘못된 상태 생성을 막습니다.
enum ResetAction { SendLink, GenericResponse, Block }

// record는 값 중심 불변 데이터에 적합합니다. Email은 입력에서 없을 수 있으므로 string?로 nullable 가능성을 명시합니다.
sealed record ResetRequest(string Id, string? Email, bool AccountExists, int RecentRequestCount, DateTimeOffset RequestedAt);
sealed record ResetPlanItem(string RequestId, ResetAction Action, string Reason);
sealed record ResetSummary(IReadOnlyList<ResetPlanItem> Items)
{
    // LINQ Count는 조건별 개수를 구한다는 의도를 반복문보다 직접 보여 줍니다.
    public int SendCount => Items.Count(x => x.Action == ResetAction.SendLink);
    public int GenericCount => Items.Count(x => x.Action == ResetAction.GenericResponse);
    public int BlockCount => Items.Count(x => x.Action == ResetAction.Block);
}

// 예상 가능한 업무 실패는 Result로 돌려 호출자가 명시적으로 분기하게 합니다. 인프라 장애나 취소는 예외로 유지합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IResetRequestRepository
{
    Task<IReadOnlyList<ResetRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SavePlanAsync(ResetPlanItem item, CancellationToken cancellationToken);
}

// Strategy 계약 덕분에 분류 정책을 추가해도 Application Service를 고치지 않아도 됩니다(OCP).
interface IResetPolicy
{
    ResetPlanItem Decide(ResetRequest request, DateTimeOffset now);
}

interface ISecurityLog
{
    void PlanSaved(ResetPlanItem item);
}

// Application Service는 조회 → 판단 → 저장의 사용 사례 순서만 책임지고, 규칙과 저장 기술은 협력 객체에 위임합니다(SRP).
sealed class PlanPasswordResetsService(IResetRequestRepository repository, IResetPolicy policy, ISecurityLog securityLog)
{
    public async Task<Result<ResetSummary>> PlanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await repository.GetPendingAsync(cancellationToken);
        if (requests.Count == 0)
            return Result<ResetSummary>.Failure("처리할 요청이 없습니다.");

        var plans = new List<ResetPlanItem>();
        // OrderBy로 처리 순서를 고정하면 실행 결과와 테스트가 환경에 따라 흔들리지 않습니다.
        foreach (var request in requests.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = policy.Decide(request, now);
            var saved = await repository.SavePlanAsync(item, cancellationToken);
            if (!saved.IsSuccess)
                return Result<ResetSummary>.Failure($"{request.Id} 저장 실패: {saved.Error}");

            plans.Add(item);
            securityLog.PlanSaved(item);
        }

        return Result<ResetSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class SafeResetPolicy(int maxRecentRequests) : IResetPolicy
{
    public ResetPlanItem Decide(ResetRequest request, DateTimeOffset now)
    {
        // 잘못된 식별자나 이메일은 정상 발송으로 넘기지 않아 경계에서 입력 오류를 차단합니다.
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Email))
            return new(request.Id, ResetAction.Block, "필수 입력을 확인해야 합니다.");

        if (request.RequestedAt > now.AddMinutes(1))
            return new(request.Id, ResetAction.Block, "서버 시간보다 미래인 요청입니다.");

        if (request.RecentRequestCount >= maxRecentRequests)
            return new(request.Id, ResetAction.Block, "짧은 시간의 반복 요청을 제한합니다.");

        // 존재하지 않는 계정에도 외부 응답은 같게 보여 계정 존재 여부가 노출되는 계정 열거 공격을 줄입니다.
        if (!request.AccountExists)
            return new(request.Id, ResetAction.GenericResponse, "계정 존재 여부를 공개하지 않습니다.");

        return new(request.Id, ResetAction.SendLink, "일회용 재설정 링크를 발송합니다.");
    }
}

sealed class InMemoryResetRequestRepository(IEnumerable<ResetRequest> seed) : IResetRequestRepository
{
    private readonly List<ResetRequest> _pending = [.. seed];
    private readonly Dictionary<string, ResetPlanItem> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ResetRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 호출자가 저장소 내부 상태를 우연히 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ResetRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SavePlanAsync(ResetPlanItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 동일 계획 재저장은 성공으로 처리해 재시도 시 중복 발송 작업이 만들어지지 않게 합니다(멱등성).
        if (_saved.TryGetValue(item.RequestId, out var existing))
            return Task.FromResult(existing == item
                ? Result<bool>.Success(true)
                : Result<bool>.Failure("이미 다른 계획이 저장되었습니다."));

        _saved.Add(item.RequestId, item);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleSecurityLog : ISecurityLog
{
    public void PlanSaved(ResetPlanItem item)
    {
        // 로그에는 요청 ID와 결정만 남깁니다. 이메일, 재설정 토큰, 링크는 개인정보·인증정보이므로 기록하지 않습니다.
        Console.WriteLine($"[security] request={item.RequestId} action={item.Action}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new SafeResetPolicy(3);
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("정상 계정은 링크 발송", () => AssertActionAsync(policy, new("T-1", "a@example.com", true, 0, now), now, ResetAction.SendLink)),
            ("없는 계정은 동일 응답", () => AssertActionAsync(policy, new("T-2", "b@example.com", false, 0, now), now, ResetAction.GenericResponse)),
            ("반복 요청은 차단", () => AssertActionAsync(policy, new("T-3", "c@example.com", true, 3, now), now, ResetAction.Block)),
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

    private static Task AssertActionAsync(IResetPolicy policy, ResetRequest request, DateTimeOffset now, ResetAction expected)
    {
        var actual = policy.Decide(request, now).Action;
        if (actual != expected)
            throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryResetRequestRepository([
            new("T-4", "a@example.com", true, 0, now),
            new("T-5", "b@example.com", false, 0, now)
        ]);
        var service = new PlanPasswordResetsService(repository, new SafeResetPolicy(3), new SilentLog());
        var result = await service.PlanAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 2)
            throw new InvalidOperationException("모든 요청이 처리되어야 합니다.");
    }

    private sealed class SilentLog : ISecurityLog
    {
        // 테스트는 반환값에 집중하므로 콘솔 부작용이 없는 대역을 사용합니다.
        public void PlanSaved(ResetPlanItem item) { }
    }
}
