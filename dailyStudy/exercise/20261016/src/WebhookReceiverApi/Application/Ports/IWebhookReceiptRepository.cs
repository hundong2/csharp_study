using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Application.Ports;

/// <summary>
/// 웹훅 접수 결과의 원자적 저장과 조회를 정의하는 Repository Port입니다.
/// </summary>
public interface IWebhookReceiptRepository
{
    /// <summary>
    /// event ID가 아직 없을 때만 접수 결과를 한 번 저장합니다.
    /// </summary>
    /// <param name="receipt">인증과 Domain 검증을 끝낸 불변 접수 결과입니다.</param>
    /// <param name="cancellationToken">요청 중단 시 저장 시도를 취소하라는 신호입니다.</param>
    /// <returns>이번 호출이 저장에 성공했으면 true, 이미 같은 ID가 있으면 false를 반환합니다.</returns>
    Task<bool> TryAddAsync(
        WebhookReceipt receipt,
        CancellationToken cancellationToken);

    /// <summary>
    /// event ID로 이미 저장된 접수 결과를 찾습니다.
    /// </summary>
    /// <param name="eventId">찾을 검증된 이벤트 식별자입니다.</param>
    /// <param name="cancellationToken">요청 중단 시 조회를 취소하라는 신호입니다.</param>
    /// <returns>찾은 접수 결과이며 없으면 null인 Task를 반환합니다.</returns>
    Task<WebhookReceipt?> FindAsync(
        WebhookEventId eventId,
        CancellationToken cancellationToken);
}
