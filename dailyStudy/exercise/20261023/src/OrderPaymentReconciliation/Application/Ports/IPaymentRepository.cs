using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Application.Ports;

/// <summary>
/// Application 계층이 결제의 실제 저장 방식에 의존하지 않게 만드는 Repository Port입니다.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// 현재 대사 대상인 모든 결제를 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 작업을 중단하라는 신호입니다.</param>
    /// <returns>수정할 수 없는 것으로 취급해야 하는 결제 목록을 비동기로 반환합니다.</returns>
    Task<IReadOnlyList<Payment>> ListAsync(CancellationToken cancellationToken);
}
