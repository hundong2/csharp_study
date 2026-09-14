// 파일 범위 네임스페이스는 이 파일의 타입을 같은 이름 공간에 넣으면서
// 중괄호 한 단계를 줄여, 처음 읽는 사람이 핵심 로직에 집중하게 합니다.
namespace DailyStudy.DeploymentDashboard;

/// <summary>
/// 배포가 현재 어느 단계에 있는지를 제한된 값으로 표현합니다.
/// <c>enum</c>을 쓰면 임의 문자열보다 오타를 막고 가능한 상태를 한눈에 볼 수 있습니다.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>배포 요청이 등록되었지만 아직 실행되지 않은 상태입니다.</summary>
    Registered,

    /// <summary>배포 실행이 시작된 상태입니다.</summary>
    Running,

    /// <summary>배포가 성공적으로 끝난 상태입니다.</summary>
    Succeeded,

    /// <summary>배포가 실패로 끝난 상태입니다.</summary>
    Failed,
}

/// <summary>
/// 이미 일어난 배포 사실이 공통으로 가져야 할 식별자와 시각을 정의합니다.
/// <c>record</c>는 값으로 비교되는 불변 데이터에 알맞고, 이벤트를 나중에 고치지 못하게 하는 데 도움이 됩니다.
/// </summary>
public abstract record DeploymentEvent
{
    /// <summary>
    /// 모든 배포 이벤트가 공유하는 값을 검증하고 보관합니다.
    /// </summary>
    /// <param name="deploymentId">이벤트가 속한 배포 스트림의 식별자입니다.</param>
    /// <param name="occurredAtUtc">업무 사건이 일어난 시각이며 내부에서는 UTC로 통일됩니다.</param>
    /// <remarks>이벤트의 공통 상태를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    // string?의 물음표는 외부 값이 null일 수 있음을 nullable 분석에 알리고, 생성자가 실제 검증 책임을 갖게 합니다.
    protected DeploymentEvent(string? deploymentId, DateTimeOffset occurredAtUtc)
    {
        DeploymentId = DomainGuard.Required(deploymentId, nameof(deploymentId));
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    /// <summary>이 이벤트가 속한 배포를 구분하는 식별자입니다.</summary>
    public string DeploymentId { get; }

    /// <summary>서버 지역과 무관하게 비교할 수 있도록 UTC로 보관한 발생 시각입니다.</summary>
    public DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>새 배포 요청이 등록되었다는 변경 불가능한 사실입니다.</summary>
public sealed record DeploymentRegistered : DeploymentEvent
{
    /// <summary>
    /// 배포 등록에 필요한 모든 값을 검증하여 등록 이벤트를 만듭니다.
    /// </summary>
    /// <param name="deploymentId">새 배포를 유일하게 구분할 식별자입니다.</param>
    /// <param name="environment">production, staging처럼 배포할 환경 이름입니다.</param>
    /// <param name="artifactVersion">배포할 애플리케이션 또는 이미지 버전입니다.</param>
    /// <param name="occurredAtUtc">등록이 일어난 시각이며 UTC로 정규화됩니다.</param>
    /// <remarks>검증된 등록 이벤트를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentRegistered(
        string? deploymentId,
        string? environment,
        string? artifactVersion,
        DateTimeOffset occurredAtUtc)
        : base(deploymentId, occurredAtUtc)
    {
        Environment = DomainGuard.Required(environment, nameof(environment));
        ArtifactVersion = DomainGuard.Required(artifactVersion, nameof(artifactVersion));
    }

    /// <summary>배포 대상 환경입니다.</summary>
    public string Environment { get; }

    /// <summary>배포할 산출물 버전입니다.</summary>
    public string ArtifactVersion { get; }
}

/// <summary>등록된 배포의 실제 실행이 시작되었다는 사실입니다.</summary>
public sealed record DeploymentStarted : DeploymentEvent
{
    /// <summary>
    /// 어느 배포가 언제 실행되기 시작했는지를 담은 이벤트를 만듭니다.
    /// </summary>
    /// <param name="deploymentId">실행을 시작한 배포 식별자입니다.</param>
    /// <param name="occurredAtUtc">실행 시작 시각이며 UTC로 정규화됩니다.</param>
    /// <remarks>시작 이벤트를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentStarted(string? deploymentId, DateTimeOffset occurredAtUtc)
        : base(deploymentId, occurredAtUtc)
    {
    }
}

/// <summary>실행 중이던 배포가 성공했다는 사실입니다.</summary>
public sealed record DeploymentSucceeded : DeploymentEvent
{
    /// <summary>
    /// 어느 배포가 언제 성공했는지를 담은 이벤트를 만듭니다.
    /// </summary>
    /// <param name="deploymentId">성공한 배포 식별자입니다.</param>
    /// <param name="occurredAtUtc">성공 시각이며 UTC로 정규화됩니다.</param>
    /// <remarks>성공 이벤트를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentSucceeded(string? deploymentId, DateTimeOffset occurredAtUtc)
        : base(deploymentId, occurredAtUtc)
    {
    }
}

/// <summary>실행 중이던 배포가 주어진 이유로 실패했다는 사실입니다.</summary>
public sealed record DeploymentFailed : DeploymentEvent
{
    /// <summary>
    /// 어느 배포가 왜, 언제 실패했는지를 담은 이벤트를 만듭니다.
    /// </summary>
    /// <param name="deploymentId">실패한 배포 식별자입니다.</param>
    /// <param name="reason">운영자가 원인을 파악할 수 있는 실패 설명입니다.</param>
    /// <param name="occurredAtUtc">실패 시각이며 UTC로 정규화됩니다.</param>
    /// <remarks>실패 이벤트를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentFailed(string? deploymentId, string? reason, DateTimeOffset occurredAtUtc)
        : base(deploymentId, occurredAtUtc)
    {
        Reason = DomainGuard.Required(reason, nameof(reason));
    }

