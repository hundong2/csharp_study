using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Infrastructure;

/// <summary>
/// 납기일을 지키는 견적만 남긴 뒤 총액, 빠른 배송, 공급업체 ID 순으로 선택하는 Strategy입니다.
/// 정책을 별도 class로 두면 서비스의 병렬 수집 코드를 건드리지 않고 선택 규칙을 교체할 수 있습니다.
/// </summary>
public sealed class LowestTotalCostPolicy : IQuoteSelectionPolicy
{
    /// <summary>
    /// 납기 조건을 만족하는 성공 견적 중 결정적인 정렬 규칙으로 가장 좋은 하나를 고릅니다.
    /// </summary>
    /// <param name="request">총액 계산 수량과 마지막 허용 납기일을 제공하는 요청입니다.</param>
    /// <param name="quotes">Gateway 호출에 성공한 견적 모음입니다.</param>
    /// <returns>최저 총액 견적을 반환하며 납기 내 항목이 하나도 없으면 null을 반환합니다.</returns>
    public SupplierQuote? Select(
        QuoteRequest request,
        IReadOnlyCollection<SupplierQuote> quotes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(quotes);

        // Where/OrderBy/ThenBy는 원본 collection을 바꾸지 않고 필터와 정렬 규칙을 이어 붙이는 LINQ입니다.
        // FirstOrDefault는 항목이 없을 때 reference type의 기본값 null을 반환합니다.
        return quotes
            .Where(quote => quote.DeliveryDate <= request.NeedBy)
            .OrderBy(quote => quote.TotalFor(request.Quantity))
            .ThenBy(quote => quote.DeliveryDate)
            .ThenBy(quote => quote.SupplierId, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
