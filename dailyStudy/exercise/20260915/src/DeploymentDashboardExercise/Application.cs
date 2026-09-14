namespace DailyStudy.DeploymentDashboard;

/// <summary>
/// 저장된 도메인 이벤트에 스트림 순서와 전체 저장소 순서를 덧씌운 봉투입니다.
/// 이벤트 본문과 저장 위치를 나누면 같은 업무 사실을 스트림 복원과 전역 프로젝션에서 모두 사용할 수 있습니다.
/// </summary>
public sealed class EventEnvelope
{
    /// <summary>
    /// 이벤트와 두 종류의 순번을 검증해 변경 불가능한 저장 계약을 만듭니다.
    /// </summary>
    /// <param name="streamId">이벤트가 속한 배포 스트림 식별자입니다.</param>
    /// <param name="streamVersion">해당 스트림 안에서 1부터 시작하는 이벤트 순번입니다.</param>
    /// <param name="globalPosition">모든 스트림을 합친 로그에서 1부터 시작하는 순번입니다.</param>
    /// <param name="domainEvent">실제로 일어난 배포 업무 사실입니다.</param>
    /// <remarks>저장 봉투를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    // string?의 물음표는 호출자가 null을 보낼 가능성을 드러내며, 아래 Required가 검증 후 non-null 문자열로 바꿉니다.
    public EventEnvelope(
        string? streamId,
        int streamVersion,
        long globalPosition,
        DeploymentEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        StreamId = DomainGuard.Required(streamId, nameof(streamId));

        if (!string.Equals(StreamId, domainEvent.DeploymentId, StringComparison.Ordinal))
        {
            throw new ArgumentException("스트림 ID와 이벤트의 배포 ID가 같아야 합니다.", nameof(domainEvent));
        }

        if (streamVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamVersion), "스트림 버전은 1 이상이어야 합니다.");
        }

        if (globalPosition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(globalPosition), "전역 위치는 1 이상이어야 합니다.");
        }

        // 한 스트림의 N번째 이벤트가 되려면 전체 로그에도 최소 N개 이벤트가 있어야 하므로 stream version이 전역 위치를 넘을 수 없습니다.
        if (streamVersion > globalPosition)
        {
            throw new ArgumentException(
                "스트림 버전은 전체 이벤트 로그의 전역 위치를 넘을 수 없습니다.",
                nameof(streamVersion));
        }

        if (!IsSupportedEvent(domainEvent))
        {
            throw new ArgumentException("지원하지 않는 배포 이벤트 타입입니다.", nameof(domainEvent));
        }

        StreamVersion = streamVersion;
        GlobalPosition = globalPosition;
        Event = domainEvent;
    }

    /// <summary>이벤트가 속한 배포 스트림 식별자입니다.</summary>
    public string StreamId { get; }

    /// <summary>한 배포 스트림 안에서의 연속된 버전입니다.</summary>
    public int StreamVersion { get; }

    /// <summary>서로 다른 배포까지 포함한 전체 이벤트 로그의 연속된 위치입니다.</summary>
    public long GlobalPosition { get; }

    /// <summary>봉투 안에 보관한 변경 불가능한 도메인 이벤트입니다.</summary>
    public DeploymentEvent Event { get; }

    /// <summary>
    /// 재전달된 봉투가 이미 처리한 봉투와 완전히 같은 계약인지 비교합니다.
    /// </summary>
    /// <param name="other">같은 전역 위치로 다시 도착한 후보 봉투입니다.</param>
    /// <returns>식별자, 순번, 이벤트 값이 모두 같으면 true를 반환합니다.</returns>
    public bool HasSameContract(EventEnvelope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(StreamId, other.StreamId, StringComparison.Ordinal)
            && StreamVersion == other.StreamVersion
            && GlobalPosition == other.GlobalPosition
            && Event.Equals(other.Event);
    }

    /// <summary>
    /// 이벤트 저장소와 프로젝터가 이해하는 폐쇄된 이벤트 종류인지 확인합니다.
    /// </summary>
    /// <param name="domainEvent">지원 여부를 검사할 도메인 이벤트입니다.</param>
    /// <returns>현재 예제에서 정의한 네 이벤트 중 하나이면 true를 반환합니다.</returns>
    private static bool IsSupportedEvent(DeploymentEvent domainEvent)
    {
        // or 패턴은 여러 타입 패턴 가운데 하나라도 맞는지를 짧게 표현합니다.
        return domainEvent is DeploymentRegistered
            or DeploymentStarted
            or DeploymentSucceeded
            or DeploymentFailed;
    }
}

