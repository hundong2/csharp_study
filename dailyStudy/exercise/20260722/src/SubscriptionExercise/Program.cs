// 초보자 읽기 순서: ① 맨 아래 Program ② Subscription ③ Result ④ Strategy/Repository ⑤ Service ⑥ CompositionRoot.
// 한 파일에 모은 이유는 처음에는 파일 이동보다 객체가 협력하는 흐름에 집중하기 위해서입니다.

var service = CompositionRoot.CreateService();

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await BeginnerTests.RunAsync();
    return;
}

// [기초] new는 객체를 만들고, var는 오른쪽 값으로 컴파일러가 타입을 추론합니다. 타입이 사라지는 동적 문법은 아닙니다.
var subscriptions = new[]
{
    new Subscription(Guid.NewGuid(), " beginner@example.com ", Plan.Basic, new DateOnly(2026, 7, 22)),
    new Subscription(Guid.NewGuid(), "team@example.com", Plan.Team, new DateOnly(2026, 7, 25))
};

// [기초] foreach는 컬렉션의 각 값을 순서대로 꺼냅니다. await는 I/O가 끝날 때까지 스레드를 붙잡지 않고 기다립니다.
foreach (var subscription in subscriptions)
{
    var result = await service.RenewAsync(subscription, CancellationToken.None);
    Console.WriteLine(result.IsSuccess ? $"성공: {result.Value}" : $"실패: {result.Error}");
}

// enum은 허용된 선택지를 이름으로 제한하여 "Basic" 같은 오타 문자열을 줄입니다.
enum Plan { Basic, Team }

// record는 값 중심 데이터에 적합합니다. 생성 후 속성을 바꾸지 않아 여러 계층이 같은 값을 안전하게 공유합니다.
sealed record Subscription(Guid Id, string Email, Plan Plan, DateOnly RenewalDate)
{
    public Result<Subscription> Validate()
    {
        // 문자열 메서드와 &&(그리고)를 함께 사용해 업무 입력을 검증합니다.
        var normalizedEmail = Email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length == 0 || !normalizedEmail.Contains('@'))
            return Result<Subscription>.Failure("올바른 이메일이 필요합니다.");

        // with는 원본 record를 바꾸지 않고 일부 값만 바꾼 복사본을 만듭니다.
        return Result<Subscription>.Success(this with { Email = normalizedEmail });
    }
}

// 예상 가능한 업무 실패는 Result로 반환합니다. 호출자가 예외 처리 없이 성공과 실패를 모두 보게 하기 위함입니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Strategy는 바뀔 수 있는 가격 규칙을 인터페이스 뒤에 둡니다. 새 요금제를 추가해도 서비스 흐름을 덜 수정하게 됩니다(OCP).
interface IRenewalPriceStrategy
{
    decimal Calculate(Subscription subscription);
}

sealed class RenewalPriceStrategy : IRenewalPriceStrategy
{
    public decimal Calculate(Subscription subscription) => subscription.Plan switch
    {
        Plan.Basic => 9_900m,
        Plan.Team => 29_000m,
        // enum 값이 늘었는데 규칙을 빠뜨리면 운영 오류가 되므로 명시적으로 예외를 발생시킵니다.
        _ => throw new ArgumentOutOfRangeException(nameof(subscription.Plan))
    };
}

// Repository는 저장 기술이 아니라 도메인이 필요한 동작을 표현합니다. 테스트에서는 메모리 구현으로 교체할 수 있습니다(DIP).
interface ISubscriptionRepository
{
    Task<bool> WasRenewedAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(Renewal renewal, CancellationToken cancellationToken);
    Task<IReadOnlyList<Renewal>> ListAsync(CancellationToken cancellationToken);
}

// 결제 완료 사실은 바뀌지 않아야 하므로 불변 record로 모델링합니다. decimal은 돈 계산에서 이진 부동소수 오차를 피합니다.
sealed record Renewal(Guid SubscriptionId, decimal Amount, DateTimeOffset RenewedAt);

