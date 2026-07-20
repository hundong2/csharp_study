using System.Diagnostics.CodeAnalysis;
using System.Text;

// 학습 순서: ① BasicSyntaxTour의 변수·nullable·switch·컬렉션·람다,
// ② SupportTicketDemo의 실행 흐름, ③ Service와 Domain, ④ Interface와 구현,
// ⑤ SelfTest 순서로 읽으세요. 실무에서는 역할별 파일/계층으로 분리할 내용을
// 초보자가 한 번에 흐름을 추적할 수 있도록 한 파일에 모았습니다.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

BasicSyntaxTour.Run();
await SupportTicketDemo.RunAsync();

public static class BasicSyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== Basic syntax tour ==");

        // [기초] decimal은 시간/금액처럼 정확한 소수 계산에 사용하고,
        // string?는 값이 없을 수 있음을 타입에 명시합니다.
        string requester = "junior@example.com";
        int openTicketCount = 2;
        bool isPremiumCustomer = true;
        decimal estimatedHours = 1.5m;
        string? optionalPhone = null;

        // [기초] switch 식은 여러 if/else를 결과 중심으로 읽기 좋게 표현합니다.
        string workloadMessage = openTicketCount switch
        {
            0 => "no active work",
            <= 3 => "normal support load",
            _ => "needs triage"
        };

        string[] tags = ["syntax", "nullable", "architecture"];
        List<string> queue = ["billing", "login"];
        Dictionary<string, int> priorityByCategory = new()
        {
            ["billing"] = 2,
            ["login"] = 1,
            ["general"] = 3
        };

        foreach (KeyValuePair<string, int> item in priorityByCategory)
        {
            Console.WriteLine($"category {item.Key}: priority {item.Value}");
        }

        // [중급] 람다는 작은 동작을 변수에 담으며, 교체 가능한 정책으로 발전시킬 수 있습니다.
        Func<decimal, decimal> addBuffer = hours => hours + 0.5m;
        decimal plannedHours = addBuffer(estimatedHours);

        Console.WriteLine($"requester: {requester}, premium: {isPremiumCustomer}");
        Console.WriteLine($"workload: {workloadMessage}, queue count: {queue.Count}");
        Console.WriteLine($"tags: {string.Join(", ", tags)}");
        Console.WriteLine($"phone length: {optionalPhone?.Length ?? 0}, planned hours: {plannedHours}");
        Console.WriteLine();
    }
}

public static class SupportTicketDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== Support ticket workflow ==");

        HelpDeskApp app = DemoCompositionRoot.Build();
        CreateTicketCommand command = new(
            RequesterEmail: "junior@example.com",
            Title: "Cannot sign in",
            Category: TicketCategory.Login,
            Severity: Severity.High,
            Description: "The user sees an invalid token message after password reset.");

        Result<TicketSummary> result = await app.Tickets.CreateAsync(command);

        if (result.IsSuccess)
        {
            Console.WriteLine($"created ticket: {result.Value.TicketId}");
            Console.WriteLine($"owner: {result.Value.AssignedAgent}");
            Console.WriteLine($"status: {result.Value.Status}");
            Console.WriteLine($"due: {result.Value.DueAt:yyyy-MM-dd HH:mm} UTC");
        }
        else
        {
            Console.WriteLine($"ticket failed: {result.Error}");
        }

        Console.WriteLine();
        Console.WriteLine("Run with --self-test to verify the beginner checkpoints.");
    }
}

public sealed class HelpDeskApp(TicketService tickets, InMemoryTicketRepository repository)
{
    public TicketService Tickets { get; } = tickets;
    public InMemoryTicketRepository Repository { get; } = repository;
}

public static class DemoCompositionRoot
{
    // [실무] 생성과 연결을 한곳에 모아 서비스가 구체 구현을 직접 만들지 않게 합니다.
    public static HelpDeskApp Build()
    {
        AgentDirectory agents = new(
        [
            new SupportAgent("A-100", "Mina", new HashSet<TicketCategory>
            {
                TicketCategory.Login,
                TicketCategory.General
            }),
            new SupportAgent("A-200", "Dae", new HashSet<TicketCategory>
            {
                TicketCategory.Billing
            })
        ]);

        BusinessClock clock = new(DateTimeOffset.Parse("2026-07-15T09:00:00+00:00"));
        InMemoryTicketRepository repository = new();
        TicketService service = new(agents, clock, repository);

        return new HelpDeskApp(service, repository);
    }
}

