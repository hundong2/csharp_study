namespace WebhookReceiverApi.Application;

/// <summary>
/// HTTP 세부 구현에서 분리해 Application Service에 전달할 웹훅 수신 명령입니다.
/// </summary>
/// <param name="EventId">X-Webhook-Id 헤더의 단일 문자열 값입니다.</param>
/// <param name="Timestamp">X-Webhook-Timestamp 헤더의 단일 문자열 값입니다.</param>
/// <param name="Signature">X-Webhook-Signature 헤더의 단일 문자열 값입니다.</param>
/// <param name="RawBody">서명 검증에 반드시 그대로 사용해야 하는 원본 HTTP body 바이트입니다.</param>
// ReadOnlyMemory<byte>는 비동기 메서드를 건너 전달할 수 있으면서 이 명령이 body를 수정하지 않는다는 의도를 나타냅니다.
public sealed record ReceiveWebhookCommand(
    string EventId,
    string Timestamp,
    string Signature,
    ReadOnlyMemory<byte> RawBody);
