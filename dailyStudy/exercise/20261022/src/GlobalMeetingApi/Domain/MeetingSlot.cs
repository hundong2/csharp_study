namespace GlobalMeetingApi.Domain;

/// <summary>
/// 시작과 종료를 UTC로 보관해 시간대가 달라도 충돌을 일관되게 비교하는 값 객체입니다.
/// </summary>
public sealed record MeetingSlot
{
    /// <summary>
    /// 검증된 UTC 구간만 만들 수 있도록 생성자를 숨깁니다.
    /// </summary>
    /// <param name="startsAtUtc">포함되는 시작 순간입니다.</param>
    /// <param name="endsAtUtc">포함되지 않는 종료 순간입니다.</param>
    private MeetingSlot(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc)
    {
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    /// <summary>
    /// 회의가 시작되는 UTC 순간입니다.
    /// </summary>
    public DateTimeOffset StartsAtUtc { get; }

    /// <summary>
    /// 회의가 끝나는 UTC 순간이며 반열린 구간에서 이 순간은 포함하지 않습니다.
    /// </summary>
    public DateTimeOffset EndsAtUtc { get; }

    /// <summary>
    /// 시작 UTC와 분 단위 길이로 안전한 반열린 회의 구간을 만듭니다.
    /// </summary>
    /// <param name="startsAtUtc">확정된 회의 시작 순간입니다.</param>
    /// <param name="durationMinutes">15분 단위의 15~480분 회의 길이입니다.</param>
    /// <returns>유효한 회의 구간 또는 길이·범위 오류를 반환합니다.</returns>
    public static Result<MeetingSlot> Create(DateTimeOffset startsAtUtc, int durationMinutes)
    {
        // is < 15 or > 480은 '15보다 작거나 480보다 큼'을 문장처럼 묶는 관계·논리 패턴입니다.
        if (durationMinutes is < 15 or > 480 || durationMinutes % 15 != 0)
        {
            return Result<MeetingSlot>.Failure(
                new Error(
                    "duration.invalid",
                    "durationMinutes는 15~480 사이의 15분 단위여야 합니다.",
                    "durationMinutes"));
        }

        // ToUniversalTime은 입력 offset을 제거하는 것이 아니라 같은 순간을 +00:00 표현으로 정규화합니다.
        var normalizedStart = startsAtUtc.ToUniversalTime();
        try
        {
            var end = normalizedStart.AddMinutes(durationMinutes);
            return Result<MeetingSlot>.Success(new MeetingSlot(normalizedStart, end));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<MeetingSlot>.Failure(
                new Error("meeting.time_out_of_range", "계산된 종료 시각이 표현 범위를 벗어납니다.", "durationMinutes"));
        }
    }

    /// <summary>
    /// 다른 회의 구간과 실제 시간이 한 순간이라도 겹치는지 계산합니다.
    /// </summary>
    /// <param name="other">비교할 다른 UTC 회의 구간입니다.</param>
    /// <returns>겹치면 true, 종료와 다음 시작만 맞닿으면 false를 반환합니다.</returns>
    public bool Overlaps(MeetingSlot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // 반열린 구간 [start, end)를 쓰면 10:00 종료와 10:00 시작을 충돌로 보지 않습니다.
        return StartsAtUtc < other.EndsAtUtc && other.StartsAtUtc < EndsAtUtc;
    }
}
