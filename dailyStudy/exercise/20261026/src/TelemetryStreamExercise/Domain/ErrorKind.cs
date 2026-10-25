namespace TelemetryStreamExercise.Domain;

/// <summary>
/// 오류가 byte stream 계약의 실패인지, 정상적으로 읽은 데이터의 업무 규칙 실패인지 구분합니다.
/// 이 구분 덕분에 protocol 오류는 즉시 중단하고 business 오류는 다음 frame을 계속 처리할 수 있습니다.
/// </summary>
public enum ErrorKind
{
    /// <summary>
    /// 길이 prefix, frame 완결성, JSON 구문처럼 송신자와 수신자의 wire protocol 계약이 깨진 경우입니다.
    /// </summary>
    Protocol,

    /// <summary>
    /// JSON은 읽었지만 필수 ID나 허용 온도처럼 domain 규칙을 만족하지 못한 경우입니다.
    /// </summary>
    Business
}
