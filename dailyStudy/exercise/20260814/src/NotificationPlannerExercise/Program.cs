// 오늘 예제는 고객의 연락처와 선호도를 검사해 알림 발송 계획을 저장하는 작은 실무 프로그램입니다.
// 위에서 아래로 읽으면 데이터 모델 → 업무 규칙 → 실행 순서 → 테스트의 책임 분리를 자연스럽게 볼 수 있습니다.

var requests = new[]
{
    new NotificationRequest("EVT-101", "customer-1", "배송이 시작되었습니다.", "kim@example.com", "010-1111-2222", ChannelPreference.Email),
    new NotificationRequest("EVT-102", "customer-2", "결제가 완료되었습니다.", null, "010-3333-4444", ChannelPreference.Email),
    new NotificationRequest("EVT-103", "customer-3", "쿠폰이 발급되었습니다.", null, null, ChannelPreference.Any),
    new NotificationRequest("EVT-101", "customer-1", "중복 이벤트입니다.", "kim@example.com", null, ChannelPreference.Email)
};

// Composition Root는 프로그램 시작점에서 구현 객체를 한 번 조립합니다. 업무 클래스가 구체 클래스 생성법을 몰라 테스트 대역으로 교체하기 쉽습니다.
INotificationChannelStrategy strategy = new PreferredChannelStrategy();
var repository = new InMemoryNotificationRepository();
var service = new PlanNotificationsService(repository, strategy, new ConsoleOperationLogger());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var summary = await service.ExecuteAsync(requests, CancellationToken.None);
Console.WriteLine($"처리 완료: 계획 {summary.PlannedCount}건, 제외 {summary.Errors.Count}건");
foreach (var error in summary.Errors)
{
    Console.WriteLine($"- {error.EventId}: {error.Message}");
}

// record는 값 중심 데이터를 간결하게 표현하며 init 전용 속성 덕분에 생성 뒤 뜻밖의 변경을 줄입니다.
// string?는 값이 없을 수 있음을 형식에 드러내므로, 컴파일러가 null 검사 누락을 경고할 수 있습니다.
sealed record NotificationRequest(
    string EventId,
    string CustomerId,
    string Message,
    string? Email,
    string? PhoneNumber,
    ChannelPreference Preference);

sealed record NotificationPlan(string EventId, string CustomerId, string Message, DeliveryChannel Channel, string Destination);
sealed record PlanningError(string EventId, string Message);
sealed record PlanningSummary(int PlannedCount, IReadOnlyList<PlanningError> Errors);

// enum은 허용되는 선택지를 제한해 "emali" 같은 오타가 업무 상태로 흘러드는 것을 막습니다.
enum ChannelPreference { Email, Sms, Any }
enum DeliveryChannel { Email, Sms }

// 예상 가능한 입력 실패는 Result로 반환합니다. 예외를 정상 분기로 남용하지 않아 호출자가 실패 처리를 빠뜨리기 어렵습니다.
// 반대로 저장소 장애나 취소처럼 정상 업무 흐름 밖의 문제는 예외로 전파해 상위 계층에서 로깅·재시도 여부를 결정하게 합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Repository는 저장 기술을 감춘 계약입니다. 메모리 구현을 DB 구현으로 바꿔도 Application Service는 수정할 필요가 없습니다(DIP).
interface INotificationRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken);
    Task SaveAsync(NotificationPlan plan, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationPlan>> GetAllAsync(CancellationToken cancellationToken);
}

// Strategy는 채널 선택 규칙을 교체 가능한 객체로 분리합니다. 새 정책을 추가할 때 실행 흐름을 고치지 않아도 됩니다(OCP).
interface INotificationChannelStrategy
{
    Result<NotificationPlan> CreatePlan(NotificationRequest request);
}

interface IOperationLogger
{
    void Planned(NotificationPlan plan);
}

