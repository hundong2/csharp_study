using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Application.Ports;

/// <summary>
/// Application이 데이터 저장 방식에 의존하지 않도록 만드는 Repository Port입니다.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// 한 고객의 청구 항목을 비동기로 읽고 취소 요청을 저장소 작업까지 전달합니다.
    /// </summary>
    /// <param name="customerId">정규화가 끝난 고객 번호입니다.</param>
    /// <param name="simulatedLatency">timeout 흐름을 재현할 교육용 지연 시간입니다.</param>
    /// <param name="cancellationToken">호출 중단이나 서버 제한 시간을 알리는 협력적 취소 신호입니다.</param>
    /// <returns>고객의 청구 항목 목록을 담아 완료되는 Task를 반환하며, 고객이 없으면 빈 목록입니다.</returns>
    Task<IReadOnlyList<InvoiceLine>> LoadInvoiceLinesAsync(
        string customerId,
        TimeSpan simulatedLatency,
        CancellationToken cancellationToken);
}
