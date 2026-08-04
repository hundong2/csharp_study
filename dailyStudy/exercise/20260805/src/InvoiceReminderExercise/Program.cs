// 오늘 예제는 미납 청구서 알림을 고르는 작은 업무 프로그램입니다.
// 한 파일에 모았지만, 각 타입의 책임을 구분해 실제 프로젝트의 계층 구조를 연습합니다.

var invoices = new InMemoryInvoiceRepository(
[
    new Invoice("INV-100", "customer1@example.com", 120_000m, new DateOnly(2026, 8, 1), null),
    new Invoice("INV-101", "customer2@example.com", 50_000m, new DateOnly(2026, 8, 10), null),
    new Invoice("INV-102", "customer3@example.com", 80_000m, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 25))
]);

// Composition Root: 프로그램 시작점에서 구현체를 조립하면 업무 코드는 구체 기술에 묶이지 않습니다.
IClock clock = new FixedClock(new DateOnly(2026, 8, 5));
IReminderPolicy policy = new OverdueReminderPolicy();
var service = new SendInvoiceRemindersService(invoices, policy, new ConsoleReminderSender(), clock);

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(CancellationToken.None);
Console.WriteLine(result.IsSuccess ? $"알림 {result.Value}건 처리 완료" : $"처리 실패: {result.Error}");

// record는 값 중심 데이터에 적합하고 init 전용 속성처럼 동작해 생성 뒤 실수로 바꾸기 어렵습니다.
public sealed record Invoice(
    string Id,
    string? CustomerEmail,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? PaidDate)
{
    // 도메인 모델이 자신의 규칙을 가지면 여러 서비스가 같은 규칙을 중복하지 않습니다.
    public bool IsPaid => PaidDate is not null;
}

public sealed record Reminder(string InvoiceId, string Recipient, decimal Amount, int DaysOverdue);

// 예상 가능한 업무 실패는 예외 대신 Result로 돌려 호출자가 처리 방법을 선택하게 합니다.
public sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default!, error);
}

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> GetOpenAsync(CancellationToken cancellationToken);
}

public interface IReminderPolicy
{
    Result<Reminder> Create(Invoice invoice, DateOnly today);
}

public interface IReminderSender
{
    Task SendAsync(Reminder reminder, CancellationToken cancellationToken);
}

public interface IClock
{
    DateOnly Today { get; }
}

// Strategy: 알림 선정 규칙을 인터페이스 뒤에 두면 고객 등급별 정책을 서비스 수정 없이 추가할 수 있습니다.
public sealed class OverdueReminderPolicy : IReminderPolicy
{
    public Result<Reminder> Create(Invoice invoice, DateOnly today)
    {
        if (invoice.IsPaid || invoice.DueDate >= today)
            return Result<Reminder>.Failure("아직 알림 대상이 아닙니다.");

        // nullable 문자열은 먼저 검사해야 이후 코드가 안전한 string으로 사용할 수 있습니다.
        if (string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            return Result<Reminder>.Failure("고객 이메일이 없습니다.");

        var daysOverdue = today.DayNumber - invoice.DueDate.DayNumber;
        return Result<Reminder>.Success(new(invoice.Id, invoice.CustomerEmail, invoice.Amount, daysOverdue));
    }
}

// Application Service는 조회→판정→전송 순서를 조정하고 세부 규칙은 협력 객체에 맡깁니다.
public sealed class SendInvoiceRemindersService(
    IInvoiceRepository repository,
    IReminderPolicy policy,
    IReminderSender sender,
    IClock clock)
{
    public async Task<Result<int>> ExecuteAsync(CancellationToken cancellationToken)
    {
        // await는 I/O를 기다리는 동안 스레드를 붙잡지 않으며, CancellationToken은 운영 중 취소를 전달합니다.
        var openInvoices = await repository.GetOpenAsync(cancellationToken);

        // LINQ는 컬렉션 변환 의도를 간결하게 표현합니다. 중간 결과를 배열로 만들어 한 번만 평가합니다.
        var reminders = openInvoices
            .Select(invoice => policy.Create(invoice, clock.Today))
            .Where(result => result.IsSuccess)
            .Select(result => result.Value)
            .ToArray();

        try
        {
            foreach (var reminder in reminders)
                await sender.SendAsync(reminder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 취소는 상위 계층도 알아야 하므로 삼키지 않습니다.
            throw;
        }
        catch (Exception ex)
        {
            // 네트워크 장애처럼 비정상 기술 실패는 예외로 받고 경계에서 Result로 번역합니다.
            return Result<int>.Failure($"알림 전송 장애: {ex.Message}");
        }

        return Result<int>.Success(reminders.Length);
    }
}

public sealed class InMemoryInvoiceRepository(IEnumerable<Invoice> seed) : IInvoiceRepository
{
    private readonly IReadOnlyList<Invoice> _items = seed.ToArray();

    public Task<IReadOnlyList<Invoice>> GetOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Invoice> result = _items.Where(invoice => !invoice.IsPaid).ToArray();
        return Task.FromResult(result);
    }
}

public sealed class ConsoleReminderSender : IReminderSender
{
    public Task SendAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"{reminder.Recipient}: {reminder.InvoiceId}, {reminder.Amount:N0}원, {reminder.DaysOverdue}일 연체");
        return Task.CompletedTask;
    }
}

public sealed record FixedClock(DateOnly Today) : IClock;

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new OverdueReminderPolicy();
        var today = new DateOnly(2026, 8, 5);

        Check(policy.Create(new("1", "a@b.com", 1m, today.AddDays(-2), null), today).Value.DaysOverdue == 2, "연체 일수", ref passed);
        Check(!policy.Create(new("2", "a@b.com", 1m, today, null), today).IsSuccess, "미도래 제외", ref passed);
        Check(!policy.Create(new("3", null, 1m, today.AddDays(-1), null), today).IsSuccess, "null 이메일", ref passed);

        var sender = new CollectingSender();
        var repository = new InMemoryInvoiceRepository([new("4", "a@b.com", 10m, today.AddDays(-1), null)]);
        var service = new SendInvoiceRemindersService(repository, policy, sender, new FixedClock(today));
        Check((await service.ExecuteAsync(CancellationToken.None)).Value == 1 && sender.Count == 1, "서비스 흐름", ref passed);
        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name, ref int passed)
    {
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }

    private sealed class CollectingSender : IReminderSender
    {
        public int Count { get; private set; }
        public Task SendAsync(Reminder reminder, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
