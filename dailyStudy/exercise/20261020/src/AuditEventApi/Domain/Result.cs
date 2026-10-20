namespace AuditEventApi.Domain;

/// <summary>
/// 정상 흐름에서 생길 수 있는 성공 값 또는 업무 오류 중 정확히 하나를 운반합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 호출자에게 전달할 값의 형식입니다.</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// 성공 값과 오류를 한곳에 보관하되, 모순된 상태를 만들지 못하도록 생성 경로를 내부로 제한합니다.
    /// </summary>
    /// <param name="value">성공 결과이며 실패일 때는 형식의 기본값입니다.</param>
    /// <param name="error">예상 가능한 실패 정보이며 성공일 때는 null입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 새 결과 객체를 초기화합니다.</returns>
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 이 결과가 오류 없이 성공 값을 담고 있는지 알려 줍니다.
    /// </summary>
    /// <returns>오류가 없으면 true, 오류가 있으면 false입니다.</returns>
    // =>는 중괄호와 return 대신 식 하나의 결과를 바로 돌려주는 식 본문 문법입니다.
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공 값을 가져옵니다. 먼저 IsSuccess가 true인지 확인한 뒤 사용해야 합니다.
    /// </summary>
    /// <returns>성공 값이며 실패 결과에서는 기본값일 수 있습니다.</returns>
    // ?는 실패 결과에서는 값이 없을 수 있음을 compiler와 독자에게 알리는 nullable 표기입니다.
    public T? Value { get; }

    /// <summary>
    /// 실패 원인을 가져옵니다. 성공 결과에서는 null입니다.
    /// </summary>
    /// <returns>예상 실패 정보 또는 성공을 뜻하는 null입니다.</returns>
    public Error? Error { get; }

    /// <summary>
    /// 유효한 값을 성공 결과로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>값만 담고 오류는 없는 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        // 성공인데 값이 null인 모순 상태를 생성 경계에서 막아 호출자가 Value!에 의존하는 범위를 줄입니다.
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 결과로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 읽고 처리할 업무 오류입니다.</param>
    /// <returns>오류만 담고 성공 값은 없는 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        // 실패인데 Error가 null이면 IsSuccess가 잘못 true가 되므로 public factory에서 즉시 거절합니다.
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}
