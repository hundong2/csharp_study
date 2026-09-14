namespace DailyStudy.DeploymentDashboard;

/// <summary>
/// 여러 배포 스트림을 한 append-only 로그로 보관하는 교육용 메모리 이벤트 저장소입니다.
/// <c>lock</c>으로 버전 확인과 전체 이벤트 추가를 한 임계 구역에 묶어 부분 저장과 순번 중복을 막습니다.
/// </summary>
public sealed class InMemoryDeploymentEventStore : IDeploymentEventStore
{
    // new()는 왼쪽 필드 타입이 분명할 때 생성자 타입 이름을 반복하지 않는 target-typed new 문법입니다.
    private readonly object _gate = new();
    private readonly Dictionary<string, List<EventEnvelope>> _streams = new(StringComparer.Ordinal);
    private readonly List<EventEnvelope> _globalEvents = [];
    private long _nextGlobalPosition = 1;

    /// <summary>
    /// 이벤트가 없는 빈 append-only 저장소를 만듭니다.
    /// </summary>
    /// <remarks>초기 컬렉션과 전역 위치 1을 준비하며, 매개변수와 반환값은 없습니다.</remarks>
    public InMemoryDeploymentEventStore()
    {
    }

    /// <summary>
    /// 한 스트림의 이벤트를 내부 컬렉션과 분리된 스냅샷으로 읽습니다.
    /// </summary>
    /// <param name="streamId">읽을 배포 스트림 식별자입니다.</param>
    /// <param name="cancellationToken">조회 시작 전 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>빈 스트림이면 버전 0, 아니면 전체 이벤트를 담은 완료 Task를 반환합니다.</returns>
    // string?는 외부 호출이 null일 수 있음을 표시하며, Required를 지난 뒤에는 non-null ID만 저장소에서 사용합니다.
    public Task<StreamSlice> ReadStreamAsync(string? streamId, CancellationToken cancellationToken)
    {
        var normalizedStreamId = DomainGuard.Required(streamId, nameof(streamId));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_streams.TryGetValue(normalizedStreamId, out var stored))
            {
                return Task.FromResult(new StreamSlice(normalizedStreamId, 0, []));
            }

