// 이 파일은 외부 테스트 패키지 없이도 핵심 규칙과 동시성 경계를 실행해 확인하는 작은 테스트 러너입니다.
static class SelfTests
{
    /// <summary>
    /// 입력 검증, TTL, 동시성, 실패·예외·취소 복구 테스트를 정해진 순서로 실행합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 각 테스트가 자신의 고정 입력과 가짜 저장소를 만듭니다.</remarks>
    /// <returns>모든 테스트가 끝나면 완료되고, 하나라도 실패하면 예외를 담아 실패하는 Task를 반환합니다.</returns>
    public static async Task RunAsync()
    {
        // (string Name, Func<Task> Run)은 테스트 이름과 실행 함수를 함께 보관하는 이름 있는 튜플입니다.
        // Func<Task>는 매개변수 없이 실행되어 Task를 반환하는 메서드를 가리키는 delegate 형식입니다.
        (string Name, Func<Task> Run)[] tests =
        [
            ("잘못된 SKU는 저장소를 호출하지 않는다", InvalidSkuSkipsRepositoryAsync),
            ("첫 miss 뒤 hit는 원본을 한 번만 읽는다", ColdMissThenHitAsync),
            ("만료 직전은 hit이고 정확한 TTL 경계는 refresh다", ExactExpiryBoundaryAsync),
            ("같은 SKU의 동시 miss는 성공 refresh 한 번으로 합쳐진다", ConcurrentSameSkuCoalescesAsync),
            ("서로 다른 SKU는 독립적으로 원본을 조회한다", DifferentSkusRunIndependentlyAsync),
            ("실패와 예외는 캐시하지 않고 gate를 복구한다", FailureAndExceptionRecoverAsync),
            ("취소된 대기자는 gate를 손상시키지 않는다", CanceledWaiterDoesNotCorruptGateAsync)
        ];

        var passed = 0;
        foreach (var test in tests)
        {
            await test.Run();
            passed++;
            Console.WriteLine($"PASS {passed}/{tests.Length}: {test.Name}");
        }

        Console.WriteLine($"SELF-TESTS PASSED: {passed}/{tests.Length}");
    }

    /// <summary>
    /// 공백 SKU가 검증 단계에서 실패하고 Repository까지 내려가지 않는지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 빈 메모리 Repository와 수동 시계를 사용합니다.</remarks>
    /// <returns>검증이 끝나면 완료되고 단언이 틀리면 예외를 담는 Task를 반환합니다.</returns>
    private static async Task InvalidSkuSkipsRepositoryAsync()
    {
        var repository = new InMemoryPriceRepository([]);
        var provider = CreateProvider(repository, CreateClock());
        var service = new PriceQueryService(provider);

        var result = await service.QueryAsync(" ", CancellationToken.None);

        Assert(!result.IsSuccess, "공백 SKU는 실패해야 합니다.");
        Assert(result.Problem!.Kind == ErrorKind.Validation, "검증 오류여야 합니다.");
        Assert(repository.CallCount == 0, "잘못된 입력은 Repository를 호출하면 안 됩니다.");
    }

