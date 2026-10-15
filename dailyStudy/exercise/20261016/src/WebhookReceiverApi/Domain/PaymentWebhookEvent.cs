namespace WebhookReceiverApi.Domain;

/// <summary>
/// 형식과 업무 규칙 검증을 모두 통과한 불변 결제 이벤트입니다.
/// </summary>
public sealed record PaymentWebhookEvent
{
    private const decimal MaximumAmount = 100_000_000m;

    /// <summary>
    /// 외부에서 잘못된 상태를 직접 만들지 못하도록 검증된 값만 초기화합니다.
    /// </summary>
    /// <param name="kind">해석을 마친 이벤트 종류입니다.</param>
    /// <param name="paymentId">검증된 결제 식별자입니다.</param>
    /// <param name="amount">검증된 양수 금액입니다.</param>
    /// <param name="currency">검증된 세 글자 대문자 통화 코드입니다.</param>
    /// <returns>생성자는 새 Domain 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private PaymentWebhookEvent(
        WebhookEventKind kind,
        string paymentId,
        decimal amount,
        string currency)
    {
        Kind = kind;
        PaymentId = paymentId;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>검증을 마친 결제 이벤트 종류입니다.</summary>
    public WebhookEventKind Kind { get; }

    /// <summary>검증을 마친 결제 식별자입니다.</summary>
    public string PaymentId { get; }

    /// <summary>검증을 마친 양수 결제 금액입니다.</summary>
    public decimal Amount { get; }

    /// <summary>검증을 마친 세 글자 대문자 통화 코드입니다.</summary>
    public string Currency { get; }

    /// <summary>
    /// 파싱된 초안을 Domain 규칙으로 검증하고 안전한 결제 이벤트를 만듭니다.
    /// </summary>
    /// <param name="draft">JSON에서 읽은 아직 검증하지 않은 값 묶음입니다.</param>
    /// <returns>검증된 이벤트 또는 첫 번째 예상 오류를 반환합니다.</returns>
    public static Result<PaymentWebhookEvent> Create(PaymentWebhookDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // WebhookEventKind?의 ?는 enum 값뿐 아니라 실패 표시인 null도 담게 하고, switch expression은 입력 하나를 결과 하나로 대응시킵니다.
        WebhookEventKind? kind = draft.Type switch
        {
            "payment.succeeded" => WebhookEventKind.PaymentSucceeded,
            "payment.failed" => WebhookEventKind.PaymentFailed,
            _ => null
        };
        if (kind is null)
        {
            return Result<PaymentWebhookEvent>.Failure(
                new Error(
                    "webhook.type.unsupported",
                    "type은 payment.succeeded 또는 payment.failed여야 합니다.",
                    "type"));
        }

        if (string.IsNullOrWhiteSpace(draft.PaymentId) ||
            draft.PaymentId.Length > 100 ||
            !draft.PaymentId.All(
                character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
        {
            return Result<PaymentWebhookEvent>.Failure(
                new Error(
                    "payment.id.invalid",
                    "paymentId는 1~100자의 영문자, 숫자, 밑줄, 하이픈이어야 합니다.",
                    "data.paymentId"));
        }

        // decimal literal의 m 접미사는 double이 아니라 10진 decimal임을 나타내며 금융 입력 예제에 더 알맞습니다.
        if (draft.Amount <= 0m ||
            draft.Amount > MaximumAmount ||
            decimal.Round(draft.Amount, 2) != draft.Amount)
        {
            return Result<PaymentWebhookEvent>.Failure(
                new Error(
                    "payment.amount.invalid",
                    "amount는 0보다 크고 100,000,000 이하이며 소수 둘째 자리까지만 허용됩니다.",
                    "data.amount"));
        }

        if (draft.Currency is null ||
            draft.Currency.Length != 3 ||
            !draft.Currency.All(char.IsAsciiLetterUpper))
        {
            return Result<PaymentWebhookEvent>.Failure(
                new Error(
                    "payment.currency.invalid",
                    "currency는 KRW처럼 대문자 영문 세 글자여야 합니다.",
                    "data.currency"));
        }

        // nullable 값의 null 검사를 위에서 마쳤으므로 flow 분석이 PaymentId와 Currency를 non-null로 좁혀 줍니다.
        return Result<PaymentWebhookEvent>.Success(
            new PaymentWebhookEvent(
                kind.Value,
                draft.PaymentId,
                draft.Amount,
                draft.Currency));
    }

    /// <summary>
    /// 내부 enum을 외부 HTTP 계약에서 사용하는 안정적인 문자열로 바꿉니다.
    /// </summary>
    /// <returns>payment.succeeded 또는 payment.failed 문자열을 반환합니다.</returns>
    public string ToExternalType()
    {
        return Kind switch
        {
            WebhookEventKind.PaymentSucceeded => "payment.succeeded",
            WebhookEventKind.PaymentFailed => "payment.failed",
            _ => throw new InvalidOperationException("지원하지 않는 Domain 이벤트 종류입니다.")
        };
    }
}
