using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application.Ports;

/// <summary>
/// Application이 Data Protection 구현 세부사항 없이 토큰을 보호하고 검증하게 하는 Port입니다.
/// </summary>
public interface IInvitationTokenProtector
{
    /// <summary>
    /// 초대 payload를 외부에서 읽거나 고칠 수 없는 불투명 문자열로 보호합니다.
    /// </summary>
    /// <param name="payload">초대 ID, 프로젝트, 권한, 만료 시각을 담은 최소 payload입니다.</param>
    /// <returns>클라이언트에게 전달할 URL-safe 보호 토큰을 반환합니다.</returns>
    string Protect(InvitationTokenPayload payload);

    /// <summary>
    /// 보호 토큰의 형식, 암호학적 무결성, purpose, payload, 만료를 한 경계에서 확인합니다.
    /// </summary>
    /// <param name="token">외부에서 돌아온 신뢰할 수 없는 토큰 문자열입니다.</param>
    /// <param name="nowUtc">만료 판단에 사용할 현재 UTC 시각입니다.</param>
    /// <returns>검증된 payload 또는 공격자에게 원인을 구분해 주지 않는 공통 실패를 반환합니다.</returns>
    Result<InvitationTokenPayload> Unprotect(string? token, DateTimeOffset nowUtc);
}
