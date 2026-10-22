namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 성공 값 또는 예상 가능한 오류 중 정확히 하나만 보관하는 Result 패턴 구현입니다.
/// 잘못된 사용자 데이터는 정상적인 분기이므로 예외를 던지지 않고 호출자가 명시적으로 처리하게 합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 null이 아닌 값의 형식입니다.</typeparam>
public sealed class Result<T>
    // where T : notnull은 성공 값의 generic 형식으로 null 허용 형식을 받지 않겠다는 제약입니다.
    where T : notnull
{
    // T?의 ?는 실패 Result에서는 성공 값이 없을 수 있음을 나타냅니다.
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// 성공과 실패가 동시에 존재하지 않도록 내부 상태를 한곳에서 만듭니다.
    /// </summary>
    /// <param name="isSuccess">true이면 value만, false이면 error만 유효하다는 표시입니다.</param>
    /// <param name="value">성공 시 반환할 값이며 실패 시 null입니다.</param>
    /// <param name="error">실패 시 반환할 오류이며 성공 시 null입니다.</param>
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// 이 결과가 성공 값을 갖는지 알려 줍니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 성공 값에 접근합니다. 실패인데 값을 읽는 것은 프로그래머 실수이므로 예외로 즉시 드러냅니다.
    /// </summary>
    // =>는 멤버 본문이 식 하나일 때 쓰는 식 본문 문법이고, ?:는 조건에 따라 두 값 중 하나를 고릅니다.
    // _value!의 !는 runtime null 검사가 아니라, IsSuccess 불변식을 근거로 nullable 경고만 억제합니다.
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("실패한 Result에서는 Value를 읽을 수 없습니다.");

    /// <summary>
    /// 실패 오류에 접근합니다. 성공인데 오류를 읽는 것은 프로그래머 실수이므로 예외로 즉시 드러냅니다.
    /// </summary>
    // _error! 역시 생성자가 보장하는 성공/실패 불변식을 compiler에 설명하는 null-forgiving 연산자입니다.
    public Error Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("성공한 Result에서는 Error를 읽을 수 없습니다.");

    /// <summary>
    /// null이 아닌 성공 값을 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>IsSuccess가 true인 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 분기하고 사용자에게 설명할 오류입니다.</param>
    /// <returns>IsSuccess가 false인 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
