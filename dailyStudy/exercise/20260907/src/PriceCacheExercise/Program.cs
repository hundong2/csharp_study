// Program은 구체 객체를 만들고 연결하는 Composition Root입니다.
// 의존성 조립을 한곳에 모으면 Domain과 Application은 캐시나 저장소 구현을 몰라도 됩니다.
static class Program
{
    /// <summary>
    /// 명령행 옵션을 확인하여 자체 테스트 또는 결정적인 가격 캐시 데모를 실행합니다.
    /// </summary>
    /// <param name="args">--self-test를 포함할 수 있는 명령행 인수 배열입니다.</param>
    /// <returns>정상 완료 시 0, 자체 테스트 실패 시 1을 담아 완료되는 Task를 반환합니다.</returns>
    /// <remarks>async는 이 메서드가 await로 비동기 작업을 기다리며 Task&lt;int&gt;가 미래의 종료 코드를 뜻함을 나타냅니다.</remarks>
    public static async Task<int> Main(string[] args)
    {
        // LINQ의 Contains는 배열을 직접 반복하지 않고 특정 옵션이 들어 있는지 확인합니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await RunSelfTestsAsync();
        }

        // [ ... ]는 여러 항목으로 배열을 만드는 컬렉션 식(collection expression)입니다.
        ProductPrice[] seedPrices =
        [
            CreateSeedPrice("LAPTOP-15", 1_490_000m, "KRW"),
            CreateSeedPrice("KEYBOARD-MINI", 89_000m, "KRW"),
            CreateSeedPrice("MOUSE-PRO", 59_000m, "KRW")
        ];

