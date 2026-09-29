namespace OrderIntakeApi.Infrastructure;

/// <summary>
/// 입력 오류가 아니라 외부 상품 카탈로그의 일시 장애임을 오류 경계에 전달합니다.
/// </summary>
public sealed class ProductCatalogUnavailableException : Exception
{
    /// <summary>
    /// 내부 진단용 원인을 포함한 일시 장애 예외를 만듭니다.
    /// </summary>
    /// <param name="message">서버 로그에서만 사용할 진단 설명입니다.</param>
    /// <returns>생성자는 예외를 초기화하므로 별도 반환값은 없습니다.</returns>
    public ProductCatalogUnavailableException(string message)
        : base(message)
    {
    }
}
