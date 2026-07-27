// 오늘의 읽기 순서: 실행부 → 기본 타입/record → Result → Domain Model → Strategy → Repository → Application Service.
// 한 파일에 모아 초보자가 파일 이동보다 데이터가 객체 사이를 흐르는 과정에 먼저 집중하게 합니다.

var repository = new InMemoryWebhookRepository(
[
    new WebhookSubscription("order-created", "https://shop.example/webhooks/orders", true),
    new WebhookSubscription("payment-failed", "https://billing.example/webhooks/payments", false)
]);
IRetryStrategy retryStrategy = new FixedRetryStrategy(maximumAttempts: 3);
var service = new WebhookDeliveryApplicationService(
    repository, new FakeWebhookClient(), retryStrategy, new ConsoleDeliveryLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

var commands = new[]
{
    new DeliverWebhookCommand("MSG-100", "order-created", """{"orderId":100}"""),
    new DeliverWebhookCommand("MSG-101", "payment-failed", """{"paymentId":7}"""),
    new DeliverWebhookCommand("MSG-102", "unknown", "{}")
};

foreach (var command in commands)
{
    var result = await service.DeliverAsync(command, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"성공: {result.Value!.MessageId}, 시도 {result.Value.AttemptCount}회"
        : $"실패: {result.Error}");
}

var enabledTopics = (await repository.GetAllAsync(CancellationToken.None))
    .Where(subscription => subscription.IsEnabled)
    .OrderBy(subscription => subscription.Topic)
    .Select(subscription => subscription.Topic);
Console.WriteLine($"활성 주제: {string.Join(", ", enabledTopics)}");

// enum은 허용 상태를 제한해 오타가 있는 문자열 상태가 시스템 전체로 퍼지는 것을 막습니다.
enum DeliveryStatus { Delivered }

// record는 명령과 결과처럼 값 자체가 중요한 메시지를 간결하고 불변에 가깝게 표현합니다.
sealed record DeliverWebhookCommand(string MessageId, string Topic, string Payload);
sealed record DeliveryReceipt(string MessageId, int AttemptCount, DeliveryStatus Status);

// Result는 구독 없음처럼 호출자가 처리할 수 있는 예상된 실패를 예외와 구별합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

sealed class WebhookSubscription
{
    public string Topic { get; }
    public Uri Endpoint { get; }
    public bool IsEnabled { get; }

    public WebhookSubscription(string topic, string endpoint, bool isEnabled)
    {
        // 생성자 검증은 잘못된 도메인 객체가 이후 계층으로 흘러가는 것을 초기에 차단합니다.
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        Topic = topic;
        Endpoint = new Uri(endpoint, UriKind.Absolute);
        IsEnabled = isEnabled;
    }

    public Result<Uri> GetDeliveryEndpoint() =>
        IsEnabled
            ? Result<Uri>.Success(Endpoint)
            : Result<Uri>.Failure("비활성 구독에는 전송할 수 없습니다.");
}

// Strategy는 재시도 규칙을 분리해 지수 backoff 같은 정책으로 서비스 수정 없이 교체하게 합니다(OCP).
interface IRetryStrategy
{
    IEnumerable<int> Attempts();
}

sealed class FixedRetryStrategy(int maximumAttempts) : IRetryStrategy
{
    public IEnumerable<int> Attempts()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAttempts);
        return Enumerable.Range(1, maximumAttempts);
    }
}

// Repository는 저장 기술을 숨겨 Application Service가 메모리나 SQL 구현에 의존하지 않게 합니다(DIP).
interface IWebhookRepository
{
    Task<WebhookSubscription?> FindByTopicAsync(string topic, CancellationToken cancellationToken);
    Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken);
}

sealed class InMemoryWebhookRepository(IEnumerable<WebhookSubscription> seed) : IWebhookRepository
{
    private readonly Dictionary<string, WebhookSubscription> _subscriptions =
        seed.ToDictionary(item => item.Topic, StringComparer.OrdinalIgnoreCase);

