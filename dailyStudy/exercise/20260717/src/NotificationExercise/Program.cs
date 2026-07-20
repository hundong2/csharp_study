using System.Diagnostics.CodeAnalysis;
using System.Text;

// 학습 순서: ① BasicSyntaxTour → ② NotificationDemo → ③ Application Service
// → ④ Strategy 구현 → ⑤ Repository/Clock → ⑥ SelfTest입니다.
// [고급 관점] 채널별 발송 전략과 템플릿 책임을 분리해 변경 이유가 다른 코드를 격리합니다.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

BasicSyntaxTour.Run();
await NotificationDemo.RunAsync();

public static class BasicSyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== 1. 기본 문법 둘러보기 ==");

        string userName = "민수";
        int unreadCount = 3;
        bool marketingAgreed = false;
        string? optionalNickname = null;

        string urgency = unreadCount switch
        {
            0 => "새 알림 없음",
            <= 2 => "보통",
            _ => "확인 필요"
        };

        NotificationChannel[] enabledChannels =
        [
            NotificationChannel.Email,
            NotificationChannel.Sms
        ];
        List<string> tags = ["보안", "계정"];
        Dictionary<NotificationChannel, int> dailyLimits = new()
        {
            [NotificationChannel.Email] = 20,
            [NotificationChannel.Sms] = 5,
            [NotificationChannel.Push] = 30
        };

        foreach (NotificationChannel channel in enabledChannels)
        {
            Console.WriteLine($"{channel} 하루 한도: {dailyLimits[channel]}회");
        }

        string consentMessage = marketingAgreed ? "마케팅 수신 동의" : "필수 알림만 수신";
        Console.WriteLine($"{userName}: {unreadCount}개 ({urgency}, {consentMessage})");
        Console.WriteLine($"태그: {string.Join(", ", tags)}");
        Console.WriteLine($"표시 이름 길이: {optionalNickname?.Length ?? userName.Length}");
        Console.WriteLine();
    }
}

public static class NotificationDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== 2. 사용자 알림 발송 흐름 ==");

        NotificationApp app = CompositionRoot.Build();
        SendNotificationCommand command = new(
            UserId: "U-101",
            Recipient: "minsu@example.com",
            Channel: NotificationChannel.Email,
            Kind: NotificationKind.PasswordReset,
            Variables: new Dictionary<string, string>
            {
                ["name"] = "민수",
                ["code"] = "482913"
            });

        Result<NotificationReceipt> result = await app.Service.SendAsync(command);

        if (result.IsSuccess)
        {
            NotificationReceipt receipt = result.Value;
            Console.WriteLine($"발송 번호: {receipt.Id}");
            Console.WriteLine($"채널: {receipt.Channel}");
            Console.WriteLine($"제목: {receipt.Subject}");
            Console.WriteLine($"상태: {receipt.Status}");
        }
        else
        {
            Console.WriteLine($"발송 실패: {result.Error}");
        }

        Console.WriteLine("--self-test 옵션으로 초보자 검증을 실행할 수 있습니다.");
    }
}

// Composition Root: 어떤 구현을 사용할지 고르고 객체를 한곳에서 연결합니다.
public static class CompositionRoot
{
    public static NotificationApp Build()
    {
        INotificationLogRepository repository = new InMemoryNotificationLogRepository();
        IClock clock = new FixedClock(DateTimeOffset.Parse("2026-07-17T00:00:00+00:00"));
        INotificationSender[] senders = [new EmailSender(), new SmsSender()];

        NotificationTemplate template = new();
        NotificationService service = new(senders, template, repository, clock);
        return new NotificationApp(service, repository);
    }
}

public sealed record NotificationApp(
    NotificationService Service,
    INotificationLogRepository Repository);

// Application Service: 유효성 검사, 전략 선택, 발송, 기록의 순서를 조정합니다.
public sealed class NotificationService(
    IEnumerable<INotificationSender> senders,
    NotificationTemplate template,
    INotificationLogRepository repository,
    IClock clock)
{
    // [실무] 채널에 맞는 전략을 IEnumerable에서 선택합니다. 새 채널은 기존 분기문을
    // 키우기보다 새 INotificationSender 구현을 추가하는 방식으로 확장할 수 있습니다.
    public async ValueTask<Result<NotificationReceipt>> SendAsync(
        SendNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = Validate(command);
        if (validationError is not null)
        {
            return Result<NotificationReceipt>.Fail(validationError);
        }

        INotificationSender? sender = senders.FirstOrDefault(candidate =>
            candidate.Channel == command.Channel);

        if (sender is null)
        {
            return Result<NotificationReceipt>.Fail(
                $"{command.Channel} 채널을 처리할 발송기가 없습니다.");
        }

        NotificationMessage message = template.Render(command.Kind, command.Variables);
        await sender.SendAsync(command.Recipient, message, cancellationToken);

        NotificationLog log = new(
            Guid.NewGuid(),
            command.UserId.Trim(),
            command.Channel,
            command.Recipient.Trim(),
            message.Subject,
            NotificationStatus.Sent,
            clock.UtcNow);

        await repository.SaveAsync(log, cancellationToken);
        return Result<NotificationReceipt>.Ok(new NotificationReceipt(
            log.Id,
            log.Channel,
            log.Subject,
            log.Status));
    }

    private static string? Validate(SendNotificationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return "사용자 ID는 필수입니다.";
        }

        if (string.IsNullOrWhiteSpace(command.Recipient))
        {
            return "수신 주소는 필수입니다.";
        }

        if (command.Channel == NotificationChannel.Email &&
            !command.Recipient.Contains('@', StringComparison.Ordinal))
        {
            return "이메일 주소에는 @가 필요합니다.";
        }

        return null;
    }
}

