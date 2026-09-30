namespace WorkshopContractApi.Domain;

/// <summary>
/// 정상 값 또는 예상 가능한 오류 중 정확히 하나만 운반해, 업무 실패를 예외와 구분합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 호출자에게 전달할 값의 형식입니다.</typeparam>
// <T>는 호출할 때 WorkshopRegistration 같은 실제 형식으로 채우는 generic 문법입니다.
public sealed class Result<T>
{
    /// <summary>
    /// 외부에서 잘못된 성공/실패 조합을 만들지 못하도록 생성자를 숨깁니다.
    /// </summary>
    /// <param name="value">성공 값이며 실패일 때는 기본값입니다.</param>
    /// <param name="error">실패 설명이며 성공일 때는 null입니다.</param>
    /// <param name="isSuccess">현재 결과가 성공인지 나타냅니다.</param>
    private Result(T? value, Error? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// 현재 결과가 정상 값인지 알려 줍니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 성공 값이며, 실패 결과에서는 null일 수 있습니다.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 실패 정보이며, 성공 결과에서는 null입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// 주어진 값을 담은 성공 Result를 만듭니다.
    /// </summary>
    /// <param name="value">null이 아니어야 하는 정상 값입니다.</param>
    /// <returns>호출자가 IsSuccess와 Value로 읽을 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, error: null, isSuccess: true);
    }

    /// <summary>
    /// 주어진 오류를 담은 실패 Result를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 처리할 예상 가능한 오류입니다.</param>
    /// <returns>호출자가 IsSuccess와 Error로 읽을 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(value: default, error, isSuccess: false);
    }
}
