using System.Diagnostics.CodeAnalysis;

namespace EquipmentRentalApi.Domain;

/// <summary>
/// 정상 값과 예상된 실패 중 정확히 하나만 담는 간단한 Result 형식입니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다.</typeparam>
public sealed class Result<T>
    // where T : class는 성공 값 형식을 reference type으로 제한해 null을 '값 없음' 표시에 일관되게 사용합니다.
    where T : class
{
    /// <summary>
    /// 성공 값 또는 실패 정보를 받아 둘 중 하나만 존재한다는 불변식을 검사합니다.
    /// </summary>
    /// <param name="value">성공일 때 담을 값이며 실패일 때는 null입니다.</param>
    /// <param name="error">실패일 때 담을 오류이며 성공일 때는 null입니다.</param>
    /// <exception cref="InvalidOperationException">값과 오류가 함께 있거나 함께 없으면 발생합니다.</exception>
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
    /// 오류가 없어서 결과가 성공인지 알려 줍니다.
    /// </summary>
    // =>는 계산 결과를 바로 돌려주는 식 본문 속성입니다. 저장된 Error 유무에서 매번 성공 여부를 계산합니다.
    // MemberNotNullWhen은 true일 때 Value, false일 때 Error가 null이 아니라고 nullable 분석기에 알려 줍니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공 결과의 값이며 실패 결과에서는 null입니다.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 실패 결과의 설명이며 성공 결과에서는 null입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// 주어진 값을 담은 성공 Result를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 null이 아닌 성공 값입니다.</param>
    /// <returns>값만 담긴 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>
    /// 주어진 오류를 담은 실패 Result를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 처리할 null이 아닌 예상 오류입니다.</param>
    /// <returns>오류만 담긴 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}
