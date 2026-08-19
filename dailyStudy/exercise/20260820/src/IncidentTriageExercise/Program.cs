// 이 파일은 작은 콘솔 앱 하나에서 문법부터 실무 구조까지 위에서 아래로 따라가도록 구성했습니다.
// 먼저 조립(Composition Root), 실행 흐름, 도메인 모델, 계약, 구현, 자체 테스트 순서로 읽으세요.

var repository = new InMemoryIncidentRepository(
[
    new("INC-101", "결제 승인 실패", IncidentSeverity.Critical, 240, true, "payments"),
    new("INC-102", "검색 응답 지연", IncidentSeverity.High, 35, false, null),
    new("INC-103", "관리자 화면 오탈자", IncidentSeverity.Low, 2, false, "admin"),
    new("INC-104", "로그인 간헐 실패", IncidentSeverity.High, 120, true, "identity")
]);

// 구체 구현은 시작 지점에서 한 번만 조립합니다. 서비스가 인터페이스에 의존하면 테스트 대역으로 교체하기 쉽습니다(DI/DIP).
IIncidentPriorityPolicy policy = new ImpactBasedPriorityPolicy();
var service = new TriageIncidentsService(repository, policy, new ConsoleOperationsLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.TriageOpenIncidentsAsync(CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"분류 실패: {result.Error}");
    return;
}

var summary = result.Value!; // 성공 여부를 확인했으므로 Value가 null이 아님을 컴파일러에 알려 줍니다.
Console.WriteLine($"즉시 대응 {summary.ImmediateCount}건, 신속 대응 {summary.UrgentCount}건, 일반 {summary.NormalCount}건");
foreach (var assignment in summary.Assignments)
    Console.WriteLine($"- {assignment.IncidentId}: {assignment.Priority} / {assignment.Team} ({assignment.Reason})");

// enum은 허용되는 값을 이름으로 제한해 "high" 같은 임의 문자열의 오타를 막습니다.
enum IncidentSeverity { Low, Medium, High, Critical }
enum ResponsePriority { Normal, Urgent, Immediate }

// record는 값 중심 데이터에 적합합니다. init 전용 값이라 처리 도중 원본 사고 정보가 몰래 바뀌지 않습니다.
sealed record Incident(string Id, string Title, IncidentSeverity Severity, int AffectedUsers, bool RevenueBlocked, string? ServiceHint);
sealed record IncidentAssignment(string IncidentId, ResponsePriority Priority, string Team, string Reason);
sealed record TriageSummary(IReadOnlyList<IncidentAssignment> Assignments)
{
    // LINQ Count는 "조건에 맞는 항목 수"라는 의도를 반복문보다 짧고 분명하게 표현합니다.
    public int ImmediateCount => Assignments.Count(x => x.Priority == ResponsePriority.Immediate);
    public int UrgentCount => Assignments.Count(x => x.Priority == ResponsePriority.Urgent);
    public int NormalCount => Assignments.Count(x => x.Priority == ResponsePriority.Normal);
}

// 예상 가능한 업무 실패는 Result로 반환해 호출자가 빠뜨리지 않고 분기하게 합니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IIncidentRepository
{
    Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAssignmentAsync(IncidentAssignment assignment, CancellationToken cancellationToken);
}

// Strategy 계약은 우선순위 정책 변경을 서비스 수정 없이 새 구현 추가로 해결하게 합니다(OCP).
interface IIncidentPriorityPolicy
{
    IncidentAssignment Classify(Incident incident);
}

interface IOperationsLog
{
    void AssignmentSaved(IncidentAssignment assignment);
}

// Application Service는 조회→분류→저장의 사용 사례 순서만 맡고, 업무 규칙과 저장 기술은 협력 객체에 위임합니다(SRP).
sealed class TriageIncidentsService(
    IIncidentRepository repository,
    IIncidentPriorityPolicy policy,
    IOperationsLog operationsLog)
{
    public async Task<Result<TriageSummary>> TriageOpenIncidentsAsync(CancellationToken cancellationToken)
    {
        var incidents = await repository.GetOpenAsync(cancellationToken);
        if (incidents.Count == 0)
            return Result<TriageSummary>.Failure("분류할 열린 장애가 없습니다.");

        var assignments = new List<IncidentAssignment>();
        // OrderBy로 처리 순서를 고정하면 실행마다 로그와 테스트 결과가 달라지는 혼란을 줄입니다.
        foreach (var incident in incidents.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assignment = policy.Classify(incident);
            var saved = await repository.SaveAssignmentAsync(assignment, cancellationToken);
            if (!saved.IsSuccess)
                return Result<TriageSummary>.Failure($"{incident.Id} 저장 실패: {saved.Error}");

            assignments.Add(assignment);
            operationsLog.AssignmentSaved(assignment);
        }

        return Result<TriageSummary>.Success(new(assignments.AsReadOnly()));
    }
}

