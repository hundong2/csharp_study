using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Application.Ports;

/// <summary>
/// Application이 데이터베이스 종류를 몰라도 완료된 수집 회차를 저장하게 하는 Repository Port입니다.
/// </summary>
public interface IQuoteRoundRepository
{
    /// <summary>
    /// 모든 공급업체 작업이 끝난 뒤 최종 report를 한 번 저장합니다.
    /// </summary>
    /// <param name="report">불변 완료 순서와 선택 결과를 담은 report입니다.</param>
    /// <param name="cancellationToken">저장 전후에 호출자가 보낼 수 있는 취소 신호입니다.</param>
    /// <returns>저장이 완료될 때 끝나는 Task를 반환합니다.</returns>
    Task SaveAsync(QuoteRoundReport report, CancellationToken cancellationToken);
}
