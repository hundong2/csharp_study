using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Application.Ports;

/// <summary>
/// Application이 구체 HTTP SDK를 몰라도 공급업체 견적을 요청하게 하는 Gateway Port입니다.
/// 인터페이스 뒤에 외부 연동을 숨기면 fake로 빠르고 결정적인 테스트를 만들 수 있습니다.
/// </summary>
public interface ISupplierQuoteGateway
{
    /// <summary>이 gateway가 연결한 공급업체의 안정적인 ID를 반환합니다.</summary>
    string SupplierId { get; }

    /// <summary>
    /// 한 공급업체에 견적을 비동기로 요청합니다.
    /// </summary>
    /// <param name="request">상품, 수량, 납기일을 담은 검증된 요청입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 보내는 취소 신호입니다.</param>
    /// <returns>견적 성공 또는 재고 없음 같은 예상 가능한 외부 실패를 담은 Task를 반환합니다.</returns>
    Task<Result<SupplierQuote>> RequestQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken);
}