            // ToArray로 저장소 내부 List와 수명이 분리된 스냅샷을 만듭니다.
            var copy = stored.ToArray();
            return Task.FromResult(new StreamSlice(normalizedStreamId, copy.Length, copy));
        }
    }

    /// <summary>
    /// 체크포인트 다음 이벤트들을 전역 위치 오름차순으로 읽습니다.
    /// </summary>
    /// <param name="positionExclusive">이미 처리했으므로 결과에서 제외할 마지막 전역 위치입니다.</param>
    /// <param name="cancellationToken">조회 시작 전 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>조회 순간의 원본 끝 위치와 새 이벤트 복사본을 담은 완료 Task를 반환합니다.</returns>
    public Task<GlobalEventBatch> ReadAllAfterAsync(
        long positionExclusive,
        CancellationToken cancellationToken)
    {
        if (positionExclusive < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionExclusive), "체크포인트는 음수일 수 없습니다.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var head = _nextGlobalPosition - 1;
            if (positionExclusive > head)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(positionExclusive),
                    "체크포인트가 이벤트 저장소의 끝 위치보다 클 수 없습니다.");
            }

            // Where는 조건을 만족하는 항목만 고르고, OrderBy는 계약상 전역 순서를 다시 분명히 합니다.
            // =>는 각 항목으로 조건/키를 계산하는 람다 식이며, 짧은 일회성 함수를 LINQ에 전달할 때 사용합니다.
            var copy = _globalEvents
                .Where(envelope => envelope.GlobalPosition > positionExclusive)
                .OrderBy(envelope => envelope.GlobalPosition)
                .ToArray();
            return Task.FromResult(new GlobalEventBatch(head, copy));
        }
    }

    /// <summary>
    /// 예상 버전을 확인한 뒤 명령의 모든 이벤트를 스트림과 전역 로그에 한꺼번에 추가합니다.
    /// </summary>
    /// <param name="streamId">이벤트를 추가할 배포 스트림 식별자입니다.</param>
    /// <param name="expectedVersion">호출자가 읽었던 마지막 버전이며 새 스트림은 0입니다.</param>
    /// <param name="events">원자적으로 함께 저장할 하나 이상의 도메인 이벤트입니다.</param>
    /// <param name="cancellationToken">커밋 전에 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>새 스트림 버전과 마지막 전역 위치를 담은 완료 Task를 반환합니다.</returns>
    public Task<AppendReceipt> AppendAsync(
        string? streamId,
        int expectedVersion,
        IReadOnlyList<DeploymentEvent> events,
        CancellationToken cancellationToken)
    {
        var normalizedStreamId = DomainGuard.Required(streamId, nameof(streamId));
        ArgumentNullException.ThrowIfNull(events);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "예상 버전은 음수일 수 없습니다.");
        }

        if (events.Count == 0)
        {
            throw new ArgumentException("append할 이벤트가 하나 이상 필요합니다.", nameof(events));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var incoming = events.ToArray();
        foreach (var domainEvent in incoming)
        {
            if (domainEvent is null)
            {
                throw new ArgumentException("append 목록에는 null 이벤트가 들어갈 수 없습니다.", nameof(events));
            }

            if (!string.Equals(domainEvent.DeploymentId, normalizedStreamId, StringComparison.Ordinal))
            {
                throw new ArgumentException("모든 이벤트의 배포 ID가 스트림 ID와 같아야 합니다.", nameof(events));
            }
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _streams.TryGetValue(normalizedStreamId, out var existing);
            var actualVersion = existing?.Count ?? 0;
            if (actualVersion != expectedVersion)
            {
                throw new ExpectedVersionConflictException(
                    normalizedStreamId,
                    expectedVersion,
                    actualVersion);
            }

            // 저장 전에 기존 기록과 새 이벤트를 전부 재생합니다. 불가능한 상태 전이라면 아직 컬렉션을 바꾸지 않았으므로 부분 쓰기가 없습니다.
            var candidateHistory = (existing ?? [])
                .Select(envelope => envelope.Event)
                .Concat(incoming)
                .ToArray();
            _ = Deployment.Rehydrate(normalizedStreamId, candidateHistory);

            var prepared = new List<EventEnvelope>(incoming.Length);
            var nextGlobalPosition = _nextGlobalPosition;
            for (var index = 0; index < incoming.Length; index++)
            {
                var streamVersion = checked(actualVersion + index + 1);
                prepared.Add(
                    new EventEnvelope(
                        normalizedStreamId,
                        streamVersion,
                        nextGlobalPosition,
                        incoming[index]));
                nextGlobalPosition = checked(nextGlobalPosition + 1);
            }

            // 취소를 마지막으로 한 번 더 확인한 뒤에만 실제 저장 컬렉션을 변경합니다.
            cancellationToken.ThrowIfCancellationRequested();
            var replacementStream = existing is null
                ? new List<EventEnvelope>(prepared.Count)
                : new List<EventEnvelope>(existing);
            replacementStream.AddRange(prepared);

            _streams[normalizedStreamId] = replacementStream;
            _globalEvents.AddRange(prepared);
            _nextGlobalPosition = nextGlobalPosition;

            // ^1 인덱스는 배열이나 List의 끝에서 첫 번째, 즉 마지막 항목을 뜻합니다.
            var receipt = new AppendReceipt(replacementStream.Count, prepared[^1].GlobalPosition);
            return Task.FromResult(receipt);
        }
    }

    /// <summary>
    /// 현재 전역 이벤트 로그의 마지막 위치를 동기화된 상태에서 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">조회 시작 전 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>로그가 비었으면 0인 현재 끝 위치를 담은 완료 Task를 반환합니다.</returns>
    public Task<long> GetHeadPositionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_nextGlobalPosition - 1);
        }
    }
}

