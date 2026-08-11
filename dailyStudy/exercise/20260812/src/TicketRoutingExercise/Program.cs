// 오늘 예제는 고객 지원 티켓을 적절한 팀에 배정하는 작은 업무 프로그램입니다.
// 한 파일에 모은 이유는 초보자가 실행 흐름을 위에서 아래로 따라가기 쉽게 하기 위해서입니다.

var repository = new InMemoryTicketRepository(
[
    new Ticket("T-101", "결제가 두 번 되었어요", "refund duplicate payment", TicketPriority.High, null),
    new Ticket("T-102", "로그인이 안 됩니다", "password reset", TicketPriority.Normal, "  "),
    new Ticket("T-103", "영수증이 필요합니다", "invoice request", TicketPriority.Low, "billing")
]);

// Composition Root는 구체 객체를 한곳에서 조립합니다. 업무 로직이 new 문에 묶이지 않아 테스트 대역으로 교체하기 쉽습니다.
ITicketRoutingStrategy strategy = new KeywordTicketRoutingStrategy();
var service = new RouteOpenTicketsService(repository, strategy, new ConsoleRoutingNotifier());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"배정 완료: {result.Value}건"
    : $"처리 실패: {result.Error}");

// enum은 가능한 값을 제한하여 "Hihg" 같은 문자열 오타를 컴파일 단계에서 막습니다.
enum TicketPriority
{
    Low,
    Normal,
    High
}

// record는 값 중심 데이터에 적합합니다. init-only 속성처럼 동작하므로 생성 후 실수로 내용을 바꾸기 어렵습니다.
sealed record Ticket(
    string Id,
    string Title,
    string Description,
    TicketPriority Priority,
    string? AssignedTeam);

sealed record RoutingDecision(string TicketId, string Team, TicketPriority Priority);

// Result는 "배정할 티켓이 없음"처럼 예상 가능한 실패를 예외 없이 호출자에게 명시적으로 전달합니다.
// 반면 DB 연결 끊김이나 취소처럼 정상 흐름 밖의 문제는 예외로 전파하여 운영 계층에서 기록·재시도할 수 있게 합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Repository는 저장 방식이라는 인프라 관심사를 업무 흐름에서 분리합니다.
interface ITicketRepository
{
    Task<IReadOnlyList<Ticket>> GetOpenAsync(CancellationToken cancellationToken);
    Task SaveAssignmentsAsync(IReadOnlyList<RoutingDecision> decisions, CancellationToken cancellationToken);
}

// Strategy는 배정 규칙을 교체 가능한 계약으로 만듭니다. 새 규칙을 추가해도 Application Service는 수정할 필요가 없습니다(OCP).
interface ITicketRoutingStrategy
{
    Result<RoutingDecision> Decide(Ticket ticket);
}

interface IRoutingNotifier
{
    Task NotifyAsync(IReadOnlyList<RoutingDecision> decisions, CancellationToken cancellationToken);
}

sealed class KeywordTicketRoutingStrategy : ITicketRoutingStrategy
{
    public Result<RoutingDecision> Decide(Ticket ticket)
    {
        // nullable 참조 형식(string?)을 켜면 null 가능성을 코드에 드러낼 수 있습니다.
        // IsNullOrWhiteSpace는 null, 빈 문자열, 공백만 있는 문자열을 한 번에 안전하게 검사합니다.
        if (!string.IsNullOrWhiteSpace(ticket.AssignedTeam))
        {
            return Result<RoutingDecision>.Failure($"{ticket.Id}는 이미 배정되었습니다.");
        }

        var searchableText = $"{ticket.Title} {ticket.Description}";
        var team = searchableText.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
                   searchableText.Contains("refund", StringComparison.OrdinalIgnoreCase) ||
                   searchableText.Contains("결제", StringComparison.OrdinalIgnoreCase)
            ? "billing"
            : searchableText.Contains("login", StringComparison.OrdinalIgnoreCase) ||
              searchableText.Contains("password", StringComparison.OrdinalIgnoreCase) ||
              searchableText.Contains("로그인", StringComparison.OrdinalIgnoreCase)
                ? "identity"
                : "general";

        return Result<RoutingDecision>.Success(new RoutingDecision(ticket.Id, team, ticket.Priority));
    }
}