    /// <summary>빈 문자열이 아닌 배포 실패 이유입니다.</summary>
    public string Reason { get; }
}

/// <summary>
/// 애그리게이트의 업무 판단 결과를 새 이벤트 또는 예상 가능한 오류로 표현합니다.
/// 잘못된 상태 전이는 복구 가능한 업무 실패이므로 예외 대신 이 결과 객체로 호출자에게 알립니다.
/// </summary>
public sealed class DeploymentDecision
{
    private readonly DeploymentEvent[] _events;

    /// <summary>
    /// 성공 여부에 맞는 이벤트와 오류 정보를 방어적으로 복사해 보관합니다.
    /// </summary>
    /// <param name="isSuccess">업무 규칙을 통과했으면 true입니다.</param>
    /// <param name="events">성공 시 저장해야 하는 새 이벤트 모음입니다.</param>
    /// <param name="errorCode">실패를 프로그램이 구분할 안정적인 코드입니다.</param>
    /// <param name="errorMessage">초보자도 원인을 이해할 수 있는 오류 설명입니다.</param>
    /// <remarks>판단 결과를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    private DeploymentDecision(
        bool isSuccess,
        IReadOnlyList<DeploymentEvent> events,
        string? errorCode,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        // ToArray는 호출자가 원래 목록을 바꿔도 이 결정의 이벤트가 달라지지 않게 새 배열을 만듭니다.
        _events = events.ToArray();
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>새 이벤트를 저장해도 되는 성공 판단인지 알려 줍니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>성공 판단이 만든 이벤트의 읽기 전용 복사본입니다.</summary>
    // =>는 오른쪽 식을 바로 반환하는 식 본문 문법이며, 단순 읽기 속성의 복사 의도를 짧게 보여 줍니다.
    public IReadOnlyList<DeploymentEvent> Events => _events.ToArray();

    /// <summary>실패 종류를 구분하는 코드이며 성공일 때는 null입니다.</summary>
    // 물음표(?)는 성공 결과에는 오류 코드가 없어서 null일 수 있음을 컴파일러에 알립니다.
    public string? ErrorCode { get; }

