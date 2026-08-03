// 읽는 순서: 맨 위 실행 흐름 → 값 객체와 Result → 도메인 모델 → Strategy → Repository → Application Service → self-test 순서로 보세요.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

// Composition Root는 구체 구현을 조립하는 한 곳입니다. 업무 서비스가 저장 방식과 정책 생성법을 몰라 교체와 테스트가 쉬워집니다.
IProductRepository products = new InMemoryProductRepository(
[
    new Product("BOOK-1", "C# 입문서", 1.2m, IsFragile: false),
    new Product("MUG-1", "개발자 머그", 0.4m, IsFragile: true)
]);
IShippingPolicy shippingPolicy = new StandardShippingPolicy();
var service = new CreateDeliveryQuoteService(products, shippingPolicy, new ConsoleQuoteLog());

var command = new CreateDeliveryQuoteCommand("BOOK-1", Quantity: 2, Destination: "제주", CustomerEmail: null);
var result = await service.ExecuteAsync(command, CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"견적 완료: {result.Value!.ProductName}, {result.Value.TotalWeightKg}kg, {result.Value.Fee:N0}원"
    : $"견적 실패: {result.Error}");

// record는 값 중심 자료형입니다. 생성 뒤 값을 바꾸지 않는 불변 명령/결과는 비동기 처리 중 상태가 엇갈릴 위험을 줄입니다.
public sealed record CreateDeliveryQuoteCommand(string ProductCode, int Quantity, string Destination, string? CustomerEmail);
public sealed record DeliveryQuote(string ProductName, decimal TotalWeightKg, decimal Fee, string? NotificationEmail);

// 예상 가능한 실패를 예외 대신 값으로 돌려주면 호출자가 정상 흐름 안에서 실패를 빠뜨리지 않고 처리할 수 있습니다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

// Product는 저장소에서 읽은 도메인 자료입니다. decimal은 배송 무게·금액처럼 정확한 소수 계산이 필요한 값에 적합합니다.
public sealed record Product(string Code, string Name, decimal WeightKg, bool IsFragile);

// Strategy는 배송비 규칙을 계약 뒤에 숨깁니다. 특급·해외 정책을 추가해도 Application Service를 수정하지 않아 OCP를 지킵니다.
public interface IShippingPolicy
{
    Result<decimal> CalculateFee(decimal totalWeightKg, string destination, bool isFragile);
}

public sealed class StandardShippingPolicy : IShippingPolicy
{
    public Result<decimal> CalculateFee(decimal totalWeightKg, string destination, bool isFragile)
    {
        if (totalWeightKg <= 0)
            return Result<decimal>.Failure("전체 무게는 0보다 커야 합니다.");

        var baseFee = 3_000m + (Math.Ceiling(totalWeightKg) * 700m);
        var remoteAreaFee = destination.Contains("제주", StringComparison.OrdinalIgnoreCase) ? 3_000m : 0m;
        var fragileFee = isFragile ? 1_500m : 0m;
        return Result<decimal>.Success(baseFee + remoteAreaFee + fragileFee);
    }
}

// Repository는 데이터 접근 계약입니다. 운영에서는 EF Core 구현으로 바꿔도 업무 흐름은 같은 인터페이스를 사용합니다.
public interface IProductRepository
{
    Task<Product?> FindByCodeAsync(string code, CancellationToken cancellationToken);
}

public sealed class InMemoryProductRepository(IEnumerable<Product> seed) : IProductRepository
{
    // ToDictionary는 LINQ로 상품 코드별 빠른 조회 구조를 만듭니다. 대소문자를 무시해 사용자 입력 차이도 흡수합니다.
    private readonly Dictionary<string, Product> _products = seed.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public Task<Product?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _products.TryGetValue(code, out var product);
        return Task.FromResult(product);
    }
}

public interface IQuoteLog
{
    void Created(DeliveryQuote quote);
}

public sealed class ConsoleQuoteLog : IQuoteLog
{
    public void Created(DeliveryQuote quote) =>
        Console.WriteLine($"운영 로그: product={quote.ProductName}, weightKg={quote.TotalWeightKg}, fee={quote.Fee}");
}

