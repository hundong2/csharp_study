using System.Diagnostics.CodeAnalysis;

namespace GlobalMeetingApi.Domain;

/// <summary>
/// 성공 값과 사용자가 고칠 수 있는 예상 실패 중 정확히 하나만 담는 결과 형식입니다.
/// 호출자에게 성공/실패 분기를 강제하되, 코드 불변식 위반·예상 밖 장애·취소는 예외로 유지해 원인을 숨기지 않습니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 reference type입니다.</typeparam>
public sealed class Result<T>
    // where는 T를 class로 제한합니다. 이 예제는 null을 '성공 값 없음' 표시에 안전하게 사용합니다.
    where T : class
{
    /// <summary>
    /// 성공 값 또는 오류를 받아 둘 중 정확히 하나만 존재한다는 불변식을 지킵니다.
    /// </summary>
    /// <param name="value">성공이면 값, 실패이면 null입니다.</param>
    /// <param name="error">실패이면 오류, 성공이면 null입니다.</param>
    /// <exception cref="InvalidOperationException">값과 오류가 함께 있거나 함께 없을 때 발생합니다.</exception>
    private Result(T? value, Error? error)
    {
        if ((value is null) == (error is null))
        {
            throw new InvalidOperationException("Result에는 값과 오류 중 정확히 하나만 있어야 합니다.");
        }

        Value = value;
        Error = error;
    }

    /// <summary>
    /// 오류가 없어 성공한 결과인지 알려 줍니다.
    /// </summary>
    // =>는 하나의 식으로 값을 계산하는 식 본문 문법입니다.
    // MemberNotNullWhen은 성공일 때 Value, 실패일 때 Error가 null이 아니라고 nullable 분석기에 알려 줍니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공 결과에 담긴 값이며 실패 결과에서는 null입니다.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 실패 결과에 담긴 오류이며 성공 결과에서는 null입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// null이 아닌 값을 담은 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>값만 담긴 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>
    /// 예상된 오류를 담은 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 처리할 안전한 오류입니다.</param>
    /// <returns>오류만 담긴 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(null, error);
    }
}