/// <summary>
/// 대시보드가 빠르게 표시할 수 있도록 이벤트를 펼쳐 놓은 배포 한 줄입니다.
/// 쓰기 애그리게이트와 다른 모양을 가져도 된다는 점이 CQRS 읽기 모델의 핵심입니다.
/// </summary>
public sealed record DashboardRow
{
    /// <summary>
    /// 프로젝션된 행의 상태와 마지막 처리 위치가 서로 맞는지 검증합니다.
    /// </summary>
    /// <param name="deploymentId">대시보드 행이 나타내는 배포 식별자입니다.</param>
    /// <param name="environment">배포 대상 환경입니다.</param>
    /// <param name="artifactVersion">배포한 산출물 버전입니다.</param>
    /// <param name="status">마지막 이벤트가 만든 현재 배포 상태입니다.</param>
    /// <param name="failureReason">실패 상태일 때만 존재하는 실패 이유입니다.</param>
    /// <param name="updatedAtUtc">마지막 이벤트가 일어난 UTC 시각입니다.</param>
    /// <param name="lastStreamVersion">이 행에 적용한 마지막 스트림 버전입니다.</param>
    /// <param name="lastGlobalPosition">이 행에 적용한 마지막 전역 위치입니다.</param>
    /// <remarks>변경 불가능한 대시보드 행을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DashboardRow(
        string? deploymentId,
        string? environment,
        string? artifactVersion,
        DeploymentStatus status,
        string? failureReason,
        DateTimeOffset updatedAtUtc,
        int lastStreamVersion,
        long lastGlobalPosition)
    {
        DeploymentId = DomainGuard.Required(deploymentId, nameof(deploymentId));
        Environment = DomainGuard.Required(environment, nameof(environment));
        ArtifactVersion = DomainGuard.Required(artifactVersion, nameof(artifactVersion));

        if (lastStreamVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastStreamVersion));
        }

        if (lastGlobalPosition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastGlobalPosition));
        }

        // Enum.IsDefined는 숫자 강제 변환으로 만든 계약 밖 상태가 읽기 모델에 들어오는 것을 막습니다.
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "정의된 배포 상태만 저장할 수 있습니다.");
        }

        if (lastStreamVersion > lastGlobalPosition)
        {
            throw new ArgumentException(
                "행의 스트림 버전은 전체 로그의 전역 위치를 넘을 수 없습니다.",
                nameof(lastStreamVersion));
        }

        if (status is DeploymentStatus.Failed && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("실패 상태에는 실패 이유가 필요합니다.", nameof(failureReason));
        }

        if (status is not DeploymentStatus.Failed && failureReason is not null)
        {
            throw new ArgumentException("실패가 아닌 상태에는 실패 이유를 둘 수 없습니다.", nameof(failureReason));
        }

        Status = status;
        FailureReason = failureReason?.Trim();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        LastStreamVersion = lastStreamVersion;
        LastGlobalPosition = lastGlobalPosition;
    }

    /// <summary>배포를 유일하게 구분하는 식별자입니다.</summary>
    public string DeploymentId { get; }

    /// <summary>production, staging 같은 배포 대상 환경입니다.</summary>
    public string Environment { get; }

    /// <summary>배포한 산출물 버전입니다.</summary>
    public string ArtifactVersion { get; }

    /// <summary>대시보드에 표시할 최신 배포 상태입니다.</summary>
    public DeploymentStatus Status { get; }

    /// <summary>실패 상태의 원인이며 다른 상태에서는 null입니다.</summary>
    public string? FailureReason { get; }

    /// <summary>마지막 상태 변화가 일어난 UTC 시각입니다.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>이 행에 적용된 마지막 스트림 버전입니다.</summary>
    public int LastStreamVersion { get; }

    /// <summary>이 행에 적용된 마지막 전역 이벤트 위치입니다.</summary>
    public long LastGlobalPosition { get; }
}

/// <summary>읽기 모델 전체 행과 마지막 처리 위치를 한 시점의 값으로 묶습니다.</summary>
public sealed class ReadModelState
{
    private readonly DashboardRow[] _rows;

    /// <summary>
    /// 체크포인트와 행들의 위치 관계를 검사하고 방어적 스냅샷을 만듭니다.
    /// </summary>
    /// <param name="checkpoint">읽기 모델이 연속으로 처리 완료한 마지막 전역 위치입니다.</param>
    /// <param name="rows">현재 대시보드에 저장된 모든 배포 행입니다.</param>
    /// <remarks>읽기 모델 스냅샷을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public ReadModelState(long checkpoint, IReadOnlyList<DashboardRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (checkpoint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        _rows = rows.ToArray();
        if (_rows.Any(row => row is null || row.LastGlobalPosition > checkpoint))
        {
            throw new ArgumentException("행의 마지막 위치는 읽기 모델 체크포인트를 넘을 수 없습니다.", nameof(rows));
        }

        if (_rows.Select(row => row.DeploymentId).Distinct(StringComparer.Ordinal).Count() != _rows.Length)
        {
            throw new ArgumentException("읽기 모델에는 같은 배포 ID의 행을 두 개 둘 수 없습니다.", nameof(rows));
        }

