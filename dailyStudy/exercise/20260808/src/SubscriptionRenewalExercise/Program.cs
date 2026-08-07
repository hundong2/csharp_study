// 오늘 예제는 만료가 임박한 구독을 조회하고 갱신 금액을 계산한 뒤 결제를 요청합니다.
// 데이터, 업무 규칙, 작업 순서, 외부 결제를 분리해 실제 .NET 서비스의 기본 구조를 작게 연습합니다.

var repository = new InMemorySubscriptionRepository(
[
    new Subscription("SUB-101", "basic", 12_000m, true, DateOnly.Parse("2026-08-08"), "pay-ok"),
    new Subscription("SUB-102", "pro", 25_000m, true, DateOnly.Parse("2026-08-09"), "pay-declined"),
    new Subscription("SUB-103", null, 9_000m, false, DateOnly.Parse("2026-08-08"), "pay-unused")
]);

// Composition Root는 프로그램 시작점에서 구현 객체를 조립합니다.
// 서비스가 객체 생성법을 몰라도 되므로 테스트에서는 가짜 구현으로 쉽게 교체할 수 있습니다.
IRenewalPricePolicy pricePolicy = new PercentageDiscountPolicy("pro", 10m);
var service = new RenewSubscriptionsService(repository, pricePolicy, new ConsolePaymentGateway());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(DateOnly.Parse("2026-08-08"), CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"처리 완료: 성공 {result.Value.Succeeded}건, 거절 {result.Value.Declined}건, 건너뜀 {result.Value.Skipped}건"
    : $"처리 실패: {result.Error}");

// record는 값 중심 데이터를 간결하게 표현하며 init 전용 속성처럼 생성 후 변경을 줄여 추적을 쉽게 합니다.
// Plan은 외부 입력이라 누락될 수 있으므로 string?로 표시하고 정책에서 명시적으로 검증합니다.
public sealed record Subscription(
    string Id,
    string? Plan,
    decimal MonthlyPrice,
    bool AutoRenew,
    DateOnly RenewalDate,
    string PaymentMethodId);

public sealed record RenewalCharge(string SubscriptionId, decimal Amount, string PaymentMethodId);
public sealed record RenewalSummary(int Succeeded, int Declined, int Skipped);

// 예상 가능한 업무 실패는 Result로 반환하면 호출자가 예외 처리 없이 성공과 실패를 모두 읽을 수 있습니다.
// 연결 끊김 같은 예상 밖 기술 장애는 예외로 전파한 뒤 애플리케이션 경계에서 안전한 메시지로 바꿉니다.
public sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default!, error);
}

public interface ISubscriptionRepository
{
    Task<IReadOnlyList<Subscription>> GetDueAsync(DateOnly today, CancellationToken cancellationToken);
}

public interface IRenewalPricePolicy
{
    Result<decimal> Calculate(Subscription subscription);
}

public interface IPaymentGateway
{
    Task<Result<bool>> ChargeAsync(RenewalCharge charge, CancellationToken cancellationToken);
}

// Strategy로 가격 정책을 분리하면 할인 규칙이 추가되어도 작업 순서를 담당하는 서비스를 고치지 않아도 됩니다.
// 이는 SOLID의 단일 책임, 개방-폐쇄, 의존 역전 원칙을 작은 코드에서 보여 줍니다.
public sealed class PercentageDiscountPolicy(string discountedPlan, decimal discountPercent) : IRenewalPricePolicy
{
    public Result<decimal> Calculate(Subscription subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Plan))
            return Result<decimal>.Failure("요금제가 필요합니다.");

        if (subscription.MonthlyPrice <= 0)
            return Result<decimal>.Failure("월 요금은 0보다 커야 합니다.");

        var rate = subscription.Plan.Equals(discountedPlan, StringComparison.OrdinalIgnoreCase)
            ? 1m - discountPercent / 100m
            : 1m;
        return Result<decimal>.Success(decimal.Round(subscription.MonthlyPrice * rate, 0));
    }
}

