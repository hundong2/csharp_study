// 읽는 순서: 조립(Composition Root) → 실행 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryExportRepository([
    new("E-101", "USER-1", ExportFormat.Json, true, now.AddMinutes(-30)),
    new("E-102", "USER-2", ExportFormat.Csv, false, now.AddMinutes(-20)),
    new("E-103", "", ExportFormat.Json, true, now.AddMinutes(-10)),
    new("E-104", "USER-4", ExportFormat.Json, true, now.AddDays(-8))
]);

// Composition Root에서 구현을 조립하면 업무 코드는 인터페이스에 의존하여 저장소와 정책을 쉽게 교체할 수 있습니다(DI/DIP).
IExportPolicy policy = new StandardExportPolicy(TimeSpan.FromDays(7));
var service = new PlanExportsService(repository, policy, new ConsoleOperationsLog());

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

// 성공을 확인했으므로 Value가 null이 아님을 null-forgiving 연산자(!)로 컴파일러에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"생성 {summary.GenerateCount}건, 본인 확인 {summary.VerifyCount}건, 거절 {summary.RejectCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.RequestId}: {item.Decision} / {item.RetentionDays}일 ({item.Reason})");

// enum은 가능한 값의 범위를 제한해 문자열 오타와 불가능한 상태를 줄입니다.
enum ExportFormat { Json, Csv }
enum ExportDecision { Generate, RequireVerification, Reject }

// record는 값 중심의 불변 데이터에 적합하고, DateTimeOffset은 시간대가 포함된 시각을 보존합니다.
sealed record ExportRequest(string Id, string UserId, ExportFormat Format, bool IsIdentityVerified, DateTimeOffset RequestedAt);
sealed record ExportPlan(string RequestId, ExportDecision Decision, int RetentionDays, string Reason);
sealed record ExportSummary(IReadOnlyList<ExportPlan> Items)
{
    // LINQ Count는 조건에 맞는 항목 수를 센다는 의도를 반복문보다 직접 표현합니다.
    public int GenerateCount => Items.Count(x => x.Decision == ExportDecision.Generate);
    public int VerifyCount => Items.Count(x => x.Decision == ExportDecision.RequireVerification);
    public int RejectCount => Items.Count(x => x.Decision == ExportDecision.Reject);
}

// 예상 가능한 입력·중복 실패는 Result로 표현하고, DB 장애나 코드 결함은 예외로 남겨 운영 장애를 숨기지 않습니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IExportRepository
{
    Task<IReadOnlyList<ExportRequest>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SavePlanAsync(ExportPlan plan, CancellationToken cancellationToken);
}

// Strategy는 정책을 계약 뒤에 숨겨 국가별 보존기간 정책을 서비스 수정 없이 추가하게 합니다(OCP).
interface IExportPolicy { ExportPlan Decide(ExportRequest request, DateTimeOffset now); }
interface IOperationsLog { void PlanSaved(ExportPlan plan); }

// Application Service는 조회→판단→저장 순서만 조정하고 규칙과 저장 기술은 협력 객체에 맡깁니다(SRP).
sealed class PlanExportsService(IExportRepository repository, IExportPolicy policy, IOperationsLog log)
{
    public async Task<Result<ExportSummary>> PlanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await repository.GetPendingAsync(cancellationToken);
        if (requests.Count == 0)
            return Result<ExportSummary>.Failure("처리할 내보내기 요청이 없습니다.");

        var plans = new List<ExportPlan>();
        // 정렬을 명시하면 DB 반환 순서가 달라도 처리와 테스트 결과가 일정합니다.
        foreach (var request in requests.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = policy.Decide(request, now);
            var saved = await repository.SavePlanAsync(plan, cancellationToken);
            if (!saved.IsSuccess)
                return Result<ExportSummary>.Failure($"{request.Id} 저장 실패: {saved.Error}");

            plans.Add(plan);
            log.PlanSaved(plan);
        }

        return Result<ExportSummary>.Success(new(plans.AsReadOnly()));
    }
}

sealed class StandardExportPolicy(TimeSpan maximumRequestAge) : IExportPolicy
{
    public ExportPlan Decide(ExportRequest request, DateTimeOffset now)
    {
        // 경계에서 잘못된 식별자와 시각을 거절해 유효하지 않은 상태가 안쪽으로 퍼지지 않게 합니다.
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.UserId))
            return new(request.Id, ExportDecision.Reject, 0, "요청 ID와 사용자 ID가 필요합니다.");
        if (request.RequestedAt > now.AddMinutes(1))
            return new(request.Id, ExportDecision.Reject, 0, "서버 시간보다 미래인 요청입니다.");
        if (now - request.RequestedAt > maximumRequestAge)
            return new(request.Id, ExportDecision.Reject, 0, "처리 가능 기간이 지난 요청입니다.");
        if (!request.IsIdentityVerified)
            return new(request.Id, ExportDecision.RequireVerification, 0, "개인정보 제공 전 본인 확인이 필요합니다.");

        var retentionDays = request.Format == ExportFormat.Csv ? 1 : 3;
        return new(request.Id, ExportDecision.Generate, retentionDays, "암호화된 다운로드 파일을 생성합니다.");
    }
}

sealed class InMemoryExportRepository(IEnumerable<ExportRequest> seed) : IExportRepository
{
    private readonly List<ExportRequest> _pending = [.. seed];
    private readonly Dictionary<string, ExportPlan> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ExportRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 호출자가 저장소 내부 컬렉션을 우연히 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ExportRequest>>(_pending.ToArray());
    }

    public Task<Result<bool>> SavePlanAsync(ExportPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 동일 결과의 재저장은 성공으로 보아 재시도에도 파일 생성 작업이 중복되지 않게 합니다(멱등성).
        if (_saved.TryGetValue(plan.RequestId, out var existing))
            return Task.FromResult(existing == plan ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 계획이 저장되었습니다."));
        _saved.Add(plan.RequestId, plan);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleOperationsLog : IOperationsLog
{
    public void PlanSaved(ExportPlan plan)
    {
        // 운영 로그에는 요청 ID와 결정만 남기고 사용자 ID나 내보낸 개인정보는 남기지 않습니다.
        Console.WriteLine($"[operations] request={plan.RequestId} decision={plan.Decision}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new StandardExportPolicy(TimeSpan.FromDays(7));
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("본인 확인 요청은 생성", () => AssertDecisionAsync(policy, new("T-1", "U-1", ExportFormat.Json, true, now), now, ExportDecision.Generate)),
            ("미확인 요청은 본인 확인", () => AssertDecisionAsync(policy, new("T-2", "U-2", ExportFormat.Csv, false, now), now, ExportDecision.RequireVerification)),
            ("오래된 요청은 거절", () => AssertDecisionAsync(policy, new("T-3", "U-3", ExportFormat.Json, true, now.AddDays(-8)), now, ExportDecision.Reject)),
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

    private static Task AssertDecisionAsync(IExportPolicy policy, ExportRequest request, DateTimeOffset now, ExportDecision expected)
    {
        var actual = policy.Decide(request, now).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryExportRepository([new("T-4", "U-4", ExportFormat.Json, true, now)]);
        var result = await new PlanExportsService(repository, new StandardExportPolicy(TimeSpan.FromDays(7)), new SilentLog()).PlanAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 요청이 처리되어야 합니다.");
    }

    private sealed class SilentLog : IOperationsLog
    {
        // 테스트는 반환값에 집중하므로 콘솔 부작용이 없는 대역을 사용합니다.
        public void PlanSaved(ExportPlan plan) { }
    }
}
