namespace GlobalMeetingApi.Application;

/// <summary>
/// Presentation 입력을 Application Service에 전달하는 불변 명령입니다.
/// </summary>
/// <param name="MeetingId">중복 요청을 식별할 회의 ID입니다.</param>
/// <param name="OrganizerId">시간 충돌을 검사할 주최자 ID입니다.</param>
/// <param name="Title">사용자에게 보일 회의 제목입니다.</param>
/// <param name="LocalDate">주최자가 입력한 지역 달력 날짜입니다.</param>
/// <param name="LocalStartTime">주최자가 입력한 지역 벽시계 시작 시각입니다.</param>
/// <param name="TimeZoneId">DST 규칙을 찾을 IANA 시간대 ID입니다.</param>
/// <param name="PreferredUtcOffset">중복 시각에서 사용자가 고른 offset이며 평상시에는 null입니다.</param>
/// <param name="DurationMinutes">15분 단위의 회의 길이입니다.</param>
// record는 명령을 불변에 가깝게 전달하고 값 중심으로 읽게 합니다.
public sealed record ScheduleMeetingCommand(
    string? MeetingId,
    string? OrganizerId,
    string? Title,
    DateOnly LocalDate,
    TimeOnly LocalStartTime,
    string? TimeZoneId,
    TimeSpan? PreferredUtcOffset,
    int DurationMinutes);