// Application Service는 조회 → 검증/계산 → 결제라는 유스케이스 순서만 조정합니다.
// 생성자 매개변수로 계약을 주입받아 저장소나 결제사의 세부 구현과 분리합니다.
public sealed class RenewSubscriptionsService(
    ISubscriptionRepository repository,
    IRenewalPricePolicy pricePolicy,
    IPaymentGateway paymentGateway)
{
    public async Task<Result<RenewalSummary>> ExecuteAsync(DateOnly today, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptions = await repository.GetDueAsync(today, cancellationToken);

            // LINQ는 자동 갱신 대상만 고르고 날짜와 ID 순으로 정렬합니다. ToArray에서 쿼리가 실제 실행됩니다.
            var candidates = subscriptions
                .Where(item => item.AutoRenew)
                .OrderBy(item => item.RenewalDate)
                .ThenBy(item => item.Id)
                .ToArray();

            var succeeded = 0;
            var declined = 0;
            var skipped = subscriptions.Count - candidates.Length;

            foreach (var subscription in candidates)
            {
                var price = pricePolicy.Calculate(subscription);
                if (!price.IsSuccess)
                {
                    Console.WriteLine($"{subscription.Id}: 건너뜀 ({price.Error})");
                    skipped++;
                    continue;
                }

                var charge = new RenewalCharge(subscription.Id, price.Value, subscription.PaymentMethodId);
                var payment = await paymentGateway.ChargeAsync(charge, cancellationToken);
                if (payment.IsSuccess) succeeded++;
                else declined++;
            }

            return Result<RenewalSummary>.Success(new(succeeded, declined, skipped));
        }
        catch (OperationCanceledException)
        {
            // 취소는 장애가 아니라 상위 호출자의 제어 신호이므로 Result로 감추지 않고 다시 전달합니다.
            throw;
        }
        catch (Exception ex)
        {
            // 실무에서는 구조화 로그에 원본 예외를 남기고 사용자에게는 내부 정보가 없는 메시지를 반환합니다.
            return Result<RenewalSummary>.Failure($"구독 갱신 중 기술 오류: {ex.Message}");
        }
    }
}

public sealed class InMemorySubscriptionRepository(IEnumerable<Subscription> seed) : ISubscriptionRepository
{
    private readonly IReadOnlyList<Subscription> _subscriptions = seed.ToArray();

    public Task<IReadOnlyList<Subscription>> GetDueAsync(DateOnly today, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Subscription> due = _subscriptions.Where(item => item.RenewalDate <= today.AddDays(1)).ToArray();
        return Task.FromResult(due);
    }
}

public sealed class ConsolePaymentGateway : IPaymentGateway
{
    public Task<Result<bool>> ChargeAsync(RenewalCharge charge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var approved = charge.PaymentMethodId != "pay-declined";
        Console.WriteLine($"{charge.SubscriptionId}: {charge.Amount:N0}원 결제 {(approved ? "승인" : "거절")}");
        return Task.FromResult(approved ? Result<bool>.Success(true) : Result<bool>.Failure("결제가 거절되었습니다."));
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new PercentageDiscountPolicy("pro", 10m);
        Check(policy.Calculate(Item("A", "pro", 10_000m, true)).Value == 9_000m, "할인 계산", ref passed);
        Check(!policy.Calculate(Item("B", null, 10_000m, true)).IsSuccess, "nullable 검증", ref passed);

        var gateway = new CollectingPaymentGateway();
        var repository = new InMemorySubscriptionRepository([Item("C", "basic", 10_000m, true), Item("D", "basic", 10_000m, false)]);
        var result = await new RenewSubscriptionsService(repository, policy, gateway).ExecuteAsync(new DateOnly(2026, 8, 8), CancellationToken.None);
        Check(result.Value.Succeeded == 1 && result.Value.Skipped == 1, "서비스 흐름", ref passed);
        Check(gateway.Charges.Single().SubscriptionId == "C", "DI 가짜 결제", ref passed);
        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static Subscription Item(string id, string? plan, decimal price, bool autoRenew) =>
        new(id, plan, price, autoRenew, new DateOnly(2026, 8, 8), "test-payment");

    private static void Check(bool condition, string name, ref int passed)
    {
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }

    private sealed class CollectingPaymentGateway : IPaymentGateway
    {
        public List<RenewalCharge> Charges { get; } = [];
        public Task<Result<bool>> ChargeAsync(RenewalCharge charge, CancellationToken cancellationToken)
        {
            Charges.Add(charge);
            return Task.FromResult(Result<bool>.Success(true));
        }
    }
}
