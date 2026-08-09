// 장바구니를 조회하고 쿠폰을 적용해 가격을 계산하는 작은 실무 예제입니다.
// 데이터·규칙·작업 순서·저장 기술을 분리하는 이유까지 함께 연습합니다.
var repository = new InMemoryCartRepository([
    new Cart("CART-101", "SAVE10", [new CartLine("keyboard", 45_000m, 1), new CartLine("mouse", 20_000m, 2)]),
    new Cart("CART-102", null, [new CartLine("monitor", 300_000m, 1)]),
    new Cart("CART-103", "UNKNOWN", [new CartLine("cable", 8_000m, 0)])
]);

// Composition Root는 시작점에서 구현을 조립합니다. 서비스가 생성법을 몰라 테스트 대역으로 교체하기 쉽습니다.
IDiscountStrategy strategy = new CouponDiscountStrategy("SAVE10", 10m);
var service = new PriceCartService(repository, strategy);
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) { await SelfTests.RunAsync(); return; }

foreach (var id in new[] { "CART-101", "CART-102", "CART-103", "MISSING" })
{
    var result = await service.ExecuteAsync(id, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"{id}: 소계 {result.Value.Subtotal:N0}원, 할인 {result.Value.Discount:N0}원, 결제 {result.Value.Total:N0}원"
        : $"{id}: 계산 실패 ({result.Error})");
}

// record는 값 중심 데이터에 알맞고 생성 뒤 변경을 줄입니다. 쿠폰은 없을 수 있어 string?로 표시합니다.
public sealed record Cart(string Id, string? CouponCode, IReadOnlyList<CartLine> Lines);
public sealed record CartLine(string ProductId, decimal UnitPrice, int Quantity);
public sealed record PriceQuote(string CartId, decimal Subtotal, decimal Discount, decimal Total);

// 예상 가능한 업무 실패는 Result로 반환해 호출자가 성공과 실패를 명시적으로 처리하게 합니다.
public sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default!, error);
}

public interface ICartRepository { Task<Cart?> FindAsync(string id, CancellationToken cancellationToken); }
public interface IDiscountStrategy { Result<decimal> Calculate(string? couponCode, decimal subtotal); }

// Strategy는 할인 규칙을 서비스에서 분리합니다. SRP·OCP·DIP를 지키며 정책 추가 시 흐름 수정을 줄입니다.
public sealed class CouponDiscountStrategy(string supportedCode, decimal percent) : IDiscountStrategy
{
    public Result<decimal> Calculate(string? couponCode, decimal subtotal)
    {
        if (subtotal < 0) return Result<decimal>.Failure("소계는 음수일 수 없습니다.");
        if (string.IsNullOrWhiteSpace(couponCode)) return Result<decimal>.Success(0m);
        if (!couponCode.Equals(supportedCode, StringComparison.OrdinalIgnoreCase))
            return Result<decimal>.Failure("지원하지 않는 쿠폰입니다.");
        return Result<decimal>.Success(decimal.Round(subtotal * percent / 100m, 0));
    }
}

// Application Service는 조회 → 검증 → 합계 → 할인 순서만 조정하고 계약을 생성자로 주입받습니다.
public sealed class PriceCartService(ICartRepository repository, IDiscountStrategy strategy)
{
    public async Task<Result<PriceQuote>> ExecuteAsync(string cartId, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await repository.FindAsync(cartId, cancellationToken);
            if (cart is null) return Result<PriceQuote>.Failure("장바구니를 찾을 수 없습니다.");
            if (cart.Lines.Count == 0 || cart.Lines.Any(x => x.UnitPrice < 0 || x.Quantity <= 0))
                return Result<PriceQuote>.Failure("가격은 0 이상, 수량은 1 이상이어야 합니다.");

            // LINQ Sum은 각 항목 금액의 합이라는 업무 의도를 짧고 분명하게 보여 줍니다.
            var subtotal = cart.Lines.Sum(x => x.UnitPrice * x.Quantity);
            var discount = strategy.Calculate(cart.CouponCode, subtotal);
            if (!discount.IsSuccess) return Result<PriceQuote>.Failure(discount.Error!);
            return Result<PriceQuote>.Success(new(cart.Id, subtotal, discount.Value, subtotal - discount.Value));
        }
        catch (OperationCanceledException) { throw; } // 취소는 장애가 아니라 호출자의 제어 신호이므로 감추지 않습니다.
        catch (Exception ex)
        {
            // 예상 밖 기술 장애는 예외입니다. 실무에서는 원본을 로그에 남기고 외부에는 안전한 메시지만 줍니다.
            return Result<PriceQuote>.Failure($"가격 계산 중 기술 오류: {ex.Message}");
        }
    }
}

public sealed class InMemoryCartRepository(IEnumerable<Cart> seed) : ICartRepository
{
    private readonly IReadOnlyDictionary<string, Cart> _carts = seed.ToDictionary(x => x.Id);
    public Task<Cart?> FindAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _carts.TryGetValue(id, out var cart);
        return Task.FromResult(cart);
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new CouponDiscountStrategy("SAVE10", 10m);
        Check(policy.Calculate(null, 10_000m).Value == 0m, "nullable 쿠폰", ref passed);
        Check(policy.Calculate("SAVE10", 10_000m).Value == 1_000m, "할인", ref passed);
        var repo = new InMemoryCartRepository([new Cart("A", "SAVE10", [new CartLine("P", 5_000m, 2)])]);
        var result = await new PriceCartService(repo, policy).ExecuteAsync("A", CancellationToken.None);
        Check(result.IsSuccess && result.Value.Total == 9_000m, "서비스", ref passed);
        var missing = await new PriceCartService(repo, policy).ExecuteAsync("X", CancellationToken.None);
        Check(!missing.IsSuccess, "없는 장바구니", ref passed);
        Console.WriteLine($"self-test: {passed}/4 통과");
    }
    private static void Check(bool condition, string name, ref int passed)
    {
        // 실패를 예외로 즉시 알리면 자동 빌드가 잘못된 결과를 놓치지 않습니다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }
}
