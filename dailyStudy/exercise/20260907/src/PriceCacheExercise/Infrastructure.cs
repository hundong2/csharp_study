// ConcurrentDictionary는 여러 Task가 동시에 읽고 쓸 수 있는 스레드 안전 키-값 컬렉션입니다.
using System.Collections.Concurrent;

// 메모리 저장소는 Repository Port의 작은 Adapter입니다. 실제 환경에서는 SQL 또는 외부 API 구현으로 바꿀 수 있습니다.
sealed class InMemoryPriceRepository : IPriceRepository
{
    private readonly IReadOnlyDictionary<ProductSku, ProductPrice> _prices;
    private int _callCount;

    // Volatile.Read는 다른 스레드가 바꾼 최신 정수 값을 관찰하도록 돕습니다.
    public int CallCount => Volatile.Read(ref _callCount);

    /// <summary>
    /// 검증된 가격 목록을 복사해 SKU로 빠르게 찾을 수 있는 메모리 Repository를 만듭니다.
    /// </summary>
    /// <param name="prices">중복 SKU가 없어야 하는 초기 상품 가격 목록입니다.</param>
    /// <remarks>입력을 복사하므로 호출자가 원래 컬렉션을 바꿔도 저장소 상태가 달라지지 않습니다. 반환값은 없습니다.</remarks>
    public InMemoryPriceRepository(IEnumerable<ProductPrice> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        var copy = new Dictionary<ProductSku, ProductPrice>();
        foreach (var price in prices)
        {
            ArgumentNullException.ThrowIfNull(price);
            if (!copy.TryAdd(price.Sku, price))
            {
                throw new ArgumentException(
                    $"중복 SKU가 있습니다: {price.Sku.Code}",
                    nameof(prices));
            }
        }

        _prices = copy;
    }

    /// <summary>
    /// 메모리 사전에서 SKU를 찾아 원본 가격 또는 미발견 오류를 반환합니다.
    /// </summary>
    /// <param name="sku">찾을 검증된 상품 코드입니다.</param>
    /// <param name="cancellationToken">조회 시작 전에 취소되었는지 확인할 신호입니다.</param>
    /// <returns>찾은 가격이나 NotFound 실패를 이미 완료된 Task로 감싸 반환합니다.</returns>
    public Task<Result<ProductPrice>> FindAsync(
        ProductSku sku,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sku);
        cancellationToken.ThrowIfCancellationRequested();

        // Interlocked.Increment는 동시 호출에서도 증가 연산이 서로 덮어쓰이지 않게 합니다.
        Interlocked.Increment(ref _callCount);

        // out var는 TryGetValue가 찾은 값을 새 지역 변수 price에 기록하게 하는 출력 매개변수 문법입니다.
        // ?:는 앞의 bool 조건에 따라 콜론 양쪽 값 중 하나를 고르는 조건 연산자입니다.
        var result = _prices.TryGetValue(sku, out var price)
            ? Result<ProductPrice>.Success(price)
            : Result<ProductPrice>.Failure(
                new DomainError(
                    ErrorKind.NotFound,
                    $"상품 {sku.Code}의 가격을 찾을 수 없습니다."));

        // 실제 I/O가 없는 학습용 저장소이므로 Task.FromResult로 비동기 계약을 맞춥니다.
        return Task.FromResult(result);
    }
}

// Adapter는 Repository의 ProductPrice 결과를 Application Port가 요구하는 PriceLookup 결과로 바꿉니다.
sealed class RepositoryPriceProvider : IPriceProvider
{
    private readonly IPriceRepository _repository;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 원본 Repository와 시계를 주입받아 IPriceProvider 계약에 맞추는 Adapter를 만듭니다.
    /// </summary>
    /// <param name="repository">상품 가격만 반환하는 영속 저장소 Port입니다.</param>
    /// <param name="timeProvider">성공한 원본 조회의 UTC 시각을 기록할 시간 추상화입니다.</param>
    /// <remarks>생성자는 두 의존성을 보관하며 반환값이 없습니다. Adapter 덕분에 Decorator는 같은 IPriceProvider 계약을 감쌀 수 있습니다.</remarks>
    public RepositoryPriceProvider(
        IPriceRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository ??
            throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Repository에서 가격을 읽고 원본 출처와 조회 시각을 더해 PriceLookup으로 변환합니다.
    /// </summary>
    /// <param name="sku">Repository에서 찾을 검증된 상품 코드입니다.</param>
    /// <param name="cancellationToken">원본 저장소 대기를 중단할 취소 신호입니다.</param>
    /// <returns>성공하면 Repository 출처의 PriceLookup, 실패하면 같은 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<PriceLookup>> GetAsync(
        ProductSku sku,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sku);

        var priceResult = await _repository.FindAsync(sku, cancellationToken);
        if (!priceResult.IsSuccess)
        {
            return Result<PriceLookup>.Failure(priceResult.Problem!);
        }

