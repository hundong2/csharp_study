namespace TelemetryStreamExercise.Domain;

/// <summary>
/// 성공 값과 예상 가능한 오류 중 정확히 하나만 보관하는 Result 패턴입니다.
/// 입력 실패를 예외로 숨기지 않으므로 호출자는 성공과 실패를 compiler가 보이는 분기로 처리합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 null이 아닌 값의 형식입니다.</typeparam>
public sealed class Result<T>
    // where T : notnull은 성공 값으로 null 허용 형식을 받지 않겠다는 generic 제약입니다.
    where T : notnull
{
    // T?의 ?는 실패 Result에서는 성공 값이 존재하지 않을 수 있음을 nullable 형식으로 표현합니다.
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// 성공과 실패가 동시에 생기지 않도록 Result의 내부 상태를 한곳에서 조립합니다.
    /// </summary>
    /// <param name="isSuccess">true이면 value만, false이면 error만 유효하다는 표시입니다.</param>
    /// <param name="value">성공 시 보관할 값이며 실패일 때는 null입니다.</param>
    /// <param name="error">실패 시 보관할 오류이며 성공일 때는 null입니다.</param>
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// 이 결과가 성공 값을 가지고 있는지 알려 줍니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 성공 값을 꺼냅니다. 실패 값을 성공처럼 읽는 것은 programmer 오류이므로 즉시 예외로 드러냅니다.
    /// </summary>
    // =>는 본문이 식 하나인 멤버를 간결하게 쓰는 식 본문 문법입니다.
    // _value!의 !는 runtime 검사가 아니라 생성자 불변식을 compiler에 알려 nullable 경고를 없앱니다.
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("실패한 Result에서는 Value를 읽을 수 없습니다.");

    /// <summary>
    /// 실패 오류를 꺼냅니다. 성공 값을 실패처럼 읽는 programmer 오류도 즉시 드러냅니다.
    /// </summary>
    public Error Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("성공한 Result에서는 Error를 읽을 수 없습니다.");

    /// <summary>
    /// null이 아닌 값을 성공 Result로 감쌉니다.
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
    /// <param name="error">호출자가 종류와 코드로 처리할 오류입니다.</param>
    /// <returns>IsSuccess가 false인 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
