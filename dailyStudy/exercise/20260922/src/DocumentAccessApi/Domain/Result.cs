namespace DocumentAccessApi.Domain;

/// <summary>
/// 호출자가 고칠 수 있는 예상 실패의 코드와 설명을 함께 보관합니다.
/// 예외와 달리 정상적인 업무 분기로 다룰 오류만 이 타입에 담습니다.
/// </summary>
/// <param name="Code">로그와 응답에서 안정적으로 비교할 짧은 오류 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 오류 설명입니다.</param>
public sealed record Error(string Code, string Message);

/// <summary>
/// 성공 값 또는 예상 오류 중 정확히 하나만 가지는 간단한 Result 타입입니다.
/// Domain factory가 Application 계층을 거꾸로 의존하지 않도록 Domain에 둡니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 타입입니다.</typeparam>
public sealed class Result<T>
{
    // T?의 물음표는 성공 값이 없을 수 있음을 컴파일러에 알리는 nullable 문법입니다.
    private readonly T? _value;

    /// <summary>
    /// 성공 값과 오류를 한 객체 안에서 모순 없이 초기화합니다. 외부에서는 Success 또는 Failure factory만 사용합니다.
    /// </summary>
    /// <param name="value">성공일 때 보관할 값이며 실패일 때는 기본값입니다.</param>
    /// <param name="error">실패일 때 보관할 오류이며 성공일 때는 null입니다.</param>
    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>
    /// 오류가 없어서 성공 값을 안전하게 읽을 수 있는지 알려 줍니다.
    /// 화살표(=&gt;)는 계산식 하나로 속성을 구현하는 expression-bodied 문법입니다.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 실패 원인을 반환하며, 성공 결과라면 null을 반환합니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// 성공 값을 반환합니다. 실패 결과에서 읽으면 프로그래밍 오류를 빨리 발견하도록 예외를 던집니다.
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패한 Result에서는 Value를 읽을 수 없습니다.");
            }

            // 느낌표(!)는 위의 성공 검사 때문에 null이 아님을 컴파일러에 알려 주는 null-forgiving 연산자입니다.
            return _value!;
        }
    }

    /// <summary>
    /// 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>오류가 없는 새 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, error: null);
    }

    /// <summary>
    /// 예상 가능한 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 판단할 수 있는 오류 코드와 설명입니다.</param>
    /// <returns>성공 값이 없는 새 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(value: default, error);
    }
}
