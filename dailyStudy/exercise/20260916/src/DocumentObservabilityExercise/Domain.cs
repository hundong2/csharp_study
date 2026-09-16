namespace DocumentObservabilityExercise;

/// <summary>
/// 호출자가 고칠 수 있는 실패를 예외 대신 값으로 전달할 때 사용하는 오류 정보입니다.
/// Code는 프로그램이 분기하고 관측 태그로도 사용할 제한된 값이고, Message는 사람이 읽는 설명입니다.
/// </summary>
public sealed record OperationError
{
    /// <summary>
    /// 검증된 오류 코드와 설명을 한 쌍으로 보관합니다.
    /// code는 낮은 카디널리티 분류, message는 사용자·개발자용 설명이며 새 오류 값을 만듭니다.
    /// 생성자를 internal로 두어 같은 어셈블리의 Result factory만 오류를 만들게 합니다.
    /// </summary>
    /// <param name="code">소문자 ASCII, 숫자, 점, 하이픈, 밑줄로만 구성된 80자 이하 코드입니다.</param>
    /// <param name="message">호출자가 실패 이유를 이해할 수 있는 비어 있지 않은 설명입니다.</param>
    internal OperationError(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (code.Length > 80 || !code.All(IsAllowedCodeCharacter))
        {
            throw new ArgumentException(
                "오류 코드는 80자 이하의 소문자 ASCII·숫자·점·하이픈·밑줄만 사용할 수 있습니다.",
                nameof(code));
        }

        if (!IsKnownCode(code))
        {
            throw new ArgumentOutOfRangeException(
                nameof(code),
                "오류 코드는 애플리케이션의 고정 허용 목록에 있어야 합니다.");
        }

        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }

    /// <summary>
    /// 오류 코드 한 문자가 허용 목록에 속하는지 검사합니다.
    /// character는 검사할 문자이며, 소문자 ASCII·숫자·점·하이픈·밑줄이면 true를 반환합니다.
    /// </summary>
    /// <param name="character">오류 코드에서 검사할 한 문자입니다.</param>
    /// <returns>관측 태그와 프로그램 분기에 안전한 제한 문자이면 true입니다.</returns>
    private static bool IsAllowedCodeCharacter(char character)
    {
        // `is` 뒤의 관계 패턴은 문자의 코드 범위를 비교하고, `or`는 허용 범위를 하나로 묶습니다.
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_';
    }

    /// <summary>
    /// 오류 코드가 요청마다 달라지지 않는 애플리케이션 고정 vocabulary에 속하는지 검사합니다.
    /// code는 검사할 분류 문자열이며, 미리 정의한 요청·변환 오류 중 하나이면 true를 반환합니다.
    /// </summary>
    /// <param name="code">구문 검증을 먼저 통과한 오류 코드입니다.</param>
    /// <returns>집계 가능한 고정 오류 코드이면 true입니다.</returns>
    private static bool IsKnownCode(string code)
    {
        return code is "request.job_id_required"
            or "request.job_id_invalid"
            or "request.source_required"
            or "request.source_too_large"
            or "request.target_required"
            or "request.target_unsupported"
            or "conversion.syntax_unsupported";
    }
}

