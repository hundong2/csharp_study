namespace WebhookReceiverApi.Presentation;

/// <summary>
/// 접수 결과에서 외부에 공개해도 되는 필드만 담는 HTTP 응답 DTO입니다.
/// </summary>
/// <param name="EventId">공급자가 보낸 고유 이벤트 식별자입니다.</param>
/// <param name="Type">payment.succeeded 또는 payment.failed 이벤트 종류입니다.</param>
/// <param name="PaymentId">결제 식별자입니다.</param>
/// <param name="Amount">검증된 결제 금액입니다.</param>
/// <param name="Currency">검증된 통화 코드입니다.</param>
/// <param name="SignedAtUtc">공급자가 서명한 UTC 시각입니다.</param>
/// <param name="ReceivedAtUtc">수신기가 인증을 마친 UTC 시각입니다.</param>
/// <param name="Status">이 예제에서 항상 accepted인 처리 상태입니다.</param>
// positional record는 괄호 안 항목을 읽기 전용 응답 속성으로 만들고 값 중심 비교를 제공합니다.
public sealed record WebhookReceiptResponse(
    string EventId,
    string Type,
    string PaymentId,
    decimal Amount,
    string Currency,
    DateTimeOffset SignedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string Status);

/// <summary>
/// OpenAPI metadata에서 공통 Problem Details 확장 필드를 설명하기 위한 응답 DTO입니다.
/// 실제 오류 body는 ASP.NET Core Results.Problem이 작성합니다.
/// </summary>
/// <param name="Type">오류 종류 문서 URI이며 이 예제에서는 about:blank입니다.</param>
/// <param name="Title">HTTP 상태에 맞는 짧은 제목입니다.</param>
/// <param name="Status">HTTP 상태 코드입니다.</param>
/// <param name="Detail">사용자가 다음 행동을 정할 수 있는 안전한 설명입니다.</param>
/// <param name="Instance">오류가 발생한 요청 경로입니다.</param>
/// <param name="Code">프로그램이 분기할 안정적인 오류 코드입니다.</param>
/// <param name="Field">문제가 있는 헤더나 JSON 필드입니다.</param>
/// <param name="TraceId">운영 로그와 응답을 연결할 추적 식별자입니다.</param>
public sealed record ApiProblemResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string Field,
    string TraceId);
