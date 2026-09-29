using OrderIntakeApi.Application.Ports;

namespace OrderIntakeApi.Application;

/// <summary>
/// 5만 원 이상이면 무료, 아니면 기본 배송비를 적용하는 배송 Strategy입니다.
/// </summary>
public sealed class ThresholdShippingFeePolicy : IShippingFeePolicy
{
    private const decimal FreeShippingThreshold = 50_000m;
    private const decimal StandardShippingFee = 3_000m;

    /// <summary>
    /// 상품 합계가 무료 배송 기준을 넘는지에 따라 배송비를 선택합니다.
    /// </summary>
    /// <param name="subtotal">검증할 상품 합계입니다.</param>
    /// <param name="totalQuantity">0보다 커야 하는 전체 상품 수량입니다.</param>
    /// <returns>무료 배송이면 0, 아니면 3,000원을 반환합니다.</returns>
    public decimal Calculate(decimal subtotal, int totalQuantity)
    {
        if (subtotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(subtotal), "상품 합계는 음수일 수 없습니다.");
        }

        if (totalQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalQuantity), "전체 수량은 0보다 커야 합니다.");
        }

        // 조건 ? A : B는 조건이 참이면 A, 거짓이면 B를 선택하는 삼항 연산자입니다.
        return subtotal >= FreeShippingThreshold ? 0m : StandardShippingFee;
    }
}
