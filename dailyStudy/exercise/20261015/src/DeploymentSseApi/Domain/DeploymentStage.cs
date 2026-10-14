namespace DeploymentSseApi.Domain;

/// <summary>
/// 배포가 지나가는 업무 단계를 제한된 값으로 표현합니다.
/// 문자열을 여기저기 비교하는 대신 enum을 사용하면 오타를 컴파일러와 Strategy가 일찍 발견할 수 있습니다.
/// </summary>
public enum DeploymentStage
{
    Queued,
    Building,
    Deploying,
    Completed,
    Failed
}