sealed class PreferredChannelStrategy : INotificationChannelStrategy
{
    public Result<NotificationPlan> CreatePlan(NotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.Message))
        {
            return Result<NotificationPlan>.Failure("이벤트 ID와 메시지는 필수입니다.");
        }

        // switch 식은 선호도와 연락처 존재 여부를 하나의 값으로 매핑합니다. _는 앞 조건에 해당하지 않는 나머지를 뜻합니다.
        var destination = request.Preference switch
        {
            ChannelPreference.Email when !string.IsNullOrWhiteSpace(request.Email) =>
                (DeliveryChannel.Email, request.Email),
            ChannelPreference.Sms when !string.IsNullOrWhiteSpace(request.PhoneNumber) =>
                (DeliveryChannel.Sms, request.PhoneNumber),
            ChannelPreference.Any when !string.IsNullOrWhiteSpace(request.Email) =>
                (DeliveryChannel.Email, request.Email),
            ChannelPreference.Any when !string.IsNullOrWhiteSpace(request.PhoneNumber) =>
                (DeliveryChannel.Sms, request.PhoneNumber),
            _ => ((DeliveryChannel Channel, string Destination)?)null
        };

        if (destination is null)
        {
            return Result<NotificationPlan>.Failure("선호 채널에 사용할 연락처가 없습니다.");
        }

        return Result<NotificationPlan>.Success(new(
            request.EventId, request.CustomerId, request.Message, destination.Value.Channel, destination.Value.Destination));
    }
}

// Application Service는 '중복 확인 → 규칙 적용 → 저장 → 기록'의 순서만 조정합니다. 각 세부 책임은 협력 객체에 맡깁니다(SRP).
sealed class PlanNotificationsService(
    INotificationRepository repository,
    INotificationChannelStrategy strategy,
    IOperationLogger logger)
{
    public async Task<PlanningSummary> ExecuteAsync(
        IEnumerable<NotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        var errors = new List<PlanningError>();
        var planned = new List<NotificationPlan>();

        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // EventId 중복 검사는 재실행 때 같은 알림이 두 번 저장되는 것을 줄이는 멱등성 경계입니다.
            if (await repository.ExistsAsync(request.EventId, cancellationToken))
            {
                errors.Add(new(request.EventId, "이미 처리한 이벤트입니다."));
                continue;
            }

            var result = strategy.CreatePlan(request);
            if (!result.IsSuccess || result.Value is null)
            {
                errors.Add(new(request.EventId, result.Error ?? "알 수 없는 오류"));
                continue;
            }

            await repository.SaveAsync(result.Value, cancellationToken);
            logger.Planned(result.Value);
            planned.Add(result.Value);
        }

        // LINQ의 GroupBy와 Select는 결과를 채널별로 집계하는 방법을 보여 줍니다. 실제 운영에서는 이 값을 메트릭으로 보낼 수 있습니다.
        var channelCounts = planned.GroupBy(plan => plan.Channel).Select(group => $"{group.Key}={group.Count()}");
        Console.WriteLine($"[메트릭] {string.Join(", ", channelCounts)}");
        return new(planned.Count, errors);
    }
}

sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly Dictionary<string, NotificationPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_plans.ContainsKey(eventId));
    }

    public Task SaveAsync(NotificationPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _plans.Add(plan.EventId, plan);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationPlan>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<NotificationPlan>>(_plans.Values.ToArray());
    }
}

sealed class ConsoleOperationLogger : IOperationLogger
{
    public void Planned(NotificationPlan plan) =>
        // 운영 로그에는 이메일·전화번호 같은 개인정보를 남기지 않고 추적 가능한 이벤트 ID와 채널만 기록합니다.
        Console.WriteLine($"[계획] event={plan.EventId}, channel={plan.Channel}");
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var strategy = new PreferredChannelStrategy();
        var passed = 0;

        Check(strategy.CreatePlan(new("1", "c", "m", "a@b.com", null, ChannelPreference.Email)).IsSuccess, "이메일 선택");
        passed++;
        Check(!strategy.CreatePlan(new("2", "c", "m", null, null, ChannelPreference.Any)).IsSuccess, "연락처 누락 거절");
        passed++;

        var repository = new InMemoryNotificationRepository();
        var service = new PlanNotificationsService(repository, strategy, new SilentLogger());
        var duplicate = new NotificationRequest("3", "c", "m", "a@b.com", null, ChannelPreference.Email);
        var summary = await service.ExecuteAsync([duplicate, duplicate], CancellationToken.None);
        Check(summary.PlannedCount == 1 && summary.Errors.Count == 1, "중복 방지");
        passed++;
        Check((await repository.GetAllAsync(CancellationToken.None)).Count == 1, "저장 결과");
        passed++;

        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 복구할 업무 결과가 아니라 코드 결함이므로 예외를 던져 실행과 CI를 즉시 실패시킵니다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
    }

    private sealed class SilentLogger : IOperationLogger
    {
        public void Planned(NotificationPlan plan) { }
    }
}
