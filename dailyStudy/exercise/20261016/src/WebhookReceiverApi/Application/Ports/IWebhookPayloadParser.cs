using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Application.Ports;

/// <summary>
/// Application 계층을 System.Text.Json 같은 구체 직렬화 구현에서 분리하는 Port입니다.
/// </summary>
public interface IWebhookPayloadParser
{
    /// <summary>
    /// 인증을 마친 원본 JSON body를 아직 Domain 검증 전인 초안으로 변환합니다.
    /// </summary>
    /// <param name="rawBody">HMAC 검증에 사용했던 것과 동일한 body 바이트입니다.</param>
    /// <returns>파싱된 초안 또는 JSON 형식 오류를 반환합니다.</returns>
    Result<PaymentWebhookDraft> Parse(ReadOnlyMemory<byte> rawBody);
}
