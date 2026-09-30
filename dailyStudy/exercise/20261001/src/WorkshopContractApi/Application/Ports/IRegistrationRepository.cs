using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Application.Ports;

/// <summary>
/// 같은 예약 ID와 같은 회차 좌석의 충돌을 원자적으로 판정하는 Repository Port입니다.
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// 예약 ID와 회차·좌석을 한 번에 확인해 새 예약을 저장합니다.
    /// </summary>
    /// <param name="registration">검증과 가격 계산이 끝난 불변 Domain Model입니다.</param>
    /// <param name="cancellationToken">저장 호출을 취소할 신호입니다.</param>
    /// <returns>추가 성공, 예약 ID 중복, 좌석 선점 중 하나를 담아 완료되는 ValueTask를 반환합니다.</returns>
    ValueTask<RegistrationSaveOutcome> TryAddAsync(
        WorkshopRegistration registration,
        CancellationToken cancellationToken);

    /// <summary>
    /// 정규화된 예약 식별자로 저장된 예약을 조회합니다.
    /// </summary>
    /// <param name="registrationId">RegistrationRules를 통과한 예약 식별자입니다.</param>
    /// <param name="cancellationToken">조회 호출을 취소할 신호입니다.</param>
    /// <returns>예약이 있으면 Domain Model, 없으면 null을 담아 완료되는 ValueTask를 반환합니다.</returns>
    ValueTask<WorkshopRegistration?> FindByIdAsync(
        string registrationId,
        CancellationToken cancellationToken);
}
/// <summary>
/// Repository가 예외 없이 알려 줄 수 있는 예상 저장 결과입니다.
/// </summary>
public enum RegistrationSaveOutcome
{
    Added,
    DuplicateRegistrationId,
    SeatAlreadyTaken
}
