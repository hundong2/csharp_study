using EquipmentRentalApi.Application;
using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Tests.TestDoubles;

/// <summary>
/// 각 테스트가 중요하지 않은 문자열 조립보다 검증하려는 차이에 집중하도록 기본 입력을 제공합니다.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// 고정 현재 시각을 기준으로 유효한 노트북 대여 명령을 만듭니다.
    /// </summary>
    /// <param name="reservationId">각 테스트가 중복 여부를 제어할 예약 식별자입니다.</param>
    /// <param name="equipmentId">각 테스트가 충돌 여부를 제어할 장비 식별자입니다.</param>
    /// <returns>한 시간 뒤부터 하루 동안 빌리는 유효한 CreateRentalCommand를 반환합니다.</returns>
    internal static CreateRentalCommand ValidLaptopCommand(
        string reservationId = "RES-TEST-001",
        string equipmentId = "EQ-LAPTOP-001")
    {
        return new CreateRentalCommand(
            reservationId,
            equipmentId,
            "USER-100",
            "laptop",
            "통합 테스트 실습 장비",
            FixedNow.AddHours(1),
            FixedNow.AddDays(1));
    }

    /// <summary>
    /// Repository 단독 테스트에 쓸 완성된 노트북 예약을 만듭니다.
    /// </summary>
    /// <param name="reservationId">저장 경쟁에서 각 후보를 구분할 예약 ID입니다.</param>
    /// <param name="equipmentId">시간 충돌을 의도적으로 만들 장비 ID입니다.</param>
    /// <returns>Domain 불변식을 만족하는 RentalReservation을 반환합니다.</returns>
    internal static RentalReservation ValidReservation(
        string reservationId,
        string equipmentId = "EQ-LAPTOP-001")
    {
        // var는 factory 반환 형식이 분명한 지역 변수에서 긴 generic 형식 반복을 피합니다.
        var window = RentalWindow.Create(FixedNow.AddHours(1), FixedNow.AddDays(1));
        if (!window.IsSuccess)
        {
            throw new InvalidOperationException($"테스트 기본 시간 구간이 잘못되었습니다: {window.Error.Message}");
        }

        var reservation = RentalReservation.Create(
            reservationId,
            equipmentId,
            "USER-100",
            "laptop",
            "동시성 저장 검증 장비",
            window.Value,
            FixedNow);
        if (!reservation.IsSuccess)
        {
            throw new InvalidOperationException($"테스트 기본 예약이 잘못되었습니다: {reservation.Error.Message}");
        }

        return reservation.Value;
    }

    /// <summary>
    /// 모든 테스트에서 공유하는 논리적 현재 시각을 제공합니다.
    /// </summary>
    // => 식 본문 속성은 별도 상태 없이 같은 고정 시각을 즉시 반환하는 짧은 구현에 사용합니다.
    internal static DateTimeOffset FixedNow =>
        new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
}