    /// <summary>사람이 읽을 실패 설명이며 성공일 때는 null입니다.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 저장할 이벤트 하나를 가진 성공 판단을 만듭니다.
    /// </summary>
    /// <param name="domainEvent">명령 때문에 새로 일어난 업무 사실입니다.</param>
    /// <returns>주어진 이벤트를 방어적으로 보관한 성공 판단을 반환합니다.</returns>
    public static DeploymentDecision Success(DeploymentEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        // 대괄호 컬렉션 식은 항목 하나짜리 배열을 간결하게 만드는 C# 문법입니다.
        return new DeploymentDecision(true, [domainEvent], null, null);
    }

    /// <summary>
    /// 상태를 바꾸지 않는 예상 가능한 업무 실패를 만듭니다.
    /// </summary>
    /// <param name="code">호출자와 테스트가 실패 종류를 구분할 코드입니다.</param>
    /// <param name="message">사람에게 보여 줄 구체적인 실패 이유입니다.</param>
    /// <returns>이벤트가 하나도 없는 실패 판단을 반환합니다.</returns>
    public static DeploymentDecision Failure(string code, string message)
    {
        return new DeploymentDecision(
            false,
            [],
            DomainGuard.Required(code, nameof(code)),
            DomainGuard.Required(message, nameof(message)));
    }
}

/// <summary>
/// 한 배포 스트림의 이벤트를 순서대로 적용해 현재 업무 상태를 복원하는 도메인 모델입니다.
/// 이벤트 소싱은 상태 저장 방식이고, CQRS는 명령 모델과 조회 모델을 나누는 방식이므로 둘은 서로 분리해 선택할 수 있습니다.
/// 이 예제는 학습을 위해 두 패턴을 함께 사용하지만, 어느 하나가 다른 하나를 필수로 요구하지는 않습니다.
/// </summary>
public sealed class Deployment
{
    private readonly string _deploymentId;
    private DateTimeOffset? _lastOccurredAtUtc;

    /// <summary>
    /// 비어 있는 스트림도 식별할 수 있도록 배포 ID를 먼저 보관합니다.
    /// </summary>
    /// <param name="deploymentId">복원하거나 새로 등록할 배포 스트림 식별자입니다.</param>
    /// <remarks>애그리게이트의 초기 상태를 만드는 생성자이므로 별도 반환값은 없습니다.</remarks>
    private Deployment(string deploymentId)
    {
        _deploymentId = DomainGuard.Required(deploymentId, nameof(deploymentId));
    }

    /// <summary>현재 애그리게이트가 가리키는 배포 식별자입니다.</summary>
    // =>는 오른쪽 식의 값을 바로 돌려주는 "식 본문" 문법으로, 단순 읽기 속성의 의도를 짧게 보여 줍니다.
    public string DeploymentId => _deploymentId;

    /// <summary>등록 이벤트가 적용되었는지를 알려 줍니다.</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>현재 배포 대상 환경이며 아직 등록되지 않았으면 null입니다.</summary>
    public string? Environment { get; private set; }

    /// <summary>현재 배포 산출물 버전이며 아직 등록되지 않았으면 null입니다.</summary>
    public string? ArtifactVersion { get; private set; }

    /// <summary>복원된 현재 상태이며 아직 등록되지 않았으면 null입니다.</summary>
    public DeploymentStatus? Status { get; private set; }