/// <summary>한 배포 스트림 전체를 읽은 시점의 버전과 이벤트 스냅샷입니다.</summary>
public sealed class StreamSlice
{
    private readonly EventEnvelope[] _events;

    /// <summary>
    /// 스트림 조회 결과가 연속된 버전인지 검증하고 이벤트 배열을 복사합니다.
    /// </summary>
    /// <param name="streamId">조회한 배포 스트림 식별자입니다.</param>
    /// <param name="currentVersion">조회 시점에 저장된 마지막 스트림 버전이며 빈 스트림이면 0입니다.</param>
    /// <param name="events">버전 1부터 차례대로 읽은 이벤트 봉투입니다.</param>
    /// <remarks>안전한 스트림 스냅샷을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public StreamSlice(string? streamId, int currentVersion, IReadOnlyList<EventEnvelope> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        StreamId = DomainGuard.Required(streamId, nameof(streamId));

        if (currentVersion < 0 || currentVersion != events.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentVersion),
                "현재 버전은 0 이상이며 전체 이벤트 수와 같아야 합니다.");
        }

        _events = events.ToArray();
        for (var index = 0; index < _events.Length; index++)
        {
            var envelope = _events[index]
                ?? throw new ArgumentException("스트림에는 null 봉투가 들어갈 수 없습니다.", nameof(events));
            var expectedVersion = checked(index + 1);
            if (!string.Equals(envelope.StreamId, StreamId, StringComparison.Ordinal)
                || envelope.StreamVersion != expectedVersion)
            {
                throw new ArgumentException("스트림 이벤트의 ID와 버전은 끊김 없이 이어져야 합니다.", nameof(events));
            }
        }

        CurrentVersion = currentVersion;
    }

    /// <summary>조회한 배포 스트림 식별자입니다.</summary>
    public string StreamId { get; }

    /// <summary>조회 시점의 마지막 스트림 버전이며 이벤트가 없으면 0입니다.</summary>
    public int CurrentVersion { get; }

    /// <summary>호출자가 내부 배열을 바꾸지 못하도록 매번 복사해서 돌려주는 이벤트 목록입니다.</summary>
    // =>는 오른쪽 식을 바로 반환하는 식 본문 문법이며, 단순 읽기 속성의 복사 의도를 짧게 보여 줍니다.
    public IReadOnlyList<EventEnvelope> Events => _events.ToArray();
}

/// <summary>전역 체크포인트 뒤에서 읽은 이벤트와 읽기 시작 당시 원본 로그의 끝 위치입니다.</summary>
public sealed class GlobalEventBatch
{
    private readonly EventEnvelope[] _events;

    /// <summary>
    /// 전역 이벤트 묶음의 위치 계약을 검사하고 방어적으로 복사합니다.
    /// </summary>
    /// <param name="sourceHeadPosition">조회 시점 이벤트 저장소의 마지막 전역 위치입니다.</param>
    /// <param name="events">체크포인트 다음부터 전역 순서로 읽은 이벤트입니다.</param>
    /// <remarks>전역 이벤트 스냅샷을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public GlobalEventBatch(long sourceHeadPosition, IReadOnlyList<EventEnvelope> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (sourceHeadPosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeadPosition), "원본 끝 위치는 음수일 수 없습니다.");
        }

        _events = events.ToArray();
        EventEnvelope? previousEnvelope = null;
        foreach (var envelope in _events)
        {
            if (envelope is null)
            {
                throw new ArgumentException("전역 이벤트 묶음에는 null 봉투가 들어갈 수 없습니다.", nameof(events));
            }

            // 같은 위치의 동일 이벤트 재전달은 멱등성 검증용으로 허용하고, 위치가 뒤로 가는 순서는 거부합니다.
            if ((previousEnvelope is not null
                    && envelope.GlobalPosition < previousEnvelope.GlobalPosition)
                || envelope.GlobalPosition > sourceHeadPosition)
            {
                throw new ArgumentException("전역 이벤트는 오름차순이며 원본 끝 위치를 넘을 수 없습니다.", nameof(events));
            }

            if (previousEnvelope is not null
                && envelope.GlobalPosition == previousEnvelope.GlobalPosition
                && !previousEnvelope.HasSameContract(envelope))
            {
                throw new ArgumentException(
                    "같은 전역 위치의 재전달은 봉투 계약도 완전히 같아야 합니다.",
                    nameof(events));
            }

            previousEnvelope = envelope;
        }

        SourceHeadPosition = sourceHeadPosition;
    }

    /// <summary>이 묶음을 읽기 시작했을 때 원본 이벤트 로그의 마지막 위치입니다.</summary>
    public long SourceHeadPosition { get; }

    /// <summary>내부 배열을 노출하지 않는 전역 이벤트 봉투 복사본입니다.</summary>
    public IReadOnlyList<EventEnvelope> Events => _events.ToArray();
}