        Checkpoint = checkpoint;
    }

    /// <summary>연속으로 처리 완료한 마지막 전역 이벤트 위치입니다.</summary>
    public long Checkpoint { get; }

    /// <summary>호출자가 내부 배열을 바꿀 수 없도록 복사해서 제공하는 대시보드 행들입니다.</summary>
    public IReadOnlyList<DashboardRow> Rows => _rows.ToArray();

    /// <summary>
    /// 배포 ID로 현재 행을 찾습니다.
    /// </summary>
    /// <param name="deploymentId">찾으려는 배포 식별자입니다.</param>
    /// <returns>행이 있으면 그 불변 값, 아직 투영되지 않았으면 null을 반환합니다.</returns>
    public DashboardRow? Find(string? deploymentId)
    {
        var normalizedId = DomainGuard.Required(deploymentId, nameof(deploymentId));
        return _rows.SingleOrDefault(
            row => string.Equals(row.DeploymentId, normalizedId, StringComparison.Ordinal));
    }
}

/// <summary>프로젝션 쓰기 시도가 새 이벤트 적용, 중복 확인, 경쟁 재시도 중 무엇이었는지 나타냅니다.</summary>
public enum ProjectionApplyOutcome
{
    /// <summary>새 이벤트가 행과 체크포인트에 원자적으로 반영되었습니다.</summary>
    Applied,

    /// <summary>완전히 같은 이벤트가 이미 처리되어 상태를 바꾸지 않았습니다.</summary>
    Duplicate,

    /// <summary>다른 프로젝터가 체크포인트를 먼저 바꾸어 최신 상태로 다시 계산해야 합니다.</summary>
    Retry,
}

/// <summary>
/// Query Service가 갱신 권한 없이 체크포인트와 대시보드 행 snapshot만 읽게 하는 좁은 포트입니다.
/// </summary>
public interface IDeploymentDashboardReader
{
    /// <summary>
    /// 현재 체크포인트와 모든 대시보드 행을 일관된 스냅샷으로 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">조회 시작 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>내부 컬렉션과 분리된 읽기 모델 상태를 비동기로 반환합니다.</returns>
    Task<ReadModelState> ReadStateAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 프로젝터용 쓰기 포트입니다. 조회 서비스에는 이 인터페이스를 주입하지 않아 TryApply 권한을 노출하지 않습니다.
/// </summary>
public interface IDeploymentReadModel : IDeploymentDashboardReader
{
    /// <summary>
    /// 체크포인트가 예상값일 때 이벤트와 계산된 행을 함께 반영하거나, 동일 재전달을 무시합니다.
    /// </summary>
    /// <param name="expectedCheckpoint">프로젝터가 행을 계산할 때 읽었던 체크포인트입니다.</param>
    /// <param name="envelope">처리하거나 중복 확인할 이벤트 봉투입니다.</param>
    /// <param name="proposedRow">새 이벤트일 때 저장할 행이며 중복 확인 때는 null이어도 됩니다.</param>
    /// <param name="cancellationToken">커밋 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>적용, 중복, 경쟁 재시도 중 하나를 비동기로 반환합니다.</returns>
    Task<ProjectionApplyOutcome> TryApplyAsync(
        long expectedCheckpoint,
        EventEnvelope envelope,
        DashboardRow? proposedRow,
        CancellationToken cancellationToken);
}

/// <summary>
/// 프로세스가 살아 있는 동안 체크포인트와 대시보드 행을 계속 보존하는 메모리 읽기 모델입니다.
/// 이벤트 계약도 위치별로 기억하므로 같은 위치의 변조된 재전달을 단순 중복으로 숨기지 않습니다.
/// </summary>
public sealed class InMemoryDeploymentReadModel : IDeploymentReadModel
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DashboardRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<long, EventEnvelope> _processedEvents = [];
    private long _checkpoint;

    /// <summary>
    /// 행이 없고 체크포인트가 0인 빈 대시보드 읽기 모델을 만듭니다.
    /// </summary>
    /// <remarks>메모리 컬렉션을 초기 상태로 준비하며, 매개변수와 반환값은 없습니다.</remarks>
    public InMemoryDeploymentReadModel()
    {
    }

