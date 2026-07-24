// 오늘 예제는 한 파일에서 위에서 아래로 읽으며 문법 → 도메인 → 아키텍처 흐름을 따라가도록 구성했습니다.
var repository = new InMemoryIncidentRepository();
IReadOnlyList<IRoutingStrategy> strategies =
[
    new CriticalRoutingStrategy(),
    new DefaultRoutingStrategy()
];

// Composition Root는 구체 구현을 선택하고 조립하는 유일한 장소입니다.
// 실제 ASP.NET Core에서는 이 부분을 DI 컨테이너 등록 코드로 옮깁니다.
var service = new IncidentApplicationService(repository, strategies, new ConsoleNotifier());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await BeginnerValidation.RunAsync();
    return;
}

var requests = new[]
{
    new CreateIncidentRequest("결제 API 지연", Severity.Critical, "payments", "trace-1001"),
    new CreateIncidentRequest("검색 색인 재시도", Severity.Warning, null, "trace-1002")
};

// foreach는 컬렉션의 각 항목을 순서대로 처리하는 기본 반복문입니다.
foreach (var request in requests)
{
    var result = await service.CreateAndRouteAsync(request, CancellationToken.None);

    // 패턴 매칭 switch는 Result의 성공과 실패 모양을 빠짐없이 나눕니다.
    Console.WriteLine(result switch
    {
        Result<Incident>.Success success =>
            $"등록 성공: {success.Value.Title} → {success.Value.AssignedChannel}",
        Result<Incident>.Failure failure =>
            $"등록 실패: {failure.Code} / {failure.Message}",
        _ => throw new InvalidOperationException("알 수 없는 결과 형식입니다.")
    });
}

var summary = await service.GetSummaryAsync(CancellationToken.None);
Console.WriteLine($"요약: 전체 {summary.Total}건, 긴급 {summary.CriticalCount}건");

// enum은 허용되는 심각도 값을 제한하여 임의 문자열 오타를 막습니다.
public enum Severity
{
    Info,
    Warning,
    Critical
}

// record는 값 중심 데이터에 적합합니다. 생성 후 바뀌지 않아 요청이 중간에 변조될 위험을 줄입니다.
// string?은 Team이 없을 수 있음을 타입에 표시하고, 나머지 string은 null이 아님을 약속합니다.
public sealed record CreateIncidentRequest(
    string Title,
    Severity Severity,
    string? Team,
    string CorrelationId);

public sealed record IncidentSummary(int Total, int CriticalCount);

// Result는 사용자의 잘못된 입력처럼 예상 가능한 실패를 예외 없이 표현합니다.
// 네트워크 단절이나 프로그래밍 버그처럼 예상 밖 실패에는 예외가 더 적합합니다.
public abstract record Result<T>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string Code, string Message) : Result<T>;
}

