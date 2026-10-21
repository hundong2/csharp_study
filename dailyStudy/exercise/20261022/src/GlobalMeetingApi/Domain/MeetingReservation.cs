namespace GlobalMeetingApi.Domain;

/// <summary>
/// 사용자가 입력한 지역 문맥과 충돌 비교용 UTC 구간을 함께 보존하는 불변 예약입니다.
/// </summary>
public sealed record MeetingReservation
{
    /// <summary>
    /// 정적 factory가 검증한 예약만 만들도록 생성자를 숨깁니다.
    /// </summary>
    /// <param name="meetingId">예약의 안정적인 식별자입니다.</param>
    /// <param name="organizerId">회의를 소유하며 시간 충돌을 검사할 주최자입니다.</param>
    /// <param name="title">화면에 표시할 정리된 회의 제목입니다.</param>
    /// <param name="localTime">사용자가 원래 입력한 지역 날짜·시각·시간대입니다.</param>
    /// <param name="resolvedTime">그 지역 입력을 해석해 확정한 offset과 UTC 순간입니다.</param>
    /// <param name="slot">UTC 시작·종료 구간입니다.</param>
    private MeetingReservation(
        string meetingId,
        string organizerId,
        string title,
        LocalMeetingTime localTime,
        ResolvedLocalTime resolvedTime,
        MeetingSlot slot)
    {
        MeetingId = meetingId;
        OrganizerId = organizerId;
        Title = title;
        LocalTime = localTime;
        ResolvedTime = resolvedTime;
        Slot = slot;
    }

    /// <summary>
    /// 중복 저장을 막고 조회에 사용하는 정규화된 예약 식별자입니다.
    /// </summary>
    public string MeetingId { get; }

    /// <summary>
    /// 어떤 예약끼리 시간 충돌을 검사할지 결정하는 정규화된 주최자 식별자입니다.
    /// </summary>
    public string OrganizerId { get; }

    /// <summary>
    /// 양끝 공백을 제거해 화면과 저장소가 같은 값으로 보는 회의 제목입니다.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 사용자에게 원래 의도를 다시 보여 주기 위해 보존한 지역 날짜·시각·시간대 값입니다.
    /// </summary>
    public LocalMeetingTime LocalTime { get; }

    /// <summary>
    /// 시간대 규칙으로 확정한 offset과 UTC 순간이며 변환 근거를 설명할 때 사용합니다.
    /// </summary>
    public ResolvedLocalTime ResolvedTime { get; }

    /// <summary>
    /// 시간대와 무관한 충돌 검사를 위해 보관하는 불변 UTC 반열린 구간입니다.
    /// </summary>
    public MeetingSlot Slot { get; }

    /// <summary>
    /// 식별자와 제목을 검증하고 지역 문맥과 UTC 구간을 가진 예약을 만듭니다.
    /// </summary>
    /// <param name="meetingId">중복을 막을 외부 예약 ID입니다.</param>
    /// <param name="organizerId">시간 충돌을 검사할 주최자 ID입니다.</param>
    /// <param name="title">공백을 정리할 회의 제목입니다.</param>
    /// <param name="localTime">검증된 원래 지역 입력입니다.</param>
    /// <param name="resolvedTime">시간대 규칙으로 해석한 확정 순간입니다.</param>
    /// <param name="slot">길이까지 반영한 UTC 구간입니다.</param>
    /// <returns>불변 예약 또는 식별자·제목 오류를 반환합니다.</returns>
    public static Result<MeetingReservation> Create(
        string? meetingId,
        string? organizerId,
        string? title,
        LocalMeetingTime localTime,
        ResolvedLocalTime resolvedTime,
        MeetingSlot slot)
    {
        ArgumentNullException.ThrowIfNull(localTime);
        ArgumentNullException.ThrowIfNull(resolvedTime);
        ArgumentNullException.ThrowIfNull(slot);

        if (!IsSimpleIdentifier(meetingId, 40))
        {
            return Result<MeetingReservation>.Failure(
                new Error(
                    "meeting_id.invalid",
                    "meetingId는 1~40자의 영문자, 숫자, 하이픈으로 작성하세요.",
                    "meetingId"));
        }

        if (!IsSimpleIdentifier(organizerId, 40))
        {
            return Result<MeetingReservation>.Failure(
                new Error(
                    "organizer_id.invalid",
                    "organizerId는 1~40자의 영문자, 숫자, 하이픈으로 작성하세요.",
                    "organizerId"));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100)
        {
            return Result<MeetingReservation>.Failure(
                new Error("title.invalid", "title은 공백이 아닌 1~100자여야 합니다.", "title"));
        }

        // 앞의 IsSimpleIdentifier 검사가 null을 거절했으므로 !로 nullable 분석기에 그 근거를 알립니다.
        // !는 runtime 검사를 추가하지 않으므로 이런 선행 검증 없이 사용하면 안 됩니다.
        return Result<MeetingReservation>.Success(
            new MeetingReservation(
                meetingId!.Trim().ToUpperInvariant(),
                organizerId!.Trim().ToUpperInvariant(),
                title.Trim(),
                localTime,
                resolvedTime,
                slot));
    }

    /// <summary>
    /// 외부 식별자가 허용 길이와 문자 집합을 지키는지 검사합니다.
    /// </summary>
    /// <param name="value">검사할 nullable 문자열입니다.</param>
    /// <param name="maximumLength">허용할 최대 문자 수입니다.</param>
    /// <returns>정리된 값이 영문자·숫자·하이픈만 가지면 true를 반환합니다.</returns>
    private static bool IsSimpleIdentifier(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        // is 관계 패턴은 길이가 허용 범위 밖인지 문장처럼 읽히게 검사합니다.
        if (trimmed.Length is < 1 || trimmed.Length > maximumLength)
        {
            return false;
        }

        // lambda의 character는 문자 하나를 받아 허용 여부를 반환합니다.
        return trimmed.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}
