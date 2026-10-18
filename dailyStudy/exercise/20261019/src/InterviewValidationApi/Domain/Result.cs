namespace InterviewValidationApi.Domain;

/// <summary>
/// 성공 값 또는 예상 가능한 오류 중 정확히 하나를 운반합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다.</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// 성공 값과 오류를 함께 받되 외부에서는 정적 팩터리만 사용하게 합니다.
    /// </summary>
    /// <param name="value">성공 결과이며 실패일 때는 기본값입니다.</param>
    /// <param name="error">실패 정보이며 성공일 때는 null입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 새 Result 인스턴스를 초기화합니다.</returns>
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 이 결과가 성공 값을 담고 있는지 알려 줍니다.
    /// </summary>
    /// <returns>오류가 없으면 true, 오류가 있으면 false를 반환합니다.</returns>
    // =>는 식 하나의 결과를 바로 반환하는 식 본문 문법입니다.
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공 값을 제공합니다. 먼저 IsSuccess를 확인한 뒤 사용해야 합니다.
    /// </summary>
    /// <returns>성공 값이며 실패 결과에서는 기본값일 수 있습니다.</returns>
    // ?는 실패 결과에서 값이 없을 수 있음을 compiler와 독자에게 알리는 nullable 표기입니다.
    public T? Value { get; }

    /// <summary>
    /// 실패 정보를 제공합니다. 성공 결과에서는 null입니다.
    /// </summary>
    /// <returns>예상 실패 정보 또는 성공을 뜻하는 null을 반환합니다.</returns>
    public Error? Error { get; }

    /// <summary>
    /// 주어진 값을 성공 Result로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>값을 담은 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>
    /// 주어진 오류를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="error">호출자에게 설명할 예상 실패입니다.</param>
    /// <returns>오류를 담은 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error) => new(default, error);
}