// 도메인 모델은 데이터만 담지 않고 유효한 상태를 만드는 규칙도 책임집니다.
public sealed record Incident
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required Severity Severity { get; init; }
    public string? Team { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? AssignedChannel { get; init; }

    public static Result<Incident> Create(CreateIncidentRequest request)
    {
        // IsNullOrWhiteSpace는 null, 빈 문자열, 공백만 있는 문자열을 한 번에 검사합니다.
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new Result<Incident>.Failure("invalid_title", "제목을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            return new Result<Incident>.Failure("invalid_correlation_id", "추적 ID를 입력하세요.");
        }

        return new Result<Incident>.Success(new Incident
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Severity = request.Severity,
            Team = string.IsNullOrWhiteSpace(request.Team) ? null : request.Team.Trim(),
            CorrelationId = request.CorrelationId.Trim(),
            // 운영 시스템끼리 시간대를 혼동하지 않도록 저장 시간은 UTC로 통일합니다.
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    // with 식은 기존 record를 직접 바꾸지 않고 일부 값만 복사해 바꾼 새 값을 만듭니다.
    public Incident AssignTo(string channel) => this with { AssignedChannel = channel };
}

// Strategy는 변하는 라우팅 정책을 서비스에서 분리합니다. 새 정책 추가 시 기존 서비스를 고치지 않아 OCP에 유리합니다.
public interface IRoutingStrategy
{
    bool CanHandle(Incident incident);
    string SelectChannel(Incident incident);
}

public sealed class CriticalRoutingStrategy : IRoutingStrategy
{
    public bool CanHandle(Incident incident) => incident.Severity == Severity.Critical;

    public string SelectChannel(Incident incident)
        // ??는 왼쪽 값이 null일 때만 오른쪽 기본값을 사용합니다.
        => $"pager-{incident.Team ?? "platform"}";
}

public sealed class DefaultRoutingStrategy : IRoutingStrategy
{
    public bool CanHandle(Incident incident) => true;
    public string SelectChannel(Incident incident) => $"chat-{incident.Team ?? "general"}";
}

// Repository 인터페이스는 저장 방식(DB, API, 메모리)을 업무 흐름에서 분리합니다.
public interface IIncidentRepository
{
    Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
    Task SaveAsync(Incident incident, CancellationToken cancellationToken);
    Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed class InMemoryIncidentRepository : IIncidentRepository
{
    private readonly List<Incident> _items = [];

    public Task<bool> ExistsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Any는 조건을 만족하는 항목이 하나라도 있는지를 반환하는 대표적인 LINQ 연산입니다.
        return Task.FromResult(_items.Any(item => item.CorrelationId == correlationId));
    }

    public Task SaveAsync(Incident incident, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.Add(incident);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 호출자가 Repository 내부 목록을 직접 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<Incident>>(_items.ToArray());
    }
}

public interface INotifier
{
    Task NotifyAsync(Incident incident, CancellationToken cancellationToken);
}

public sealed class ConsoleNotifier : INotifier
{
    public Task NotifyAsync(Incident incident, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(
            $"알림 전송: channel={incident.AssignedChannel}, correlationId={incident.CorrelationId}");
        return Task.CompletedTask;
    }
}

// Application Service는 유스케이스 순서만 조정하고 세부 정책과 저장 구현은 주입받습니다.
// 생성자 주입은 의존성을 명시해 테스트 대역으로 쉽게 교체하게 해 주며 DIP를 만족시킵니다.
public sealed class IncidentApplicationService(
    IIncidentRepository repository,
    IReadOnlyList<IRoutingStrategy> strategies,
    INotifier notifier)
{
    public async Task<Result<Incident>> CreateAndRouteAsync(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var created = Incident.Create(request);
        if (created is Result<Incident>.Failure failure)
        {
            return failure;
        }

        var incident = ((Result<Incident>.Success)created).Value;
        if (await repository.ExistsByCorrelationIdAsync(
                incident.CorrelationId,
                cancellationToken))
        {
            return new Result<Incident>.Failure(
                "duplicate",
                "같은 추적 ID의 장애가 이미 등록되었습니다.");
        }

        // First는 일치하는 정책이 없으면 예외가 납니다. 마지막 기본 Strategy가 항상 true라 불변 조건이 보장됩니다.
        var strategy = strategies.First(item => item.CanHandle(incident));
        var routed = incident.AssignTo(strategy.SelectChannel(incident));

        await repository.SaveAsync(routed, cancellationToken);
        await notifier.NotifyAsync(routed, cancellationToken);
        return new Result<Incident>.Success(routed);
    }

    public async Task<IncidentSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        // Count(predicate)는 반복문을 직접 쓰지 않고 “긴급 항목 수”라는 의도를 드러냅니다.
        return new IncidentSummary(
            items.Count,
            items.Count(item => item.Severity == Severity.Critical));
    }
}

public static class BeginnerValidation
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var repository = new InMemoryIncidentRepository();
        var notifier = new RecordingNotifier();
        var service = new IncidentApplicationService(
            repository,
            [new CriticalRoutingStrategy(), new DefaultRoutingStrategy()],
            notifier);

        await CheckAsync("빈 제목 거부", async () =>
        {
            var result = await service.CreateAndRouteAsync(
                new CreateIncidentRequest(" ", Severity.Info, null, "test-1"),
                CancellationToken.None);
            return result is Result<Incident>.Failure { Code: "invalid_title" };
        });

        await CheckAsync("긴급 장애 pager 배정", async () =>
        {
            var result = await service.CreateAndRouteAsync(
                new CreateIncidentRequest("DB 연결 오류", Severity.Critical, "data", "test-2"),
                CancellationToken.None);
            return result is Result<Incident>.Success
            {
                Value.AssignedChannel: "pager-data"
            };
        });

        await CheckAsync("중복 추적 ID 거부", async () =>
        {
            var result = await service.CreateAndRouteAsync(
                new CreateIncidentRequest("중복", Severity.Warning, null, "test-2"),
                CancellationToken.None);
            return result is Result<Incident>.Failure { Code: "duplicate" };
        });

        await CheckAsync("성공한 알림만 기록", () =>
            Task.FromResult(notifier.NotifiedCount == 1));

        Console.WriteLine($"초보자 검증 통과 ({passed}/4)");
        return;

        async Task CheckAsync(string name, Func<Task<bool>> test)
        {
            if (!await test())
            {
                throw new InvalidOperationException($"검증 실패: {name}");
            }

            passed++;
            Console.WriteLine($"[통과] {name}");
        }
    }

    private sealed class RecordingNotifier : INotifier
    {
        public int NotifiedCount { get; private set; }

        public Task NotifyAsync(Incident incident, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NotifiedCount++;
            return Task.CompletedTask;
        }
    }
}