// Application Service는 검증→조회→계산→기록의 유스케이스 순서만 조정합니다. 각 규칙은 전문 객체에 맡겨 SRP를 지킵니다.
public sealed class CreateDeliveryQuoteService(
    IProductRepository products,
    IShippingPolicy shippingPolicy,
    IQuoteLog log)
{
    public async Task<Result<DeliveryQuote>> ExecuteAsync(CreateDeliveryQuoteCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ProductCode))
            return Result<DeliveryQuote>.Failure("상품 코드는 필수입니다.");
        if (command.Quantity is < 1 or > 100)
            return Result<DeliveryQuote>.Failure("수량은 1~100이어야 합니다.");
        if (string.IsNullOrWhiteSpace(command.Destination))
            return Result<DeliveryQuote>.Failure("배송지는 필수입니다.");

        // nullable 형식인 이메일은 선택값입니다. 값이 있을 때만 문법을 검사해 null을 정상 상태로 다룹니다.
        if (command.CustomerEmail is not null && !command.CustomerEmail.Contains('@'))
            return Result<DeliveryQuote>.Failure("이메일 형식이 올바르지 않습니다.");

        try
        {
            // await는 DB I/O가 끝나기를 기다리는 동안 스레드를 붙잡지 않습니다. 취소 토큰은 요청 중단을 아래 계층까지 전달합니다.
            var product = await products.FindByCodeAsync(command.ProductCode.Trim(), cancellationToken);
            if (product is null)
                return Result<DeliveryQuote>.Failure("상품을 찾을 수 없습니다.");

            var totalWeight = product.WeightKg * command.Quantity;
            var fee = shippingPolicy.CalculateFee(totalWeight, command.Destination.Trim(), product.IsFragile);
            if (!fee.IsSuccess)
                return Result<DeliveryQuote>.Failure(fee.Error!);

            var quote = new DeliveryQuote(product.Name, totalWeight, fee.Value!, command.CustomerEmail);
            log.Created(quote);
            return Result<DeliveryQuote>.Success(quote);
        }
        catch (OperationCanceledException)
        {
            throw; // 취소는 장애가 아니라 제어 신호이므로 감싸지 않고 호출자에게 그대로 전달합니다.
        }
        catch (Exception exception)
        {
            // DB 단절 같은 예상 밖 기술 장애는 예외로 전파해 재시도·알림 정책을 상위 경계에서 일관되게 적용합니다.
            throw new InvalidOperationException("배송 견적 생성 중 저장소 오류가 발생했습니다.", exception);
        }
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var repository = new InMemoryProductRepository([new Product("A", "테스트 상품", 1.5m, false)]);
        var service = new CreateDeliveryQuoteService(repository, new StandardShippingPolicy(), new SilentQuoteLog());
        var passed = 0;
        passed += Check(!(await service.ExecuteAsync(new("A", 0, "서울", null), CancellationToken.None)).IsSuccess, "0개 수량 거절");
        passed += Check(!(await service.ExecuteAsync(new("NONE", 1, "서울", null), CancellationToken.None)).IsSuccess, "없는 상품 거절");
        passed += Check(!(await service.ExecuteAsync(new("A", 1, "서울", "wrong"), CancellationToken.None)).IsSuccess, "잘못된 이메일 거절");
        var success = await service.ExecuteAsync(new("A", 2, "제주", null), CancellationToken.None);
        passed += Check(success.Value?.Fee == 8_100m, "무게와 제주 추가 요금 계산");

        Console.WriteLine($"self-test: {passed}/4 통과");
        if (passed != 4) Environment.ExitCode = 1;
    }

    private static int Check(bool condition, string name)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
        return condition ? 1 : 0;
    }

    // 테스트에서는 콘솔 출력이라는 부수 효과를 제거해 계산 결과만 빠르고 결정적으로 검증합니다.
    private sealed class SilentQuoteLog : IQuoteLog
    {
        public void Created(DeliveryQuote quote) { }
    }
}
