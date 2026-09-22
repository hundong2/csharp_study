using ConditionalCatalogApi.Domain;

namespace ConditionalCatalogApi.Infrastructure;

/// <summary>
/// 데모 서버와 자체 검증이 같은 유효한 시작 상품을 만들게 하는 작은 구성 도우미입니다.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// 매번 같은 ID와 값으로 검증된 시작 상품 한 개를 만듭니다.
    /// </summary>
    /// <returns>InMemoryCatalogRepository에 넣을 불변 상품 목록을 반환합니다.</returns>
    public static IReadOnlyList<CatalogItem> Create()
    {
        var creation = CatalogItem.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "기계식 키보드",
            129_000m);

        if (!creation.IsSuccess)
        {
            throw new InvalidOperationException(
                $"개발자가 작성한 seed data가 Domain 규칙을 어겼습니다: {creation.Error?.Message}");
        }

        // collection expression [값]은 C# 12부터 배열·목록을 짧게 만드는 문법입니다.
        // 시작 상품이 한 개라는 사실을 군더더기 없이 보여 주기 위해 사용합니다.
        return [creation.Value];
    }
}
