namespace TelemetryStreamExercise.Domain;

/// <summary>
/// 호출자가 안정적인 <paramref name="Code"/>로 분기하고 사람이 <paramref name="Message"/>를 읽을 수 있는 오류 값입니다.
/// record는 같은 세 값이 같으면 같은 오류로 비교되는 값 중심 형식이라서 오류 계약과 테스트에 알맞습니다.
/// </summary>
/// <param name="Kind">protocol 실패와 business 실패 중 어느 경계의 오류인지 나타냅니다.</param>
/// <param name="Code">로그와 테스트에서 사용할 짧고 안정적인 식별자입니다.</param>
/// <param name="Message">초보자도 원인을 이해할 수 있는 설명입니다.</param>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    /// <summary>
    /// byte stream 또는 JSON 표현 계약이 깨진 오류를 만듭니다.
    /// </summary>
    /// <param name="code">protocol 오류를 구분할 안정적인 코드입니다.</param>
    /// <param name="message">깨진 protocol 조건을 설명하는 문장입니다.</param>
    /// <returns>Kind가 Protocol인 오류를 반환합니다.</returns>
    public static Error Protocol(string code, string message)
    {
        return new Error(ErrorKind.Protocol, code, message);
    }

    /// <summary>
    /// 정상적으로 읽은 값이 domain 규칙을 어긴 business 오류를 만듭니다.
    /// </summary>
    /// <param name="code">business 오류를 구분할 안정적인 코드입니다.</param>
    /// <param name="message">어떤 업무 규칙을 어겼는지 설명하는 문장입니다.</param>
    /// <returns>Kind가 Business인 오류를 반환합니다.</returns>
    public static Error Business(string code, string message)
    {
        return new Error(ErrorKind.Business, code, message);
    }
}
