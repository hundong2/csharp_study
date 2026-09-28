namespace ServiceHealthApi.Domain;

/// <summary>
/// 의존성이 요청 처리에 반드시 필요한지, 일부 기능에만 필요한지 구분합니다.
/// </summary>
public enum DependencyImportance
{
    Required,
    Optional
}

/// <summary>
/// 한 번의 probe에서 관찰한 의존성의 현재 상태입니다.
/// </summary>
public enum DependencyCondition
{
    Available,
    Unavailable
}

/// <summary>
/// 여러 의존성 관찰을 합친 애플리케이션의 준비 상태입니다.
/// </summary>
public enum ReadinessLevel
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// 의존성 한 개를 검사한 결과를 ASP.NET Core 형식과 무관한 Domain 값으로 표현합니다.
/// </summary>
// private 생성자를 가진 record는 반드시 Create 검증을 통과한 값만 만들어지게 하면서 불변성을 유지합니다.
public sealed record DependencyObservation
{
    /// <summary>
    /// 검증된 관찰 값을 생성합니다.
    /// </summary>
    /// <param name="name">운영자가 구분할 수 있는 짧은 의존성 이름입니다.</param>
    /// <param name="importance">요청 처리에 필수인지 선택인지 나타냅니다.</param>
    /// <param name="condition">이번 probe에서 실제로 관찰한 상태입니다.</param>
    /// <param name="duration">probe가 완료되는 데 걸린 시간입니다.</param>
    /// <param name="safeCode">연결 문자열이나 예외 원문이 없는 공개 가능한 코드입니다.</param>
    /// <returns>모든 값이 안전하면 관찰 성공 Result, 아니면 검증 실패 Result를 반환합니다.</returns>
    public static Result<DependencyObservation> Create(
        string? name,
        DependencyImportance importance,
        DependencyCondition condition,
        TimeSpan duration,
        string? safeCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.name.required", "의존성 이름은 비워 둘 수 없습니다."));
        }

        if (!Enum.IsDefined(importance))
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.importance.invalid", "정의되지 않은 의존성 중요도입니다."));
        }

        if (!Enum.IsDefined(condition))
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.condition.invalid", "정의되지 않은 의존성 상태입니다."));
        }

        if (duration < TimeSpan.Zero)
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.duration.negative", "검사 시간은 음수일 수 없습니다."));
        }

        if (string.IsNullOrWhiteSpace(safeCode))
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.code.required", "외부에 공개할 안전한 상태 코드가 필요합니다."));
        }

        // var는 Trim 호출 결과가 string으로 분명할 때 지역 변수 형식 이름의 중복을 줄입니다.
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedCode = safeCode.Trim().ToLowerInvariant();
        // Any의 lambda와 not/or pattern은 허용 목록 밖 문자가 하나라도 있는지 읽기 쉽게 검사합니다.
        if (normalizedCode.Length > 64 || normalizedCode.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-')))
        {
            return Result<DependencyObservation>.Failure(
                new Error("dependency.code.invalid", "안전 코드는 64자 이하의 영문·숫자·점·밑줄·하이픈만 허용합니다."));
        }

        return Result<DependencyObservation>.Success(
            new DependencyObservation(
                normalizedName,
                importance,
                condition,
                duration,
                normalizedCode));
    }

    /// <summary>
    /// Create가 검증한 값만 보관하도록 외부의 직접 생성을 막습니다.
    /// </summary>
    /// <param name="name">정규화된 의존성 이름입니다.</param>
    /// <param name="importance">필수 또는 선택 분류입니다.</param>
    /// <param name="condition">관찰된 가용 상태입니다.</param>
    /// <param name="duration">0 이상인 검사 시간입니다.</param>
    /// <param name="safeCode">민감정보가 제거된 상태 코드입니다.</param>
    /// <returns>생성자는 새 관찰 값을 만들며 별도 반환값은 없습니다.</returns>
    private DependencyObservation(
        string name,
        DependencyImportance importance,
        DependencyCondition condition,
        TimeSpan duration,
        string safeCode)
    {
        Name = name;
        Importance = importance;
        Condition = condition;
        Duration = duration;
        SafeCode = safeCode;
    }

    /// <summary>
    /// 정규화된 의존성 이름을 가져옵니다.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 의존성이 필수인지 선택인지 가져옵니다.
    /// </summary>
    public DependencyImportance Importance { get; }

    /// <summary>
    /// 관찰된 의존성 상태를 가져옵니다.
    /// </summary>
    public DependencyCondition Condition { get; }

    /// <summary>
    /// probe 소요 시간을 가져옵니다.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// 외부 응답에 포함해도 되는 상태 코드를 가져옵니다.
    /// </summary>
    public string SafeCode { get; }
}

/// <summary>
/// 준비 상태 정책이 만든 최종 단계와 그 근거가 된 관찰 스냅샷을 보관합니다.
/// </summary>
public sealed record ReadinessDecision
{
    /// <summary>
    /// 외부 collection을 복사해 판정 뒤 원본 변경이 근거를 바꾸지 못하게 합니다.
    /// </summary>
    /// <param name="level">전체 요청 수신 가능성을 나타내는 단계입니다.</param>
    /// <param name="observations">판정에 실제 사용한 관찰 모음입니다.</param>
    /// <returns>복사된 read-only 관찰 목록을 가진 새 결정을 반환합니다.</returns>
    public static ReadinessDecision Create(
        ReadinessLevel level,
        IEnumerable<DependencyObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), "정의되지 않은 준비 상태 단계입니다.");
        }

        var snapshot = Array.AsReadOnly(observations.ToArray());
        if (snapshot.Count == 0)
        {
            throw new ArgumentException("준비 상태 결정에는 관찰이 하나 이상 필요합니다.", nameof(observations));
        }

        return new ReadinessDecision(level, snapshot);
    }

    /// <summary>
    /// Create가 복사한 read-only 목록만 보관하도록 외부의 직접 생성을 막습니다.
    /// </summary>
    /// <param name="level">최종 준비 단계입니다.</param>
    /// <param name="observations">변경할 수 없는 관찰 스냅샷입니다.</param>
    /// <returns>생성자는 새 결정을 만들며 별도 반환값은 없습니다.</returns>
    private ReadinessDecision(
        ReadinessLevel level,
        IReadOnlyList<DependencyObservation> observations)
    {
        Level = level;
        Observations = observations;
    }

    /// <summary>
    /// 전체 요청 수신 가능성을 나타내는 단계를 가져옵니다.
    /// </summary>
    public ReadinessLevel Level { get; }

    /// <summary>
    /// 판정에 실제 사용한 불변 관찰 스냅샷을 가져옵니다.
    /// </summary>
    public IReadOnlyList<DependencyObservation> Observations { get; }
}
