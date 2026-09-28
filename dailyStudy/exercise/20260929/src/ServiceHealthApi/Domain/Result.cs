namespace ServiceHealthApi.Domain;

/// <summary>
/// 호출자가 예상하고 분기할 수 있는 실패의 코드와 설명을 함께 보관합니다.
/// </summary>
/// <param name="Code">로그와 HTTP 응답에서 안정적으로 비교할 기계 친화적 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 설명입니다.</param>
// record는 값이 같으면 같은 데이터로 비교되는 불변 자료형입니다. 오류는 정체성보다 값이 중요해 record가 어울립니다.
public sealed record Error(string Code, string Message);

/// <summary>
/// 예상 가능한 성공 또는 실패를 예외 대신 값으로 전달합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 참조 형식입니다.</typeparam>
public sealed class Result<T>
    // where는 T를 class로 제한해 성공 값의 null 여부를 nullable 분석기가 정확히 추적하게 합니다.
    where T : class
{
    /// <summary>
    /// 성공 값과 실패 값을 동시에 만들 수 없도록 생성 경로를 클래스 내부로 제한합니다.
    /// </summary>
    /// <param name="value">성공 시의 값이며 실패라면 null입니다.</param>
    /// <param name="error">실패 시의 오류이며 성공이라면 null입니다.</param>
    /// <returns>생성자는 새 Result 인스턴스를 만들며 별도 반환값은 없습니다.</returns>
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 현재 결과가 성공인지 알려 줍니다.
    /// </summary>
    // =>는 한 식으로 끝나는 멤버를 짧게 쓰는 식 본문 문법이고, is null은 null 여부를 검사하는 pattern입니다.
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공 결과의 값입니다. 실패일 때는 값이 없으므로 null일 수 있습니다.
    /// </summary>
    // T?의 ?는 이 값이 null일 수 있음을 컴파일러의 nullable 분석기에 알립니다.
    public T? Value { get; }

    /// <summary>
    /// 실패 결과의 오류입니다. 성공일 때는 오류가 없으므로 null일 수 있습니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// null이 아닌 값을 성공 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>성공 상태와 값을 가진 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 코드로 분기할 수 있는 오류입니다.</param>
    /// <returns>실패 상태와 오류를 가진 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(null, error);
    }
}
