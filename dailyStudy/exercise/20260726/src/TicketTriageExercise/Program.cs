// 오늘 예제는 한 파일 안에서 실행 흐름과 설계 요소를 위에서 아래로 따라갈 수 있게 구성했다.
// 실무에서는 타입별 파일로 나누지만, 처음 배우는 단계에서는 이동 비용을 줄이는 편이 이해에 유리하다.

var repository = new InMemoryTicketRepository();
ITriageStrategy strategy = new SlaTriageStrategy();
var service = new TicketApplicationService(repository, strategy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await BeginnerValidation.RunAsync();
    return;
}

var requests = new[]
{
    new CreateTicketRequest(" 결제가 두 번 됐어요 ", CustomerTier.Vip, Severity.High),
    new CreateTicketRequest("로그인 방법을 알려 주세요", CustomerTier.Standard, Severity.Low),
    new CreateTicketRequest(null, CustomerTier.Standard, Severity.Medium)
};

foreach (var request in requests)
{
    var result = await service.CreateAsync(request, CancellationToken.None);
    Console.WriteLine(result.IsSuccess ? $"등록: {result.Value}" : $"실패: {result.Error}");
}

var queue = await service.GetQueueAsync(CancellationToken.None);
Console.WriteLine("\n처리 순서");
foreach (var ticket in queue)
{
    Console.WriteLine($"- P{ticket.Priority}: {ticket.Title} ({ticket.CustomerTier})");
}

enum CustomerTier { Standard, Vip }
enum Severity { Low, Medium, High }

// record는 값 중심 데이터를 간결하게 표현하고 값 비교를 제공한다.
// init 속성은 생성 뒤 임의 변경을 막아 여러 계층을 오갈 때 상태 추적을 쉽게 한다.
sealed record Ticket
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required CustomerTier CustomerTier { get; init; }
    public required Severity Severity { get; init; }
    public required int Priority { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static Result<Ticket> Create(CreateTicketRequest request, int priority)
    {
        // nullable 참조 형식 string?은 값이 없을 수 있음을 컴파일러와 독자에게 알린다.
        // 공백까지 검사한 뒤 정규화하므로 null-forgiving 연산자(!)에 기대지 않는다.
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<Ticket>.Failure("제목은 필수입니다.");

        return Result<Ticket>.Success(new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            CustomerTier = request.CustomerTier,
            Severity = request.Severity,
            Priority = priority,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}

sealed record CreateTicketRequest(string? Title, CustomerTier CustomerTier, Severity Severity);

// 예상 가능한 입력 실패는 Result로 반환하면 호출자가 정상 흐름으로 분기할 수 있다.
// 반대로 DB 연결 끊김이나 취소처럼 복구 정책이 필요한 비정상 실패는 예외로 전파한다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

interface ITriageStrategy
{
    int Calculate(CustomerTier tier, Severity severity);
}

// Strategy는 자주 바뀌는 우선순위 정책을 도메인 생성과 분리한다.
// 새 정책을 추가해도 서비스 수정이 적어 OCP를 지키고 독립 테스트가 쉬워진다.
sealed class SlaTriageStrategy : ITriageStrategy
{
    public int Calculate(CustomerTier tier, Severity severity)
    {
        var severityScore = severity switch
        {
            Severity.High => 1,
            Severity.Medium => 2,
            _ => 3
        };

        return tier == CustomerTier.Vip ? Math.Max(1, severityScore - 1) : severityScore;
    }
}

interface ITicketRepository
{
    Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken cancellationToken);
}

// 메모리 구현은 외부 DB 없이 실행되며 테스트 대역으로도 쓸 수 있다.
// 인터페이스 뒤에 숨겼으므로 실제 DB 구현으로 바꿔도 서비스 규칙은 유지된다.
sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly List<Ticket> _tickets = [];

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_tickets.Any(x =>
            string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Ticket>>(_tickets.ToList());
    }
}

interface IAuditLog
{
    void TicketCreated(Ticket ticket);
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void TicketCreated(Ticket ticket) =>
        Console.WriteLine($"감사 로그: ticket={ticket.Id} priority={ticket.Priority}");
}

// Application Service는 생성, 중복 확인, 저장, 감사 기록의 사용 사례 순서를 조정한다.
// 생성자 주입은 구체 클래스가 아닌 인터페이스에 의존하게 하여 DIP와 테스트 가능성을 높인다.
sealed class TicketApplicationService(
    ITicketRepository repository,
    ITriageStrategy strategy,
    IAuditLog auditLog)
{
    public async Task<Result<Ticket>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = request.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTitle) &&
            await repository.ExistsByTitleAsync(normalizedTitle, cancellationToken))
        {
            return Result<Ticket>.Failure("같은 제목의 티켓이 이미 있습니다.");
        }

        var priority = strategy.Calculate(request.CustomerTier, request.Severity);
        var created = Ticket.Create(request, priority);
        if (!created.IsSuccess || created.Value is null)
            return created;

        await repository.AddAsync(created.Value, cancellationToken);
        auditLog.TicketCreated(created.Value);
        return created;
    }

    public async Task<IReadOnlyList<Ticket>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var tickets = await repository.GetAllAsync(cancellationToken);
        // LINQ는 “우선순위, 생성 시각 순으로 정렬”이라는 의도를 반복문보다 직접 드러낸다.
        return tickets.OrderBy(x => x.Priority).ThenBy(x => x.CreatedAtUtc).ToList();
    }
}

static class BeginnerValidation
{
    public static async Task RunAsync()
    {
        var passed = 0;

        Check("빈 제목 거부", !Ticket.Create(
            new CreateTicketRequest(" ", CustomerTier.Standard, Severity.Low), 3).IsSuccess);

        var strategy = new SlaTriageStrategy();
        Check("VIP 긴급 티켓은 P1", strategy.Calculate(CustomerTier.Vip, Severity.High) == 1);

        var service = new TicketApplicationService(
            new InMemoryTicketRepository(), strategy, new SilentAuditLog());
        var first = await service.CreateAsync(
            new CreateTicketRequest("결제 오류", CustomerTier.Standard, Severity.High),
            CancellationToken.None);
        var duplicate = await service.CreateAsync(
            new CreateTicketRequest(" 결제 오류 ", CustomerTier.Standard, Severity.Low),
            CancellationToken.None);
        Check("정상 티켓 등록", first.IsSuccess);
        Check("정규화 후 중복 거부", !duplicate.IsSuccess);

        Console.WriteLine($"초보자 검증 통과 ({passed}/4)");
        return;

        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException($"검증 실패: {name}");
            passed++;
            Console.WriteLine($"[통과] {name}");
        }
    }

    private sealed class SilentAuditLog : IAuditLog
    {
        // 테스트에서는 콘솔 출력이라는 부수 효과를 제거해 검증 결과에만 집중한다.
        public void TicketCreated(Ticket ticket) { }
    }
}
