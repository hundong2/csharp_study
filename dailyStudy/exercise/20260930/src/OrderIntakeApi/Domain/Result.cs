namespace OrderIntakeApi.Domain;

/// <summary>
/// 정상 값 또는 예상 가능한 오류 중 정확히 하나만 운반하는 결과 형식입니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 참조 형식입니다.</typeparam>
// T는 아직 정하지 않은 형식을 뜻하는 generic 매개변수입니다. where T : class는 null과 실패를 구분하기 쉽게 참조 형식만 허용합니다.
public sealed class Result<T>
    where T : class
{
    /// <summary>
    /// 성공 값과 오류를 한곳에서 보관하되 외부에서는 정적 factory만 사용하게 합니다.
    /// </summary>
    /// <param name="value">성공이면 실제 값, 실패이면 null입니다.</param>
    /// <param name="error">실패이면 안전한 오류, 성공이면 null입니다.</param>
    /// <returns>생성자는 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 성공했을 때의 값을 가져옵니다. 실패 결과에서는 null입니다.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 실패했을 때의 오류를 가져옵니다. 성공 결과에서는 null입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Error가 없으면 성공이라고 판정합니다.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// null이 아닌 값을 성공 결과로 감쌉니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 정상 값입니다.</param>
    /// <returns>값이 든 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, error: null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 결과로 감쌉니다.
    /// </summary>
    /// <param name="error">공개 가능한 코드와 설명입니다.</param>
    /// <returns>오류가 든 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(value: null, error);
    }
}