// Application Service는 조회 → 판단 → 저장 → 알림이라는 유스케이스 순서만 조정합니다.
// 각 세부 작업은 인터페이스에 위임하므로 SRP와 DIP를 지키고 단위 테스트도 간단해집니다.
sealed class RouteOpenTicketsService(
    ITicketRepository repository,
    ITicketRoutingStrategy strategy,
    IRoutingNotifier notifier)
{
    public async Task<Result<int>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var tickets = await repository.GetOpenAsync(cancellationToken);

        // LINQ의 Where는 미배정 티켓만 고르고 OrderByDescending은 긴급도를 높은 순으로 정렬합니다.
        // ToArray로 즉시 평가해 이후 단계가 동일한 스냅샷을 사용하도록 합니다.
        var openTickets = tickets
            .Where(ticket => string.IsNullOrWhiteSpace(ticket.AssignedTeam))
            .OrderByDescending(ticket => ticket.Priority)
            .ToArray();

        if (openTickets.Length == 0)
        {
            return Result<int>.Failure("배정할 미처리 티켓이 없습니다.");
        }

        var decisions = new List<RoutingDecision>();
        foreach (var ticket in openTickets)
        {
            var decision = strategy.Decide(ticket);
            if (decision.IsSuccess && decision.Value is not null)
            {
                decisions.Add(decision.Value);
            }
        }

        // await는 저장 I/O가 끝날 때까지 비동기적으로 기다립니다. CancellationToken은 종료 요청을 하위 작업까지 전달합니다.
        await repository.SaveAssignmentsAsync(decisions, cancellationToken);
        await notifier.NotifyAsync(decisions, cancellationToken);
        return Result<int>.Success(decisions.Count);
    }
}

sealed class InMemoryTicketRepository(IEnumerable<Ticket> seed) : ITicketRepository
{
    private readonly Dictionary<string, Ticket> _tickets = seed.ToDictionary(ticket => ticket.Id);

    public Task<IReadOnlyList<Ticket>> GetOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Ticket>>(_tickets.Values.ToArray());
    }

    public Task SaveAssignmentsAsync(IReadOnlyList<RoutingDecision> decisions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var decision in decisions)
        {
            if (_tickets.TryGetValue(decision.TicketId, out var ticket))
            {
                // with 식은 기존 record를 바꾸지 않고 변경된 복사본을 만들어 불변성을 유지합니다.
                // 같은 배정을 다시 저장해도 결과가 같아 간단한 멱등성도 확보됩니다.
                _tickets[decision.TicketId] = ticket with { AssignedTeam = decision.Team };
            }
        }

        return Task.CompletedTask;
    }
}

sealed class ConsoleRoutingNotifier : IRoutingNotifier
{
    public Task NotifyAsync(IReadOnlyList<RoutingDecision> decisions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var decision in decisions)
        {
            Console.WriteLine($"[{decision.Priority}] {decision.TicketId} -> {decision.Team}");
        }

        return Task.CompletedTask;
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        Check(new KeywordTicketRoutingStrategy().Decide(
            new Ticket("1", "환불", "duplicate payment", TicketPriority.High, null)).Value?.Team == "billing",
            "결제 키워드 배정");
        passed++;

        Check(new KeywordTicketRoutingStrategy().Decide(
            new Ticket("2", "로그인", "password", TicketPriority.Normal, null)).Value?.Team == "identity",
            "인증 키워드 배정");
        passed++;

        Check(!new KeywordTicketRoutingStrategy().Decide(
            new Ticket("3", "문의", "question", TicketPriority.Low, "general")).IsSuccess,
            "이미 배정된 티켓 거부");
        passed++;

        var repository = new InMemoryTicketRepository(
        [
            new Ticket("4", "문의", "question", TicketPriority.Low, null),
            new Ticket("5", "완료", "done", TicketPriority.High, "general")
        ]);
        var service = new RouteOpenTicketsService(repository, new KeywordTicketRoutingStrategy(), new SilentNotifier());
        Check((await service.ExecuteAsync(CancellationToken.None)).Value == 1, "미배정 티켓만 처리");
        passed++;

        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 정상 업무 실패가 아니라 개발자가 즉시 고쳐야 할 결함이므로 예외가 적합합니다.
        if (!condition)
        {
            throw new InvalidOperationException($"테스트 실패: {name}");
        }
    }

    private sealed class SilentNotifier : IRoutingNotifier
    {
        public Task NotifyAsync(IReadOnlyList<RoutingDecision> decisions, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
