namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 성공 값 또는 예상 가능한 실패 중 정확히 하나만 담는 Result 패턴입니다.
/// 제네릭 &lt;T&gt;는 여러 형식에 같은 성공/실패 계약을 재사용하게 합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 null이 아닌 값의 형식입니다.</typeparam>
public sealed class Result<T>
    where T : notnull
{
    // T?의 ?는 성공하지 않은 Result에는 값이 없을 수 있음을 compiler에 알려 주는 nullable 표기입니다.
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// 성공 또는 실패 상태와 그 상태에 맞는 값 하나를 보관합니다.
    /// 세 파라미터로 상태를 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="isSuccess">true면 성공 값, false면 오류가 유효하다는 뜻입니다.</param>
    /// <param name="value">성공일 때 보관할 값이며 실패일 때는 없습니다.</param>
    /// <param name="error">실패일 때 보관할 오류이며 성공일 때는 없습니다.</param>
    private Result(bool isSuccess, T? value, Error? error)
    {
        if (isSuccess == (value is null) || isSuccess == (error is not null))
        {
            throw new ArgumentException("Result는 성공 값 또는 실패 오류 중 정확히 하나만 가져야 합니다.");
        }

        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>현재 결과가 성공이면 true를 반환합니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>성공 값을 읽으며 실패 Result에서 읽으면 계약 위반 예외를 냅니다.</summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패 Result에는 성공 값이 없습니다.");
            }

            // postfix !는 runtime 연산이 아니라 생성자 불변식 때문에 null이 아님을 compiler에 설명합니다.
            return _value!;
        }
    }

    /// <summary>실패 오류를 읽으며 성공 Result에서 읽으면 계약 위반 예외를 냅니다.</summary>
    public Error Error
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("성공 Result에는 실패 오류가 없습니다.");
            }

            return _error!;
        }
    }

    /// <summary>
    /// null이 아닌 성공 값을 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>성공 상태의 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 분기할 수 있는 오류 값입니다.</param>
    /// <returns>실패 상태의 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
