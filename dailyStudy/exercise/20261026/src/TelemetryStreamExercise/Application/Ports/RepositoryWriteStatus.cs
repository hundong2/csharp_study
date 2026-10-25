namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// event ID 기반 원자적 저장 시도를 세 가지 의미로 구분합니다.
/// 동일 재전송과 같은 ID의 다른 내용은 운영상 의미가 다르므로 하나의 bool로 뭉치지 않습니다.
/// </summary>
public enum RepositoryWriteStatus
{
    /// <summary>
    /// 이 event ID가 처음이라 새로 저장했습니다.
    /// </summary>
    Stored,

    /// <summary>
    /// event ID와 내용이 모두 같아 멱등하게 쓰기를 생략했습니다.
    /// </summary>
    Duplicate,

    /// <summary>
    /// 같은 event ID에 다른 내용이 이미 있어 잘못된 재사용으로 거절했습니다.
    /// </summary>
    Conflict
}
