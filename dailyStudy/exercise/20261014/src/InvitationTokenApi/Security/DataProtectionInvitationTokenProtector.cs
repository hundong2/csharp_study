using System.Security.Cryptography;
using System.Text.Json;
using InvitationTokenApi.Application;
using InvitationTokenApi.Application.Ports;
using InvitationTokenApi.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace InvitationTokenApi.Security;

/// <summary>
/// ASP.NET Core Data Protection으로 초대 payload의 기밀성과 변조 방지를 제공하는 Adapter입니다.
/// </summary>
public sealed class DataProtectionInvitationTokenProtector : IInvitationTokenProtector
{
    private const int MaximumTokenLength = 4096;

    // new(...)는 왼쪽 선언 형식에서 생성할 형식을 추론하는 target-typed new 문법으로 긴 형식 이름 반복을 줄입니다.
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _protector;
    private readonly IInvitationExpiryPolicy _expiryPolicy;

    /// <summary>
    /// provider에서 초대 전용 purpose의 protector를 만들고 만료 정책을 보관합니다.
    /// </summary>
    /// <param name="provider">DI가 제공하는 ASP.NET Core Data Protection provider입니다.</param>
    /// <param name="purpose">다른 기능의 토큰과 암호학적으로 격리할 안정적인 용도 문자열입니다.</param>
    /// <param name="expiryPolicy">payload 만료 경계를 같은 제품 정책으로 판단할 Strategy입니다.</param>
    /// <returns>생성자는 Adapter를 초기화하므로 별도 반환값은 없습니다.</returns>
    public DataProtectionInvitationTokenProtector(
        IDataProtectionProvider provider,
        string purpose,
        IInvitationExpiryPolicy expiryPolicy)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(expiryPolicy);

        // Data Protection의 purpose는 같은 key ring을 써도 다른 기능이 서로의 토큰을 열지 못하게 하는 격리 경계입니다.
        _protector = provider.CreateProtector(purpose);
        _expiryPolicy = expiryPolicy;
    }

    /// <summary>
    /// payload를 UTF-8 JSON bytes로 직렬화한 뒤 보호하고 URL-safe 문자열로 바꿉니다.
    /// </summary>
    /// <param name="payload">서버가 검증한 초대 식별자, 권한, 만료 정보입니다.</param>
    /// <returns>원문을 드러내지 않고 변조를 탐지할 불투명 토큰을 반환합니다.</returns>
    public string Protect(InvitationTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        var protectedBytes = _protector.Protect(plaintext);
        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    /// <summary>
    /// URL-safe token을 복원하고 Data Protection 검증, JSON 구조, 버전, 만료를 확인합니다.
    /// </summary>
    /// <param name="token">클라이언트가 보낸 신뢰할 수 없는 보호 문자열입니다.</param>
    /// <param name="nowUtc">만료 Strategy에 전달할 이번 요청의 현재 UTC 시각입니다.</param>
    /// <returns>모든 검사를 통과한 payload 또는 원인을 합친 안전한 실패를 반환합니다.</returns>
    public Result<InvitationTokenPayload> Unprotect(
        string? token,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength)
        {
            return InvalidToken();
        }

        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(token);
            var plaintext = _protector.Unprotect(protectedBytes);
            var payload = JsonSerializer.Deserialize<InvitationTokenPayload>(
                plaintext,
                SerializerOptions);

            // payload!의 !는 앞의 구조 검사가 성공하면 null이 아님을 nullable 분석기에 알리며 값을 바꾸지 않습니다.
            if (!IsStructurallyValid(payload) ||
                _expiryPolicy.IsExpired(payload!.ExpiresAtUtc, nowUtc))
            {
                return InvalidToken();
            }

            return Result<InvitationTokenPayload>.Success(payload);
        }
        // 공격자가 형식, 변조, purpose 불일치 중 무엇이 맞았는지 추측하지 못하게 모두 같은 Domain 오류로 축약합니다.
        // catch의 when은 지정한 예외에만 이 처리 경로를 적용하는 exception filter입니다.
        // `is A or B` type pattern은 형식·암호·JSON 입력 오류만 묶고 메모리 부족 같은 치명적 예외는 숨기지 않습니다.
        catch (Exception exception) when (
            exception is FormatException or
            CryptographicException or
            JsonException or
            ArgumentException)
        {
            return InvalidToken();
        }
    }

    /// <summary>
    /// 복호화된 JSON이 현재 버전의 최소 필드를 모두 가졌는지 검사합니다.
    /// </summary>
    /// <param name="payload">JSON에서 만든 값이며 null일 수도 있습니다.</param>
    /// <returns>버전, ID, 프로젝트, 권한, 만료가 유효한 모양이면 true를 반환합니다.</returns>
    private static bool IsStructurallyValid(InvitationTokenPayload? payload)
    {
        // property pattern은 payload가 null이 아니면서 여러 속성 조건을 동시에 만족하는지 안전하게 검사합니다.
        return payload is
        {
            Version: 1,
            InvitationId.Length: > 0,
            ProjectId.Length: > 0,
            ExpiresAtUtc: var expiresAtUtc
        } &&
        payload.Role is ProjectRole.Member or ProjectRole.Admin &&
        expiresAtUtc != default;
    }

    /// <summary>
    /// 토큰 검증 실패 원인을 하나의 안정적인 보안 오류로 만듭니다.
    /// </summary>
    /// <returns>HTTP 400으로 매핑할 invalid-or-expired 실패를 반환합니다.</returns>
    private static Result<InvitationTokenPayload> InvalidToken()
    {
        return Result<InvitationTokenPayload>.Failure(
            new Error(
                "invitation.token.invalid_or_expired",
                "초대 토큰이 올바르지 않거나 만료되었습니다.",
                "token"));
    }
}
