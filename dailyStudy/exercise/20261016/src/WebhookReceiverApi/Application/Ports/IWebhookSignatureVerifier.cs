using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Application.Ports;

/// <summary>
/// Application 계층이 구체 암호화 라이브러리를 몰라도 웹훅 진위를 확인하게 하는 Port입니다.
/// 다른 공급자의 서명 규칙이 필요하면 이 interface 구현만 교체할 수 있어 Strategy 패턴 역할도 합니다.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>
    /// timestamp, event ID, 원본 body를 같은 규칙으로 서명해 제공된 HMAC과 안전하게 비교합니다.
    /// </summary>
    /// <param name="eventId">문법 검증을 마친 이벤트 식별자입니다.</param>
    /// <param name="timestampText">서명 입력에 그대로 포함할 Unix timestamp 문자열입니다.</param>
    /// <param name="signatureHeader">v1= 접두사와 64자리 16진수 digest가 든 헤더입니다.</param>
    /// <param name="rawBody">JSON 재직렬화 없이 그대로 서명할 HTTP body 바이트입니다.</param>
    /// <returns>인증 시각 정보 또는 안전한 예상 오류를 반환합니다.</returns>
    Result<VerifiedWebhook> Verify(
        WebhookEventId eventId,
        string timestampText,
        string signatureHeader,
        ReadOnlyMemory<byte> rawBody);
}
