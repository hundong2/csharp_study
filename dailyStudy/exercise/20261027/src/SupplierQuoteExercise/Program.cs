using SupplierQuoteExercise.Application;
using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;
using SupplierQuoteExercise.Infrastructure;
using SupplierQuoteExercise.SelfTesting;

namespace SupplierQuoteExercise;

/// <summary>
/// 구체 Gateway, Strategy, Repository를 조립하고 demo 또는 self-test를 실행하는 Composition Root입니다.
/// 객체 생성 책임을 이곳에 모으면 Domain과 Application은 학습용 지연이나 메모리 저장소를 직접 알지 않습니다.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// static 진입점만 가진 Composition Root를 실수로 객체화하지 못하게 막습니다.
    /// 파라미터가 없으며 생성자는 객체 초기화만 하고 값을 반환하지 않습니다.
    /// </summary>
    private Program()
    {
    }

    /// <summary>
    /// 명령줄 인수에 따라 완료 순서 demo 또는 자동 self-test를 실행합니다.
    /// async는 await를 쓸 수 있게 하고 Task&lt;int&gt;는 나중에 완료될 exit code를 나타냅니다.
    /// </summary>
    /// <param name="args">`--demo` 또는 `--self-test` 실행 mode를 담은 명령줄 인수입니다.</param>
    /// <returns>성공은 0, demo 검증 실패는 1, 알 수 없는 인수는 2를 담은 Task를 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        if (args.Length == 0 || args.Contains("--demo", StringComparer.Ordinal))
        {
            return await RunDemoAsync();
        }

        Console.Error.WriteLine("사용법: --demo 또는 --self-test");
        return 2;
    }

    /// <summary>
    /// 빠른 성공, 중간 실패, 느리지만 저렴한 성공을 완료 즉시 출력하고 최종 선택을 검증합니다.
    /// </summary>
    /// <returns>완료 순서·부분 실패·선택·저장 결과가 예상과 같으면 0, 다르면 1을 반환합니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        // var는 오른쪽 반환값에서 형식이 분명할 때 compiler가 정적 형식을 추론하며 dynamic이 아닙니다.
        var request = RequireRequest("  gear-100  ", 25, new DateOnly(2026, 11, 5));
        // IReadOnlyList는 service가 공급업체 목록을 임의로 추가·삭제하지 않는다는 의도를 드러냅니다.
        // []는 목표 collection 형식에 맞춰 원소를 만드는 collection expression 문법입니다.
        IReadOnlyList<ISupplierQuoteGateway> gateways =
        [
            // 가장 느린 Gateway를 배열 첫 칸에 두어도 WhenEach는 실제 완료 순서로 내보냄을 보여 줍니다.
            new ScriptedSupplierQuoteGateway(
                "BusanBudget",
                TimeSpan.FromMilliseconds(160),
                Result<SupplierQuote>.Success(
                    RequireQuote("BusanBudget", 1_050m, new DateOnly(2026, 11, 4)))),
            new ScriptedSupplierQuoteGateway(
                "SeoulParts",
                TimeSpan.FromMilliseconds(40),
                Result<SupplierQuote>.Success(
                    RequireQuote("SeoulParts", 1_200m, new DateOnly(2026, 11, 2)))),
            new ScriptedSupplierQuoteGateway(
                "NoStockParts",
                TimeSpan.FromMilliseconds(100),
                Result<SupplierQuote>.Failure(
                    Error.External("supplier.out_of_stock", "요청 수량의 재고가 없습니다.")))
        ];

        var (service, repository) = BuildApplication(gateways);
        var completionOrder = new List<string>();

        Console.WriteLine($"SKU={request.Sku}, 수량={request.Quantity}, 납기={request.NeedBy:yyyy-MM-dd}");
        Console.WriteLine("세 공급업체 요청을 동시에 시작합니다.");

        // lambda `attempt => { ... }`는 완료 때 실행할 짧은 동작을 이름 없는 함수 값으로 전달합니다.
        var report = await service.CollectAsync(
            request,
            attempt =>
            {
                completionOrder.Add(attempt.SupplierId);
                if (attempt.Outcome.IsSuccess)
                {
                    var quote = attempt.Outcome.Value;
                    Console.WriteLine(
                        $"[{completionOrder.Count}] 성공 {quote.SupplierId}: "
                        + $"단가={quote.UnitPrice:N0}, 납기={quote.DeliveryDate:yyyy-MM-dd}");
                    return;
                }

                Console.WriteLine(
                    $"[{completionOrder.Count}] 실패 {attempt.SupplierId}: "
                    + $"{attempt.Outcome.Error.Code}");
            });

        var selected = report.SelectedQuote;
        Console.WriteLine(
            $"요약: 성공={report.CountSuccesses()}, 실패={report.CountFailures()}, 저장={repository.Count}");
        Console.WriteLine(
            // `is null` pattern은 선택 결과가 실제로 없는지를 안전하게 검사합니다.
            selected is null
                ? "선택 가능한 견적이 없습니다."
                : $"선택: {selected.SupplierId}, 총액={selected.TotalFor(request.Quantity):N0}");

        // new[]는 첫 문자열 원소를 보고 string[] 형식을 추론하는 암시적 배열 생성 문법입니다.
        var expectedOrder = new[] { "SeoulParts", "NoStockParts", "BusanBudget" };
        // ?.는 selected가 null이면 SupplierId 접근 대신 null을 만드는 null-conditional 문법입니다.
        var isExpected = completionOrder.SequenceEqual(expectedOrder, StringComparer.Ordinal)
            && report.CountSuccesses() == 2
            && report.CountFailures() == 1
            && selected?.SupplierId == "BusanBudget"
            && repository.Count == 1;

        Console.WriteLine(isExpected ? "[DEMO:SUCCESS]" : "[DEMO:UNEXPECTED]");
        return isExpected ? 0 : 1;
    }

    /// <summary>
    /// 공급업체 Port 목록을 최저 총액 Strategy와 메모리 Repository에 생성자 주입으로 연결합니다.
    /// </summary>
    /// <param name="gateways">동시에 호출할 구체 Gateway Adapter 목록입니다.</param>
    /// <returns>조립된 Application Service와 결과를 관찰할 Repository를 tuple로 반환합니다.</returns>
    private static (
        CollectSupplierQuotesService Service,
        InMemoryQuoteRoundRepository Repository) BuildApplication(
        IEnumerable<ISupplierQuoteGateway> gateways)
    {
        // DI는 필요한 객체를 class 안에서 몰래 만들지 않고 바깥 Composition Root에서 넣는 설계 방식입니다.
        var repository = new InMemoryQuoteRoundRepository();
        var policy = new LowestTotalCostPolicy();
        var service = new CollectSupplierQuotesService(gateways, policy, repository);
        return (service, repository);
    }

    /// <summary>
    /// demo에 하드코딩한 요청이 잘못됐다면 학습 자료의 결함이므로 즉시 예외로 드러냅니다.
    /// </summary>
    /// <param name="sku">demo 상품 코드입니다.</param>
    /// <param name="quantity">demo 요청 수량입니다.</param>
    /// <param name="needBy">demo 최종 납기일입니다.</param>
    /// <returns>검증이 끝난 QuoteRequest를 반환합니다.</returns>
    private static QuoteRequest RequireRequest(string sku, int quantity, DateOnly needBy)
    {
        var result = QuoteRequest.Create(sku, quantity, needBy);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    /// <summary>
    /// demo에 하드코딩한 견적이 잘못됐다면 학습 자료의 결함이므로 즉시 예외로 드러냅니다.
    /// </summary>
    /// <param name="supplierId">demo 공급업체 ID입니다.</param>
    /// <param name="unitPrice">demo 개당 가격입니다.</param>
    /// <param name="deliveryDate">demo 약속 납기일입니다.</param>
    /// <returns>검증이 끝난 SupplierQuote를 반환합니다.</returns>
    private static SupplierQuote RequireQuote(
        string supplierId,
        decimal unitPrice,
        DateOnly deliveryDate)
    {
        var result = SupplierQuote.Create(supplierId, unitPrice, deliveryDate);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }
}
