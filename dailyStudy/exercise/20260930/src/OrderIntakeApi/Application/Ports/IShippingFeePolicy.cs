namespace OrderIntakeApi.Application.Ports;

/// <summary>
/// 배송비 계산 규칙을 교체 가능한 Strategy로 표현합니다.
/// </summary>
public interface IShippingFeePolicy
{
    /// <summary>
    /// 상품 합계와 총수량을 바탕으로 배송비를 계산합니다.
    /// </summary>
    /// <param name="subtotal">0 이상인 상품 금액 합계입니다.</param>
    /// <param name="totalQuantity">모든 주문 줄 수량의 합입니다.</param>
    /// <returns>0 이상인 배송비를 반환합니다.</returns>
    decimal Calculate(decimal subtotal, int totalQuantity);
}
