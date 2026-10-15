namespace WebhookReceiverApi.Domain;

/// <summary>
/// 문법 검증을 마친 웹훅 이벤트 식별자를 나타냅니다.
/// 문자열을 별도 Domain 형식으로 감싸면 검증 전 문자열을 저장소 키로 실수로 사용하는 일을 막을 수 있습니다.
/// </summary>
// record는 객체의 주소보다 Value 값을 중심으로 비교하는 불변 값 객체에 잘 맞습니다.
public sealed record WebhookEventId
{
    private const int MaximumLength = 100;

    /// <summary>
    /// 검증을 우회한 식별자를 만들지 못하도록 생성자를 private으로 숨깁니다.
    /// </summary>
    /// <param name="value">서명과 재전송 방지 키에 그대로 사용할 검증된 값입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private WebhookEventId(string value)
    {
        Value = value;
    }

    /// <summary>
    /// 서명과 Repository key에 정규화 없이 그대로 사용할 검증된 문자열입니다.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 외부에서 받은 문자열이 안전한 웹훅 식별자 형식인지 검사합니다.
    /// </summary>
    /// <param name="value">HTTP 헤더에서 읽은 아직 신뢰하지 않은 문자열입니다.</param>
    /// <returns>영문자, 숫자, 밑줄, 하이픈으로만 된 식별자 또는 예상 오류를 반환합니다.</returns>
    public static Result<WebhookEventId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<WebhookEventId>.Failure(
                new Error(
                    "webhook.event_id.invalid",
                    "X-Webhook-Id는 비어 있을 수 없습니다.",
                    "X-Webhook-Id"));
        }

        if (value.Length > MaximumLength)
        {
            return Result<WebhookEventId>.Failure(
                new Error(
                    "webhook.event_id.invalid",
                    $"X-Webhook-Id는 {MaximumLength}자 이하여야 합니다.",
                    "X-Webhook-Id"));
        }

        // var는 오른쪽 결과에서 bool 형식을 추론하고, =>는 각 character를 검사하는 이름 없는 함수(lambda)를 뜻합니다.
        // LINQ All은 모든 문자가 조건을 만족할 때만 true이고, is ... or는 허용 문자 후보 중 하나인지 확인하는 패턴입니다.
        var containsOnlySafeCharacters = value.All(
            character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        if (!containsOnlySafeCharacters)
        {
            return Result<WebhookEventId>.Failure(
                new Error(
                    "webhook.event_id.invalid",
                    "X-Webhook-Id에는 영문자, 숫자, 밑줄, 하이픈만 사용할 수 있습니다.",
                    "X-Webhook-Id"));
        }

        return Result<WebhookEventId>.Success(new WebhookEventId(value));
    }
}
