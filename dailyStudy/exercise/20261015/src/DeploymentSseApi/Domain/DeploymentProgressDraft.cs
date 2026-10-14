namespace DeploymentSseApi.Domain;

/// <summary>
/// HTTP 요청처럼 아직 신뢰할 수 없는 배포 진행 입력을 불변 값으로 묶습니다.
/// Draft와 검증된 Event를 나누면 잘못된 외부 입력이 저장된 Domain 사실처럼 취급되는 일을 막을 수 있습니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="DeploymentId">진행률을 기록할 배포 식별자이며 아직 공백과 문자를 검증하지 않았습니다.</param>
/// <param name="Stage">Queued 같은 단계 문자열이며 아직 대소문자와 허용 값을 검증하지 않았습니다.</param>
/// <param name="Percent">0부터 100 사이여야 하는 후보 진행률입니다.</param>
/// <param name="Message">사람이 읽을 진행 설명이며 아직 길이와 공백을 검증하지 않았습니다.</param>
// record의 위치 매개변수는 읽기 전용 init 속성을 자동으로 만들어, 요청을 전달하는 동안 값이 바뀌지 않게 합니다.
public sealed record DeploymentProgressDraft(
    string? DeploymentId,
    string? Stage,
    int Percent,
    string? Message);