    /// <summary>
    /// 잠금 안에서 체크포인트와 정렬된 행의 방어적 스냅샷을 만듭니다.
    /// </summary>
    /// <param name="cancellationToken">조회 시작 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>호출자가 바꿔도 내부 상태에 영향이 없는 완료 Task를 반환합니다.</returns>
    public Task<ReadModelState> ReadStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = _rows.Values
                .OrderBy(row => row.DeploymentId, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(new ReadModelState(_checkpoint, rows));
        }
    }

    /// <summary>
    /// 같은 이벤트 재전달은 무시하고, 다음 위치의 새 행과 체크포인트는 한 잠금 안에서 함께 저장합니다.
    /// </summary>
    /// <param name="expectedCheckpoint">행 계산에 사용한 읽기 모델 체크포인트입니다.</param>
    /// <param name="envelope">반영할 이벤트 또는 이미 처리한 중복 후보입니다.</param>
    /// <param name="proposedRow">새 이벤트가 만든 행이며 중복 확인에서는 사용하지 않습니다.</param>
    /// <param name="cancellationToken">실제 변경 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>적용, 동일 중복, 체크포인트 경쟁 중 하나를 담은 완료 Task를 반환합니다.</returns>
    public Task<ProjectionApplyOutcome> TryApplyAsync(
        long expectedCheckpoint,
        EventEnvelope envelope,
        DashboardRow? proposedRow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (expectedCheckpoint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCheckpoint));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (envelope.GlobalPosition <= _checkpoint)
            {
                if (!_processedEvents.TryGetValue(envelope.GlobalPosition, out var processed)
                    || !processed.HasSameContract(envelope))
                {
                    throw new InvalidDataException(
                        "이미 처리한 전역 위치에 서로 다른 이벤트 계약이 도착했습니다.");
                }

                return Task.FromResult(ProjectionApplyOutcome.Duplicate);
            }

            if (expectedCheckpoint != _checkpoint)
            {
                return Task.FromResult(ProjectionApplyOutcome.Retry);
            }

            if (envelope.GlobalPosition != checked(_checkpoint + 1))
            {
                throw new InvalidDataException("프로젝션 이벤트의 전역 위치에 빈 구간이 있습니다.");
            }

            if (proposedRow is null)
            {
                throw new ArgumentNullException(nameof(proposedRow), "새 이벤트에는 저장할 대시보드 행이 필요합니다.");
            }

            if (!string.Equals(proposedRow.DeploymentId, envelope.StreamId, StringComparison.Ordinal)
                || proposedRow.LastStreamVersion != envelope.StreamVersion
                || proposedRow.LastGlobalPosition != envelope.GlobalPosition)
            {
                throw new InvalidDataException("제안된 행의 식별자와 마지막 위치가 이벤트 봉투와 다릅니다.");
            }

            _rows[proposedRow.DeploymentId] = proposedRow;
            _processedEvents.Add(envelope.GlobalPosition, envelope);
            _checkpoint = envelope.GlobalPosition;
            return Task.FromResult(ProjectionApplyOutcome.Applied);
        }
    }
}

/// <summary>
/// 도메인 이벤트 하나를 조회에 최적화된 <see cref="DashboardRow"/>로 바꾸는 순수 프로젝션 규칙입니다.
/// 저장 책임이 없어서 같은 입력에 같은 출력을 내고, 재생 테스트가 쉽습니다.
/// 현재 선택 가능한 규칙은 하나뿐이라 Strategy 인터페이스를 미리 만들지 않았고, 실제 교체 요구가 생기면 Port로 추출할 수 있습니다.
/// </summary>
public sealed class DeploymentDashboardProjection
{
    /// <summary>
    /// 상태를 따로 갖지 않는 배포 대시보드 프로젝션 규칙 객체를 만듭니다.
    /// </summary>
    /// <remarks>주입받을 값이 없는 생성자이며 별도 반환값은 없습니다.</remarks>
    public DeploymentDashboardProjection()
    {
    }

    /// <summary>
    /// 기존 행과 다음 이벤트를 검증한 뒤 새 불변 행을 계산합니다.
    /// </summary>
    /// <param name="current">이 배포의 현재 행이며 등록 이벤트 전에는 null입니다.</param>
    /// <param name="envelope">전역 순서로 처리할 다음 이벤트 봉투입니다.</param>
    /// <returns>이벤트 내용과 위치가 반영된 새 DashboardRow를 반환합니다.</returns>
    public DashboardRow Project(DashboardRow? current, EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Event is DeploymentRegistered registered)
        {
            if (current is not null || envelope.StreamVersion != 1)
            {
                throw new InvalidDataException("등록 이벤트는 빈 행의 스트림 버전 1이어야 합니다.");
            }

            return new DashboardRow(
                registered.DeploymentId,
                registered.Environment,
                registered.ArtifactVersion,
                DeploymentStatus.Registered,
                null,
                registered.OccurredAtUtc,
                envelope.StreamVersion,
                envelope.GlobalPosition);
        }

