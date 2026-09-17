namespace ReadingListApi.Application;

// 오류 종류를 분리하면 HTTP 계층이 비즈니스 규칙을 몰라도 적절한 상태 코드를 고를 수 있습니다.
public enum ServiceErrorKind
{
    Validation,
    NotFound
}

/// <summary>
/// 성공 값 또는 예상 가능한 실패를 담습니다. 오류를 일상적인 흐름으로 표현해 예외를 남용하지 않습니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다.</typeparam>
// <T>는 성공 값의 형식을 호출하는 쪽이 정한다는 뜻입니다. 책과 책 목록에 같은 결과 규칙을 재사용합니다.
public sealed class ServiceResult<T>
{
    // T?의 ?는 값이 없을 수 있음을 뜻합니다. 성공일 때만 Value를 사용하도록 호출자가 IsSuccess를 먼저 확인합니다.
    public T? Value { get; }
    public ServiceErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    // =>는 짧은 식 하나로 값을 계산하는 문법이고 is null은 값이 없는지 검사합니다. 오류 종류가 없으면 성공입니다.
    public bool IsSuccess => ErrorKind is null;

    /// <summary>
    /// 서비스 결과를 만듭니다. value는 성공 값, errorKind는 오류 종류, errorMessage는 설명이며 생성자에는 반환값이 없습니다.
    /// </summary>
    private ServiceResult(T? value, ServiceErrorKind? errorKind, string? errorMessage)
    {
        Value = value;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 성공 값을 결과 상자에 담습니다. value는 반환할 값이며 성공 결과를 돌려줍니다.
    /// </summary>
    // new(...)는 앞의 ServiceResult<T> 형식을 컴파일러가 알고 있을 때 형식 이름을 생략하는 문법입니다.
    public static ServiceResult<T> Success(T value) => new(value, null, null);

    /// <summary>
    /// 예상 가능한 실패를 결과 상자에 담습니다. kind는 오류 종류, message는 설명이며 실패 결과를 돌려줍니다.
    /// </summary>
    public static ServiceResult<T> Failure(ServiceErrorKind kind, string message) => new(default, kind, message);
}