public sealed class TicketService(
    IAgentDirectory agents,
    IClock clock,
    ITicketRepository repository)
{
    // [실무] 입력 검증, 담당자 선택, 마감 계산, 저장을 조정하는 Application Service입니다.
    // 인터페이스를 생성자로 주입해 저장소와 시간을 테스트용 구현으로 교체할 수 있습니다.
    public async ValueTask<Result<TicketSummary>> CreateAsync(
        CreateTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        Result<CreateTicketCommand> validation = Validate(command);
        if (!validation.IsSuccess)
        {
            return Result<TicketSummary>.Fail(validation.Error);
        }

        SupportAgent? agent = await agents.FindForCategoryAsync(command.Category, cancellationToken);
        if (agent is null)
        {
            return Result<TicketSummary>.Fail($"No agent can handle category: {command.Category}");
        }

        Ticket ticket = Ticket.Open(
            command.RequesterEmail,
            command.Title.Trim(),
            command.Category,
            command.Severity,
            command.Description.Trim(),
            clock.UtcNow);

        ticket.Assign(agent);
        await repository.SaveAsync(ticket, cancellationToken);

        return Result<TicketSummary>.Ok(new TicketSummary(
            ticket.Id,
            ticket.Title,
            ticket.Status,
            agent.Name,
            ticket.DueAt));
    }

    private static Result<CreateTicketCommand> Validate(CreateTicketCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RequesterEmail) || !command.RequesterEmail.Contains('@'))
        {
            return Result<CreateTicketCommand>.Fail("A valid requester email is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Result<CreateTicketCommand>.Fail("Ticket title is required.");
        }

        if (command.Description.Trim().Length < 20)
        {
            return Result<CreateTicketCommand>.Fail("Description must explain the issue in at least 20 characters.");
        }

        return Result<CreateTicketCommand>.Ok(command);
    }
}