/// <summary>이벤트 추가가 성공한 뒤 새 스트림 버전과 전역 위치를 알려 주는 영수증입니다.</summary>
public sealed record AppendReceipt
{
    /// <summary>
    /// 성공한 append의 마지막 버전과 전역 위치를 검증해 영수증을 만듭니다.
    /// </summary>
    /// <param name="newStreamVersion">append 뒤 스트림의 마지막 버전입니다.</param>
    /// <param name="lastGlobalPosition">append된 마지막 이벤트의 전역 위치입니다.</param>
    /// <remarks>저장 영수증을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public AppendReceipt(int newStreamVersion, long lastGlobalPosition)
    {
        if (newStreamVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newStreamVersion));
        }

        if (lastGlobalPosition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastGlobalPosition));
        }

        NewStreamVersion = newStreamVersion;
        LastGlobalPosition = lastGlobalPosition;
    }

    /// <summary>append 뒤 해당 스트림의 마지막 버전입니다.</summary>
    public int NewStreamVersion { get; }

    /// <summary>append된 마지막 이벤트의 전역 위치입니다.</summary>
    public long LastGlobalPosition { get; }
}

/// <summary>낙관적 동시성 검사의 예상 버전과 실제 버전이 다를 때 발생하는 계약 예외입니다.</summary>
public sealed class ExpectedVersionConflictException : Exception
{
    /// <summary>
    /// 충돌한 스트림과 두 버전을 담아 문제 원인을 잃지 않는 예외를 만듭니다.
    /// </summary>
    /// <param name="streamId">동시에 수정된 배포 스트림 식별자입니다.</param>
    /// <param name="expectedVersion">명령이 읽고 저장을 기대한 버전입니다.</param>
    /// <param name="actualVersion">append 직전에 저장소가 확인한 실제 버전입니다.</param>
    /// <remarks>충돌 예외를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public ExpectedVersionConflictException(string? streamId, int expectedVersion, int actualVersion)
        : base($"스트림 '{streamId}'의 예상 버전 {expectedVersion}과 실제 버전 {actualVersion}이 다릅니다.")
    {
        StreamId = DomainGuard.Required(streamId, nameof(streamId));
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>충돌이 난 배포 스트림 식별자입니다.</summary>
    public string StreamId { get; }

    /// <summary>호출자가 마지막으로 읽었던 스트림 버전입니다.</summary>
    public int ExpectedVersion { get; }

    /// <summary>저장 시점에 발견된 최신 스트림 버전입니다.</summary>
    public int ActualVersion { get; }
}

/// <summary>
/// 조회 경로가 append 권한 없이 전역 이벤트 로그의 현재 끝 위치만 읽게 하는 좁은 포트입니다.
/// 인터페이스 분리 원칙(ISP)을 적용해 Query Service가 필요 이상의 쓰기 기능에 의존하지 않게 합니다.
/// </summary>
public interface IEventLogPositionReader
{
    /// <summary>
    /// 조회 모델의 지연을 계산할 수 있도록 현재 전역 로그 끝 위치만 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">호출자가 조회 중단을 요청하는 취소 신호입니다.</param>
    /// <returns>이벤트가 없으면 0, 있으면 마지막 전역 위치를 비동기로 반환합니다.</returns>
    Task<long> GetHeadPositionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 명령 경로가 스트림을 읽고 append할 수 있게 하며, 위치 조회 전용 포트도 함께 구현합니다.
/// Query Service는 이 넓은 인터페이스 대신 <see cref="IEventLogPositionReader"/>만 받아 쓰기 권한을 갖지 않습니다.
/// 일반 CRUD Repository보다 expected version과 전역 로그 의미를 드러내는 특화된 Repository Port입니다.
/// </summary>
public interface IDeploymentEventStore : IEventLogPositionReader
{
    /// <summary>
    /// 한 배포 스트림의 전체 기록을 버전 순으로 읽습니다.
    /// </summary>
    /// <param name="streamId">읽을 배포 스트림 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 조회 중단을 요청하는 취소 신호입니다.</param>
    /// <returns>현재 버전과 이벤트의 방어적 스냅샷을 비동기로 반환합니다.</returns>
    Task<StreamSlice> ReadStreamAsync(string? streamId, CancellationToken cancellationToken);

