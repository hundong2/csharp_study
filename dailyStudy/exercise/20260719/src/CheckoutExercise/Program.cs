using System.Diagnostics.CodeAnalysis;
using System.Text;

// 학습 순서: ① SyntaxTour의 타입·nullable·switch·컬렉션,
// ② CheckoutDemo의 호출법, ③ Service/Strategy/Result, ④ SelfTest입니다.
// [고급 관점] 할인 규칙을 Strategy로 분리하고 Composition Root에서 주입하는 이유를 봅니다.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest.Run();
    return;
}

SyntaxTour.Run();
CheckoutDemo.Run();

static class SyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== 1. 기본 문법 ==");
        // [기초] decimal은 금액, string?는 null 가능성, bool은 참/거짓 상태를 표현합니다.
        string customer = "민수";
        int itemCount = 3;
        decimal unitPrice = 12_000m;
        bool isMember = true;
        string? couponCode = null;
        decimal subtotal = itemCount * unitPrice;

        // [기초] switch 식은 위에서 아래로 처음 일치하는 패턴의 값을 반환합니다.
        string grade = subtotal switch
        {
            >= 50_000m => "큰 주문",
            >= 20_000m => "보통 주문",
            _ => "작은 주문"
        };

        List<decimal> samplePrices = [unitPrice, 8_000m, 5_000m];
        foreach (decimal price in samplePrices)
        {
            Console.WriteLine($"상품 가격: {price:N0}원");
        }

        Console.WriteLine($"{customer}: {grade}, 회원={isMember}, 쿠폰={couponCode ?? "없음"}");
        Console.WriteLine();
    }
}

static class CheckoutDemo
{
    public static void Run()
    {
        Console.WriteLine("== 2. 결제 금액 계산 ==");
        CheckoutService service = CompositionRoot.Build();
        CheckoutCommand command = new("ORD-1001", [new("책", 15_000m, 2), new("펜", 2_000m, 3)], true);
        Result<Receipt> result = service.Checkout(command);

        Console.WriteLine(result.IsSuccess
            ? $"주문 {result.Value.OrderId}: {result.Value.Subtotal:N0}원 → {result.Value.Total:N0}원"
            : $"실패: {result.Error}");
        Console.WriteLine("검증: dotnet run --project .\\src\\CheckoutExercise -- --self-test");
    }
}

static class CompositionRoot
{
    // [실무] 할인 정책 선택을 조립 지점에 모으면 서비스는 인터페이스 계약만 알면 됩니다.
    public static CheckoutService Build() => new(new MemberDiscountPolicy(0.1m));
}

// Application Service: 입력 검증과 유스케이스의 실행 순서를 담당합니다.
sealed class CheckoutService(IDiscountPolicy discountPolicy)
{
    public Result<Receipt> Checkout(CheckoutCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.OrderId))
            return Result<Receipt>.Fail("주문 번호는 필수입니다.");
        if (command.Items.Count == 0)
            return Result<Receipt>.Fail("상품을 한 개 이상 담아 주세요.");
        if (command.Items.Any(item => item.UnitPrice < 0 || item.Quantity <= 0))
            return Result<Receipt>.Fail("가격은 0원 이상, 수량은 1개 이상이어야 합니다.");

        decimal subtotal = command.Items.Sum(item => item.LineTotal);
        decimal discount = discountPolicy.Calculate(subtotal, command.IsMember);
        return Result<Receipt>.Ok(new(command.OrderId.Trim(), subtotal, discount, subtotal - discount));
    }
}

// Strategy 패턴: 할인 규칙을 서비스에서 분리하여 교체하고 테스트하기 쉽게 합니다.
interface IDiscountPolicy
{
    decimal Calculate(decimal subtotal, bool isMember);
}

sealed class MemberDiscountPolicy(decimal rate) : IDiscountPolicy
{
    public decimal Calculate(decimal subtotal, bool isMember) =>
        isMember ? decimal.Round(subtotal * rate, 0, MidpointRounding.AwayFromZero) : 0m;
}

sealed record CartItem(string Name, decimal UnitPrice, int Quantity)
{
    // [도메인] 계산 속성은 원본 값에서 매번 계산해 저장된 합계와의 불일치를 방지합니다.
    public decimal LineTotal => UnitPrice * Quantity;
}

sealed record CheckoutCommand(string OrderId, IReadOnlyList<CartItem> Items, bool IsMember);
sealed record Receipt(string OrderId, decimal Subtotal, decimal Discount, decimal Total);

sealed record Result<T>(T? Value, string? Error)
{
    // [고급] MemberNotNullWhen은 성공 시 Value가 null이 아님을 컴파일러에 알려줍니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

static class SelfTest
{
    // [검증] 정상 회원/비회원과 빈 장바구니/0개 수량의 경계까지 확인합니다.
    public static void Run()
    {
        CheckoutService service = CompositionRoot.Build();
        Result<Receipt> member = service.Checkout(new("A-1", [new("책", 10_000m, 2)], true));
        Check(member.IsSuccess && member.Value.Total == 18_000m, "회원은 10% 할인되어야 합니다.");

        Result<Receipt> guest = service.Checkout(new("A-2", [new("책", 10_000m, 2)], false));
        Check(guest.IsSuccess && guest.Value.Total == 20_000m, "비회원 금액은 그대로여야 합니다.");

        Result<Receipt> empty = service.Checkout(new("A-3", [], true));
        Check(!empty.IsSuccess && empty.Error.Contains("한 개", StringComparison.Ordinal), "빈 장바구니 오류가 쉬워야 합니다.");

        Result<Receipt> badQuantity = service.Checkout(new("A-4", [new("책", 10_000m, 0)], true));
        Check(!badQuantity.IsSuccess, "0개 상품은 거절해야 합니다.");

        Console.WriteLine("초보자 검증 통과 (4/4)");
        Console.WriteLine("✓ 회원/비회원 금액 계산");
        Console.WriteLine("✓ 빈 장바구니/잘못된 수량 검증");
    }

    private static void Check([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"검증 실패: {message}");
    }
}
