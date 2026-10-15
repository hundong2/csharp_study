using System.Text.Json;
using System.Text.Json.Serialization;
using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Infrastructure;

/// <summary>
/// 인증을 마친 UTF-8 body를 System.Text.Json으로 엄격하게 읽는 Payload Parser Adapter입니다.
/// </summary>
public sealed class SystemTextJsonWebhookPayloadParser : IWebhookPayloadParser
{
    // new()는 왼쪽 선언에서 JsonSerializerOptions 형식을 추론하는 target-typed new이며, 중괄호는 속성 초기화 문법입니다.
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    /// <summary>
    /// 원본 UTF-8 JSON을 transport DTO로 읽고 Domain 초안으로 복사합니다.
    /// </summary>
    /// <param name="rawBody">HMAC 검증을 이미 마친 exact HTTP body 바이트입니다.</param>
    /// <returns>파싱된 결제 웹훅 초안 또는 외부에 안전한 JSON 오류를 반환합니다.</returns>
    public Result<PaymentWebhookDraft> Parse(ReadOnlyMemory<byte> rawBody)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(
                rawBody.Span,
                _serializerOptions);
            // ?.는 payload가 null이면 Type 접근 대신 null을 만들고, is null pattern이 누락과 명시적 JSON null을 함께 잡습니다.
            if (payload?.Type is null ||
                payload.Data?.PaymentId is null ||
                payload.Data.Currency is null)
            {
                return InvalidPayload();
            }

            return Result<PaymentWebhookDraft>.Success(
                new PaymentWebhookDraft(
                    payload.Type,
                    payload.Data.PaymentId,
                    payload.Data.Amount,
                    payload.Data.Currency));
        }
        catch (JsonException)
        {
            // parser의 상세 경로나 byte offset은 공격자에게 구현 단서를 줄 수 있으므로 외부 오류에는 넣지 않습니다.
            return InvalidPayload();
        }
    }

    /// <summary>
    /// JSON 상세 파싱 정보를 숨긴 공통 payload 오류를 만듭니다.
    /// </summary>
    /// <returns>webhook.payload.invalid_json 오류가 든 실패 결과를 반환합니다.</returns>
    private static Result<PaymentWebhookDraft> InvalidPayload()
    {
        return Result<PaymentWebhookDraft>.Failure(
            new Error(
                "webhook.payload.invalid_json",
                "요청 body는 필수 필드를 가진 올바른 결제 웹훅 JSON이어야 합니다.",
                "request"));
    }

    /// <summary>
    /// 외부 JSON의 최상위 type과 data 구조만 표현하는 transport DTO입니다.
    /// </summary>
    /// <param name="Type">공급자 이벤트 종류입니다.</param>
    /// <param name="Data">결제 상세 객체이며 JSON null이면 null일 수 있습니다.</param>
    private sealed record PaymentWebhookPayload(
        string? Type,
        PaymentWebhookData? Data);

    /// <summary>
    /// 외부 JSON data 객체의 결제 필드를 표현하는 transport DTO입니다.
    /// </summary>
    /// <param name="PaymentId">공급자의 결제 식별자입니다.</param>
    /// <param name="Amount">결제 금액입니다.</param>
    /// <param name="Currency">세 글자 통화 코드입니다.</param>
    private sealed record PaymentWebhookData(
        string? PaymentId,
        decimal Amount,
        string? Currency);
}
