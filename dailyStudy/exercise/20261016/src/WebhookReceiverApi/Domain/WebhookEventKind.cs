namespace WebhookReceiverApi.Domain;

/// <summary>
/// 이 학습 API가 명시적으로 처리할 수 있는 결제 웹훅 종류입니다.
/// </summary>
public enum WebhookEventKind
{
    /// <summary>결제가 성공적으로 완료된 이벤트입니다.</summary>
    PaymentSucceeded,

    /// <summary>결제가 실패한 이벤트입니다.</summary>
    PaymentFailed
}
