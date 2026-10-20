namespace EquipmentRentalApi.Application;

/// <summary>
/// Presentation 계층이 해석한 장비 대여 입력을 Application Service에 전달하는 불변 명령입니다.
/// </summary>
/// <param name="ReservationId">클라이언트가 재시도에도 동일하게 보내는 예약 식별자입니다.</param>
/// <param name="EquipmentId">대여할 장비 식별자입니다.</param>
/// <param name="BorrowerId">장비를 사용할 사내 사용자 식별자입니다.</param>
/// <param name="Category">laptop 또는 camera 정책을 선택할 장비 분류입니다.</param>
/// <param name="Purpose">대여 목적을 설명하는 문자열입니다.</param>
/// <param name="StartsAt">호출자가 보낸 offset을 포함한 대여 시작 시각입니다.</param>
/// <param name="EndsAt">호출자가 보낸 offset을 포함한 대여 종료 시각입니다.</param>
// record는 값 중심 자료를 간결하게 선언하는 문법입니다. 명령을 만든 뒤 바꾸지 않게 해 흐름을 추적하기 쉽습니다.
// string?의 ?는 null도 올 수 있음을 compiler에 알립니다. 외부 입력의 누락을 숨기지 않고 Domain 검증으로 넘깁니다.
public sealed record CreateRentalCommand(
    string? ReservationId,
    string? EquipmentId,
    string? BorrowerId,
    string? Category,
    string? Purpose,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