    /// <summary>실패 상태의 이유이며 다른 상태에서는 null입니다.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>지금까지 적용한 이벤트 수이며 이벤트 저장소의 예상 버전으로 사용합니다.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// 새 배포를 등록할 수 있는지 검사하고 등록 이벤트를 결정합니다.
    /// </summary>
    /// <param name="deploymentId">새 배포 스트림의 식별자입니다.</param>
    /// <param name="environment">배포할 환경 이름입니다.</param>
    /// <param name="artifactVersion">배포할 산출물 버전입니다.</param>
    /// <param name="occurredAtUtc">등록 명령을 처리하는 기준 시각입니다.</param>
    /// <returns>입력이 유효하면 등록 이벤트, 아니면 입력 오류를 담은 판단을 반환합니다.</returns>
    public static DeploymentDecision Register(
        string? deploymentId,
        string? environment,
        string? artifactVersion,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return DeploymentDecision.Failure("deployment.id_required", "배포 ID를 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(environment))
        {
            return DeploymentDecision.Failure("deployment.environment_required", "배포 환경을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(artifactVersion))
        {
            return DeploymentDecision.Failure("deployment.artifact_required", "산출물 버전을 입력하세요.");
        }

        return DeploymentDecision.Success(
            new DeploymentRegistered(deploymentId, environment, artifactVersion, occurredAtUtc));
    }

    /// <summary>
    /// 저장된 이벤트를 처음부터 순서대로 재생해 배포의 현재 상태를 복원합니다.
    /// </summary>
    /// <param name="deploymentId">읽어 온 스트림의 식별자입니다.</param>
    /// <param name="history">스트림 버전 순으로 정렬된 과거 이벤트입니다.</param>
    /// <returns>모든 이벤트의 불변식을 확인하고 복원한 Deployment를 반환합니다.</returns>
    /// <exception cref="InvalidDataException">이벤트 순서나 식별자가 업무 불변식을 어기면 발생합니다.</exception>
    public static Deployment Rehydrate(
        string? deploymentId,
        IEnumerable<DeploymentEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        // var는 오른쪽 생성식에서 타입이 분명할 때 컴파일러가 타입을 추론하게 하며, 실제 타입은 Deployment입니다.
        var deployment = new Deployment(DomainGuard.Required(deploymentId, nameof(deploymentId)));

        // foreach는 열거 가능한 과거 이벤트를 한 항목씩 순서대로 방문합니다.
        foreach (var domainEvent in history)
        {
            if (domainEvent is null)
            {
                throw new InvalidDataException("배포 이벤트 기록에는 null 항목이 들어갈 수 없습니다.");
            }

            deployment.ApplyHistoryEvent(domainEvent);
        }

        return deployment;
    }

    /// <summary>
    /// 등록된 배포를 실행 중 상태로 바꿀 시작 이벤트를 결정합니다.
    /// </summary>
    /// <param name="occurredAtUtc">배포 실행을 시작한 기준 시각입니다.</param>
    /// <returns>현재 상태가 Registered이면 시작 이벤트, 아니면 상태 전이 오류를 반환합니다.</returns>
    public DeploymentDecision Start(DateTimeOffset occurredAtUtc)
    {
        if (Status is not DeploymentStatus.Registered)
        {
            return DeploymentDecision.Failure(
                "deployment.cannot_start",
                "등록된 배포만 실행을 시작할 수 있습니다.");
        }

        var timeError = ValidateDecisionTime(occurredAtUtc);
        if (timeError is not null)
        {
            return timeError;
        }

        return DeploymentDecision.Success(new DeploymentStarted(_deploymentId, occurredAtUtc));
    }

    /// <summary>
    /// 실행 중인 배포를 성공으로 끝내는 이벤트를 결정합니다.
    /// </summary>
    /// <param name="occurredAtUtc">배포 성공이 확인된 기준 시각입니다.</param>
    /// <returns>현재 상태가 Running이면 성공 이벤트, 아니면 상태 전이 오류를 반환합니다.</returns>
    public DeploymentDecision Succeed(DateTimeOffset occurredAtUtc)
    {
        if (Status is not DeploymentStatus.Running)
        {
            return DeploymentDecision.Failure(
                "deployment.cannot_succeed",
                "실행 중인 배포만 성공으로 완료할 수 있습니다.");
        }

        var timeError = ValidateDecisionTime(occurredAtUtc);
        if (timeError is not null)
        {
            return timeError;
        }

        return DeploymentDecision.Success(new DeploymentSucceeded(_deploymentId, occurredAtUtc));
    }

    /// <summary>
    /// 실행 중인 배포를 실패로 끝내는 이벤트를 결정합니다.
    /// </summary>
    /// <param name="reason">운영 대시보드에 남길 구체적인 실패 이유입니다.</param>
    /// <param name="occurredAtUtc">배포 실패가 확인된 기준 시각입니다.</param>
    /// <returns>현재 상태와 이유가 유효하면 실패 이벤트, 아니면 업무 오류를 반환합니다.</returns>
    public DeploymentDecision Fail(string? reason, DateTimeOffset occurredAtUtc)
    {
        if (Status is not DeploymentStatus.Running)
        {
            return DeploymentDecision.Failure(
                "deployment.cannot_fail",
                "실행 중인 배포만 실패로 완료할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeploymentDecision.Failure(
                "deployment.failure_reason_required",
                "실패 이유를 입력하세요.");
        }

        var timeError = ValidateDecisionTime(occurredAtUtc);
        if (timeError is not null)
        {
            return timeError;
        }

        return DeploymentDecision.Success(new DeploymentFailed(_deploymentId, reason, occurredAtUtc));
    }

    /// <summary>
    /// 새 명령의 시각이 마지막 저장 이벤트보다 과거인지 검사합니다.
    /// </summary>
    /// <param name="occurredAtUtc">검사할 새 업무 사건 시각입니다.</param>
    /// <returns>시각이 역행하면 실패 판단, 그렇지 않으면 null을 반환합니다.</returns>
    private DeploymentDecision? ValidateDecisionTime(DateTimeOffset occurredAtUtc)
    {
        if (_lastOccurredAtUtc is not null
            && occurredAtUtc.ToUniversalTime() < _lastOccurredAtUtc.Value)
        {
            return DeploymentDecision.Failure(
                "deployment.time_moved_backwards",
                "새 이벤트 시각은 마지막 이벤트 시각보다 이를 수 없습니다.");
        }

        return null;
    }

    /// <summary>
    /// 과거 이벤트 하나를 상태에 반영하며 불가능한 기록은 즉시 거부합니다.
    /// </summary>
    /// <param name="domainEvent">다음 스트림 버전에 해당하는 과거 이벤트입니다.</param>
    /// <returns>상태를 내부에서 바꾸며 별도 값을 반환하지 않습니다.</returns>
    private void ApplyHistoryEvent(DeploymentEvent domainEvent)
    {
        if (!string.Equals(domainEvent.DeploymentId, _deploymentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("스트림 ID와 이벤트의 배포 ID가 서로 다릅니다.");
        }

        if (_lastOccurredAtUtc is not null && domainEvent.OccurredAtUtc < _lastOccurredAtUtc.Value)
        {
            throw new InvalidDataException("이벤트 발생 시각이 과거 방향으로 역행했습니다.");
        }

        // 타입 패턴 switch는 실제 이벤트 종류에 따라 안전하게 다른 상태 변경을 실행합니다.
        switch (domainEvent)
        {
            case DeploymentRegistered registered when Version == 0 && !IsRegistered:
                IsRegistered = true;
                Environment = registered.Environment;
                ArtifactVersion = registered.ArtifactVersion;
                Status = DeploymentStatus.Registered;
                FailureReason = null;
                break;

            case DeploymentStarted when Status is DeploymentStatus.Registered:
                Status = DeploymentStatus.Running;
                break;

            case DeploymentSucceeded when Status is DeploymentStatus.Running:
                Status = DeploymentStatus.Succeeded;
                break;

            case DeploymentFailed failed when Status is DeploymentStatus.Running:
                Status = DeploymentStatus.Failed;
                FailureReason = failed.Reason;
                break;

            default:
                throw new InvalidDataException(
                    $"'{domainEvent.GetType().Name}' 이벤트는 현재 상태 '{Status?.ToString() ?? "Empty"}'에 적용할 수 없습니다.");
        }

        _lastOccurredAtUtc = domainEvent.OccurredAtUtc;
        // checked는 int 범위를 넘는 비정상적으로 긴 스트림을 조용히 감싸지 않고 오류로 드러냅니다.
        Version = checked(Version + 1);
    }
}

/// <summary>도메인 값 객체와 계약 객체가 공유하는 최소 입력 검증 도우미입니다.</summary>
internal static class DomainGuard
{
    /// <summary>
    /// 필수 문자열이 비어 있지 않은지 검사하고 앞뒤 공백을 제거합니다.
    /// </summary>
    /// <param name="value">외부에서 전달된 문자열입니다.</param>
    /// <param name="parameterName">예외에 표시할 매개변수 이름입니다.</param>
    /// <returns>검증을 통과하고 앞뒤 공백이 제거된 문자열을 반환합니다.</returns>
    public static string Required(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("필수 문자열은 비어 있을 수 없습니다.", parameterName);
        }

        return value.Trim();
    }
}
