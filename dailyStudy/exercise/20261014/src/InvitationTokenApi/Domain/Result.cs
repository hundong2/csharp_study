namespace InvitationTokenApi.Domain;

/// <summary>
/// 성공 값 또는 예상 오류 중 정확히 하나만 운반합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다.</typeparam>
public sealed record Result<T>
{
    /// <summary>
    /// 외부에서 성공과 실패가 동시에 들어간 잘못된 Result를 만들지 못하게 내부 값을 초기화합니다.
    /// </summary>
    /// <param name="isSuccess">성공 값이 들어 있는지 나타냅니다.</param>
    /// <param name="value">성공 결과입니다. 실패이면 default(T)이며, 참조 형식에서는 null이고 값 형식에서는 0에 해당하는 기본값입니다.</param>
    /// <param name="error">예상 오류이며 성공일 때는 null입니다.</param>
    /// <returns>생성자는 새 Result를 초기화하므로 별도 반환값은 없습니다.</returns>
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    // unconstrained T의 T?는 nullable annotation입니다. 값 형식을 Nullable<T>로 바꾸지 않으므로 호출자는 Value보다 IsSuccess를 먼저 확인해야 합니다.
    public T? Value { get; }

    public Error? Error { get; }

    /// <summary>
    /// 성공 값을 담은 Result를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>오류가 없고 성공 값만 든 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 오류를 담은 Result를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 상태 코드나 안내 문구로 바꿀 오류입니다.</param>
    /// <returns>성공 값이 없고 오류만 든 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        // default는 T가 참조 형식이면 null, 값 형식이면 그 형식의 기본값입니다. 실패 값은 읽지 않고 Error를 사용해야 합니다.
        return new Result<T>(false, default, error);
    }
}