/// <summary>
/// 성공 값 또는 예상 가능한 실패 중 정확히 하나만 담는 간단한 Result 형식입니다.
/// 예외는 네트워크 단절이나 코드 계약 위반처럼 이 흐름에서 정상 처리할 수 없는 경우에 남겨 둡니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 형식입니다. null이 아닌 값만 허용합니다.</typeparam>
public sealed class OperationResult<T>
    where T : notnull
{
    private readonly T? _value;
    private readonly OperationError? _error;

    /// <summary>
    /// null이 아닌 성공 값만 받아 성공 Result를 만듭니다.
    /// value는 성공 값이며, 오류가 함께 들어올 수 없는 별도 생성자로 불변식을 구조적으로 지킵니다.
    /// public 호출자가 생성자를 직접 사용하지 못하게 internal로 제한하고 공개 factory를 사용하게 합니다.
    /// </summary>
    /// <param name="value">성공했을 때 보존할 null이 아닌 값입니다.</param>
    internal OperationResult(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        IsSuccess = true;
        _value = value;
        _error = null;
    }

    /// <summary>
    /// 검증된 오류만 받아 실패 Result를 만듭니다.
    /// error는 예상 가능한 실패 이유이며, 성공 값이 함께 들어올 수 없는 별도 생성자로 불변식을 지킵니다.
    /// </summary>
    /// <param name="error">호출자가 이해하고 대응할 수 있는 실패 정보입니다.</param>
    internal OperationResult(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }

    /// <summary>
    /// 성공 Result에 담긴 값을 돌려줍니다.
    /// 파라미터는 없고 T 값을 반환하며, 실패 Result에서 읽으면 호출 순서 오류를 즉시 알려 줍니다.
    /// </summary>
    public T Value
    {
        get
        {
            // 값 형식 T의 default는 null이 아닐 수 있으므로 성공 여부를 먼저 검사해야 합니다.
            // 그렇지 않으면 실패 OperationResult<int>에서 0을 성공 값처럼 읽는 버그가 생길 수 있습니다.
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패 Result에는 성공 값이 없습니다.");
            }

            return _value ?? throw new InvalidOperationException("성공 Result의 값이 비어 있습니다.");
        }
    }

    /// <summary>
    /// 실패 Result에 담긴 오류를 돌려줍니다.
    /// 파라미터는 없고 OperationError를 반환하며, 성공 Result에서 읽으면 호출 순서 오류를 즉시 알려 줍니다.
    /// </summary>
    public OperationError Error => _error ?? throw new InvalidOperationException("성공 Result에는 오류가 없습니다.");

}

/// <summary>
/// 제네릭 Result의 성공·실패 생성을 한곳에 모으는 non-generic factory입니다.
/// `OperationResult&lt;T&gt;`마다 정적 멤버가 생기는 모양을 피하면서 형식 추론은 유지합니다.
/// </summary>
public static class OperationResult
{
    /// <summary>
    /// null이 아닌 성공 값을 Result로 감쌉니다.
    /// value는 호출자에게 돌려줄 결과이고, 성공 상태의 OperationResult를 반환합니다.
    /// </summary>
    /// <typeparam name="T">성공 값의 null이 아닌 형식입니다.</typeparam>
    /// <param name="value">성공했을 때 보존할 값입니다.</param>
    /// <returns>성공 값이 들어 있는 Result입니다.</returns>
    public static OperationResult<T> Success<T>(T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OperationResult<T>(value);
    }

    /// <summary>
    /// 호출자가 이해하고 대응할 수 있는 오류를 실패 Result로 만듭니다.
    /// code는 프로그램용 분류, message는 사용자·개발자용 설명이며, 실패 상태의 OperationResult를 반환합니다.
    /// </summary>
    /// <typeparam name="T">이 작업이 성공했을 때였으면 반환했을 값의 형식입니다.</typeparam>
    /// <param name="code">분기와 집계에 사용할 안정된 오류 코드입니다.</param>
    /// <param name="message">실패 이유를 설명하는 안전한 문장입니다.</param>
    /// <returns>오류가 들어 있는 실패 Result입니다.</returns>
    public static OperationResult<T> Failure<T>(string code, string message)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new OperationResult<T>(new OperationError(code, message));
    }
}

