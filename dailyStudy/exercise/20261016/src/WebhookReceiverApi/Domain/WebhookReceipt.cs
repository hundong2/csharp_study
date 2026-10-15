namespace WebhookReceiverApi.Domain;

/// <summary>
/// 인증·검증·원자적 재전송 방지 저장까지 마친 웹훅 접수 결과입니다.
/// </summary>
/// <param name="EventId">공급자가 보낸 고유 이벤트 식별자입니다.</param>
/// <param name="PaymentEvent">업무 규칙을 통과한 결제 이벤트입니다.</param>
/// <param name="SignedAtUtc">공급자가 Unix timestamp로 서명한 UTC 시각입니다.</param>
/// <param name="ReceivedAtUtc">수신기가 인증을 완료한 UTC 시각입니다.</param>
// positional record는 접수 사실을 생성 뒤 수정하지 않는 값 묶음으로 표현하고 값 기준 비교를 제공합니다.
public sealed record WebhookReceipt(
    WebhookEventId EventId,
    PaymentWebhookEvent PaymentEvent,
    DateTimeOffset SignedAtUtc,
    DateTimeOffset ReceivedAtUtc);
