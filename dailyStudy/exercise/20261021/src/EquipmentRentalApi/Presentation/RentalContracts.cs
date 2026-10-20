using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Presentation;

/// <summary>
/// HTTP JSON 본문으로 들어오는 nullable 값을 숨기지 않는 장비 대여 요청 DTO입니다.
/// </summary>
/// <param name="ReservationId">재시도에도 같은 값을 써야 하는 예약 ID입니다.</param>
/// <param name="EquipmentId">빌릴 장비 ID입니다.</param>
/// <param name="BorrowerId">대여자 ID입니다.</param>
/// <param name="Category">laptop 또는 camera 문자열입니다.</param>
/// <param name="Purpose">대여 목적입니다.</param>
/// <param name="StartsAt">ISO 8601 형식의 대여 시작 시각입니다.</param>
/// <param name="EndsAt">ISO 8601 형식의 대여 종료 시각입니다.</param>
// record는 HTTP 경계의 값을 간결하게 묶고 값 기반 비교와 with 복사를 지원합니다.
// ?는 JSON 필드가 누락되거나 null일 수 있음을 보존하여 Application/Domain이 친절한 오류를 만들게 합니다.
public sealed record CreateRentalRequest(
    string? ReservationId,
    string? EquipmentId,
    string? BorrowerId,
    string? Category,
    string? Purpose,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>
/// 내부 Domain Model에서 공개할 값만 골라 만든 장비 대여 응답 DTO입니다.
/// </summary>
/// <param name="ReservationId">예약 조회에 사용할 ID입니다.</param>
/// <param name="EquipmentId">예약된 장비 ID입니다.</param>
/// <param name="BorrowerId">대여자 ID입니다.</param>
/// <param name="Category">적용한 장비 분류입니다.</param>
/// <param name="Purpose">정규화된 대여 목적입니다.</param>
/// <param name="StartsAtUtc">UTC 대여 시작 시각입니다.</param>
/// <param name="EndsAtUtc">UTC 대여 종료 시각입니다.</param>
/// <param name="CreatedAtUtc">UTC 예약 생성 시각입니다.</param>
public sealed record RentalResponse(
    string ReservationId,
    string EquipmentId,
    string BorrowerId,
    string Category,
    string Purpose,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// Domain Model을 HTTP에 공개해도 되는 평평한 응답 DTO로 변환합니다.
    /// </summary>
    /// <param name="reservation">Application이 반환한 유효한 불변 예약입니다.</param>
    /// <returns>내부 구현 형식을 노출하지 않는 RentalResponse를 반환합니다.</returns>
    public static RentalResponse FromDomain(RentalReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return new RentalResponse(
            reservation.ReservationId,
            reservation.EquipmentId,
            reservation.BorrowerId,
            reservation.Category,
            reservation.Purpose,
            reservation.Window.StartsAtUtc,
            reservation.Window.EndsAtUtc,
            reservation.CreatedAtUtc);
    }
}
