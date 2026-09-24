namespace AnnouncementCacheApi.Domain;

/// <summary>
/// 사용자가 고칠 수 있는 예상 실패를 예외 대신 코드와 메시지로 표현하는 불변 값입니다.
/// </summary>
/// <param name="Code">HTTP 상태와 독립적으로 실패 종류를 식별하는 안정적인 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 바로 이해할 수 있는 실패 설명입니다.</param>
// record는 값이 같으면 같은 것으로 비교되는 불변 데이터에 알맞아 오류 전달 객체에 사용합니다.
public sealed record DomainError(string Code, string Message);

/// <summary>
/// 성공값 또는 예상 가능한 DomainError 중 정확히 하나를 운반합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 담을 값의 형식입니다.</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// 성공이면 true이고 실패이면 false입니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 성공값이며 실패일 때는 값이 없음을 null로 나타냅니다.
    /// </summary>
    // ?는 이 참조가 null일 수 있음을 컴파일러와 독자에게 알려 Nullable 안전성 검사를 켭니다.
    public T? Value { get; }

    /// <summary>
    /// 실패 정보이며 성공일 때는 오류가 없으므로 null입니다.
    /// </summary>
    public DomainError? Error { get; }

    /// <summary>
    /// 성공 여부와 서로 배타적인 값/오류를 받아 Result 내부 상태를 만듭니다.
    /// </summary>
    /// <param name="isSuccess">성공 Result인지 나타냅니다.</param>
    /// <param name="value">성공일 때 보관할 값입니다.</param>
    /// <param name="error">실패일 때 보관할 오류입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고, 외부 factory가 보장한 성공·실패 조합을 보관합니다.</returns>
    private Result(bool isSuccess, T? value, DomainError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 검증되거나 처리된 값을 성공 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공값입니다.</param>
    /// <returns>값은 있고 오류는 없는 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자가 분기 처리할 오류 코드와 메시지입니다.</param>
    /// <returns>오류는 있고 성공값은 없는 Result를 반환합니다.</returns>
    public static Result<T> Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
