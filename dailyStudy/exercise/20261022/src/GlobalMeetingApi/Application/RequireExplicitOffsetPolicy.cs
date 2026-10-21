using System.Globalization;
using GlobalMeetingApi.Application.Ports;
using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Application;

/// <summary>
/// 모호한 지역 시각을 서버가 추측하지 않고 사용자가 가능한 offset 중 하나를 명시하게 하는 Strategy입니다.
/// </summary>
public sealed class RequireExplicitOffsetPolicy : IAmbiguousTimePolicy
{
    /// <summary>
    /// 선택이 빠졌으면 후보를 안내하고, 후보에 포함된 선택만 승인합니다.
    /// </summary>
    /// <param name="validOffsets">시간대 규칙에서 얻은 실제 offset 후보입니다.</param>
    /// <param name="preferredUtcOffset">사용자가 요청에 적은 선택적 offset입니다.</param>
    /// <returns>검증된 선택 또는 허용 후보를 가진 오류를 반환합니다.</returns>
    public Result<UtcOffsetChoice> Choose(
        IReadOnlyCollection<TimeSpan> validOffsets,
        TimeSpan? preferredUtcOffset)
    {
        ArgumentNullException.ThrowIfNull(validOffsets);
        if (validOffsets.Count == 0)
        {
            throw new InvalidOperationException("모호한 시각에는 하나 이상의 offset 후보가 있어야 합니다.");
        }

        // LINQ는 '중복 제거→표시 순서→문자열 변환' 의도를 단계로 읽게 해 수동 loop의 임시 상태를 줄입니다.
        // collection expression [.. sequence]는 sequence의 모든 원소를 새 배열에 펼치는 C# 문법입니다.
        string[] allowedValues =
        [
            .. validOffsets
                .Distinct()
                // offset => offset은 입력을 그대로 정렬 키로 돌려주는 lambda이며, 큰 UTC offset부터 보여 주려고 씁니다.
                .OrderByDescending(offset => offset)
                .Select(FormatOffset)
        ];

        if (preferredUtcOffset is null)
        {
            return Result<UtcOffsetChoice>.Failure(
                new Error(
                    "local_time.ambiguous",
                    "이 지역 시각은 DST 종료 때문에 두 번 나타납니다. preferredUtcOffset을 선택하세요.",
                    "preferredUtcOffset",
                    allowedValues));
        }

        if (!validOffsets.Contains(preferredUtcOffset.Value))
        {
            return Result<UtcOffsetChoice>.Failure(
                new Error(
                    "utc_offset.not_valid_for_local_time",
                    "preferredUtcOffset이 이 지역 시각의 가능한 offset과 일치하지 않습니다.",
                    "preferredUtcOffset",
                    allowedValues));
        }

        return Result<UtcOffsetChoice>.Success(new UtcOffsetChoice(preferredUtcOffset.Value));
    }

    /// <summary>
    /// UTC offset을 부호가 포함된 고정 `+HH:mm` 형식으로 표시합니다.
    /// </summary>
    /// <param name="offset">표시할 -14시간부터 +14시간 사이의 값입니다.</param>
    /// <returns>예를 들어 +09:00 또는 -04:00 문자열을 반환합니다.</returns>
    public static string FormatOffset(TimeSpan offset)
    {
        // Duration은 음수 offset의 절댓값 TimeSpan을 만들어 시·분 숫자 부분을 양수로 출력하게 합니다.
        var absolute = offset.Duration();
        // ?: 삼항 연산자는 offset 비교 결과에 따라 부호 문자열 하나를 선택합니다.
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{sign}{(int)absolute.TotalHours:00}:{absolute.Minutes:00}");
    }
}