/// <summary>
/// 문서 변환 한 건의 검증된 입력입니다.
/// SourceText는 고객 내용일 수 있으므로 로그·trace·metric에 직접 넣지 않는 것이 핵심 계약입니다.
/// </summary>
public sealed record ConversionRequest
{
    /// <summary>
    /// 검증을 통과한 값만 저장해 이후 계층이 같은 null·길이 검사를 반복하지 않게 합니다.
    /// 세 파라미터는 각각 작업 식별자, 원문, 출력 형식이며, 새 요청 객체를 만듭니다.
    /// </summary>
    /// <param name="jobId">구문 검증을 마친 불투명 작업 식별자입니다. 별도의 개인정보 정책도 지켜야 합니다.</param>
    /// <param name="sourceText">변환할 실제 문서 내용이며 관측 데이터에 넣으면 안 됩니다.</param>
    /// <param name="targetFormat">plain 또는 upper 중 하나인 낮은 카디널리티 값입니다.</param>
    private ConversionRequest(string jobId, string sourceText, string targetFormat)
    {
        JobId = jobId;
        SourceText = sourceText;
        TargetFormat = targetFormat;
    }

    public string JobId { get; }

    public string SourceText { get; }

    public string TargetFormat { get; }

    /// <summary>
    /// nullable 외부 입력을 검사하고 안전한 ConversionRequest로 바꿉니다.
    /// jobId는 작업 키, sourceText는 민감할 수 있는 원문, targetFormat은 출력 종류이며,
    /// 검증 성공 시 요청을, 실패 시 호출자가 고칠 수 있는 오류 Result를 반환합니다.
    /// </summary>
    /// <param name="jobId">null일 수도 있는 외부 작업 식별자입니다.</param>
    /// <param name="sourceText">null일 수도 있는 외부 문서 내용입니다.</param>
    /// <param name="targetFormat">null일 수도 있는 외부 출력 형식입니다.</param>
    /// <returns>검증된 요청 또는 구체적인 입력 오류입니다.</returns>
    public static OperationResult<ConversionRequest> Create(
        string? jobId,
        string? sourceText,
        string? targetFormat)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.job_id_required",
                "작업 ID를 입력하세요.");
        }

        var normalizedJobId = jobId.Trim();

        // All은 모든 문자가 조건을 만족하는지 검사하는 LINQ 메서드입니다.
        // 작업 ID 구문을 제한하면 로그 줄바꿈 주입을 막을 수 있지만, 개인정보가 아님을 보장하지는 않습니다.
        if (normalizedJobId.Length > 40 ||
            !normalizedJobId.All(IsAllowedJobIdCharacter))
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.job_id_invalid",
                "작업 ID는 40자 이하의 영문·숫자·하이픈·밑줄만 사용할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.source_required",
                "변환할 문서 내용을 입력하세요.");
        }

        if (sourceText.Length > 2_000)
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.source_too_large",
                "학습 예제에서는 문서 내용을 2,000자 이하로 제한합니다.");
        }

        if (string.IsNullOrWhiteSpace(targetFormat))
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.target_required",
                "출력 형식을 입력하세요.");
        }

        var normalizedTarget = targetFormat.Trim().ToLowerInvariant();

        // `is not ("plain" or "upper")`는 두 허용값 어디에도 맞지 않을 때 true가 되는 패턴 문법입니다.
        // 가능한 metric 태그 값을 작은 집합으로 제한해 카디널리티 폭증도 함께 예방합니다.
        if (normalizedTarget is not ("plain" or "upper"))
        {
            return OperationResult.Failure<ConversionRequest>(
                "request.target_unsupported",
                "출력 형식은 plain 또는 upper만 지원합니다.");
        }

        return OperationResult.Success(
            new ConversionRequest(normalizedJobId, sourceText, normalizedTarget));
    }

    /// <summary>
    /// 작업 ID 한 문자가 명시한 ASCII 허용 목록에 속하는지 검사합니다.
    /// character는 검사할 문자이며, 영문 ASCII·숫자·하이픈·밑줄이면 true를 반환합니다.
    /// </summary>
    /// <param name="character">작업 ID에서 검사할 한 문자입니다.</param>
    /// <returns>허용된 ASCII 문자이면 true, 유니코드 문자나 제어 문자이면 false입니다.</returns>
    internal static bool IsAllowedJobIdCharacter(char character)
    {
        // 관계 패턴으로 ASCII 범위를 직접 적어 `char.IsLetterOrDigit`가 한글 등도 허용하는 일을 막습니다.
        return character is (>= 'a' and <= 'z')
            or (>= 'A' and <= 'Z')
            or (>= '0' and <= '9')
            or '-' or '_';
    }

    /// <summary>
    /// 디버거 또는 문자열 보간에서 요청을 표시할 때 원문 대신 제한된 메타데이터만 반환합니다.
    /// 파라미터는 없고 본문 없는 요약 문자열을 반환하며, 실수로 SourceText가 로그에 노출될 가능성을 낮춥니다.
    /// </summary>
    /// <returns>작업 ID, 출력 형식, 원문 길이만 담은 문자열입니다.</returns>
    public override string ToString()
    {
        return $"ConversionRequest {{ JobId = {JobId}, TargetFormat = {TargetFormat}, SourceLength = {SourceText.Length} }}";
    }
}

