// interface는 구현 세부사항 대신 호출자가 기대할 수 있는 메서드 모양을 선언하는 계약입니다.
// Repository Port 덕분에 캐시 코드는 데이터베이스나 HTTP 클라이언트를 직접 알 필요가 없습니다.
interface IPriceRepository
{
    /// <summary>
    /// 지정한 SKU의 최신 원본 가격을 영속 저장소에서 찾습니다.
    /// </summary>
    /// <param name="sku">조회할 검증된 상품 코드입니다.</param>
    /// <param name="cancellationToken">대기 중인 저장소 작업을 중단하도록 전달하는 취소 신호입니다.</param>
    /// <returns>가격을 찾으면 성공 Result, 없거나 의존성이 실패하면 오류 Result를 담아 완료되는 Task를 반환합니다.</returns>
    Task<Result<ProductPrice>> FindAsync(
        ProductSku sku,
        CancellationToken cancellationToken);
}

// Application Service는 이 Port만 보므로 캐시가 추가되어도 유스케이스 코드는 바뀌지 않습니다.
// 이는 SOLID의 DIP(의존성 역전)와 OCP(확장에는 열고 수정에는 닫기)를 적용한 모습입니다.
interface IPriceProvider
{
    /// <summary>
    /// 구현 방식과 관계없이 지정한 SKU의 현재 가격 조회 결과를 제공합니다.
    /// </summary>
    /// <param name="sku">조회할 검증된 상품 코드입니다.</param>
    /// <param name="cancellationToken">구현이 수행하는 비동기 조회 작업을 중단할 취소 신호입니다.</param>
    /// <returns>가격·이번 호출의 출처·가격의 신선도 기준 시각 또는 예상 실패를 담아 완료되는 Task를 반환합니다.</returns>
    Task<Result<PriceLookup>> GetAsync(
        ProductSku sku,
        CancellationToken cancellationToken);
}

// Strategy는 "캐시가 아직 신선한가"라는 바뀔 수 있는 정책을 저장 방식과 분리합니다.
interface ICacheFreshnessPolicy
{
    /// <summary>
    /// 가격이 원본에서 확인된 신선도 기준 시각과 현재 시각을 비교해 항목을 재사용해도 되는지 판정합니다.
    /// </summary>
    /// <param name="freshAsOfUtc">가격이 원본에서 확인되어 TTL 계산을 시작한 UTC 시각입니다.</param>
    /// <param name="nowUtc">TimeProvider가 알려 준 현재 UTC 시각입니다.</param>
    /// <returns>나이가 TTL보다 짧고 시간이 뒤로 가지 않았으면 true, 아니면 false를 반환합니다.</returns>
    bool IsFresh(DateTimeOffset freshAsOfUtc, DateTimeOffset nowUtc);
}

// 고정 TTL(Time To Live)은 원본 확인 후 정해진 시간까지만 값을 재사용하는 가장 단순한 Strategy입니다.
sealed class FixedTtlFreshnessPolicy : ICacheFreshnessPolicy
{
    public TimeSpan Lifetime { get; }

    /// <summary>
    /// 모든 캐시 항목에 적용할 양수 TTL을 받아 고정 만료 정책을 만듭니다.
    /// </summary>
    /// <param name="lifetime">항목을 신선하다고 볼 최대 기간이며 0보다 커야 합니다.</param>
    /// <remarks>잘못된 TTL은 사용자 입력 실패가 아니라 구성 오류이므로 생성 시 예외로 빠르게 알립니다. 반환값은 없습니다.</remarks>
    public FixedTtlFreshnessPolicy(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "캐시 TTL은 0보다 커야 합니다.");
        }

        Lifetime = lifetime;
    }

    /// <summary>
    /// 원본 확인 시각부터 현재까지의 나이를 계산해 고정 TTL 안인지 확인합니다.
    /// </summary>
    /// <param name="freshAsOfUtc">가격이 원본에서 확인되어 TTL 계산을 시작한 UTC 시각입니다.</param>
    /// <param name="nowUtc">판정 기준이 되는 현재 UTC 시각입니다.</param>
    /// <returns>0 이상 TTL 미만의 나이면 true, 정확히 TTL에 닿거나 시간을 거슬렀으면 false를 반환합니다.</returns>
    public bool IsFresh(DateTimeOffset freshAsOfUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - freshAsOfUtc;
        return age >= TimeSpan.Zero && age < Lifetime;
    }
}

// Application Service는 입력 검증과 가격 조회의 순서를 조정할 뿐 캐시·잠금·저장소 구현은 모릅니다.
sealed class PriceQueryService
{
    private readonly IPriceProvider _priceProvider;

    /// <summary>
    /// 가격을 가져올 Port 구현을 외부에서 주입받아 조회 유스케이스를 조립합니다.
    /// </summary>
    /// <param name="priceProvider">캐시 여부와 관계없이 가격을 제공하는 추상화입니다.</param>
    /// <remarks>생성자는 의존성을 보관하므로 반환값이 없습니다. DI 덕분에 테스트에서는 가짜 구현을 넣을 수 있습니다.</remarks>
    public PriceQueryService(IPriceProvider priceProvider)
    {
        // ?? throw는 왼쪽 값이 null이면 즉시 구성 오류를 던지고, nameof는 매개변수 이름을 안전하게 얻습니다.
        _priceProvider = priceProvider ??
            throw new ArgumentNullException(nameof(priceProvider));
    }

    /// <summary>
    /// 바깥 문자열을 SKU 값 객체로 검증한 뒤 주입된 가격 Provider의 조회를 실행합니다.
    /// </summary>
    /// <param name="rawSku">콘솔이나 API에서 들어온 아직 검증되지 않은 상품 코드 문자열입니다.</param>
    /// <param name="cancellationToken">전체 조회 흐름을 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>성공하면 가격 조회 정보, 검증·Provider 실패면 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<PriceLookup>> QueryAsync(
        string? rawSku,
        CancellationToken cancellationToken)
    {
        var skuResult = ProductSku.Create(rawSku);
        if (!skuResult.IsSuccess)
        {
            // !는 앞선 검사 덕분에 Problem이 null이 아님을 컴파일러에 알립니다. 실제 null 검사를 대신하지는 않습니다.
            return Result<PriceLookup>.Failure(skuResult.Problem!);
        }

        // async/await는 저장소나 잠금을 기다리는 동안 스레드를 붙잡지 않고, 끝난 뒤 결과 처리를 이어 갑니다.
        return await _priceProvider.GetAsync(
            skuResult.Value!,
            cancellationToken);
    }
}