        return Result<PriceLookup>.Success(
            new PriceLookup(
                priceResult.Value!,
                PriceOrigin.Repository,
                _timeProvider.GetUtcNow()));
    }
}

// GoF Decorator는 자신과 같은 IPriceProvider 계약의 inner 객체를 감싸 캐시 책임을 덧붙입니다.
// Application Service를 수정하지 않고 성능 정책을 추가할 수 있어 OCP를 지키고 각 역할을 따로 테스트할 수 있습니다.
sealed class CachedPriceProvider : IPriceProvider
{
    private readonly IPriceProvider _inner;
    private readonly ICacheFreshnessPolicy _freshnessPolicy;
    private readonly TimeProvider _timeProvider;
    // 오른쪽 new()는 왼쪽 필드 선언에서 구체 형식을 추론하는 target-typed new 문법입니다.
    private readonly ConcurrentDictionary<ProductSku, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<ProductSku, SemaphoreSlim> _gates = new();
    private int _hitCount;
    private int _innerLoadCount;

    /// <summary>
    /// 같은 계약의 원본 Provider, 만료 Strategy, 교체 가능한 시계를 주입받아 캐시 Decorator를 만듭니다.
    /// </summary>
    /// <param name="inner">cache miss 때 호출할 또 다른 IPriceProvider 구현입니다.</param>
    /// <param name="freshnessPolicy">캐시 항목을 재사용할지 결정하는 Strategy입니다.</param>
    /// <param name="timeProvider">운영에서는 시스템 시각, 테스트에서는 수동 시각을 제공하는 시간 추상화입니다.</param>
    /// <remarks>생성자는 세 의존성을 보관하며 반환값이 없습니다. 생성자 주입으로 필수 의존성이 분명해집니다.</remarks>
    public CachedPriceProvider(
        IPriceProvider inner,
        ICacheFreshnessPolicy freshnessPolicy,
        TimeProvider timeProvider)
    {
        _inner = inner ??
            throw new ArgumentNullException(nameof(inner));
        _freshnessPolicy = freshnessPolicy ??
            throw new ArgumentNullException(nameof(freshnessPolicy));
        _timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 신선한 캐시를 우선 반환하고, miss면 SKU별 잠금 안에서 inner Provider를 호출해 성공값을 캐시합니다.
    /// </summary>
    /// <param name="sku">조회할 검증된 상품 코드이자 캐시와 잠금의 키입니다.</param>
    /// <param name="cancellationToken">잠금 대기와 inner Provider 호출을 중단할 취소 신호입니다.</param>
    /// <returns>가격·이번 호출의 출처·가격의 신선도 기준 시각 또는 예상 가능한 실패를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<PriceLookup>> GetAsync(
        ProductSku sku,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sku);
        cancellationToken.ThrowIfCancellationRequested();

        var cached = FindFresh(sku, _timeProvider.GetUtcNow());
        if (cached is not null)
        {
            Interlocked.Increment(ref _hitCount);
            return Result<PriceLookup>.Success(
                new PriceLookup(cached.Price, PriceOrigin.Cache, cached.FreshAsOfUtc));
        }

        // GetOrAdd는 SKU마다 SemaphoreSlim 하나를 공유하게 합니다.
        // delegate가 여러 번 실행될 수 있으므로 여기서는 외부 I/O 없이 값싼 객체만 만들고, 원본 조회는 잠금 안에서 수행합니다.
        // static 람다는 바깥 지역 변수를 캡처하지 않아 이 팩터리가 입력 key 외 상태에 의존하지 않음을 드러냅니다.
        var gate = _gates.GetOrAdd(
            sku,
            static _ => new SemaphoreSlim(initialCount: 1, maxCount: 1));

