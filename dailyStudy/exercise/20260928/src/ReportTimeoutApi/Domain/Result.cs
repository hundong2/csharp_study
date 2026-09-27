namespace ReportTimeoutApi.Domain;

/// <summary>
/// 사용자가 고칠 수 있는 예상된 실패를 코드와 설명으로 표현합니다.
/// 예외와 달리 호출자가 분기해서 HTTP 400 또는 404 같은 계약으로 바꿀 수 있습니다.
/// </summary>
/// <param name="Code">로그·테스트·클라이언트가 안정적으로 비교할 영문 오류 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 오류 설명입니다.</param>
/// <remarks>괄호의 두 positional 파라미터는 생성자와 읽기 전용 속성을 함께 만들며, 생성자는 별도 값을 반환하지 않습니다.</remarks>
// record는 값 중심 데이터를 선언하는 문법입니다. 두 DomainError의 내용이 같으면 값도 같다고 비교할 수 있습니다.
public sealed record DomainError(string Code, string Message);

/// <summary>
/// 성공 값 또는 예상된 실패 하나만 담는 불변 결과 객체입니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 참조 형식입니다.</typeparam>
// <T>는 성공 값 형식을 나중에 정하는 generic 문법이고, where T : class는 참조 형식만 허용합니다.
public sealed class Result<T>
    where T : class
{
    /// <summary>
    /// 외부에서 성공과 실패가 동시에 들어간 잘못된 객체를 만들지 못하게 생성자를 숨깁니다.
    /// </summary>
    /// <param name="isSuccess">성공 상태이면 true, 예상된 실패 상태이면 false입니다.</param>
    /// <param name="value">성공 상태에서만 존재하는 결과 값입니다.</param>
    /// <param name="error">실패 상태에서만 존재하는 오류 값입니다.</param>
    /// <returns>생성자는 현재 Result 객체를 초기화하며 별도 값을 반환하지 않습니다.</returns>
    // T?와 DomainError?의 ?는 성공/실패 상태에 따라 해당 값이 null일 수 있음을 nullable 분석기에 알립니다.
    private Result(bool isSuccess, T? value, DomainError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>성공 결과인지 알려 줍니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>성공일 때의 값이며 실패이면 null입니다.</summary>
    public T? Value { get; }

    /// <summary>실패일 때의 오류이며 성공이면 null입니다.</summary>
    public DomainError? Error { get; }

    /// <summary>
    /// null이 아닌 성공 값을 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>성공 상태인 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상된 업무 오류를 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 처리할 안정적인 코드와 설명입니다.</param>
    /// <returns>실패 상태인 Result를 반환합니다.</returns>
    public static Result<T> Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, null, error);
    }
}