        if (current is null)
        {
            throw new InvalidDataException("등록 이벤트보다 먼저 상태 변경 이벤트를 투영할 수 없습니다.");
        }

        if (!string.Equals(current.DeploymentId, envelope.StreamId, StringComparison.Ordinal)
            || envelope.StreamVersion != checked(current.LastStreamVersion + 1)
            || envelope.Event.OccurredAtUtc < current.UpdatedAtUtc)
        {
            throw new InvalidDataException("대시보드 행과 다음 이벤트의 식별자, 버전 또는 시각이 이어지지 않습니다.");
        }

        // switch 식은 이벤트 타입별로 만들 새 값을 오른쪽에서 바로 선택합니다.
        return envelope.Event switch
        {
            DeploymentStarted when current.Status is DeploymentStatus.Registered
                => Advance(current, DeploymentStatus.Running, null, envelope),
            DeploymentSucceeded when current.Status is DeploymentStatus.Running
                => Advance(current, DeploymentStatus.Succeeded, null, envelope),
            DeploymentFailed failed when current.Status is DeploymentStatus.Running
                => Advance(current, DeploymentStatus.Failed, failed.Reason, envelope),
            _ => throw new InvalidDataException(
                $"이벤트 '{envelope.Event.GetType().Name}'를 상태 '{current.Status}'에 투영할 수 없습니다."),
        };
    }

    /// <summary>
    /// 변하지 않는 식별 정보는 유지하고 상태와 마지막 위치만 갱신한 새 행을 만듭니다.
    /// </summary>
    /// <param name="current">이벤트 적용 전의 대시보드 행입니다.</param>
    /// <param name="status">이벤트 적용 뒤의 새 상태입니다.</param>
    /// <param name="failureReason">실패 이벤트면 이유, 아니면 null입니다.</param>
    /// <param name="envelope">새 시각과 버전, 전역 위치를 제공하는 이벤트 봉투입니다.</param>
    /// <returns>원본을 바꾸지 않고 다음 상태를 나타내는 새 행을 반환합니다.</returns>
    private static DashboardRow Advance(
        DashboardRow current,
        DeploymentStatus status,
        string? failureReason,
        EventEnvelope envelope)
    {
        return new DashboardRow(
            current.DeploymentId,
            current.Environment,
            current.ArtifactVersion,
            status,
            failureReason,
            envelope.Event.OccurredAtUtc,
            envelope.StreamVersion,
            envelope.GlobalPosition);
    }
}

/// <summary>프로젝터 한 번의 실행이 처리한 수와 원본 대비 지연을 명시적으로 보여 줍니다.</summary>
public sealed record ProjectionRunReport
{
    /// <summary>
    /// 프로젝션 실행 전후 위치와 처리 수를 검증해 보고서를 만듭니다.
    /// </summary>
    /// <param name="startingCheckpoint">실행을 시작할 때 읽기 모델 체크포인트입니다.</param>
    /// <param name="endingCheckpoint">실행이 끝났을 때 읽기 모델 체크포인트입니다.</param>
    /// <param name="sourceHeadPosition">배치를 읽었을 때 원본 이벤트 로그의 끝 위치입니다.</param>
    /// <param name="appliedCount">새로 행에 적용한 이벤트 수입니다.</param>
    /// <param name="duplicateCount">동일 재전달이라 변경 없이 무시한 이벤트 수입니다.</param>
    /// <remarks>한 실행의 관측 보고서를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public ProjectionRunReport(
        long startingCheckpoint,
        long endingCheckpoint,
        long sourceHeadPosition,
        int appliedCount,
        int duplicateCount)
    {
        if (startingCheckpoint < 0
            || endingCheckpoint < startingCheckpoint
            || sourceHeadPosition < 0
            || appliedCount < 0
            || duplicateCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingCheckpoint), "프로젝션 보고서 값이 유효하지 않습니다.");
        }

        StartingCheckpoint = startingCheckpoint;
        EndingCheckpoint = endingCheckpoint;
        SourceHeadPosition = sourceHeadPosition;
        AppliedCount = appliedCount;
        DuplicateCount = duplicateCount;
    }

    /// <summary>실행 시작 시 읽기 모델 체크포인트입니다.</summary>
    public long StartingCheckpoint { get; }

    /// <summary>실행 종료 시 읽기 모델 체크포인트입니다.</summary>
    public long EndingCheckpoint { get; }

    /// <summary>배치를 읽을 당시 원본 이벤트 로그의 끝 위치입니다.</summary>
    public long SourceHeadPosition { get; }

    /// <summary>이번 실행에서 새로 반영한 이벤트 수입니다.</summary>
    public int AppliedCount { get; }

    /// <summary>이미 같은 계약으로 처리되어 안전하게 무시한 이벤트 수입니다.</summary>
    public int DuplicateCount { get; }

    /// <summary>원본 끝과 최종 체크포인트 차이이며 0이면 따라잡은 상태입니다.</summary>
    public long RemainingLag => Math.Max(0, SourceHeadPosition - EndingCheckpoint);
}

