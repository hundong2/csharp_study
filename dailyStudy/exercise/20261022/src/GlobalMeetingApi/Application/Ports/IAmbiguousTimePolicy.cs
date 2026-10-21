using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Application.Ports;

/// <summary>
/// DST 종료로 같은 벽시계 시각이 두 번 나타날 때 어떤 offset을 선택할지 정하는 Strategy Port입니다.
/// 인터페이스로 분리하면 Resolver는 구체 정책을 몰라 OCP/DIP를 지키고, 운영 정책과 Test Double을 쉽게 교체할 수 있습니다.
/// </summary>
public interface IAmbiguousTimePolicy
{
    /// <summary>
    /// 시간대가 제공한 후보와 사용자의 선택을 비교해 정확히 하나의 offset을 고릅니다.
    /// </summary>
    /// <param name="validOffsets">해당 지역 시각에 실제로 가능한 UTC offset 후보입니다.</param>
    /// <param name="preferredUtcOffset">사용자가 명시한 선택이며 생략했으면 null입니다.</param>
    /// <returns>선택된 offset 또는 사용자가 다시 입력할 수 있는 오류를 반환합니다.</returns>
    Result<UtcOffsetChoice> Choose(
        IReadOnlyCollection<TimeSpan> validOffsets,
        TimeSpan? preferredUtcOffset);
}
