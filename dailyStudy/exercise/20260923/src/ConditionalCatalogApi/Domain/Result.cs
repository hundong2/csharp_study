namespace ConditionalCatalogApi.Domain;

/// <summary>
/// 사용자가 고칠 수 있는 예상 실패를 예외 대신 코드와 메시지로 표현합니다.
/// record는 데이터 중심 타입의 값 비교와 읽기 전용 사용을 간결하게 만들기 때문에 오류 값에 사용합니다.
/// 괄호 안의 두 값은 primary constructor 파라미터이며, 생성자는 값을 반환하지 않고 새 오류 객체를 초기화합니다.
/// </summary>
/// <param name="Code">프로그램과 테스트가 분기할 때 사용하는 안정적인 오류 코드입니다.</param>
/// <param name="Message">사람이 입력을 고칠 수 있도록 보여 줄 설명입니다.</param>
public sealed record DomainError(string Code, string Message);

/// <summary>
/// 성공 값 또는 예상 가능한 Domain 오류 중 하나만 담는 작은 Result 타입입니다.
/// 예외는 프로그래밍 오류나 복구할 수 없는 시스템 실패에 남겨 두고, 입력 검증 실패는 이 타입으로 명시합니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 타입입니다.</typeparam>
public sealed class Result<T>
{
    // T?의 물음표는 성공하지 않은 Result에는 값이 없을 수 있음을 compiler에 알립니다.
    private readonly T? _value;

    /// <summary>
    /// 성공 여부, 성공 값, 실패 오류를 한 번에 보관하는 Result를 만듭니다.
    /// </summary>
    /// <param name="isSuccess">성공 Result인지 나타냅니다.</param>
    /// <param name="value">성공할 때 보관할 값이며 실패할 때는 null입니다.</param>
    /// <param name="error">실패할 때 보관할 오류이며 성공할 때는 null입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 새 Result 인스턴스를 초기화합니다.</returns>
    private Result(bool isSuccess, T? value, DomainError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>
    /// 값이 들어 있는 성공 결과인지 알려 줍니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 실패 이유를 반환하며 성공 결과라면 null입니다.
    /// </summary>
    public DomainError? Error { get; }

    /// <summary>
    /// 성공 값을 반환합니다. 실패 결과에서 접근하는 것은 호출 코드의 버그이므로 예외를 던집니다.
    /// </summary>
    // =>는 중괄호와 return 대신 식 하나로 값을 계산하는 식 본문 문법입니다. 짧은 읽기 전용 속성이라 흐름을 한눈에 보이게 합니다.
    // ?:는 조건에 따라 두 식 중 하나를 고르는 조건 연산자이며, 성공일 때만 값을 내주고 실패 접근은 예외로 드러냅니다.
    // !는 nullable 분석에 “성공일 때 _value가 null이 아님을 이 타입의 생성 규칙이 보장한다”고 알려 주는 null-forgiving 연산자입니다.
    // IsSuccess 조건이 실제 runtime 안전성을 지키고, !는 compiler가 그 불변식을 알 수 없는 간격만 메웁니다.
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("실패한 Result에는 성공 값이 없습니다.");

    /// <summary>
    /// 주어진 값을 담은 성공 Result를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공 값입니다.</param>
    /// <returns>오류가 없는 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, error: null);
    }

    /// <summary>
    /// 주어진 오류를 담은 실패 Result를 만듭니다.
    /// </summary>
    /// <param name="error">사용자가 이해하고 고칠 수 있는 Domain 오류입니다.</param>
    /// <returns>성공 값이 없는 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, value: default, error);
    }
}