    /// <summary>
    /// 주어진 체크포인트보다 뒤에 저장된 모든 이벤트를 전역 순서로 읽습니다.
    /// </summary>
    /// <param name="positionExclusive">결과에서 제외할 마지막 처리 위치이며 처음에는 0입니다.</param>
    /// <param name="cancellationToken">호출자가 조회 중단을 요청하는 취소 신호입니다.</param>
    /// <returns>원본 로그 끝 위치와 그 뒤의 이벤트 스냅샷을 비동기로 반환합니다.</returns>
    Task<GlobalEventBatch> ReadAllAfterAsync(long positionExclusive, CancellationToken cancellationToken);

    /// <summary>
    /// 예상 버전이 현재 버전과 같을 때만 새 이벤트 전부를 원자적으로 끝에 추가합니다.
    /// </summary>
    /// <param name="streamId">이벤트를 추가할 배포 스트림 식별자입니다.</param>
    /// <param name="expectedVersion">명령이 읽었던 마지막 스트림 버전이며 빈 스트림은 0입니다.</param>
    /// <param name="events">한 명령이 결정한 하나 이상의 새 이벤트입니다.</param>
    /// <param name="cancellationToken">저장 전에 작업을 중단시킬 수 있는 취소 신호입니다.</param>
    /// <returns>새 스트림 버전과 마지막 전역 위치를 비동기로 반환합니다.</returns>
    Task<AppendReceipt> AppendAsync(
        string? streamId,
        int expectedVersion,
        IReadOnlyList<DeploymentEvent> events,
        CancellationToken cancellationToken);

}

/// <summary>명령 처리의 성공 버전 또는 예상 가능한 업무 오류를 호출자에게 돌려줍니다.</summary>
public sealed class CommandResult
{
    /// <summary>
    /// 성공 여부와 새 버전 또는 오류를 일관된 조합으로 보관합니다.
    /// </summary>
    /// <param name="isSuccess">이벤트 append까지 끝났으면 true입니다.</param>
    /// <param name="streamVersion">성공 뒤 스트림 버전이며 실패일 때는 null입니다.</param>
    /// <param name="errorCode">실패를 구분할 코드이며 성공일 때는 null입니다.</param>
    /// <param name="errorMessage">실패 설명이며 성공일 때는 null입니다.</param>
    /// <remarks>명령 결과를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    private CommandResult(bool isSuccess, int? streamVersion, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        StreamVersion = streamVersion;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>명령의 이벤트가 저장되었는지 알려 줍니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>성공 뒤 스트림 버전이며 실패 시 null입니다.</summary>
    public int? StreamVersion { get; }

    /// <summary>실패 분류 코드이며 성공 시 null입니다.</summary>
    public string? ErrorCode { get; }

    /// <summary>사용자가 이해할 실패 설명이며 성공 시 null입니다.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 저장 영수증으로 성공 명령 결과를 만듭니다.
    /// </summary>
    /// <param name="receipt">이벤트 저장소가 돌려준 append 영수증입니다.</param>
    /// <returns>새 스트림 버전을 담은 성공 결과를 반환합니다.</returns>
    public static CommandResult Success(AppendReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new CommandResult(true, receipt.NewStreamVersion, null, null);
    }

