using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Application.Ports;

/// <summary>
/// 성공 견적 중 하나를 고르는 변경 가능한 업무 규칙을 Strategy Port로 분리합니다.
/// </summary>
public interface IQuoteSelectionPolicy
{
    /// <summary>
    /// 요청 조건을 만족하는 성공 견적 중 정책상 가장 좋은 항목을 고릅니다.
    /// </summary>
    /// <param name="request">수량과 최종 납기일을 제공하는 원본 요청입니다.</param>
    /// <param name="quotes">공급업체 호출에서 성공한 견적 모음입니다.</param>
    /// <returns>선택한 견적을 반환하며 조건을 만족하는 항목이 없으면 null을 반환합니다.</returns>
    SupplierQuote? Select(QuoteRequest request, IReadOnlyCollection<SupplierQuote> quotes);
}
