using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebhookReceiverApi.Application;
using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Infrastructure;

/// <summary>
/// 공유 secret과 HMAC-SHA256으로 웹훅의 발신자와 무결성을 확인하는 Signature Strategy입니다.
/// </summary>
public sealed class HmacSha256WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private const string SignaturePrefix = "v1=";
    private const int Sha256ByteLength = 32;
    private const int Sha256HexLength = Sha256ByteLength * 2;

    private readonly byte[] _secretKey;
    private readonly long _allowedClockSkewSeconds;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 검증 설정과 교체 가능한 시계를 받아 재사용 가능한 검증기를 만듭니다.
    /// </summary>
    /// <param name="options">DI options가 제공하는 secret과 허용 시간창입니다.</param>
    /// <param name="timeProvider">현재 시각을 제공하며 테스트에서는 고정 시계로 바꿀 수 있습니다.</param>
    /// <returns>생성자는 검증기를 초기화하므로 별도 반환값은 없습니다.</returns>
    public HmacSha256WebhookSignatureVerifier(
        IOptions<WebhookSecurityOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var configured = options.Value;
        var secretByteCount = Encoding.UTF8.GetByteCount(configured.Secret);
        if (secretByteCount < 32)
        {
            throw new ArgumentException(
                "WebhookSecurity:Secret은 UTF-8 기준 32바이트 이상이어야 합니다.",
                nameof(options));
        }

        _secretKey = Encoding.UTF8.GetBytes(configured.Secret);
        _allowedClockSkewSeconds = WebhookSecurityOptions.AllowedClockSkewSeconds;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// canonical timestamp와 HMAC을 확인해 본문·event ID·시각이 전송 뒤 바뀌지 않았음을 검증합니다.
    /// </summary>
    /// <param name="eventId">문법 검증을 마친 이벤트 식별자입니다.</param>
    /// <param name="timestampText">Unix seconds 형식의 timestamp 헤더입니다.</param>
    /// <param name="signatureHeader">v1= 뒤에 64자리 16진수 digest가 오는 서명 헤더입니다.</param>
    /// <param name="rawBody">공급자가 서명한 exact HTTP body 바이트입니다.</param>
    /// <returns>서명 시각과 검증 시각 또는 timestamp·인증 오류를 반환합니다.</returns>
    public Result<VerifiedWebhook> Verify(
        WebhookEventId eventId,
        string timestampText,
        string signatureHeader,
        ReadOnlyMemory<byte> rawBody)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        if (!long.TryParse(
                timestampText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                // out var는 parsing 성공 여부와 변환된 long 값을 한 호출에서 받습니다.
                out var timestampSeconds) ||
            timestampSeconds.ToString(CultureInfo.InvariantCulture) != timestampText)
        {
            return Result<VerifiedWebhook>.Failure(
                new Error(
                    "webhook.timestamp.invalid",
                    "X-Webhook-Timestamp는 정규화된 Unix seconds 정수여야 합니다.",
                    "X-Webhook-Timestamp"));
        }

        DateTimeOffset signedAtUtc;
        try
        {
            signedAtUtc = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<VerifiedWebhook>.Failure(
                new Error(
                    "webhook.timestamp.invalid",
                    "X-Webhook-Timestamp가 표현 가능한 UTC 시각 범위를 벗어났습니다.",
                    "X-Webhook-Timestamp"));
        }

        var verifiedAtUtc = _timeProvider.GetUtcNow();
        // header 정밀도가 초 단위이므로 현재 시각도 Unix seconds로 맞춰 정확히 ±300초 경계를 일관되게 처리합니다.
        var verifiedAtSeconds = verifiedAtUtc.ToUnixTimeSeconds();
        if (timestampSeconds < verifiedAtSeconds - _allowedClockSkewSeconds ||
            timestampSeconds > verifiedAtSeconds + _allowedClockSkewSeconds)
        {
            return Result<VerifiedWebhook>.Failure(
                new Error(
                    "webhook.timestamp.out_of_window",
                    "X-Webhook-Timestamp가 허용 시간창을 벗어났습니다.",
                    "X-Webhook-Timestamp"));
        }

        if (!TryDecodeSignature(signatureHeader, out var providedSignature))
        {
            return AuthenticationFailure();
        }

        // canonical prefix에 event ID를 넣어 공격자가 서명된 body를 새 ID로 바꾸어 replay 차단을 우회하지 못하게 합니다.
        var prefixBytes = Encoding.UTF8.GetBytes(
            $"{timestampText}.{eventId.Value}.");
        var canonicalBytes = new byte[prefixBytes.Length + rawBody.Length];
        prefixBytes.CopyTo(canonicalBytes, 0);
        rawBody.Span.CopyTo(canonicalBytes.AsSpan(prefixBytes.Length));

        // 정적 HashData는 HMAC 객체의 수명 관리를 직접 하지 않아도 secret과 메시지로 32바이트 digest를 계산합니다.
        var expectedSignature = HMACSHA256.HashData(_secretKey, canonicalBytes);
        // FixedTimeEquals는 서로 다른 첫 바이트에서 일찍 끝내지 않아 비교 시간으로 digest 내용을 추측하기 어렵게 합니다.
        var signaturesMatch = CryptographicOperations.FixedTimeEquals(
            expectedSignature,
            providedSignature);
        // digest와 canonical 복사본은 장기 보관할 필요가 없으므로 사용 직후 덮어씁니다.
        CryptographicOperations.ZeroMemory(expectedSignature);
        CryptographicOperations.ZeroMemory(canonicalBytes);
        CryptographicOperations.ZeroMemory(providedSignature);

        return signaturesMatch
            ? Result<VerifiedWebhook>.Success(
                new VerifiedWebhook(signedAtUtc, verifiedAtUtc))
            : AuthenticationFailure();
    }

    /// <summary>
    /// v1=64자리-hex 계약을 엄격히 확인하고 32바이트 digest로 디코딩합니다.
    /// </summary>
    /// <param name="signatureHeader">아직 신뢰하지 않는 서명 헤더 문자열입니다.</param>
    /// <param name="signature">성공하면 디코딩된 digest, 실패하면 빈 배열을 받습니다.</param>
    /// <returns>형식과 길이가 정확하면 true를 반환합니다.</returns>
    private static bool TryDecodeSignature(
        string signatureHeader,
        out byte[] signature)
    {
        // []는 C# collection expression으로, 여기서는 길이 0인 byte 배열을 간결하게 만듭니다.
        signature = [];
        if (signatureHeader.Length != SignaturePrefix.Length + Sha256HexLength ||
            !signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var signatureHex = signatureHeader.AsSpan(SignaturePrefix.Length);
        if (!IsLowercaseHex(signatureHex))
        {
            return false;
        }

        try
        {
            // Span을 받는 overload는 별도의 중간 substring 없이 16진수 구간을 바로 해석합니다.
            signature = Convert.FromHexString(signatureHex);
            return signature.Length == Sha256ByteLength;
        }
        catch (FormatException)
        {
            signature = [];
            return false;
        }
    }

    /// <summary>
    /// 16진수 표현을 소문자 하나로 정규화해 서로 다른 문자열이 같은 digest를 뜻하는 애매함을 없앱니다.
    /// </summary>
    /// <param name="characters">v1= 접두사를 제외한 64개 문자입니다.</param>
    /// <returns>모든 문자가 0~9 또는 a~f이면 true를 반환합니다.</returns>
    private static bool IsLowercaseHex(ReadOnlySpan<char> characters)
    {
        foreach (var character in characters)
        {
            // relational/list가 아닌 이 pattern은 숫자 범위도 아니고 a~f 범위도 아닌 문자를 찾습니다.
            if (character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 잘못된 서명 형식과 digest 불일치를 같은 안전한 오류로 감춥니다.
    /// </summary>
    /// <returns>비밀값이나 어느 바이트가 다른지 노출하지 않는 인증 실패를 반환합니다.</returns>
    private static Result<VerifiedWebhook> AuthenticationFailure()
    {
        return Result<VerifiedWebhook>.Failure(
            new Error(
                "webhook.unauthorized",
                "웹훅 서명을 확인할 수 없습니다.",
                "X-Webhook-Signature"));
    }
}
