namespace WebhookReceiverApi.Domain;

/// <summary>
/// 성공 값 또는 호출자가 처리할 수 있는 예상 오류 중 정확히 하나만 운반합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다.</typeparam>
// T는 호출 위치에서 실제 형식으로 채워지는 제네릭 자리이고, record는 구성 값이 같은 결과를 값으로 비교하게 합니다.
public sealed record Result<T>
{
    /// <summary>
    /// 성공 값과 오류가 동시에 들어가는 잘못된 상태를 외부 코드가 만들지 못하게 내부 값을 초기화합니다.
    /// </summary>
    /// <param name="isSuccess">성공 값이 들어 있는지를 나타냅니다.</param>
    /// <param name="value">성공 결과이며 실패이면 T의 기본값입니다.</param>
    /// <param name="error">예상 오류이며 성공이면 null입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 성공 값이 들어 있으면 true, 예상 오류가 들어 있으면 false입니다.
    /// </summary>
    public bool IsSuccess { get; }

    // 제네릭 T 뒤의 ?는 실패 결과에서는 성공 값이 없을 수 있음을 표현합니다.
    public T? Value { get; }

    /// <summary>
    /// 실패 이유이며 성공 결과에서는 null입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// 성공 값을 담고 오류는 비워 둔 결과를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 null이 아닌 성공 값입니다.</param>
    /// <returns>성공 상태와 전달받은 값이 든 결과를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 오류를 담고 성공 값은 비워 둔 결과를 만듭니다.
    /// </summary>
    /// <param name="error">HTTP 오류 등으로 변환할 null이 아닌 오류입니다.</param>
    /// <returns>실패 상태와 전달받은 오류가 든 결과를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        // default는 참조 형식이면 null입니다. 실패 때는 Value 대신 Error를 읽습니다.
        return new Result<T>(false, default, error);
    }
}
