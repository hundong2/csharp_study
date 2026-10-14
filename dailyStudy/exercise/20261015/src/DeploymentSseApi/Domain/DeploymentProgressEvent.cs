namespace DeploymentSseApi.Domain;

/// <summary>
/// Strategy 검증을 통과해 저장되고 SSE로 전달할 수 있는 하나의 불변 배포 사실입니다.
/// EventId는 배포 하나 안에서 1부터 증가하며 SSE 재연결 위치와 낙관적 동시성 버전을 함께 표현합니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="EventId">배포별로 1부터 빈틈없이 증가하는 SSE id이자 저장 버전입니다.</param>
/// <param name="DeploymentId">이 진행 이벤트가 속한 검증·정규화된 배포 식별자입니다.</param>
/// <param name="Stage">Strategy가 이전 이벤트와 비교해 승인한 배포 단계입니다.</param>
/// <param name="Percent">0부터 100 사이이며 이전 이벤트보다 작아지지 않는 진행률입니다.</param>
/// <param name="Message">공백을 정리하고 길이를 제한한 사용자용 진행 설명입니다.</param>
/// <param name="OccurredAtUtc">Application Service가 TimeProvider로 한 번 읽은 UTC 발생 시각입니다.</param>
public sealed record DeploymentProgressEvent(
    long EventId,
    string DeploymentId,
    DeploymentStage Stage,
    int Percent,
    string Message,
    DateTimeOffset OccurredAtUtc)
{
    // `is A or B` 패턴은 단계가 둘 중 하나인지를 읽기 쉽게 검사합니다.
    // 종료 여부를 별도 저장하지 않고 Stage에서 계산하면 두 값이 서로 어긋날 수 없습니다.
    public bool IsTerminal => Stage is DeploymentStage.Completed or DeploymentStage.Failed;
}