// Strategy: 각 발송 방식은 같은 계약을 구현하고 자신이 맡은 채널만 처리합니다.
public interface INotificationSender
{
    NotificationChannel Channel { get; }
    ValueTask SendAsync(
        string recipient,
        NotificationMessage message,
        CancellationToken cancellationToken);
}

public sealed class EmailSender : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public ValueTask SendAsync(
        string recipient,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[Email] {recipient}에게 '{message.Subject}' 발송");
        return ValueTask.CompletedTask;
    }
}

public sealed class SmsSender : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public ValueTask SendAsync(
        string recipient,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[SMS] {recipient}에게 '{message.Body}' 발송");
        return ValueTask.CompletedTask;
    }
}

// Template 책임을 분리해 발송 방식과 메시지 작성 규칙이 서로 섞이지 않게 합니다.
public sealed class NotificationTemplate
{
    public NotificationMessage Render(
        NotificationKind kind,
        IReadOnlyDictionary<string, string> variables)
    {
        string name = variables.GetValueOrDefault("name", "사용자");

        return kind switch
        {
            NotificationKind.Welcome => new(
                "가입을 환영합니다",
                $"{name}님, 서비스 가입이 완료되었습니다."),
            NotificationKind.PasswordReset => new(
                "비밀번호 재설정 코드",
                $"{name}님의 인증 코드는 {variables.GetValueOrDefault("code", "미입력")}입니다."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}

public sealed record SendNotificationCommand(
    string UserId,
    string Recipient,
    NotificationChannel Channel,
    NotificationKind Kind,
    IReadOnlyDictionary<string, string> Variables);

public sealed record NotificationMessage(string Subject, string Body);

public sealed record NotificationReceipt(
    Guid Id,
    NotificationChannel Channel,
    string Subject,
    NotificationStatus Status);

public sealed record NotificationLog(
    Guid Id,
    string UserId,
    NotificationChannel Channel,
    string Recipient,
    string Subject,
    NotificationStatus Status,
    DateTimeOffset SentAt);

public sealed record Result<T>(T? Value, string? Error)
{
    // [고급] Result<T>와 nullable 특성은 성공/실패 계약을 호출자와 컴파일러에 알립니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

public enum NotificationChannel { Email, Sms, Push }
public enum NotificationKind { Welcome, PasswordReset }
public enum NotificationStatus { Pending, Sent, Failed }

// Repository: 저장 기술을 계약 뒤에 숨겨 Application 코드와 분리합니다.
public interface INotificationLogRepository
{
    ValueTask SaveAsync(NotificationLog log, CancellationToken cancellationToken);
    ValueTask<NotificationLog?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class InMemoryNotificationLogRepository : INotificationLogRepository
{
    private readonly Dictionary<Guid, NotificationLog> _logs = [];

    public ValueTask SaveAsync(NotificationLog log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logs[log.Id] = log;
        return ValueTask.CompletedTask;
    }

    public ValueTask<NotificationLog?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logs.TryGetValue(id, out NotificationLog? log);
        return ValueTask.FromResult(log);
    }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

public static class SelfTest
{
    // [검증] FixedClock과 메모리 저장소로 외부 환경 없이 같은 결과를 재현합니다.
    public static async Task RunAsync()
    {
        NotificationApp app = CompositionRoot.Build();

        Result<NotificationReceipt> email = await SendAsync(
            app, "minsu@example.com", NotificationChannel.Email);
        Assert(email.IsSuccess, "올바른 이메일 알림은 성공해야 합니다.");
        Assert(email.Value.Channel == NotificationChannel.Email,
            "선택된 전략은 이메일 발송기여야 합니다.");
        Assert(await app.Repository.FindAsync(email.Value.Id, CancellationToken.None) is not null,
            "성공한 발송은 저장소에 기록되어야 합니다.");

        Result<NotificationReceipt> invalidEmail = await SendAsync(
            app, "not-an-email", NotificationChannel.Email);
        Assert(!invalidEmail.IsSuccess, "@가 없는 이메일 주소는 실패해야 합니다.");
        Assert(invalidEmail.Error.Contains('@', StringComparison.Ordinal),
            "오류 메시지는 고칠 지점을 알려 줘야 합니다.");

        Result<NotificationReceipt> sms = await SendAsync(
            app, "010-1234-5678", NotificationChannel.Sms);
        Assert(sms.IsSuccess, "등록된 SMS 전략은 성공해야 합니다.");

        Result<NotificationReceipt> push = await SendAsync(
            app, "device-token", NotificationChannel.Push);
        Assert(!push.IsSuccess, "등록되지 않은 Push 전략은 실패해야 합니다.");
        Assert(push.Error.Contains("발송기", StringComparison.Ordinal),
            "누락된 전략을 설명하는 오류가 필요합니다.");

        Console.WriteLine("초보자 검증 통과: 입력 검사, 전략 선택, 발송, 저장을 모두 확인했습니다.");
    }

    private static ValueTask<Result<NotificationReceipt>> SendAsync(
        NotificationApp app,
        string recipient,
        NotificationChannel channel) => app.Service.SendAsync(new SendNotificationCommand(
            "U-TEST",
            recipient,
            channel,
            NotificationKind.Welcome,
            new Dictionary<string, string> { ["name"] = "테스터" }));

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }
    }
}
