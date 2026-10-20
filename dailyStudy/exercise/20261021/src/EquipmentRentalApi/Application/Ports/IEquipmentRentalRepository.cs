using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Application.Ports;

/// <summary>
/// 예약 저장 결과를 예외 대신 명시적인 세 상태로 표현합니다.
/// </summary>
public enum ReservationWriteStatus
{
    Saved,
    DuplicateReservationId,
    EquipmentUnavailable
}

/// <summary>
/// Application이 저장 기술을 몰라도 예약을 원자적으로 저장·조회할 수 있게 하는 Repository Port입니다.
/// </summary>
public interface IEquipmentRentalRepository
{
    /// <summary>
    /// 예약 ID 중복과 같은 장비의 시간 겹침을 확인하고 저장을 하나의 원자적 연산으로 처리합니다.
    /// </summary>
    /// <param name="reservation">정책과 Domain 검증을 모두 통과한 예약입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 작업을 중단하는 신호입니다.</param>
    /// <returns>저장, ID 중복, 장비 시간 충돌 중 하나를 담아 완료되는 ValueTask를 반환합니다.</returns>
    ValueTask<ReservationWriteStatus> TryAddAsync(
        RentalReservation reservation,
        CancellationToken cancellationToken);

    /// <summary>
    /// 예약 식별자로 저장된 예약 한 건을 조회합니다.
    /// </summary>
    /// <param name="reservationId">대문자로 정규화할 예약 식별자입니다.</param>
    /// <param name="cancellationToken">조회가 필요 없어졌을 때 작업을 중단하는 신호입니다.</param>
    /// <returns>예약이 있으면 불변 객체, 없으면 null을 담아 완료되는 ValueTask를 반환합니다.</returns>
    ValueTask<RentalReservation?> FindByIdAsync(
        string reservationId,
        CancellationToken cancellationToken);
}