sealed class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly List<Renewal> _renewals = [];

    public Task<bool> WasRenewedAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // LINQ Any는 "조건을 만족하는 항목이 하나라도 있는가"라는 의도를 반복문보다 직접 표현합니다.
        return Task.FromResult(_renewals.Any(x => x.SubscriptionId == id));
    }

    public Task SaveAsync(Renewal renewal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _renewals.Add(renewal);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Renewal>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // ToArray로 복사해 내부 List가 외부에서 변경되지 않게 합니다.
        return Task.FromResult<IReadOnlyList<Renewal>>(_renewals.ToArray());
    }
}

// 시간도 의존성으로 주입하면 테스트가 실행 시각에 흔들리지 않습니다.
interface IClock { DateTimeOffset UtcNow { get; } }
sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

// Application Service는 한 유스케이스의 순서만 조정하고 검증·가격·저장은 각 객체에 위임합니다(SRP).
sealed class RenewalService(
    ISubscriptionRepository repository,
    IRenewalPriceStrategy priceStrategy,
    IClock clock)
{
    public async Task<Result<Renewal>> RenewAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        // null은 개발 계약 위반이므로 예외, 잘못된 이메일이나 중복 갱신은 예상 가능한 실패이므로 Result를 사용합니다.
        ArgumentNullException.ThrowIfNull(subscription);
        var validation = subscription.Validate();
        if (!validation.IsSuccess || validation.Value is null)
            return Result<Renewal>.Failure(validation.Error ?? "검증에 실패했습니다.");

        if (await repository.WasRenewedAsync(subscription.Id, cancellationToken))
            return Result<Renewal>.Failure("이미 갱신된 구독입니다.");

        var renewal = new Renewal(subscription.Id, priceStrategy.Calculate(validation.Value), clock.UtcNow);
        await repository.SaveAsync(renewal, cancellationToken);
        return Result<Renewal>.Success(renewal);
    }
}

// Composition Root는 구현을 선택하고 연결하는 유일한 장소입니다. 비즈니스 코드가 new에 묶이지 않게 합니다.
static class CompositionRoot
{
    public static RenewalService CreateService() =>
        new(new InMemorySubscriptionRepository(), new RenewalPriceStrategy(), new SystemClock());
}

static class BeginnerTests
{
    public static async Task RunAsync()
    {
        var tests = new List<(string Name, Func<Task<bool>> Check)>
        {
            ("Basic 가격", () => PriceIsAsync(Plan.Basic, 9_900m)),
            ("Team 가격", () => PriceIsAsync(Plan.Team, 29_000m)),
            ("이메일 정규화", EmailIsNormalizedAsync),
            ("중복 갱신 거부", DuplicateIsRejectedAsync)
        };

        var passed = 0;
        foreach (var (name, check) in tests)
        {
            var ok = await check();
            Console.WriteLine($"[{(ok ? "통과" : "실패")}] {name}");
            if (ok) passed++;
        }

        Console.WriteLine($"초보자 검증 {(passed == tests.Count ? "통과" : "실패")} ({passed}/{tests.Count})");
        if (passed != tests.Count) Environment.ExitCode = 1;
    }

    private static async Task<bool> PriceIsAsync(Plan plan, decimal expected)
    {
        var result = await CompositionRoot.CreateService().RenewAsync(
            new Subscription(Guid.NewGuid(), "test@example.com", plan, new DateOnly(2026, 7, 22)), CancellationToken.None);
        return result.IsSuccess && result.Value?.Amount == expected;
    }

    private static Task<bool> EmailIsNormalizedAsync()
    {
        var result = new Subscription(Guid.NewGuid(), " USER@Example.COM ", Plan.Basic, new DateOnly(2026, 7, 22)).Validate();
        return Task.FromResult(result.Value?.Email == "user@example.com");
    }

    private static async Task<bool> DuplicateIsRejectedAsync()
    {
        var service = CompositionRoot.CreateService();
        var subscription = new Subscription(Guid.NewGuid(), "test@example.com", Plan.Basic, new DateOnly(2026, 7, 22));
        await service.RenewAsync(subscription, CancellationToken.None);
        var second = await service.RenewAsync(subscription, CancellationToken.None);
        return !second.IsSuccess && second.Error?.Contains("이미") == true;
    }
}