/// <summary>
/// 전역 이벤트를 읽어 순수 프로젝션으로 행을 계산하고 영속 체크포인트와 함께 저장합니다.
/// 이벤트 저장소와 읽기 모델 사이를 잇는 이 구성 요소가 eventual consistency의 지연을 줄입니다.
/// </summary>
public sealed class DeploymentProjectionRunner
{
    private readonly IDeploymentEventStore _eventStore;
    private readonly IDeploymentReadModel _readModel;
    private readonly DeploymentDashboardProjection _projection;

    /// <summary>
    /// 원본 이벤트, 대상 읽기 모델, 순수 변환 규칙을 각각 주입받습니다.
    /// </summary>
    /// <param name="eventStore">전역 이벤트 배치를 읽을 원본 저장소입니다.</param>
    /// <param name="readModel">행과 체크포인트를 원자적으로 보존할 대상입니다.</param>
    /// <param name="projection">이벤트를 DashboardRow로 변환하는 규칙입니다.</param>
    /// <remarks>프로젝션 실행기의 의존성을 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentProjectionRunner(
        IDeploymentEventStore eventStore,
        IDeploymentReadModel readModel,
        DeploymentDashboardProjection projection)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    /// <summary>
    /// 저장된 체크포인트 다음 이벤트를 한 번 읽고 가능한 만큼 따라잡습니다.
    /// </summary>
    /// <param name="cancellationToken">읽기 또는 각 이벤트 적용을 중단할 취소 신호입니다.</param>
    /// <returns>적용 수, 최종 체크포인트, 남은 지연을 비동기로 반환합니다.</returns>
    public async Task<ProjectionRunReport> RunOnceAsync(CancellationToken cancellationToken)
    {
        var state = await _readModel.ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var batch = await _eventStore
            .ReadAllAfterAsync(state.Checkpoint, cancellationToken)
            .ConfigureAwait(false);
        return await ProjectBatchAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 주어진 전역 배치를 순서대로 투영하며 동일 이벤트 재전달을 멱등하게 무시합니다.
    /// </summary>
    /// <param name="batch">원본 끝 위치와 전역 순서 이벤트를 가진 배치입니다.</param>
    /// <param name="cancellationToken">각 이벤트 처리 전에 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>새 적용 수와 중복 수, 명시적인 남은 지연을 비동기로 반환합니다.</returns>
    public async Task<ProjectionRunReport> ProjectBatchAsync(
        GlobalEventBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var initial = await _readModel.ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var appliedCount = 0;
        var duplicateCount = 0;

        foreach (var envelope in batch.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 다른 Runner가 같은 읽기 모델을 갱신했다면 최신 체크포인트로 다시 계산합니다.
            while (true)
            {
                var state = await _readModel.ReadStateAsync(cancellationToken).ConfigureAwait(false);
                if (envelope.GlobalPosition <= state.Checkpoint)
                {
                    var duplicateOutcome = await _readModel
                        .TryApplyAsync(state.Checkpoint, envelope, null, cancellationToken)
                        .ConfigureAwait(false);
                    if (duplicateOutcome is not ProjectionApplyOutcome.Duplicate)
                    {
                        throw new InvalidOperationException("처리 완료 위치는 반드시 중복으로 확인되어야 합니다.");
                    }

                    duplicateCount++;
                    break;
                }

                var current = state.Find(envelope.StreamId);
                var proposed = _projection.Project(current, envelope);
                var outcome = await _readModel
                    .TryApplyAsync(state.Checkpoint, envelope, proposed, cancellationToken)
                    .ConfigureAwait(false);

                if (outcome is ProjectionApplyOutcome.Retry)
                {
                    continue;
                }

                if (outcome is ProjectionApplyOutcome.Applied)
                {
                    appliedCount++;
                }
                else
                {
                    duplicateCount++;
                }

                break;
            }
        }

        var finalState = await _readModel.ReadStateAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionRunReport(
            initial.Checkpoint,
            finalState.Checkpoint,
            batch.SourceHeadPosition,
            appliedCount,
            duplicateCount);
    }
}

