using OrderPaymentReconciliation.Application.Ports;
using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Application;

/// <summary>
/// 결제가 하나라는 전제에서 통화가 같고 금액 차이가 0일 때만 일치로 판정하는 기본 Strategy입니다.
/// </summary>
public sealed class AmountAndCurrencyPolicy : IReconciliationPolicy
{
    /// <summary>
    /// 주문과 nullable 결제를 비교해 누락, 통화 불일치, 금액 불일치, 일치 중 하나를 결정합니다.
    /// </summary>
    /// <param name="order">기대 금액을 가진 기준 주문입니다.</param>
    /// <param name="payment">LeftJoin 결과에서 결제가 없으면 null인 실제 결제입니다.</param>
    /// <returns>판정 근거와 비교 가능한 경우의 차액을 담은 대사 행을 반환합니다.</returns>
    public ReconciliationLine Compare(Order order, Payment? payment)
    {
        ArgumentNullException.ThrowIfNull(order);

        // is null 패턴은 "결제 없음"을 먼저 처리해 아래 코드가 안전하게 Payment를 사용하게 합니다.
        if (payment is null)
        {
            return ReconciliationLine.ForMissing(
                order,
                "주문은 있지만 연결된 결제가 없습니다.");
        }

        if (!string.Equals(
                order.Total.Currency,
                payment.SettledAmount.Currency,
                StringComparison.Ordinal))
        {
            return ReconciliationLine.ForCurrencyMismatch(
                order,
                payment,
                "통화가 달라 숫자 금액만으로 차액을 계산할 수 없습니다.");
        }

        var difference = payment.SettledAmount.Amount - order.Total.Amount;
        // switch 식은 하나의 차액을 판정 문장 하나로 바꾸며, 각 => arm이 조건별 결과를 뜻합니다.
        var reason = difference switch
        {
            0m => "주문 금액과 결제 금액이 정확히 일치합니다.",
            > 0m => $"결제 금액이 주문 금액보다 {difference:0.00}만큼 큽니다.",
            _ => $"결제 금액이 주문 금액보다 {Math.Abs(difference):0.00}만큼 작습니다."
        };
        // ForPolicyMatch는 정책이 내린 결론을 기록합니다. 허용 범위를 Domain factory에 중복하면
        // 다른 Strategy를 추가할 때 factory까지 고쳐야 하므로 OCP와 역할 분리가 깨집니다.
        return difference == 0m
            ? ReconciliationLine.ForPolicyMatch(order, payment, reason)
            : ReconciliationLine.ForAmountMismatch(order, payment, reason);
    }

    /// <summary>
    /// 주문 목록에서 찾을 수 없는 결제를 사람이 확인할 고아 결제 행으로 바꿉니다.
    /// </summary>
    /// <param name="payment">존재하지 않는 OrderId를 가리키는 결제입니다.</param>
    /// <returns>기대 금액은 null이고 실제 금액은 보존된 OrphanPayment 행을 반환합니다.</returns>
    public ReconciliationLine MarkOrphan(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return ReconciliationLine.ForOrphan(
            payment,
            "결제가 가리키는 주문을 찾을 수 없습니다.");
    }
}
