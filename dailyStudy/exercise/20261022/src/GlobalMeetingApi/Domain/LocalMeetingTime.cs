namespace GlobalMeetingApi.Domain;

/// <summary>
/// 사용자가 입력한 달력 날짜, 벽시계 시각, 시간대, 선택적 UTC offset을 한 값으로 묶습니다.
/// </summary>
// record는 값이 같으면 같은 객체로 비교되는 C# 자료형입니다. 변경을 막은 값 객체로 지역 시각을 안전하게 전달하려고 사용합니다.
public sealed record LocalMeetingTime
{
    /// <summary>
    /// 외부에서 불완전한 상태로 만들지 못하게 검증된 값만 저장합니다.
    /// </summary>
    /// <param name="date">사용자가 보고 있는 지역의 달력 날짜입니다.</param>
    /// <param name="startTime">사용자가 보고 있는 지역의 벽시계 시작 시각입니다.</param>
    /// <param name="timeZoneId">DST 규칙까지 식별하는 IANA 시간대 ID입니다.</param>
    /// <param name="preferredUtcOffset">중복 시각에서 사용자가 고른 UTC offset이며 평상시에는 null일 수 있습니다.</param>
    private LocalMeetingTime(
        DateOnly date,
        TimeOnly startTime,
        string timeZoneId,
        // 값 형식 뒤의 ?는 값이 없음을 null로 표현할 수 있다는 뜻이며, 평상시에는 offset 선택이 필요 없어서 사용합니다.
        TimeSpan? preferredUtcOffset)
    {
        Date = date;
        StartTime = startTime;
        TimeZoneId = timeZoneId;
        PreferredUtcOffset = preferredUtcOffset;
    }

    /// <summary>
    /// 지역 달력 날짜입니다. 날짜만 있으므로 UTC 순간을 단독으로 뜻하지 않습니다.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// 지역 벽시계 시각입니다. 시간대와 결합되기 전에는 UTC 순간이 아닙니다.
    /// </summary>
    public TimeOnly StartTime { get; }

    /// <summary>
    /// `Asia/Seoul`, `America/New_York` 같은 IANA 시간대 ID입니다.
    /// </summary>
    public string TimeZoneId { get; }

    /// <summary>
    /// DST 종료 중복 시각에서 선택한 offset이며 선택하지 않았으면 null입니다.
    /// </summary>
    public TimeSpan? PreferredUtcOffset { get; }

    /// <summary>
    /// 외부 입력을 정리하고 시간대 ID와 offset의 기본 형식을 검증합니다.
    /// </summary>
    /// <param name="date">사용자가 입력한 지역 날짜입니다.</param>
    /// <param name="startTime">사용자가 입력한 지역 시작 시각입니다.</param>
    /// <param name="timeZoneId">사용자가 입력한 IANA 시간대 ID입니다.</param>
    /// <param name="preferredUtcOffset">중복 시각을 구분하려고 선택한 offset입니다.</param>
    /// <returns>정규화된 지역 시각 또는 입력 오류를 반환합니다.</returns>
    public static Result<LocalMeetingTime> Create(
        DateOnly date,
        TimeOnly startTime,
        string? timeZoneId,
        TimeSpan? preferredUtcOffset)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Result<LocalMeetingTime>.Failure(
                new Error("time_zone.required", "timeZoneId가 필요합니다.", "timeZoneId"));
        }

        if (timeZoneId.Trim().Length > 100)
        {
            return Result<LocalMeetingTime>.Failure(
                new Error("time_zone.too_long", "timeZoneId는 100자 이하여야 합니다.", "timeZoneId"));
        }

        // is { } offset은 nullable 값이 있을 때만 내부 TimeSpan을 offset 변수로 꺼내는 property pattern입니다.
        if (preferredUtcOffset is { } offset
            && (offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14)))
        {
            return Result<LocalMeetingTime>.Failure(
                new Error(
                    "utc_offset.out_of_range",
                    "preferredUtcOffset은 -14:00부터 +14:00 사이여야 합니다.",
                    "preferredUtcOffset"));
        }

        return Result<LocalMeetingTime>.Success(
            new LocalMeetingTime(date, startTime, timeZoneId.Trim(), preferredUtcOffset));
    }
}

/// <summary>
/// 지역 벽시계 입력을 하나의 확정된 UTC 순간으로 해석한 결과입니다.
/// </summary>
/// <param name="LocalDateTime">DateTimeKind.Unspecified인 원래 지역 벽시계 값입니다.</param>
/// <param name="TimeZoneId">변환에 사용한 시간대 ID입니다.</param>
/// <param name="UtcOffset">해당 지역 시각에 실제로 적용한 UTC offset입니다.</param>
/// <param name="UtcInstant">어디서 읽어도 같은 순간을 뜻하는 UTC 값입니다.</param>
public sealed record ResolvedLocalTime(
    DateTime LocalDateTime,
    string TimeZoneId,
    TimeSpan UtcOffset,
    DateTimeOffset UtcInstant);

/// <summary>
/// 모호한 지역 시각에서 Strategy가 선택한 UTC offset을 reference type으로 감쌉니다.
/// </summary>
/// <param name="Value">허용된 후보 중 선택된 UTC offset입니다.</param>
public sealed record UtcOffsetChoice(TimeSpan Value);