/// <summary>대시보드 행들과 원본 이벤트 대비 지연을 함께 보여 주는 조회 결과입니다.</summary>
public sealed class DashboardView
{
    private readonly DashboardRow[] _rows;

    /// <summary>
    /// 조회 행과 두 위치를 검증하여 지연을 계산할 수 있는 불변 화면 모델을 만듭니다.
    /// </summary>
    /// <param name="rows">배포 ID 순으로 표시할 대시보드 행입니다.</param>
    /// <param name="checkpoint">읽기 모델이 처리 완료한 마지막 전역 위치입니다.</param>
    /// <param name="sourceHeadPosition">조회 시점 원본 이벤트 로그의 마지막 위치입니다.</param>
    /// <remarks>대시보드 조회 결과를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DashboardView(
        IReadOnlyList<DashboardRow> rows,
        long checkpoint,
        long sourceHeadPosition)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (checkpoint < 0 || sourceHeadPosition < checkpoint)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "체크포인트는 0 이상이고 원본 끝을 넘을 수 없습니다.");
        }

        _rows = rows.OrderBy(row => row.DeploymentId, StringComparer.Ordinal).ToArray();
        Checkpoint = checkpoint;
        SourceHeadPosition = sourceHeadPosition;
    }

    /// <summary>호출자가 내부 배열을 바꾸지 못하도록 복사해서 제공하는 배포 행들입니다.</summary>
    public IReadOnlyList<DashboardRow> Rows => _rows.ToArray();

    /// <summary>읽기 모델이 마지막으로 처리한 전역 이벤트 위치입니다.</summary>
    public long Checkpoint { get; }

    /// <summary>조회 시점 쓰기 모델 이벤트 로그의 마지막 위치입니다.</summary>
    public long SourceHeadPosition { get; }

    /// <summary>아직 읽기 모델에 반영되지 않은 이벤트 수입니다.</summary>
    public long Lag => SourceHeadPosition - Checkpoint;
}

/// <summary>
/// CQRS의 조회 경로로서 도메인 애그리게이트를 복원하지 않고 대시보드 읽기 모델을 반환합니다.
/// 이벤트 저장소에서는 행 데이터가 아니라 지연 계산용 끝 위치만 읽습니다.
/// </summary>
public sealed class DeploymentQueryService
{
    private readonly IDeploymentDashboardReader _readModel;
    private readonly IEventLogPositionReader _eventLogPositionReader;

    /// <summary>
    /// 화면 데이터의 읽기 모델과 지연 기준 위치를 제공할 이벤트 저장소를 주입받습니다.
    /// </summary>
    /// <param name="readModel">대시보드 행과 체크포인트를 읽을 모델입니다.</param>
    /// <param name="eventLogPositionReader">원본 이벤트의 끝 위치만 읽도록 권한을 좁힌 조회 포트입니다.</param>
    /// <remarks>조회 서비스의 의존성을 보관하는 생성자이므로 별도 반환값은 없습니다.</remarks>
    public DeploymentQueryService(
        IDeploymentDashboardReader readModel,
        IEventLogPositionReader eventLogPositionReader)
    {
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
        _eventLogPositionReader = eventLogPositionReader
            ?? throw new ArgumentNullException(nameof(eventLogPositionReader));
    }

    /// <summary>
    /// 현재 대시보드 행과 원본 대비 프로젝션 지연을 조회합니다.
    /// </summary>
    /// <param name="cancellationToken">두 조회를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>정렬된 행, 체크포인트, 원본 끝 위치, 지연을 비동기로 반환합니다.</returns>
    public async Task<DashboardView> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var state = await _readModel.ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var head = await _eventLogPositionReader
            .GetHeadPositionAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DashboardView(state.Rows, state.Checkpoint, head);
    }
}
