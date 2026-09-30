using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Application;

/// <summary>
/// Standard는 정가, Premium은 20% 할인하는 학습용 가격 Strategy입니다.
/// </summary>
public sealed class StandardTicketPricePolicy : ITicketPricePolicy
{
    /// <summary>
    /// 회차 기본 가격과 등급을 이용해 소수점 없는 원화 가격을 계산합니다.
    /// </summary>
    /// <param name="session">양수 기본 가격을 가진 워크숍 회차입니다.</param>
    /// <param name="tier">Standard 또는 Premium 수강생 등급입니다.</param>
    /// <returns>Standard 정가 또는 Premium 20% 할인 가격을 반환합니다.</returns>
    public int CalculatePriceWon(WorkshopSession session, AttendeeTier tier)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.BasePriceWon < 0)
        {
            throw new InvalidOperationException("워크숍 기본 가격은 음수일 수 없습니다.");
        }

        // switch expression은 등급별 규칙을 입력 하나와 결과 하나의 대응으로 읽기 쉽게 표현합니다.
        // 80L의 L은 곱셈을 long으로 계산해 최종 할인액은 int여도 중간 값만 넘치는 문제를 막습니다.
        // checked는 최종 값을 int로 되돌릴 때 범위를 넘으면 잘못 감싼 숫자 대신 예외로 드러냅니다.
        return tier switch
        {
            AttendeeTier.Standard => session.BasePriceWon,
            AttendeeTier.Premium => checked((int)(session.BasePriceWon * 80L / 100)),
            // nameof는 변수 이름을 안전하게 문자열 "tier"로 만들어 rename 때 함께 갱신되게 합니다.
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "지원하지 않는 수강생 등급입니다.")
        };
    }
}
