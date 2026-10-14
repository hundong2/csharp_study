namespace DeploymentSseApi.Presentation;

/// <summary>
/// HTTP JSON body에서 받은 아직 신뢰할 수 없는 배포 진행 입력입니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Stage">Queued, Building, Deploying, Completed, Failed 중 하나인 단계 문자열입니다.</param>
/// <param name="Percent">단계와 함께 검증할 0부터 100 사이의 진행률 후보입니다.</param>
/// <param name="Message">사람에게 보여 줄 짧은 진행 설명이며 null일 수 있습니다.</param>
// string?의 ?는 외부 JSON이 필드를 명시하면서 null을 보낼 수 있음을 compiler와 독자에게 알립니다.
// 필드 자체의 누락은 Program의 RespectRequiredConstructorParameters 설정이 framework 수준 400으로 막습니다.
public sealed record PublishDeploymentProgressRequest(
    string? Stage,
    int Percent,
    string? Message);

/// <summary>
/// POST 응답과 SSE data가 공유하는 안정적인 공개 계약입니다.
/// Domain 객체를 직접 노출하지 않아 내부 이름이나 구조를 바꿔도 HTTP 계약을 따로 유지할 수 있습니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="EventId">배포 하나 안에서 1부터 증가하는 재연결 위치입니다.</param>
/// <param name="DeploymentId">앞뒤 공백을 제거하되 대소문자는 보존한 배포 식별자입니다.</param>
/// <param name="Stage">현재 배포 단계 이름입니다.</param>
/// <param name="Percent">현재 진행률입니다.</param>
/// <param name="Message">공백과 길이 검증을 통과한 진행 설명입니다.</param>
/// <param name="OccurredAtUtc">서버가 기록한 UTC 발생 시각입니다.</param>
/// <param name="IsTerminal">Completed 또는 Failed여서 이 SSE stream이 끝나는지 나타냅니다.</param>
public sealed record DeploymentProgressResponse(
    long EventId,
    string DeploymentId,
    string Stage,
    int Percent,
    string Message,
    DateTimeOffset OccurredAtUtc,
    bool IsTerminal);

/// <summary>
/// 문서화 metadata에서 공통 Problem Details 확장 필드를 보여 주기 위한 응답 모양입니다.
/// 실제 오류 작성은 ASP.NET Core의 ProblemDetails가 담당합니다.
/// </summary>
public sealed record ApiProblemResponse
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? Code { get; init; }

    public string? Field { get; init; }

    public string? TraceId { get; init; }
}
