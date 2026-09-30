namespace WorkshopContractApi.Application;

/// <summary>
/// HTTP DTO를 그대로 Domain에 흘리지 않고 use case 입력으로 옮긴 명령입니다.
/// </summary>
/// <param name="RegistrationId">클라이언트가 정한 예약 식별자입니다.</param>
/// <param name="SessionId">신청할 워크숍 회차 식별자입니다.</param>
/// <param name="AttendeeId">수강생 식별자입니다.</param>
/// <param name="SeatNumber">예약하려는 좌석 번호입니다.</param>
/// <param name="AttendeeTier">Standard 또는 Premium 문자열입니다.</param>
// nullable 속성은 JSON에 필드가 없거나 null인 현실을 경계에서 인정하고 Domain factory가 거절하게 합니다.
public sealed record RegisterWorkshopCommand(
    string? RegistrationId,
    string? SessionId,
    string? AttendeeId,
    int SeatNumber,
    string? AttendeeTier);
