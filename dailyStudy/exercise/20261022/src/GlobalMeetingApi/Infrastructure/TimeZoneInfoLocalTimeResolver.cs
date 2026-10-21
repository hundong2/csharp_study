using GlobalMeetingApi.Application;
using GlobalMeetingApi.Application.Ports;
using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Infrastructure;

/// <summary>
/// 운영체제의 TimeZoneInfo 규칙으로 지역 벽시계를 검증하고 UTC로 변환하는 Adapter입니다.
/// </summary>
/// <param name="ambiguousTimePolicy">DST 중복 시각의 선택 규칙을 제공해 기술 변환과 업무 정책을 분리합니다.</param>
// primary constructor는 DI가 Strategy를 주입하게 하므로 Adapter가 구체 정책을 직접 만들지 않습니다.
public sealed class TimeZoneInfoLocalTimeResolver(IAmbiguousTimePolicy ambiguousTimePolicy)
    : ILocalTimeResolver
{
    /// <summary>
    /// 지역 시각의 DST 공백·중복을 검사하고 선택된 offset으로 하나의 UTC 순간을 만듭니다.
    /// </summary>
    /// <param name="localTime">날짜, 시각, IANA 시간대 ID, 선택적 offset을 가진 Domain 값입니다.</param>
    /// <returns>확정된 UTC 순간 또는 시간대·DST 오류를 반환합니다.</returns>
    public Result<ResolvedLocalTime> Resolve(LocalMeetingTime localTime)
    {
        ArgumentNullException.ThrowIfNull(localTime);

        // TryFindSystemTimeZoneById는 잘못된 ID를 예외 제어 흐름으로 쓰지 않고 bool로 확인하게 합니다.
        // out var는 성공 시 TimeZoneInfo를 받을 지역 변수를 조건식 안에서 함께 선언합니다.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(localTime.TimeZoneId, out var timeZone))
        {
            return Result<ResolvedLocalTime>.Failure(
                new Error(
                    "time_zone.unknown",
                    "운영체제에서 timeZoneId를 찾을 수 없습니다. IANA ID를 확인하세요.",
                    "timeZoneId"));
        }

        // Unspecified는 이 값을 PC의 로컬 시각이나 UTC로 오해하지 말고 지정한 timeZone의 벽시계로 해석하라는 뜻입니다.
        var localDateTime = localTime.Date.ToDateTime(
            localTime.StartTime,
            DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            return Result<ResolvedLocalTime>.Failure(
                new Error(
                    "local_time.invalid",
                    "이 지역 시각은 DST 시작으로 시계가 앞으로 이동해 실제로 존재하지 않습니다.",
                    "localStartTime"));
        }

        TimeSpan selectedOffset;
        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            var choice = ambiguousTimePolicy.Choose(
                timeZone.GetAmbiguousTimeOffsets(localDateTime),
                localTime.PreferredUtcOffset);
            if (!choice.IsSuccess)
            {
                return Result<ResolvedLocalTime>.Failure(choice.Error);
            }

            selectedOffset = choice.Value.Value;
        }
        else
        {
            selectedOffset = timeZone.GetUtcOffset(localDateTime);
            if (localTime.PreferredUtcOffset is not null
                && localTime.PreferredUtcOffset.Value != selectedOffset)
            {
                // [값]은 원소 하나를 가진 collection expression이며, 클라이언트가 선택할 수 있는 목록 형태로 전달합니다.
                return Result<ResolvedLocalTime>.Failure(
                    new Error(
                        "utc_offset.not_valid_for_local_time",
                        "preferredUtcOffset이 이 지역 시각의 실제 offset과 일치하지 않습니다.",
                        "preferredUtcOffset",
                        [RequireExplicitOffsetPolicy.FormatOffset(selectedOffset)]));
            }
        }

        try
        {
            // DateTimeOffset은 벽시계와 선택된 offset을 결합해 하나의 실제 순간을 만듭니다.
            var localWithOffset = new DateTimeOffset(localDateTime, selectedOffset);
            return Result<ResolvedLocalTime>.Success(
                new ResolvedLocalTime(
                    localDateTime,
                    timeZone.Id,
                    selectedOffset,
                    localWithOffset.ToUniversalTime()));
        }
        catch (ArgumentOutOfRangeException)
        {
            // 연도 1/9999 경계에서 offset을 적용한 UTC가 DateTimeOffset 표현 범위를 넘을 수 있습니다.
            return Result<ResolvedLocalTime>.Failure(
                new Error(
                    "local_time.out_of_range",
                    "시간대 offset을 적용한 UTC 순간이 지원 범위를 벗어납니다.",
                    "localDate"));
        }
    }
}
