// 오늘 예제는 외부 CSV에서 들어온 주문을 검증하고, 올바른 주문만 저장하는 작은 업무 프로그램입니다.
// 한 파일에 모은 이유는 입문자가 실행 흐름을 위에서 아래로 따라간 뒤 역할별 분리를 한눈에 보기 쉽도록 하기 위해서입니다.

var rows = new[]
{
    new ImportRow("ORD-101", "kim@example.com", "standard", "35000"),
    new ImportRow("ORD-102", null, "express", "12000"),
    new ImportRow("ORD-103", "lee@example.com", "unknown", "5000"),
    new ImportRow("ORD-104", "park@example.com", "express", "not-a-number")
};

// Composition Root는 프로그램 시작점에서 구현 객체를 조립합니다. 업무 코드가 new에 묶이지 않아 가짜 구현으로 테스트하기 쉽습니다.
IImportPolicy policy = new StandardImportPolicy();
var repository = new InMemoryOrderRepository();
var service = new ImportOrdersService(repository, policy, new ConsoleImportReporter());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(rows, CancellationToken.None);
Console.WriteLine($"가져오기 완료: 성공 {result.ImportedCount}건, 거절 {result.Errors.Count}건");
foreach (var error in result.Errors)
{
    Console.WriteLine($"- {error.OrderId}: {error.Message}");
}

// record는 값 중심 데이터를 간결하게 표현하며 생성 뒤 바꾸지 않는 불변 데이터로 다루기 좋습니다.
// string?는 값이 없을 수 있음을 컴파일러와 독자에게 알립니다. null 검사를 빼먹으면 경고가 발생합니다.
sealed record ImportRow(string OrderId, string? CustomerEmail, string ShippingType, string AmountText);
sealed record Order(string Id, string CustomerEmail, decimal Amount, ShippingMethod Shipping);
sealed record ImportError(string OrderId, string Message);
sealed record ImportSummary(int ImportedCount, IReadOnlyList<ImportError> Errors);

// enum은 가능한 배송 방법을 제한해 오타가 섞인 임의 문자열이 도메인 안으로 들어오지 못하게 합니다.
enum ShippingMethod
{
    Standard,
    Express
}

// 예상 가능한 입력 오류는 예외가 아니라 Result로 반환합니다. 호출자가 실패를 정상 분기로 빠뜨리지 않고 처리하게 합니다.
// 반대로 DB 장애나 취소처럼 정상 업무 흐름 밖의 문제는 예외로 전파해 로깅·재시도 계층이 처리하도록 합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Repository는 저장 기술을 숨기는 계약입니다. 실제 서비스에서는 EF Core 구현으로 바꿔도 Application Service는 그대로 둡니다.
interface IOrderRepository
{
    Task SaveBatchAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken);
}

// Strategy 계약으로 검증·변환 규칙을 교체할 수 있습니다. 고객사별 CSV 정책이 늘어날 때 기존 흐름 수정을 줄입니다(OCP).
interface IImportPolicy
{
    Result<Order> ValidateAndConvert(ImportRow row);
}

interface IImportReporter
{
    Task ReportAsync(ImportSummary summary, CancellationToken cancellationToken);
}

sealed class StandardImportPolicy : IImportPolicy
{
    public Result<Order> ValidateAndConvert(ImportRow row)
    {
        // IsNullOrWhiteSpace는 null, 빈 문자열, 공백만 있는 문자열을 한 번에 검사해 nullable 값을 안전하게 좁힙니다.
        if (string.IsNullOrWhiteSpace(row.CustomerEmail))
        {
            return Result<Order>.Failure("고객 이메일이 필요합니다.");
        }

        // TryParse는 사용자가 잘못 입력할 수 있는 값을 예외 없이 검사합니다. out var에는 성공 시 변환된 값이 들어갑니다.
        if (!decimal.TryParse(row.AmountText, out var amount) || amount <= 0)
        {
            return Result<Order>.Failure("금액은 0보다 큰 숫자여야 합니다.");
        }

        var shipping = row.ShippingType.ToLowerInvariant() switch
        {
            "standard" => ShippingMethod.Standard,
            "express" => ShippingMethod.Express,
            _ => (ShippingMethod?)null
        };

        if (shipping is null)
        {
            return Result<Order>.Failure("배송 방식은 standard 또는 express여야 합니다.");
        }

        return Result<Order>.Success(new Order(row.OrderId, row.CustomerEmail, amount, shipping.Value));
    }
}

// Application Service는 '검증 → 저장 → 보고' 유스케이스 순서만 조정합니다. 세부 규칙과 저장 방식은 각 의존성에 맡깁니다(SRP, DIP).
sealed class ImportOrdersService(IOrderRepository repository, IImportPolicy policy, IImportReporter reporter)
{
    public async Task<ImportSummary> ExecuteAsync(
        IEnumerable<ImportRow> rows,
        CancellationToken cancellationToken)
    {
        var results = rows.Select(policy.ValidateAndConvert).ToArray();

        // LINQ의 Where는 조건에 맞는 값만 고르고 Select는 모양을 바꿉니다. ToArray로 이번 처리 대상을 확정합니다.
        var validOrders = results
            .Where(result => result.IsSuccess && result.Value is not null)
            .Select(result => result.Value!)
            .ToArray();

        var errors = rows.Zip(results)
            .Where(pair => !pair.Second.IsSuccess)
            .Select(pair => new ImportError(pair.First.OrderId, pair.Second.Error ?? "알 수 없는 오류"))
            .ToArray();

        // await는 저장 I/O가 끝날 때까지 비동기로 기다립니다. CancellationToken은 서버 종료 요청을 하위 작업까지 전달합니다.
        if (validOrders.Length > 0)
        {
            await repository.SaveBatchAsync(validOrders, cancellationToken);
        }

        var summary = new ImportSummary(validOrders.Length, errors);
        await reporter.ReportAsync(summary, cancellationToken);
        return summary;
    }
}

sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];

    public Task SaveBatchAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _orders.AddRange(orders);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Order>>(_orders.ToArray());
    }
}

sealed class ConsoleImportReporter : IImportReporter
{
    public Task ReportAsync(ImportSummary summary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[보고] 성공={summary.ImportedCount}, 거절={summary.Errors.Count}");
        return Task.CompletedTask;
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var policy = new StandardImportPolicy();
        var passed = 0;

        Check(policy.ValidateAndConvert(new("1", "a@b.com", "standard", "1000")).IsSuccess, "정상 행 변환");
        passed++;
        Check(!policy.ValidateAndConvert(new("2", null, "standard", "1000")).IsSuccess, "null 이메일 거절");
        passed++;
        Check(!policy.ValidateAndConvert(new("3", "a@b.com", "invalid", "1000")).IsSuccess, "잘못된 배송 방식 거절");
        passed++;

        var repository = new InMemoryOrderRepository();
        var service = new ImportOrdersService(repository, policy, new SilentReporter());
        var summary = await service.ExecuteAsync(
        [
            new("4", "a@b.com", "express", "2000"),
            new("5", "a@b.com", "express", "bad")
        ], CancellationToken.None);
        Check(summary.ImportedCount == 1 && summary.Errors.Count == 1, "부분 성공 집계");
        passed++;

        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 개발 중 발견해야 할 비정상 상황이므로 예외를 던져 실행과 CI를 즉시 실패시킵니다.
        if (!condition)
        {
            throw new InvalidOperationException($"테스트 실패: {name}");
        }
    }

    private sealed class SilentReporter : IImportReporter
    {
        public Task ReportAsync(ImportSummary summary, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
