namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// stateless frame decoder가 현재 buffer에서 내린 세 가지 결정을 나타냅니다.
/// 아직 byte가 부족한 상태는 잘못된 입력이 아니므로 ProtocolError와 별도 값으로 둡니다.
/// </summary>
public enum FrameDecodeStatus
{
    /// <summary>
    /// header 또는 payload가 더 들어와야 결정을 내릴 수 있습니다.
    /// </summary>
    NeedMoreData,

    /// <summary>
    /// 완전한 frame 하나를 읽어 payload를 만들었습니다.
    /// </summary>
    FrameDecoded,

    /// <summary>
    /// 길이 또는 EOF 규칙이 깨져 더 처리할 수 없습니다.
    /// </summary>
    ProtocolError
}