sealed class ImpactBasedPriorityPolicy : IIncidentPriorityPolicy
{
    public IncidentAssignment Classify(Incident incident)
    {
        // 잘못된 입력을 먼저 거부하면 아래 업무 규칙이 유효한 값만 다루게 됩니다.
        if (string.IsNullOrWhiteSpace(incident.Id) || string.IsNullOrWhiteSpace(incident.Title) || incident.AffectedUsers < 0)
            return new(incident.Id, ResponsePriority.Immediate, "incident-command", "입력 오류를 운영자가 확인해야 합니다.");

        var team = string.IsNullOrWhiteSpace(incident.ServiceHint) ? "platform" : incident.ServiceHint;
        if (incident.RevenueBlocked || incident.Severity == IncidentSeverity.Critical)
            return new(incident.Id, ResponsePriority.Immediate, team, "매출 차단 또는 치명도 Critical입니다.");

        if (incident.Severity == IncidentSeverity.High || incident.AffectedUsers >= 100)
            return new(incident.Id, ResponsePriority.Urgent, team, "영향도가 높아 신속 대응이 필요합니다.");

        return new(incident.Id, ResponsePriority.Normal, team, "일반 대응 기준에 해당합니다.");
    }
}

sealed class InMemoryIncidentRepository(IEnumerable<Incident> seed) : IIncidentRepository
{
    private readonly List<Incident> _open = [.. seed];
    private readonly Dictionary<string, IncidentAssignment> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 주어 호출자가 저장소 내부 목록을 직접 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<Incident>>(_open.ToArray());
    }

    public Task<Result<bool>> SaveAssignmentAsync(IncidentAssignment assignment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 요청의 같은 결과는 성공으로 처리하는 멱등성이 재시도 시 중복 부작용을 막습니다.
        if (_saved.TryGetValue(assignment.IncidentId, out var existing))
            return Task.FromResult(existing == assignment
                ? Result<bool>.Success(true)
                : Result<bool>.Failure("이미 다른 배정 결과가 저장되었습니다."));

        _saved.Add(assignment.IncidentId, assignment);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleOperationsLog : IOperationsLog
{
    public void AssignmentSaved(IncidentAssignment assignment)
    {
        // 운영 로그에는 추적용 ID와 결과만 남기고 사고 설명 같은 민감 정보는 제외합니다.
        Console.WriteLine($"[ops] incident={assignment.IncidentId} priority={assignment.Priority} team={assignment.Team}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var policy = new ImpactBasedPriorityPolicy();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("매출 차단은 즉시 대응", () => AssertPriorityAsync(policy, new("T-1", "결제", IncidentSeverity.High, 1, true, "pay"), ResponsePriority.Immediate)),
            ("사용자 100명 이상은 신속 대응", () => AssertPriorityAsync(policy, new("T-2", "지연", IncidentSeverity.Medium, 100, false, null), ResponsePriority.Urgent)),
            ("낮은 영향도는 일반 대응", () => AssertPriorityAsync(policy, new("T-3", "표시", IncidentSeverity.Low, 3, false, "web"), ResponsePriority.Normal)),
            ("서비스는 모든 장애를 처리", ServiceProcessesAllAsync)
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

    private static Task AssertPriorityAsync(IIncidentPriorityPolicy policy, Incident incident, ResponsePriority expected)
    {
        var actual = policy.Classify(incident).Priority;
        if (actual != expected)
            throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var repository = new InMemoryIncidentRepository([
            new("T-4", "A", IncidentSeverity.Low, 1, false, null),
            new("T-5", "B", IncidentSeverity.High, 2, false, "api")
        ]);
        var service = new TriageIncidentsService(repository, new ImpactBasedPriorityPolicy(), new SilentLog());
        var result = await service.TriageOpenIncidentsAsync(CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Assignments.Count != 2)
            throw new InvalidOperationException("모든 장애가 처리되어야 합니다.");
    }

    private sealed class SilentLog : IOperationsLog
    {
        // 테스트는 출력이 아니라 결과만 검증하므로 아무 작업도 하지 않는 대역을 사용합니다.
        public void AssignmentSaved(IncidentAssignment assignment) { }
    }
}
