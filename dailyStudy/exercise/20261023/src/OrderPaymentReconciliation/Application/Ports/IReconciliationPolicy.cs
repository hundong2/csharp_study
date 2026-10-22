using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Application.Ports;

/// <summary>
/// 주문과 결제를 어떤 상태로 분류할지 캡슐화하는 Strategy Port입니다.
/// 규칙을 Application Service에서 분리하면 다른 허용 오차 정책을 독립적으로 교체하고 검사할 수 있습니다.
/// </summary>
public interface IReconciliationPolicy
{
    /// <summary>
    /// 반드시 존재하는 주문과 있을 수도 없는 결제를 비교해 보고서 한 행을 만듭니다.
    /// </summary>
    /// <param name="order">LeftJoin이 항상 보존하는 주문입니다.</param>
    /// <param name="payment">연결된 결제가 없으면 null인 실제 결제입니다.</param>
    /// <returns>일치, 누락, 금액 불일치 또는 통화 불일치로 판정한 행을 반환합니다.</returns>
    ReconciliationLine Compare(Order order, Payment? payment);

    /// <summary>
    /// 어떤 주문에도 연결되지 않은 결제를 별도 검토 행으로 만듭니다.
    /// </summary>
    /// <param name="payment">OrderId가 주문 목록에 존재하지 않는 결제입니다.</param>
    /// <returns>OrphanPayment 상태와 판정 이유를 가진 행을 반환합니다.</returns>
    ReconciliationLine MarkOrphan(Payment payment);
}
