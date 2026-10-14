using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application.Ports;

/// <summary>
/// 권한별 토큰 수명과 만료 경계 규칙을 교체 가능한 Strategy로 분리합니다.
/// </summary>
public interface IInvitationExpiryPolicy
{
    /// <summary>
    /// 발급 시각과 권한에 맞는 만료 시각을 계산합니다.
    /// </summary>
    /// <param name="role">수명을 선택할 Member 또는 Admin 권한입니다.</param>
    /// <param name="issuedAtUtc">TimeProvider에서 읽은 발급 UTC 시각입니다.</param>
    /// <returns>권한별 수명을 더한 UTC 만료 시각을 반환합니다.</returns>
    DateTimeOffset CalculateExpiration(ProjectRole role, DateTimeOffset issuedAtUtc);

    /// <summary>
    /// 경계 시각을 포함해 토큰이 만료됐는지 판단합니다.
    /// </summary>
    /// <param name="expiresAtUtc">토큰과 저장소에 기록된 만료 UTC 시각입니다.</param>
    /// <param name="nowUtc">이번 요청에서 TimeProvider로 한 번 읽은 현재 UTC 시각입니다.</param>
    /// <returns>현재가 만료 시각과 같거나 뒤면 true, 아직 유효하면 false를 반환합니다.</returns>
    bool IsExpired(DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc);
}
