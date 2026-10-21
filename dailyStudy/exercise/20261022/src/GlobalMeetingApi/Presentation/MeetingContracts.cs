using System.Globalization;
using GlobalMeetingApi.Application;
using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Presentation;

/// <summary>
/// JSON으로 받는 회의 생성 계약입니다.
/// </summary>
/// <param name="MeetingId">멱등한 재요청과 중복을 식별할 회의 ID입니다.</param>
/// <param name="OrganizerId">UTC 시간 충돌을 검사할 주최자 ID입니다.</param>
/// <param name="Title">1~100자의 회의 제목입니다.</param>
/// <param name="LocalDate">주최자의 달력 날짜이며 JSON `YYYY-MM-DD` 형식입니다.</param>
/// <param name="LocalStartTime">주최자의 벽시계 시각이며 JSON `HH:mm:ss` 형식입니다.</param>
/// <param name="TimeZoneId">`Asia/Seoul` 같은 IANA 시간대 ID입니다.</param>
/// <param name="PreferredUtcOffset">중복 시각을 고르는 `+09:00` 또는 `-04:00` 문자열입니다.</param>
/// <param name="DurationMinutes">15분 단위의 15~480분 길이입니다.</param>
// HTTP 경계에서는 누락을 구분하려고 DateOnly?/TimeOnly?를 허용하지만,
// endpoint 검증 뒤에는 non-null Command로 좁혀 Domain/Application이 기본값을 실제 입력으로 오해하지 않게 합니다.
public sealed record ScheduleMeetingRequest(
    string? MeetingId,
    string? OrganizerId,
    string? Title,
    DateOnly? LocalDate,
    TimeOnly? LocalStartTime,
    string? TimeZoneId,
    string? PreferredUtcOffset,
    int DurationMinutes);

/// <summary>
/// 저장된 회의를 원래 지역 문맥과 확정된 UTC 순간으로 함께 보여 주는 응답입니다.
/// </summary>
/// <param name="MeetingId">저장된 정규화 회의 ID입니다.</param>
/// <param name="OrganizerId">저장된 정규화 주최자 ID입니다.</param>
/// <param name="Title">공백이 정리된 제목입니다.</param>
/// <param name="LocalDate">사용자가 입력한 지역 날짜입니다.</param>
/// <param name="LocalStartTime">사용자가 입력한 지역 시각입니다.</param>
/// <param name="TimeZoneId">변환에 실제 사용한 시간대 ID입니다.</param>
/// <param name="UtcOffset">선택되거나 계산된 UTC offset입니다.</param>
/// <param name="StartsAtUtc">충돌 비교와 저장에 쓰는 UTC 시작 순간입니다.</param>
/// <param name="EndsAtUtc">충돌 비교와 저장에 쓰는 UTC 종료 순간입니다.</param>
public sealed record MeetingResponse(
    string MeetingId,
    string OrganizerId,
    string Title,
    DateOnly LocalDate,
    TimeOnly LocalStartTime,
    string TimeZoneId,
    string UtcOffset,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc)
{
    /// <summary>
    /// Domain 예약을 외부에 필요한 필드만 가진 HTTP 응답으로 변환합니다.
    /// </summary>
    /// <param name="meeting">저장과 조회가 끝난 불변 Domain 예약입니다.</param>
    /// <returns>원래 지역 문맥과 UTC 구간을 가진 응답 DTO를 반환합니다.</returns>
    public static MeetingResponse FromDomain(MeetingReservation meeting)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        return new MeetingResponse(
            meeting.MeetingId,
            meeting.OrganizerId,
            meeting.Title,
            meeting.LocalTime.Date,
            meeting.LocalTime.StartTime,
            meeting.ResolvedTime.TimeZoneId,
            RequireExplicitOffsetPolicy.FormatOffset(meeting.ResolvedTime.UtcOffset),
            meeting.Slot.StartsAtUtc,
            meeting.Slot.EndsAtUtc);
    }
}
/// <summary>
/// 생략 가능한 문자열 offset을 Application 명령의 nullable TimeSpan으로 바꾼 값입니다.
/// </summary>
/// <param name="Value">파싱한 offset이며 입력이 생략되었으면 null입니다.</param>
public sealed record ParsedUtcOffset(TimeSpan? Value);

/// <summary>
/// HTTP의 친숙한 `+09:00` 문자열을 문화권에 영향받지 않고 안전하게 파싱합니다.
/// </summary>
public static class UtcOffsetParser
{
    /// <summary>
    /// 빈 입력은 선택하지 않음으로, 정확한 부호·시·분 입력은 TimeSpan으로 변환합니다.
    /// </summary>
    /// <param name="text">null 또는 `+HH:mm`/`-HH:mm` 형식 문자열입니다.</param>
    /// <returns>nullable offset 또는 형식·범위 오류를 반환합니다.</returns>
    public static Result<ParsedUtcOffset> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<ParsedUtcOffset>.Success(new ParsedUtcOffset(null));
        }

        var trimmed = text.Trim();
        // is not ('+' or '-')는 첫 문자가 +와 - 어느 pattern에도 맞지 않는지 검사합니다.
        if (trimmed.Length != 6
            || trimmed[0] is not ('+' or '-')
            || trimmed[3] != ':'
            || !int.TryParse(
                trimmed.AsSpan(1, 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var hours)
            || !int.TryParse(
                trimmed.AsSpan(4, 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes))
        {
            return Result<ParsedUtcOffset>.Failure(
                new Error(
                    "utc_offset.invalid_format",
                    "preferredUtcOffset은 +09:00 또는 -04:00 형식이어야 합니다.",
                    "preferredUtcOffset"));
        }

        if (hours > 14 || minutes > 59 || (hours == 14 && minutes != 0))
        {
            return Result<ParsedUtcOffset>.Failure(
                new Error(
                    "utc_offset.out_of_range",
                    "preferredUtcOffset은 -14:00부터 +14:00 사이여야 합니다.",
                    "preferredUtcOffset"));
        }

        var totalMinutes = (hours * 60) + minutes;
        var signedMinutes = trimmed[0] == '-' ? -totalMinutes : totalMinutes;
        return Result<ParsedUtcOffset>.Success(
            new ParsedUtcOffset(TimeSpan.FromMinutes(signedMinutes)));
    }
}