    /// <summary>
    /// 같은 SKU의 첫 조회는 Repository, 두 번째 조회는 캐시에서 오는지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 고정 가격 한 개와 5분 TTL을 사용합니다.</remarks>
    /// <returns>두 조회와 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ColdMissThenHitAsync()
    {
        var clock = CreateClock();
        var price = CreatePrice("LAPTOP-15", 1_490_000m);
        var repository = new InMemoryPriceRepository([price]);
        var provider = CreateProvider(repository, clock);

        var first = await provider.GetAsync(price.Sku, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = await provider.GetAsync(price.Sku, CancellationToken.None);

        Assert(first.IsSuccess, "첫 조회가 성공해야 합니다.");
        Assert(first.Value!.Origin == PriceOrigin.Repository, "첫 조회는 원본이어야 합니다.");
        Assert(second.IsSuccess, "두 번째 조회가 성공해야 합니다.");
        Assert(second.Value!.Origin == PriceOrigin.Cache, "두 번째 조회는 캐시여야 합니다.");
        Assert(
            second.Value.FreshAsOfUtc == first.Value.FreshAsOfUtc,
            "cache hit는 조회 시각이 아니라 원본에서 확인된 신선도 기준 시각을 보존해야 합니다.");
        Assert(repository.CallCount == 1, "원본 Repository는 한 번만 호출되어야 합니다.");
        Assert(provider.GetStatistics().InnerLoadCount == 1, "inner Provider 조회 시도도 한 번이어야 합니다.");
    }

    /// <summary>
    /// TTL 1초 전에는 캐시를 쓰고 정확히 TTL에 닿으면 원본을 다시 읽는 경계를 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 ManualTimeProvider로 실제 대기 없이 시간을 이동합니다.</remarks>
    /// <returns>세 조회와 경계 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ExactExpiryBoundaryAsync()
    {
        var clock = CreateClock();
        var price = CreatePrice("MOUSE-PRO", 59_000m);
        var repository = new InMemoryPriceRepository([price]);
        var provider = CreateProvider(repository, clock);

        var first = await provider.GetAsync(price.Sku, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        var justBefore = await provider.GetAsync(price.Sku, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));
        var exactBoundary = await provider.GetAsync(price.Sku, CancellationToken.None);

        Assert(first.Value!.Origin == PriceOrigin.Repository, "첫 조회는 원본이어야 합니다.");
        Assert(justBefore.Value!.Origin == PriceOrigin.Cache, "TTL 직전은 캐시 hit여야 합니다.");
        Assert(
            exactBoundary.Value!.Origin == PriceOrigin.Repository,
            "정확한 TTL 경계는 만료로 보고 refresh해야 합니다.");
        Assert(repository.CallCount == 2, "만료 뒤 원본을 한 번 더 읽어야 합니다.");
    }

    /// <summary>
    /// 같은 SKU를 동시에 요청한 여러 Task가 성공한 원본 refresh 하나를 공유하는지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 TaskCompletionSource 장벽으로 첫 원본 호출을 의도적으로 멈춥니다.</remarks>
    /// <returns>모든 동시 요청과 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ConcurrentSameSkuCoalescesAsync()
    {
        var price = CreatePrice("KEYBOARD-MINI", 89_000m);
        var repository = new BlockingPriceRepository(price);
        var provider = CreateProvider(repository, CreateClock());

        var leader = provider.GetAsync(price.Sku, CancellationToken.None);
        await repository.WaitUntilEnteredAsync();

        // Enumerable.Range의 각 숫자를 같은 SKU 조회 Task로 바꾸어 대기 중인 follower 아홉 개를 만듭니다.
        var followers = Enumerable.Range(0, 9)
            .Select(_ => provider.GetAsync(price.Sku, CancellationToken.None))
            .ToArray();

        repository.Release();

        // [leader, .. followers]의 ..는 기존 배열 항목을 새 컬렉션 식 안에 펼치는 spread 요소입니다.
        var results = await Task.WhenAll([leader, .. followers]);

        Assert(results.All(result => result.IsSuccess), "동시 요청이 모두 성공해야 합니다.");
        Assert(repository.CallCount == 1, "같은 SKU 원본 조회는 한 번이어야 합니다.");
        Assert(
            results.Count(result => result.Value!.Origin == PriceOrigin.Repository) == 1,
            "한 요청만 원본 결과를 받아야 합니다.");
        Assert(
            results.Count(result => result.Value!.Origin == PriceOrigin.Cache) == 9,
            "나머지 요청은 gate 안의 두 번째 확인에서 캐시를 찾아야 합니다.");
    }

    /// <summary>
    /// 키별 SemaphoreSlim 때문에 서로 다른 두 SKU는 하나의 전역 잠금에 막히지 않는지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 두 원본 호출이 모두 들어와야 열리는 장벽 Repository를 사용합니다.</remarks>
    /// <returns>두 키의 병렬 진입과 성공을 확인하면 완료되는 Task를 반환합니다.</returns>
    private static async Task DifferentSkusRunIndependentlyAsync()
    {
        var firstPrice = CreatePrice("LAPTOP-15", 1_490_000m);
        var secondPrice = CreatePrice("MOUSE-PRO", 59_000m);
        var repository = new TwoKeyBarrierRepository([firstPrice, secondPrice]);
        var provider = CreateProvider(repository, CreateClock());

        var firstTask = provider.GetAsync(firstPrice.Sku, CancellationToken.None);
        var secondTask = provider.GetAsync(secondPrice.Sku, CancellationToken.None);

        await repository.WaitUntilBothEnteredAsync();
        repository.Release();
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert(results.All(result => result.IsSuccess), "서로 다른 두 SKU 조회가 성공해야 합니다.");
        Assert(repository.CallCount == 2, "두 SKU는 각자 원본을 한 번 읽어야 합니다.");
    }

    /// <summary>
    /// 실패 Result와 예상 밖 예외가 캐시에 저장되지 않고 finally가 gate를 풀어 후속 요청이 성공하는지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 첫 호출만 실패하거나 예외를 던지는 두 가짜 Repository를 사용합니다.</remarks>
    /// <returns>두 복구 시나리오의 조회와 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task FailureAndExceptionRecoverAsync()
    {
        var price = CreatePrice("MOUSE-PRO", 59_000m);
        var failureRepository = new FailOncePriceRepository(price);
        var failureProvider = CreateProvider(failureRepository, CreateClock());

        var failed = await failureProvider.GetAsync(price.Sku, CancellationToken.None);
        var recovered = await failureProvider.GetAsync(price.Sku, CancellationToken.None);
        var cached = await failureProvider.GetAsync(price.Sku, CancellationToken.None);

        Assert(!failed.IsSuccess, "첫 예상 실패는 Result로 전달되어야 합니다.");
        Assert(recovered.Value!.Origin == PriceOrigin.Repository, "실패 뒤 원본을 다시 읽어야 합니다.");
        Assert(cached.Value!.Origin == PriceOrigin.Cache, "복구 성공 뒤에는 캐시를 써야 합니다.");
        Assert(failureRepository.CallCount == 2, "실패는 캐시하지 않아 원본을 다시 호출해야 합니다.");

        var throwingRepository = new ThrowOncePriceRepository(price);
        var throwingProvider = CreateProvider(throwingRepository, CreateClock());
        var thrownTask = throwingProvider.GetAsync(price.Sku, CancellationToken.None);

        await AssertThrowsAsync<InvalidOperationException>(
            thrownTask,
            "예상 밖 Repository 예외는 숨기지 않고 전파해야 합니다.");

        var afterException = await throwingProvider.GetAsync(price.Sku, CancellationToken.None);
        Assert(afterException.IsSuccess, "예외 뒤 후속 요청은 gate에 막히지 않고 성공해야 합니다.");
        Assert(throwingRepository.CallCount == 2, "예외도 캐시하지 않아 원본을 다시 호출해야 합니다.");
    }

    /// <summary>
    /// leader가 gate를 가진 동안 취소된 follower가 잘못 Release하지 않고 후속 조회가 정상인지 확인합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 첫 원본 호출을 장벽에서 멈추고 follower의 CancellationToken만 취소합니다.</remarks>
    /// <returns>취소 전파, leader 성공, 후속 cache hit 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task CanceledWaiterDoesNotCorruptGateAsync()
    {
        var price = CreatePrice("LAPTOP-15", 1_490_000m);
        var repository = new BlockingPriceRepository(price);
        var provider = CreateProvider(repository, CreateClock());

        var leader = provider.GetAsync(price.Sku, CancellationToken.None);
        await repository.WaitUntilEnteredAsync();

        // using var는 메서드가 끝날 때 CancellationTokenSource.Dispose를 자동 호출해 자원을 정리합니다.
        using var cancellationSource = new CancellationTokenSource();
        var canceledFollower = provider.GetAsync(price.Sku, cancellationSource.Token);
        cancellationSource.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            canceledFollower,
            "gate를 기다리던 follower는 취소 예외를 받아야 합니다.");

        repository.Release();
        var leaderResult = await leader;
        var laterResult = await provider.GetAsync(price.Sku, CancellationToken.None);

        Assert(leaderResult.IsSuccess, "leader 조회가 성공해야 합니다.");
        Assert(laterResult.Value!.Origin == PriceOrigin.Cache, "취소 뒤 후속 조회는 cache hit여야 합니다.");
        Assert(repository.CallCount == 1, "취소된 follower가 원본을 호출하면 안 됩니다.");
    }

