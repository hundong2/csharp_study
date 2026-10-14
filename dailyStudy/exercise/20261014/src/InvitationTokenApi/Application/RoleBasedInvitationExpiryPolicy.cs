using InvitationTokenApi.Application.Ports;
using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application;

/// <summary>
/// 권한이 큰 Admin 토큰은 짧게, Member 토큰은 길게 유지하는 만료 Strategy입니다.
/// </summary>
public sealed class RoleBasedInvitationExpiryPolicy : IInvitationExpiryPolicy
{
    private readonly TimeSpan _memberLifetime;
    private readonly TimeSpan _adminLifetime;

    /// <summary>
    /// 두 권한의 양수 수명을 검증하고 정책으로 저장합니다.
    /// </summary>
    /// <param name="memberLifetime">Member 토큰이 유효할 기간입니다.</param>
    /// <param name="adminLifetime">Admin 토큰이 유효할 더 짧은 기간입니다.</param>
    /// <returns>생성자는 Strategy를 초기화하므로 별도 반환값은 없습니다.</returns>
    public RoleBasedInvitationExpiryPolicy(
        TimeSpan memberLifetime,
        TimeSpan adminLifetime)
    {
        // 수명은 사용자 입력이 아니라 Composition Root가 정하는 구성값이므로 잘못되면 Result 대신 예외로 시작 단계에서 빠르게 실패시킵니다.
        if (memberLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memberLifetime),
                "Member 토큰 수명은 0보다 커야 합니다.");
        }

        if (adminLifetime <= TimeSpan.Zero || adminLifetime >= memberLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adminLifetime),
                "Admin 토큰 수명은 0보다 크고 Member 수명보다 짧아야 합니다.");
        }

        _memberLifetime = memberLifetime;
        _adminLifetime = adminLifetime;
    }

    /// <summary>
    /// 권한에 대응하는 수명을 발급 시각에 더해 만료 시각을 계산합니다.
    /// </summary>
    /// <param name="role">수명을 고를 Member 또는 Admin 권한입니다.</param>
    /// <param name="issuedAtUtc">TimeProvider에서 읽은 발급 UTC 시각입니다.</param>
    /// <returns>Admin이면 짧은 수명, Member이면 긴 수명을 더한 UTC 시각을 반환합니다.</returns>
    public DateTimeOffset CalculateExpiration(
        ProjectRole role,
        DateTimeOffset issuedAtUtc)
    {
        // switch expression은 권한 하나를 수명 하나에 빠짐없이 대응시킵니다.
        var lifetime = role switch
        {
            ProjectRole.Member => _memberLifetime,
            ProjectRole.Admin => _adminLifetime,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "알 수 없는 권한입니다.")
        };

        return issuedAtUtc.ToUniversalTime().Add(lifetime);
    }

    /// <summary>
    /// 현재 시각이 만료 경계에 닿았거나 지났는지 판단합니다.
    /// </summary>
    /// <param name="expiresAtUtc">토큰이 끝나는 UTC 시각입니다.</param>
    /// <param name="nowUtc">이번 요청에서 읽은 현재 UTC 시각입니다.</param>
    /// <returns>만료됐으면 true, 아직 사용할 수 있으면 false를 반환합니다.</returns>
    public bool IsExpired(DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc)
    {
        return nowUtc >= expiresAtUtc;
    }
}