        // 실제 서비스에서는 TimeProvider.System을 주입합니다. 데모는 출력과 TTL 검증을 매번 같게 하려고 수동 시계를 씁니다.
        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero));
        IPriceRepository repository = new InMemoryPriceRepository(seedPrices);
        ICacheFreshnessPolicy freshnessPolicy =
            new FixedTtlFreshnessPolicy(TimeSpan.FromMinutes(5));

        // Adapter가 Repository 결과를 IPriceProvider 계약으로 바꾸고, 같은 계약의 Decorator가 그 객체를 감쌉니다.
        // Application은 두 구체 형식을 모르고 IPriceProvider 계약으로만 최종 객체를 사용합니다.
        IPriceProvider repositoryProvider = new RepositoryPriceProvider(repository, clock);
        var cachedProvider = new CachedPriceProvider(
            repositoryProvider,
            freshnessPolicy,
            clock);
        var service = new PriceQueryService(cachedProvider);

        await RunDemoAsync(service, cachedProvider, clock);
        return 0;
    }

    /// <summary>
    /// 자체 테스트를 실행하고 예외를 프로세스 종료 코드와 읽기 쉬운 메시지로 바꿉니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 SelfTests가 준비한 고정 입력만 사용합니다.</remarks>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<int> RunSelfTestsAsync()
    {
        try
        {
            await SelfTests.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// cache miss, hit, TTL 만료, 같은 키의 여러 조회 Task, 실패 입력을 차례로 실행해 전체 흐름을 보여 줍니다.
    /// </summary>
    /// <param name="service">문자열 검증과 가격 조회를 조정하는 Application Service입니다.</param>
    /// <param name="cachedProvider">데모 끝에 캐시 통계와 키 스냅샷을 보여 줄 Decorator입니다.</param>
    /// <param name="clock">실제 대기 없이 TTL 경계를 이동할 수 있는 수동 시계입니다.</param>
    /// <returns>모든 조회와 출력이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RunDemoAsync(
        PriceQueryService service,
        CachedPriceProvider cachedProvider,
        ManualTimeProvider clock)
    {
        Console.WriteLine("=== 상품 가격 TTL 캐시 데모 ===");

        await PrintQueryAsync(service, "laptop-15", CancellationToken.None);
        await PrintQueryAsync(service, "LAPTOP-15", CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(5));
        Console.WriteLine("-- 수동 시계를 정확히 TTL만큼 이동: 다음 요청은 만료 --");
        await PrintQueryAsync(service, "LAPTOP-15", CancellationToken.None);

        Console.WriteLine("-- 같은 SKU 조회 Task 6개를 한꺼번에 기다리기 --");
        // Enumerable.Range와 Select는 1~6을 여섯 개의 조회 Task로 투영합니다.
        // 이 메모리 Repository는 즉시 완료될 수 있으므로 실제 동시 진입 증명은 장벽을 쓰는 SelfTests가 담당합니다.
        var concurrentTasks = Enumerable.Range(1, 6)
            .Select(_ => service.QueryAsync("KEYBOARD-MINI", CancellationToken.None))
            .ToArray();

        // Task.WhenAll은 여섯 요청을 차례로 막아 기다리지 않고 모두 끝날 때까지 비동기로 기다립니다.
        var concurrentResults = await Task.WhenAll(concurrentTasks);
        foreach (var result in concurrentResults)
        {
            PrintResult("KEYBOARD-MINI", result);
        }

        await PrintQueryAsync(service, "UNKNOWN-ITEM", CancellationToken.None);
        await PrintQueryAsync(service, "  ", CancellationToken.None);

        var statistics = cachedProvider.GetStatistics();
        Console.WriteLine(
            $"통계: cache hit={statistics.HitCount}, " +
            $"inner 조회={statistics.InnerLoadCount}, " +
            $"캐시 항목={statistics.EntryCount}");
        Console.WriteLine(
            $"캐시 SKU: {string.Join(", ", cachedProvider.GetCachedSkuCodes())}");
    }

    /// <summary>
    /// 한 SKU를 Application Service로 조회하고 성공 또는 실패를 한 줄로 출력합니다.
    /// </summary>
    /// <param name="service">검증과 캐시 조회를 수행할 서비스입니다.</param>
    /// <param name="rawSku">아직 검증하지 않은 상품 코드 문자열입니다.</param>
    /// <param name="cancellationToken">조회 중단을 전달할 취소 신호입니다.</param>
    /// <returns>조회와 출력이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task PrintQueryAsync(
        PriceQueryService service,
        string? rawSku,
        CancellationToken cancellationToken)
    {
        var result = await service.QueryAsync(rawSku, cancellationToken);
        PrintResult(rawSku ?? "<null>", result);
    }

    /// <summary>
    /// 가격 조회 Result를 출처가 보이는 성공 메시지 또는 종류가 보이는 실패 메시지로 표현합니다.
    /// </summary>
    /// <param name="rawSku">사용자가 요청한 원래 상품 코드입니다.</param>
    /// <param name="result">Application Service가 반환한 성공 또는 실패 결과입니다.</param>
    /// <returns>콘솔에 쓰기만 하므로 반환값은 없습니다.</returns>
    private static void PrintResult(string rawSku, Result<PriceLookup> result)
    {
        if (!result.IsSuccess)
        {
            Console.WriteLine(
                $"[FAILURE:{result.Problem!.Kind}] {rawSku}: {result.Problem.Message}");
            return;
        }

        var lookup = result.Value!;
        // switch 식은 Origin 값에 따라 표시 문자열 하나를 선택하고, _는 미래에 값이 추가될 때의 기본 분기입니다.
        var originLabel = lookup.Origin switch
        {
            PriceOrigin.Repository => "원본",
            PriceOrigin.Cache => "캐시",
            _ => "알 수 없음"
        };

        Console.WriteLine(
            $"[SUCCESS:{originLabel}] {lookup.Price.Sku.Code} = " +
            $"{lookup.Price.Amount:N0} {lookup.Price.Currency} " +
            $"(원본 확인 {lookup.FreshAsOfUtc:HH:mm:ss} UTC)");
    }

    /// <summary>
    /// 코드에 적은 고정 데모 데이터를 Domain 팩터리로 검증하고 성공값을 꺼냅니다.
    /// </summary>
    /// <param name="skuCode">데모에서 사용할 상품 코드입니다.</param>
    /// <param name="amount">데모에서 사용할 양수 가격입니다.</param>
    /// <param name="currency">데모에서 사용할 세 글자 통화 코드입니다.</param>
    /// <returns>검증이 끝난 ProductPrice를 반환합니다.</returns>
    private static ProductPrice CreateSeedPrice(
        string skuCode,
        decimal amount,
        string currency)
    {
        var skuResult = ProductSku.Create(skuCode);
        if (!skuResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"잘못된 데모 SKU입니다: {skuResult.Problem!.Message}");
        }

        var priceResult = ProductPrice.Create(skuResult.Value!, amount, currency);
        if (!priceResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"잘못된 데모 가격입니다: {priceResult.Problem!.Message}");
        }

        return priceResult.Value!;
    }
}