    /// <summary>
    /// 공통 5분 TTL과 지정한 Repository·시계로 테스트용 캐시 Decorator를 조립합니다.
    /// </summary>
    /// <param name="repository">테스트 시나리오에 맞는 원본 가격 구현입니다.</param>
    /// <param name="clock">테스트가 직접 움직일 수 있는 수동 시계입니다.</param>
    /// <returns>모든 의존성이 연결된 CachedPriceProvider를 반환합니다.</returns>
    private static CachedPriceProvider CreateProvider(
        IPriceRepository repository,
        ManualTimeProvider clock)
    {
        IPriceProvider repositoryProvider = new RepositoryPriceProvider(repository, clock);
        return new CachedPriceProvider(
            repositoryProvider,
            new FixedTtlFreshnessPolicy(TimeSpan.FromMinutes(5)),
            clock);
    }

    /// <summary>
    /// 모든 테스트가 공유할 고정 UTC 시작 시각의 수동 시계를 만듭니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 테스트마다 새 인스턴스를 만들어 상태가 섞이지 않게 합니다.</remarks>
    /// <returns>2026-09-07 00:00:00 UTC를 가리키는 ManualTimeProvider를 반환합니다.</returns>
    private static ManualTimeProvider CreateClock()
    {
        return new ManualTimeProvider(
            new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// 테스트용 SKU와 양수 KRW 금액을 Domain 팩터리로 검증해 ProductPrice를 만듭니다.
    /// </summary>
    /// <param name="skuCode">테스트에 사용할 올바른 상품 코드입니다.</param>
    /// <param name="amount">테스트에 사용할 양수 가격입니다.</param>
    /// <returns>검증을 통과한 ProductPrice를 반환합니다.</returns>
    private static ProductPrice CreatePrice(string skuCode, decimal amount)
    {
        var sku = ProductSku.Create(skuCode);
        Assert(sku.IsSuccess, "테스트 SKU 자체가 유효해야 합니다.");

        var price = ProductPrice.Create(sku.Value!, amount, "KRW");
        Assert(price.IsSuccess, "테스트 가격 자체가 유효해야 합니다.");
        return price.Value!;
    }

    /// <summary>
    /// 비동기 작업이 지정한 예외 형식으로 실패하는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">기대하는 Exception 파생 형식입니다.</typeparam>
    /// <param name="task">실패해야 하는 이미 시작된 비동기 작업입니다.</param>
    /// <param name="message">예외가 없거나 다른 형식일 때 보여 줄 설명입니다.</param>
    /// <returns>기대한 예외를 확인하면 완료되고, 그렇지 않으면 테스트 실패 예외를 담는 Task를 반환합니다.</returns>
    // where는 TException 자리에 Exception 파생 형식만 올 수 있게 제한해 catch에 안전하게 사용하도록 합니다.
    private static async Task AssertThrowsAsync<TException>(Task task, string message)
        where TException : Exception
    {
        try
        {
            await task;
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 조건이 거짓이면 테스트 이름에 표시할 수 있는 설명과 함께 실패 예외를 던집니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 검증 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 원인을 설명할 메시지입니다.</param>
    /// <returns>조건을 검사할 뿐 값을 만들지 않으므로 반환값은 없습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // TaskCompletionSource는 테스트 코드가 비동기 작업의 진입과 해제를 직접 제어하는 신호등 역할을 합니다.
    private sealed class BlockingPriceRepository : IPriceRepository
    {
        private readonly ProductPrice _price;
        // new(...)는 왼쪽 필드 형식에서 TaskCompletionSource를 추론하는 target-typed new 문법입니다.
        // RunContinuationsAsynchronously는 신호를 보낸 스레드 안에서 대기자의 후속 코드가 즉시 끼어드는 재진입을 피합니다.
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 호출 진입과 반환 허용 시점을 테스트가 제어할 수 있는 단일 가격 Repository를 만듭니다.
        /// </summary>
        /// <param name="price">Release 뒤 성공으로 반환할 고정 가격입니다.</param>
        /// <remarks>생성자는 가격을 보관하므로 반환값이 없습니다.</remarks>
        public BlockingPriceRepository(ProductPrice price)
        {
            _price = price ?? throw new ArgumentNullException(nameof(price));
        }

        /// <summary>
        /// 호출 사실을 알린 뒤 테스트가 Release할 때까지 비동기로 기다렸다가 고정 가격을 반환합니다.
        /// </summary>
        /// <param name="sku">요청한 SKU이며 고정 가격의 SKU와 같아야 합니다.</param>
        /// <param name="cancellationToken">장벽 대기를 중단할 수 있는 취소 신호입니다.</param>
        /// <returns>Release 뒤 고정 가격 성공 Result를 담아 완료되는 Task를 반환합니다.</returns>
        public async Task<Result<ProductPrice>> FindAsync(
            ProductSku sku,
            CancellationToken cancellationToken)
        {
            Assert(sku == _price.Sku, "Blocking Repository에 알 수 없는 SKU가 전달되었습니다.");
            Interlocked.Increment(ref _callCount);
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return Result<ProductPrice>.Success(_price);
        }

        /// <summary>
        /// 원본 조회가 장벽에 진입할 때까지 기다려 동시성 테스트의 시작점을 고정합니다.
        /// </summary>
        /// <remarks>매개변수는 없고 2초 제한은 실패 시 테스트가 영원히 멈추지 않게 하는 보호 장치입니다.</remarks>
        /// <returns>진입 신호를 받으면 완료되는 Task를 반환합니다.</returns>
        public async Task WaitUntilEnteredAsync()
        {
            await _entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// 장벽에서 기다리는 원본 조회가 가격을 반환하도록 해제합니다.
        /// </summary>
        /// <remarks>매개변수와 반환값은 없으며 여러 번 호출되어도 첫 신호만 효과가 있습니다.</remarks>
        public void Release()
        {
            _release.TrySetResult();
        }
    }

    private sealed class TwoKeyBarrierRepository : IPriceRepository
    {
        private readonly IReadOnlyDictionary<ProductSku, ProductPrice> _prices;
        private readonly TaskCompletionSource _bothEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 서로 다른 두 SKU의 동시 진입을 확인할 장벽 Repository를 만듭니다.
        /// </summary>
        /// <param name="prices">정확히 두 개의 서로 다른 SKU 가격입니다.</param>
        /// <remarks>가격을 사전으로 복사하며 반환값은 없습니다.</remarks>
        public TwoKeyBarrierRepository(IEnumerable<ProductPrice> prices)
        {
            _prices = prices.ToDictionary(price => price.Sku);
            if (_prices.Count != 2)
            {
                throw new ArgumentException("서로 다른 가격 두 개가 필요합니다.", nameof(prices));
            }
        }

        /// <summary>
        /// SKU별 호출이 모두 들어올 때까지 공통 장벽에서 기다린 뒤 해당 가격을 반환합니다.
        /// </summary>
        /// <param name="sku">두 테스트 가격 중 하나의 SKU입니다.</param>
        /// <param name="cancellationToken">장벽 대기를 중단할 수 있는 취소 신호입니다.</param>
        /// <returns>Release 뒤 요청 SKU에 맞는 성공 Result를 담아 완료되는 Task를 반환합니다.</returns>
        public async Task<Result<ProductPrice>> FindAsync(
            ProductSku sku,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _callCount);
            if (count >= 2)
            {
                _bothEntered.TrySetResult();
            }

            await _release.Task.WaitAsync(cancellationToken);
            return Result<ProductPrice>.Success(_prices[sku]);
        }

        /// <summary>
        /// 두 SKU가 모두 Repository에 동시에 진입할 때까지 기다립니다.
        /// </summary>
        /// <remarks>매개변수는 없고 2초 제한은 전역 잠금 회귀가 생겼을 때 빠르게 실패시키는 보호 장치입니다.</remarks>
        /// <returns>두 번째 진입 신호를 받으면 완료되는 Task를 반환합니다.</returns>
        public async Task WaitUntilBothEnteredAsync()
        {
            await _bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// 장벽에 들어온 두 원본 조회가 각 가격을 반환하도록 해제합니다.
        /// </summary>
        /// <remarks>매개변수와 반환값은 없으며 여러 번 호출해도 안전합니다.</remarks>
        public void Release()
        {
            _release.TrySetResult();
        }
    }

    private sealed class FailOncePriceRepository : IPriceRepository
    {
        private readonly ProductPrice _price;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 첫 호출은 Dependency 실패, 이후 호출은 고정 가격을 반환하는 Repository를 만듭니다.
        /// </summary>
        /// <param name="price">복구된 호출에서 반환할 가격입니다.</param>
        /// <remarks>생성자는 가격을 보관하므로 반환값이 없습니다.</remarks>
        public FailOncePriceRepository(ProductPrice price)
        {
            _price = price ?? throw new ArgumentNullException(nameof(price));
        }

        /// <summary>
        /// 호출 횟수에 따라 첫 번에는 실패 Result, 다음부터는 성공 Result를 반환합니다.
        /// </summary>
        /// <param name="sku">조회할 SKU이며 이 가짜 구현에서는 가격 SKU와 같아야 합니다.</param>
        /// <param name="cancellationToken">호출 전에 확인할 취소 신호입니다.</param>
        /// <returns>스크립트된 실패 또는 성공을 이미 완료된 Task로 반환합니다.</returns>
        public Task<Result<ProductPrice>> FindAsync(
            ProductSku sku,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert(sku == _price.Sku, "FailOnce Repository에 알 수 없는 SKU가 전달되었습니다.");
            var call = Interlocked.Increment(ref _callCount);

            var result = call == 1
                ? Result<ProductPrice>.Failure(
                    new DomainError(ErrorKind.Dependency, "일시적인 원본 저장소 실패"))
                : Result<ProductPrice>.Success(_price);
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowOncePriceRepository : IPriceRepository
    {
        private readonly ProductPrice _price;
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 첫 호출은 예상 밖 예외, 이후 호출은 성공을 반환하는 Repository를 만듭니다.
        /// </summary>
        /// <param name="price">예외 뒤 복구 호출에서 반환할 가격입니다.</param>
        /// <remarks>생성자는 가격을 보관하므로 반환값이 없습니다.</remarks>
        public ThrowOncePriceRepository(ProductPrice price)
        {
            _price = price ?? throw new ArgumentNullException(nameof(price));
        }

        /// <summary>
        /// 첫 호출에서 예외를 던져 finally의 gate 해제를 검증하고 이후에는 가격을 반환합니다.
        /// </summary>
        /// <param name="sku">조회할 SKU이며 이 가짜 구현에서는 가격 SKU와 같아야 합니다.</param>
        /// <param name="cancellationToken">호출 전에 확인할 취소 신호입니다.</param>
        /// <returns>두 번째 호출부터 고정 가격 성공 Result를 담은 Task를 반환합니다.</returns>
        public Task<Result<ProductPrice>> FindAsync(
            ProductSku sku,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert(sku == _price.Sku, "ThrowOnce Repository에 알 수 없는 SKU가 전달되었습니다.");
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                throw new InvalidOperationException("예상 밖 원본 저장소 예외");
            }

            return Task.FromResult(Result<ProductPrice>.Success(_price));
        }
    }
}
