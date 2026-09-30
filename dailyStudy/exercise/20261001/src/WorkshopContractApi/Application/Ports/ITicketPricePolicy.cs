using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Application.Ports;

/// <summary>
/// 가격 규칙을 Application Service에서 교체할 수 있게 분리한 Strategy Port입니다.
/// </summary>
public interface ITicketPricePolicy
{
    /// <summary>
    /// 워크숍 기본 가격과 수강생 등급으로 최종 원화 가격을 계산합니다.
    /// </summary>
    /// <param name="session">카탈로그가 반환한 유효한 워크숍 회차입니다.</param>
    /// <param name="tier">RegistrationDraft가 해석한 수강생 등급입니다.</param>
    /// <returns>0 이상인 최종 원화 가격을 반환합니다.</returns>
    int CalculatePriceWon(WorkshopSession session, AttendeeTier tier);
}