        // WaitAsync는 스레드를 막지 않고 잠금 차례를 기다리며, 취소 신호도 관찰합니다.
        await gate.WaitAsync(cancellationToken);
        try
        {
            // 잠금을 기다리는 사이 앞선 요청이 캐시를 채웠을 수 있으므로 반드시 한 번 더 확인합니다.
            cached = FindFresh(sku, _timeProvider.GetUtcNow());
            if (cached is not null)
            {
                Interlocked.Increment(ref _hitCount);
                return Result<PriceLookup>.Success(
                    new PriceLookup(cached.Price, PriceOrigin.Cache, cached.FreshAsOfUtc));
            }

            Interlocked.Increment(ref _innerLoadCount);
            var innerResult = await _inner.GetAsync(
                sku,
                cancellationToken);

            if (!innerResult.IsSuccess)
            {
                // 실패를 캐시하면 일시 장애가 TTL 동안 고정될 수 있으므로 이 예제는 성공만 캐시합니다.
                return Result<PriceLookup>.Failure(innerResult.Problem!);
            }

            var innerLookup = innerResult.Value!;
            // inner가 또 다른 캐시 Decorator여도 TTL이 새로 시작되지 않도록 원본 신선도 기준 시각을 그대로 보존합니다.
            var entry = new CacheEntry(innerLookup.Price, innerLookup.FreshAsOfUtc);

            // ConcurrentDictionary의 인덱서는 같은 키의 값을 원자적으로 추가하거나 교체합니다.
            _cache[sku] = entry;

            return Result<PriceLookup>.Success(
                new PriceLookup(entry.Price, innerLookup.Origin, entry.FreshAsOfUtc));
        }
        finally
        {
            // finally는 성공, 실패, 취소 예외 어느 경로에서도 잠금을 풀어 다음 요청이 영원히 멈추지 않게 합니다.
            gate.Release();
        }
    }

    /// <summary>
    /// 지금까지의 캐시 hit, inner Provider 조회 시도, 보관 항목 수를 일관된 값 객체로 복사합니다.
    /// </summary>
    /// <returns>호출 시점의 세 카운터를 담은 CacheStatistics를 반환합니다.</returns>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics(
            Volatile.Read(ref _hitCount),
            Volatile.Read(ref _innerLoadCount),
            _cache.Count);
    }

    /// <summary>
    /// 현재 캐시에 들어 있는 SKU 코드를 정렬된 읽기 전용 스냅샷으로 만듭니다.
    /// </summary>
    /// <returns>호출 뒤 캐시가 바뀌어도 달라지지 않는 정렬된 문자열 배열을 반환합니다.</returns>
    public IReadOnlyList<string> GetCachedSkuCodes()
    {
        // Select는 키 객체에서 Code만 고르고, OrderBy는 출력이 실행마다 같은 순서가 되게 하며, ToArray는 즉시 복사합니다.
        return _cache.Keys
            .Select(sku => sku.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// SKU의 캐시 항목이 존재하고 만료 정책을 통과하면 그 항목을 찾습니다.
    /// </summary>
    /// <param name="sku">ConcurrentDictionary에서 찾을 키입니다.</param>
    /// <param name="nowUtc">신선도를 판단할 현재 UTC 시각입니다.</param>
    /// <returns>신선한 CacheEntry가 있으면 해당 객체, 없거나 만료되었으면 null을 반환합니다.</returns>
    private CacheEntry? FindFresh(ProductSku sku, DateTimeOffset nowUtc)
    {
        if (!_cache.TryGetValue(sku, out var entry))
        {
            return null;
        }

        // ?:는 조건에 따라 두 값 중 하나를 고르는 조건 연산자입니다.
        return _freshnessPolicy.IsFresh(entry.FreshAsOfUtc, nowUtc)
            ? entry
            : null;
    }

    // private record는 캐시 구현 안에서만 필요한 가격과 신선도 기준 시각을 불변 한 묶음으로 보관합니다.
    /// <summary>
    /// 캐시에 넣은 검증된 가격과 원본에서 물려받은 신선도 기준 시각을 묶습니다.
    /// </summary>
    /// <param name="Price">원본 Repository에서 성공적으로 읽은 가격입니다.</param>
    /// <param name="FreshAsOfUtc">중첩 Decorator에서도 보존하는 TTL 계산의 시작 UTC 시각입니다.</param>
    /// <remarks>주 생성자는 두 값을 초기화하며 별도 반환값은 없습니다.</remarks>
    private sealed record CacheEntry(ProductPrice Price, DateTimeOffset FreshAsOfUtc);
}

// ManualTimeProvider는 실제로 기다리지 않고 시간을 앞으로 이동시켜 TTL 경계를 검증하는 테스트 대역입니다.
sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _sync = new();
    private DateTimeOffset _utcNow;

    /// <summary>
    /// 테스트와 데모가 사용할 첫 UTC 시각으로 수동 시계를 만듭니다.
    /// </summary>
    /// <param name="initialUtc">오프셋이 UTC(0)여야 하는 시작 시각입니다.</param>
    /// <remarks>운영에서는 TimeProvider.System을 주입하며, 이 생성자는 학습용 시각만 초기화하고 반환값은 없습니다.</remarks>
    public ManualTimeProvider(DateTimeOffset initialUtc)
    {
        if (initialUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("시작 시각은 UTC여야 합니다.", nameof(initialUtc));
        }

        _utcNow = initialUtc;
    }

    /// <summary>
    /// 수동 시계가 현재 보관한 UTC 시각을 스레드 안전하게 읽습니다.
    /// </summary>
    /// <returns>Advance로 이동할 수 있는 현재 DateTimeOffset 값을 반환합니다.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        // lock은 여러 Task가 동시에 읽고 이동할 때 중간 상태를 보지 않도록 임계 구역을 만듭니다.
        lock (_sync)
        {
            return _utcNow;
        }
    }

    /// <summary>
    /// 실제 대기 없이 수동 시각을 지정한 양만큼 앞으로 이동합니다.
    /// </summary>
    /// <param name="amount">0보다 큰 이동 기간입니다.</param>
    /// <returns>상태만 변경하므로 반환값은 없습니다.</returns>
    public void Advance(TimeSpan amount)
    {
        if (amount <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "이동 시간은 0보다 커야 합니다.");
        }

        lock (_sync)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