    public Task<WebhookSubscription?> FindByTopicAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _subscriptions.TryGetValue(topic, out var subscription);
        // nullable 반환은 "찾지 못함"도 정상 조회 결과임을 타입에 표시합니다.
        return Task.FromResult(subscription);
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WebhookSubscription>>(_subscriptions.Values.ToArray());
    }
}

interface IWebhookClient
{
    Task<bool> SendAsync(Uri endpoint, string payload, int attempt, CancellationToken cancellationToken);
}

sealed class FakeWebhookClient : IWebhookClient
{
    public async Task<bool> SendAsync(
        Uri endpoint,
        string payload,
        int attempt,
        CancellationToken cancellationToken)
    {
        // 실제 HTTP 대기를 흉내 내며, await는 기다리는 동안 스레드를 붙잡지 않습니다.
        await Task.Delay(10, cancellationToken);
        return endpoint.Host.StartsWith("shop", StringComparison.OrdinalIgnoreCase) && attempt >= 2;
    }
}

interface IDeliveryLog
{
    void Delivered(DeliveryReceipt receipt);
}

sealed class ConsoleDeliveryLog : IDeliveryLog
{
    public void Delivered(DeliveryReceipt receipt) =>
        // UTC와 메시지 ID를 남기면 여러 서버의 로그를 시간순으로 맞추고 요청을 추적하기 쉽습니다.
        Console.WriteLine($"전송 로그: {receipt.MessageId}, UTC={DateTimeOffset.UtcNow:O}");
}

// Application Service는 조회→도메인 검증→재시도→기록의 사용 사례 순서만 조정합니다(SRP).
sealed class WebhookDeliveryApplicationService(
    IWebhookRepository repository,
    IWebhookClient client,
    IRetryStrategy retryStrategy,
    IDeliveryLog deliveryLog)
{
    public async Task<Result<DeliveryReceipt>> DeliverAsync(
        DeliverWebhookCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.MessageId) || string.IsNullOrWhiteSpace(command.Payload))
            return Result<DeliveryReceipt>.Failure("메시지 ID와 payload는 필수입니다.");

        var subscription = await repository.FindByTopicAsync(command.Topic, cancellationToken);
        if (subscription is null)
            return Result<DeliveryReceipt>.Failure("구독을 찾을 수 없습니다.");

        var endpointResult = subscription.GetDeliveryEndpoint();
        if (!endpointResult.IsSuccess)
            return Result<DeliveryReceipt>.Failure(endpointResult.Error!);

        foreach (var attempt in retryStrategy.Attempts())
        {
            if (await client.SendAsync(endpointResult.Value!, command.Payload, attempt, cancellationToken))
            {
                var receipt = new DeliveryReceipt(command.MessageId, attempt, DeliveryStatus.Delivered);
                deliveryLog.Delivered(receipt);
                return Result<DeliveryReceipt>.Success(receipt);
            }
        }

        // 반복 실패는 업무적으로 보고 가능한 Result이며, 취소·네트워크 라이브러리 장애는 예외로 전파합니다.
        return Result<DeliveryReceipt>.Failure("최대 재시도 횟수 안에 전송하지 못했습니다.");
    }
}

static class SelfTest
{
    public static async Task RunAsync()
    {
        var repository = new InMemoryWebhookRepository(
        [
            new("active", "https://shop.example/hook", true),
            new("disabled", "https://shop.example/hook", false)
        ]);
        var service = new WebhookDeliveryApplicationService(
            repository, new FakeWebhookClient(), new FixedRetryStrategy(3), new ConsoleDeliveryLog());

        var cases = new[]
        {
            ("재시도 후 성공", await service.DeliverAsync(new("T-1", "active", "{}"), default), true),
            ("비활성 구독", await service.DeliverAsync(new("T-2", "disabled", "{}"), default), false),
            ("없는 구독", await service.DeliverAsync(new("T-3", "missing", "{}"), default), false),
            ("잘못된 입력", await service.DeliverAsync(new("", "active", "{}"), default), false)
        };

        foreach (var (name, result, expected) in cases)
        {
            if (result.IsSuccess != expected)
                throw new InvalidOperationException($"{name} 검증 실패");
            Console.WriteLine($"PASS: {name}");
        }
    }
}
