using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Application.Ports;

/// <summary>
/// 지역 날짜·시각과 시간대 규칙을 하나의 UTC 순간으로 바꾸는 Port입니다.
/// </summary>
public interface ILocalTimeResolver
{
    /// <summary>
    /// 지역 시각이 정상·존재하지 않음·중복 중 어디에 해당하는지 판별하고 UTC로 변환합니다.
    /// </summary>
    /// <param name="localTime">검증된 지역 날짜, 시각, 시간대, 선택적 offset입니다.</param>
    /// <returns>확정된 UTC 순간 또는 시간대·DST 오류를 반환합니다.</returns>
    Result<ResolvedLocalTime> Resolve(LocalMeetingTime localTime);
}