public sealed class Ticket
{
    // [도메인] 상태 변경을 모델의 메서드로 제한해 잘못된 상태 전이를 방지합니다.
    private Ticket(
        string requesterEmail,
        string title,
        TicketCategory category,
        Severity severity,
        string description,
        DateTimeOffset openedAt)
    {
        RequesterEmail = requesterEmail;
        Title = title;
        Category = category;
        Severity = severity;
        Description = description;
        OpenedAt = openedAt;
        DueAt = openedAt.AddHours(severity.ResponseHours());
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string RequesterEmail { get; }
    public string Title { get; }
    public TicketCategory Category { get; }
    public Severity Severity { get; }
    public string Description { get; }
    public TicketStatus Status { get; private set; } = TicketStatus.New;
    public SupportAgent? AssignedAgent { get; private set; }
    public DateTimeOffset OpenedAt { get; }
    public DateTimeOffset DueAt { get; }

    public static Ticket Open(
        string requesterEmail,
        string title,
        TicketCategory category,
        Severity severity,
        string description,
        DateTimeOffset openedAt)
    {
        return new Ticket(requesterEmail, title, category, severity, description, openedAt);
    }

    public void Assign(SupportAgent agent)
    {
        if (!agent.CanHandle(Category))
        {
            throw new InvalidOperationException($"{agent.Name} cannot handle {Category} tickets.");
        }

        AssignedAgent = agent;
        Status = TicketStatus.Assigned;
    }
}

public static class SeverityRules
{
    public static int ResponseHours(this Severity severity) => severity switch
    {
        Severity.Low => 48,
        Severity.Normal => 24,
        Severity.High => 4,
        Severity.Critical => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity.")
    };
}

public sealed record SupportAgent(string Id, string Name, IReadOnlySet<TicketCategory> Categories)
{
    public bool CanHandle(TicketCategory category) => Categories.Contains(category);
}

public sealed record CreateTicketCommand(
    string RequesterEmail,
    string Title,
    TicketCategory Category,
    Severity Severity,
    string Description);

public sealed record TicketSummary(
    Guid TicketId,
    string Title,
    TicketStatus Status,
    string AssignedAgent,
    DateTimeOffset DueAt);

public enum TicketCategory
{
    General,
    Login,
    Billing
}

public enum Severity
{
    Low,
    Normal,
    High,
    Critical
}

public enum TicketStatus
{
    New,
    Assigned,
    Resolved
}

public sealed record Result<T>(T? Value, string? Error)
{
    // [고급] 예측 가능한 실패를 Result 값으로 전달하고 nullable 계약을 특성으로 보강합니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

public interface IAgentDirectory
{
    // [설계] 인터페이스는 호출자인 TicketService가 실제로 필요한 동작만 노출합니다.
    ValueTask<SupportAgent?> FindForCategoryAsync(TicketCategory category, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ITicketRepository
{
    ValueTask SaveAsync(Ticket ticket, CancellationToken cancellationToken);
    ValueTask<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class AgentDirectory(IReadOnlyList<SupportAgent> agents) : IAgentDirectory
{
    public ValueTask<SupportAgent?> FindForCategoryAsync(TicketCategory category, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SupportAgent? agent = agents.FirstOrDefault(x => x.CanHandle(category));
        return ValueTask.FromResult(agent);
    }
}

public sealed class BusinessClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

public sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly Dictionary<Guid, Ticket> _tickets = [];

    public ValueTask SaveAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tickets[ticket.Id] = ticket;
        return ValueTask.CompletedTask;
    }

    public ValueTask<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tickets.TryGetValue(id, out Ticket? ticket);
        return ValueTask.FromResult(ticket);
    }
}

public static class SelfTest
{
    // [검증] 고정 시간과 메모리 저장소 덕분에 언제 실행해도 같은 결과를 검증할 수 있습니다.
    public static async Task RunAsync()
    {
        HelpDeskApp app = DemoCompositionRoot.Build();

        Result<TicketSummary> success = await app.Tickets.CreateAsync(new CreateTicketCommand(
            "learner@example.com",
            "Password reset fails",
            TicketCategory.Login,
            Severity.High,
            "Password reset succeeds but the next sign-in shows an invalid token message."));

        Assert(success.IsSuccess, "valid ticket should succeed");
        Assert(success.Value.Status == TicketStatus.Assigned, "valid ticket should be assigned");
        Assert(success.Value.AssignedAgent == "Mina", "login ticket should be assigned to the login agent");
        Assert(await app.Repository.FindAsync(success.Value.TicketId, CancellationToken.None) is not null,
            "created ticket should be saved");

        Result<TicketSummary> invalidEmail = await app.Tickets.CreateAsync(new CreateTicketCommand(
            "not-an-email",
            "Billing issue",
            TicketCategory.Billing,
            Severity.Normal,
            "The invoice total is different from the expected contracted amount."));

        Assert(!invalidEmail.IsSuccess, "invalid email should fail");
        Assert(invalidEmail.Error.Contains("email", StringComparison.OrdinalIgnoreCase),
            "invalid email error should explain the email problem");

        Result<TicketSummary> shortDescription = await app.Tickets.CreateAsync(new CreateTicketCommand(
            "learner@example.com",
            "Too short",
            TicketCategory.General,
            Severity.Low,
            "Help"));

        Assert(!shortDescription.IsSuccess, "short description should fail");

        Result<TicketSummary> billing = await app.Tickets.CreateAsync(new CreateTicketCommand(
            "learner@example.com",
            "Refund request",
            TicketCategory.Billing,
            Severity.Critical,
            "The customer was charged twice and needs a refund before the end of day."));

        Assert(billing.IsSuccess, "billing ticket should succeed");
        Assert(billing.Value.AssignedAgent == "Dae", "billing ticket should be assigned to billing agent");

        Console.WriteLine("Self-test passed: syntax tour, validation, ticket workflow, and layer contracts are valid.");
    }

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {message}");
        }
    }
}
