namespace RequestAdmissionApi.Application;

/// <summary>
/// 애플리케이션 작업이 실패했을 때 코드, 설명, 세부 오류를 함께 전달하는 불변 값입니다.
/// 세부 오류를 생성 시 복사하므로 호출자가 원본 목록을 바꿔도 이 객체는 변하지 않습니다.
/// </summary>
public sealed record ApplicationError
{
    /// <summary>
    /// 오류 코드, 대표 설명, 세부 오류의 방어적 복사본을 저장합니다.
    /// <paramref name="code"/>는 프로그램용 코드, <paramref name="message"/>는 사람용 설명,
    /// <paramref name="details"/>는 필드별 오류들이며 새 불변 오류 값을 초기화합니다.
    /// </summary>
    public ApplicationError(string code, string message, IEnumerable<string> details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(details);

        Code = code;
        Message = message;
        Details = Array.AsReadOnly(details.ToArray());
    }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyList<string> Details { get; }
}

/// <summary>
/// 예상 가능한 성공과 실패를 호출자에게 명시적으로 돌려주는 Result 타입입니다.
/// 예외는 연결 장애 같은 예상 밖 실패에 남겨 두고, 검증 실패는 이 타입으로 처리합니다.
/// </summary>
/// <typeparam name="T">성공할 때 운반할 null이 아닌 값의 타입입니다.</typeparam>
// where T : notnull은 성공 값에 null을 허용하지 않아 성공/실패 상태가 모호해지지 않게 하는 generic 제약입니다.
public sealed class Result<T>
    where T : notnull
{
    private readonly T? value;

    /// <summary>
    /// 성공 값 또는 실패 정보를 가진 결과를 만듭니다.
    /// <paramref name="isSuccess"/>는 상태, <paramref name="value"/>는 성공 값,
    /// <paramref name="error"/>는 실패 정보이며 새 결과를 반환하지 않고 현재 인스턴스를 초기화합니다.
    /// </summary>
    private Result(bool isSuccess, T? value, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        this.value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    // 느낌표는 이 성공 분기에서는 팩터리가 반드시 값을 넣었다는 사실을 Nullable 분석기에 알려 줍니다.
    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("실패한 Result에는 성공 값이 없습니다.");

    public ApplicationError? Error { get; }

    /// <summary>
    /// 완료된 값을 성공 결과로 만듭니다.
    /// <paramref name="value"/>는 호출자에게 돌려줄 값이며 오류가 없는 성공 결과를 반환합니다.
    /// </summary>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 예상 가능한 오류를 실패 결과로 만듭니다.
    /// <paramref name="error"/>는 실패 원인이며 성공 값이 없는 실패 결과를 반환합니다.
    /// </summary>
    public static Result<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
