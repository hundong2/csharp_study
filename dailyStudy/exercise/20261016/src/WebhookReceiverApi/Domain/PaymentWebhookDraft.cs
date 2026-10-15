namespace WebhookReceiverApi.Domain;

/// <summary>
/// JSON에서 읽었지만 아직 Domain 규칙을 통과하지 않은 결제 웹훅 입력입니다.
/// </summary>
/// <param name="Type">공급자가 보낸 이벤트 종류 문자열입니다.</param>
/// <param name="PaymentId">결제 시스템의 결제 식별자입니다.</param>
/// <param name="Amount">결제 금액입니다.</param>
/// <param name="Currency">ISO 4217과 같은 세 글자 대문자 통화 코드입니다.</param>
// 괄호 속 매개변수가 곧 읽기 전용 속성이 되는 positional record 문법이며, string?는 JSON null을 정직하게 표현합니다.
public sealed record PaymentWebhookDraft(
    string? Type,
    string? PaymentId,
    decimal Amount,
    string? Currency);