    /// <summary>
    /// 코드와 메시지로 예상 가능한 실패 명령 결과를 만듭니다.
    /// </summary>
    /// <param name="code">호출자가 분기할 안정적인 실패 코드입니다.</param>
    /// <param name="message">사람이 읽을 구체적인 실패 설명입니다.</param>
    /// <returns>스트림 버전이 없는 실패 결과를 반환합니다.</returns>
    public static CommandResult Failure(string code, string message)
    {
        return new CommandResult(
            false,
            null,
            DomainGuard.Required(code, nameof(code)),
            DomainGuard.Required(message, nameof(message)));
    }
}

/// <summary>
/// 배포 명령을 받아 이벤트 기록을 복원하고, 도메인 판단을 저장하는 Application Service입니다.
/// CQRS에서 이 서비스는 쓰기 경로만 담당하며 대시보드 조회 모델을 직접 수정하지 않습니다.
/// </summary>
public sealed class DeploymentCommandService
{
    private readonly IDeploymentEventStore _eventStore;

    /// <summary>
    /// 구체 저장 기술 대신 이벤트 저장소 인터페이스를 주입받습니다.
    /// </summary>
    /// <param name="eventStore">스트림을 읽고 낙관적 버전으로 이벤트를 추가할 저장소입니다.</param>
    /// <remarks>서비스 의존성을 보관하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentCommandService(IDeploymentEventStore eventStore)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>
    /// 비어 있는 스트림에 새 배포 등록 이벤트를 저장합니다.
    /// </summary>
    /// <param name="deploymentId">새 배포를 구분할 식별자입니다.</param>
    /// <param name="environment">배포 대상 환경입니다.</param>
    /// <param name="artifactVersion">배포할 산출물 버전입니다.</param>
    /// <param name="occurredAtUtc">등록 업무가 일어난 기준 시각입니다.</param>
    /// <param name="cancellationToken">읽기 또는 저장을 중단할 취소 신호입니다.</param>
    /// <returns>append 성공 버전 또는 중복·입력 오류를 비동기로 반환합니다.</returns>
    // async는 메서드 안에서 await로 비동기 작업 완료를 기다리면서 호출 스레드를 막지 않게 하는 키워드입니다.
    public async Task<CommandResult> RegisterAsync(
        string? deploymentId,
        string? environment,
        string? artifactVersion,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return CommandResult.Failure("deployment.id_required", "배포 ID를 입력하세요.");
        }

        var streamId = deploymentId.Trim();
        // await는 저장소 조회가 끝난 뒤 다음 줄을 이어 실행하며, ConfigureAwait(false)는 특정 UI 스레드로 돌아올 필요가 없음을 뜻합니다.
        var slice = await _eventStore
            .ReadStreamAsync(streamId, cancellationToken)
            .ConfigureAwait(false);

        if (slice.CurrentVersion != 0)
        {
            return CommandResult.Failure("deployment.already_registered", "이미 등록된 배포 ID입니다.");
        }

