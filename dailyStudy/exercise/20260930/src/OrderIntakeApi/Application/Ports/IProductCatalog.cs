namespace OrderIntakeApi.Application.Ports;

/// <summary>
/// Application이 가격 제공 기술을 직접 알지 않도록 만드는 상품 카탈로그 Port입니다.
/// </summary>
public interface IProductCatalog
{
    /// <summary>
    /// 요청한 SKU 가운데 존재하는 상품의 현재 단가를 조회합니다.
    /// </summary>
    /// <param name="skus">중복을 제거한 정규화 SKU 모음입니다.</param>
    /// <param name="cancellationToken">요청 중단 신호를 실제 I/O 대기까지 전달합니다.</param>
    /// <returns>존재하는 SKU와 단가의 읽기 전용 사전을 반환합니다.</returns>
    Task<IReadOnlyDictionary<string, decimal>> GetUnitPricesAsync(
        IReadOnlySet<string> skus,
        CancellationToken cancellationToken);
}