/// <summary>
/// Converter Port가 만든 변환 결과입니다. Content는 저장 대상이며 관측 데이터에 기록하지 않습니다.
/// </summary>
public sealed record ConvertedDocument
{
    /// <summary>
    /// 변환된 본문과 출력 형식을 보관합니다.
    /// content는 민감할 수 있는 저장 대상, format은 검증된 출력 종류이며 새 결과 값을 만듭니다.
    /// </summary>
    /// <param name="content">변환된 실제 문서 본문입니다.</param>
    /// <param name="format">plain 또는 upper 출력 형식입니다.</param>
    public ConvertedDocument(string content, string format)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        if (format is not ("plain" or "upper"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(format),
                "출력 형식은 plain 또는 upper여야 합니다.");
        }

        Content = content;
        Format = format;
    }

    public string Content { get; }

    public string Format { get; }

    /// <summary>
    /// 디버거와 문자열 보간에서 민감한 본문 대신 형식과 길이만 표시합니다.
    /// 파라미터는 없고 본문을 포함하지 않는 안전한 요약 문자열을 반환합니다.
    /// </summary>
    /// <returns>출력 형식과 본문 길이만 담은 문자열입니다.</returns>
    public override string ToString()
    {
        return $"ConvertedDocument {{ Format = {Format}, ContentLength = {Content.Length} }}";
    }
}

/// <summary>
/// 변환과 저장이 끝난 뒤 호출자에게 돌려주는 본문 없는 영수증입니다.
/// 원문이나 변환 본문 대신 식별자, 형식, 길이만 포함합니다.
/// </summary>
public sealed record ConversionReceipt
{
    /// <summary>
    /// 완료된 작업의 식별자, 출력 형식, 결과 길이를 검증해 영수증으로 보관합니다.
    /// 세 값은 관측과 호출자 응답에 쓰는 메타데이터이며 새 불변 영수증을 만듭니다.
    /// </summary>
    /// <param name="jobId">40자 이하의 검증된 ASCII 작업 식별자입니다.</param>
    /// <param name="targetFormat">plain 또는 upper 중 하나인 출력 형식입니다.</param>
    /// <param name="outputLength">0 이상의 변환 결과 문자 수입니다.</param>
    public ConversionReceipt(string jobId, string targetFormat, int outputLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFormat);

        if (jobId.Length > 40 || !jobId.All(ConversionRequest.IsAllowedJobIdCharacter))
        {
            throw new ArgumentException(
                "작업 ID는 40자 이하의 영문·숫자·하이픈·밑줄만 사용할 수 있습니다.",
                nameof(jobId));
        }

        if (targetFormat is not ("plain" or "upper"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFormat),
                "출력 형식은 plain 또는 upper여야 합니다.");
        }

        if (outputLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputLength),
                "결과 길이는 0 이상이어야 합니다.");
        }

        JobId = jobId;
        TargetFormat = targetFormat;
        OutputLength = outputLength;
    }

    public string JobId { get; }

    public string TargetFormat { get; }

    public int OutputLength { get; }
}