        var decision = Deployment.Register(streamId, environment, artifactVersion, occurredAtUtc);
        return await AppendDecisionAsync(streamId, slice.CurrentVersion, decision, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 저장 기록을 복원한 뒤 배포 시작 가능 여부를 판단하고 이벤트를 저장합니다.
    /// </summary>
    /// <param name="deploymentId">시작할 배포 식별자입니다.</param>
    /// <param name="occurredAtUtc">배포 실행이 시작된 기준 시각입니다.</param>
    /// <param name="cancellationToken">읽기 또는 저장을 중단할 취소 신호입니다.</param>
    /// <returns>저장된 새 버전 또는 상태 전이 오류를 비동기로 반환합니다.</returns>
    public Task<CommandResult> StartAsync(
        string? deploymentId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        // => 람다는 복원된 Deployment를 받아 Start 판단을 호출하는 작은 함수를 전달합니다.
        return ExecuteExistingAsync(
            deploymentId,
            deployment => deployment.Start(occurredAtUtc),
            cancellationToken);
    }

    /// <summary>
    /// 저장 기록을 복원한 뒤 실행 중인 배포를 성공으로 완료하는 이벤트를 저장합니다.
    /// </summary>
    /// <param name="deploymentId">성공으로 끝낼 배포 식별자입니다.</param>
    /// <param name="occurredAtUtc">성공이 확인된 기준 시각입니다.</param>
    /// <param name="cancellationToken">읽기 또는 저장을 중단할 취소 신호입니다.</param>
    /// <returns>저장된 새 버전 또는 상태 전이 오류를 비동기로 반환합니다.</returns>
    public Task<CommandResult> SucceedAsync(
        string? deploymentId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        return ExecuteExistingAsync(
            deploymentId,
            deployment => deployment.Succeed(occurredAtUtc),
            cancellationToken);
    }

    /// <summary>
    /// 저장 기록을 복원한 뒤 실행 중인 배포를 실패 이유와 함께 완료합니다.
    /// </summary>
    /// <param name="deploymentId">실패로 끝낼 배포 식별자입니다.</param>
    /// <param name="reason">대시보드에 남길 실패 이유입니다.</param>
    /// <param name="occurredAtUtc">실패가 확인된 기준 시각입니다.</param>
    /// <param name="cancellationToken">읽기 또는 저장을 중단할 취소 신호입니다.</param>
    /// <returns>저장된 새 버전 또는 상태·입력 오류를 비동기로 반환합니다.</returns>
    public Task<CommandResult> FailAsync(
        string? deploymentId,
        string? reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        return ExecuteExistingAsync(
            deploymentId,
            deployment => deployment.Fail(reason, occurredAtUtc),
            cancellationToken);
    }

    /// <summary>
    /// 기존 스트림을 복원한 뒤 전달받은 업무 판단 함수를 실행하고 결과 이벤트를 저장합니다.
    /// </summary>
    /// <param name="deploymentId">복원할 배포 스트림 식별자입니다.</param>
    /// <param name="decide">복원된 애그리게이트에서 명령별 이벤트를 결정하는 함수입니다.</param>
    /// <param name="cancellationToken">읽기 또는 저장을 중단할 취소 신호입니다.</param>
    /// <returns>append 성공 버전 또는 업무 오류를 비동기로 반환합니다.</returns>
    private async Task<CommandResult> ExecuteExistingAsync(
        string? deploymentId,
        Func<Deployment, DeploymentDecision> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return CommandResult.Failure("deployment.id_required", "배포 ID를 입력하세요.");
        }

        var streamId = deploymentId.Trim();
        var slice = await _eventStore
            .ReadStreamAsync(streamId, cancellationToken)
            .ConfigureAwait(false);

        if (slice.CurrentVersion == 0)
        {
            return CommandResult.Failure("deployment.not_found", "등록된 배포를 찾을 수 없습니다.");
        }

        // LINQ Select는 각 봉투에서 도메인 이벤트만 골라 복원 입력으로 전달합니다.
        var deployment = Deployment.Rehydrate(streamId, slice.Events.Select(envelope => envelope.Event));
        var decision = decide(deployment);
        return await AppendDecisionAsync(streamId, slice.CurrentVersion, decision, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 성공 판단의 이벤트를 예상 버전으로 append하고 충돌을 업무 결과로 바꿉니다.
    /// </summary>
    /// <param name="streamId">이벤트를 저장할 배포 스트림 식별자입니다.</param>
    /// <param name="expectedVersion">명령이 읽었던 마지막 스트림 버전입니다.</param>
    /// <param name="decision">애그리게이트가 만든 성공 이벤트 또는 업무 실패입니다.</param>
    /// <param name="cancellationToken">append 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>저장 성공 버전 또는 판단·동시성 오류를 비동기로 반환합니다.</returns>
    private async Task<CommandResult> AppendDecisionAsync(
        string streamId,
        int expectedVersion,
        DeploymentDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.IsSuccess)
        {
            // !는 런타임 검사가 아니라 null 아님을 컴파일러에 알립니다. DeploymentDecision의 실패 생성 규칙이 두 값을 보장합니다.
            return CommandResult.Failure(decision.ErrorCode!, decision.ErrorMessage!);
        }

        try
        {
            var receipt = await _eventStore
                .AppendAsync(streamId, expectedVersion, decision.Events, cancellationToken)
                .ConfigureAwait(false);
            return CommandResult.Success(receipt);
        }
        catch (ExpectedVersionConflictException conflict)
        {
            return CommandResult.Failure("deployment.concurrency_conflict", conflict.Message);
        }
    }
}
