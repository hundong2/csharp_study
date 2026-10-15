using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Application;

/// <summary>
/// 인증, JSON 파싱, Domain 검증, 재전송 방지 저장을 올바른 순서로 조율하는 Application Service입니다.
/// 각 세부 작업은 Port에 맡기므로 HTTP 서버나 실제 암호 키 없이도 use case를 독립적으로 테스트할 수 있습니다.
/// </summary>
public sealed class WebhookReceiverApplicationService
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IWebhookPayloadParser _payloadParser;
    private readonly IWebhookReceiptRepository _repository;

    /// <summary>
    /// use case가 필요한 세 협력 객체를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="signatureVerifier">timestamp와 HMAC을 확인할 Strategy Port입니다.</param>
    /// <param name="payloadParser">인증된 body를 JSON 초안으로 바꿀 Port입니다.</param>
    /// <param name="repository">event ID를 원자적으로 한 번만 저장할 Repository Port입니다.</param>
    /// <returns>생성자는 서비스 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    public WebhookReceiverApplicationService(
        IWebhookSignatureVerifier signatureVerifier,
        IWebhookPayloadParser payloadParser,
        IWebhookReceiptRepository repository)
    {
        _signatureVerifier = signatureVerifier;
        _payloadParser = payloadParser;
        _repository = repository;
    }

    /// <summary>
    /// 외부 웹훅 하나를 인증하고 업무 입력을 검증한 뒤 event ID를 정확히 한 번만 접수합니다.
    /// </summary>
    /// <param name="command">단일 헤더 값 세 개와 exact raw body를 담은 수신 명령입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 하위 저장 작업까지 전달하는 취소 신호입니다.</param>
    /// <returns>새 접수 결과 또는 호출자가 처리할 수 있는 예상 오류를 담은 Task를 반환합니다.</returns>
    public async Task<Result<WebhookReceipt>> ReceiveAsync(
        ReceiveWebhookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // 이미 끊긴 요청에는 HMAC과 JSON parsing CPU를 쓰지 않도록 use case 입구에서 취소를 먼저 관찰합니다.
        cancellationToken.ThrowIfCancellationRequested();

        // var는 오른쪽 반환값에서 Result<WebhookEventId>라는 정적 형식을 추론합니다.
        var eventIdResult = WebhookEventId.Create(command.EventId);
        if (!eventIdResult.IsSuccess)
        {
            // !는 runtime 검사가 아니라 IsSuccess가 false라 Error가 null이 아님을 nullable 분석기에 알리는 표식입니다.
            return Result<WebhookReceipt>.Failure(eventIdResult.Error!);
        }

        var eventId = eventIdResult.Value!;
        var verificationResult = _signatureVerifier.Verify(
            eventId,
            command.Timestamp,
            command.Signature,
            command.RawBody);
        if (!verificationResult.IsSuccess)
        {
            return Result<WebhookReceipt>.Failure(verificationResult.Error!);
        }

        // HMAC 계산 중 연결이 끊겼다면 다음 parsing 단계로 넘어가기 전에 다시 중단합니다.
        cancellationToken.ThrowIfCancellationRequested();
        // 서명이 틀린 body를 먼저 JSON으로 파싱하면 공격자가 parser 비용과 오류 차이를 관찰할 수 있으므로 인증 뒤에 파싱합니다.
        var draftResult = _payloadParser.Parse(command.RawBody);
        if (!draftResult.IsSuccess)
        {
            return Result<WebhookReceipt>.Failure(draftResult.Error!);
        }

        var paymentEventResult = PaymentWebhookEvent.Create(draftResult.Value!);
        if (!paymentEventResult.IsSuccess)
        {
            return Result<WebhookReceipt>.Failure(paymentEventResult.Error!);
        }

        var verification = verificationResult.Value!;
        var receipt = new WebhookReceipt(
            eventId,
            paymentEventResult.Value!,
            verification.SignedAtUtc,
            verification.VerifiedAtUtc);

        // await는 저장이 끝날 때까지 thread를 붙잡아 두지 않고, 완료 뒤 결과부터 이어서 실행하게 합니다.
        var wasAdded = await _repository.TryAddAsync(receipt, cancellationToken);
        if (!wasAdded)
        {
            return Result<WebhookReceipt>.Failure(
                new Error(
                    "webhook.replayed",
                    "이미 접수한 X-Webhook-Id입니다.",
                    "X-Webhook-Id"));
        }

        return Result<WebhookReceipt>.Success(receipt);
    }

    /// <summary>
    /// 검증된 event ID로 이미 접수한 결과를 조회합니다.
    /// </summary>
    /// <param name="eventIdText">route에서 받은 아직 검증하지 않은 event ID 문자열입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 Repository까지 전달하는 취소 신호입니다.</param>
    /// <returns>접수 결과 또는 잘못된 ID·미존재 오류를 담은 Task를 반환합니다.</returns>
    public async Task<Result<WebhookReceipt>> FindAsync(
        string eventIdText,
        CancellationToken cancellationToken)
    {
        var eventIdResult = WebhookEventId.Create(eventIdText);
        if (!eventIdResult.IsSuccess)
        {
            return Result<WebhookReceipt>.Failure(eventIdResult.Error!);
        }

        var receipt = await _repository.FindAsync(
            eventIdResult.Value!,
            cancellationToken);
        // 조건 ? A : B는 조건에 따라 두 결과 중 하나를 고르는 조건 연산자입니다.
        return receipt is null
            ? Result<WebhookReceipt>.Failure(
                new Error(
                    "webhook.not_found",
                    "해당 event ID의 접수 결과를 찾을 수 없습니다.",
                    "eventId"))
            : Result<WebhookReceipt>.Success(receipt);
    }
}
