using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DeploymentSseApi.Application.Ports;
using DeploymentSseApi.Domain;

namespace DeploymentSseApi.Infrastructure;

/// <summary>
/// 학습용 메모리 Event 저장소이자 SSE fan-out Adapter입니다.
/// 하나의 lock으로 replay snapshot과 live 구독 등록을 원자적으로 묶어 경계 순간의 Event가 빠지지 않게 합니다.
/// 각 구독자는 bounded Channel을 가져 느린 클라이언트가 서버 메모리를 무한히 늘리지 못하게 합니다.
/// </summary>
public sealed class InMemoryDeploymentProgressRepository : IDeploymentProgressRepository
{
    public const int DefaultSubscriberCapacity = 16;

    // target-typed new는 왼쪽 선언 형식에서 object와 Dictionary 형식을 추론해 중복 표기를 줄입니다.
    private readonly object _gate = new();
    private readonly Dictionary<string, DeploymentState> _deployments =
        new(StringComparer.Ordinal);
    private readonly int _subscriberCapacity;
    private long _nextSubscriberId;

    /// <summary>
    /// 구독자별 live 버퍼 크기를 정하고 빈 메모리 저장소를 초기화합니다.
    /// </summary>
    /// <param name="subscriberCapacity">느린 소비자 한 명에게 보관할 최대 live Event 수이며 1 이상이어야 합니다.</param>
    /// <returns>생성자는 Adapter를 초기화하므로 별도 반환값은 없습니다.</returns>
    public InMemoryDeploymentProgressRepository(
        int subscriberCapacity = DefaultSubscriberCapacity)
    {
        if (subscriberCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(subscriberCapacity),
                "구독자 Channel 용량은 1 이상이어야 합니다.");
        }

        _subscriberCapacity = subscriberCapacity;
    }

    /// <summary>
    /// 테스트와 관찰용으로 현재 live 구독자 수를 thread-safe하게 계산합니다.
    /// Port에는 넣지 않아 운영 use case가 진단용 구현 세부사항에 의존하지 않게 합니다.
    /// </summary>
    public int ActiveSubscriberCount
    {
        get
        {
            lock (_gate)
            {
                return _deployments.Values.Sum(state => state.Subscribers.Count);
            }
        }
    }

    /// <summary>
    /// 정규화된 ID로 마지막 불변 Event snapshot을 조회합니다.
    /// </summary>
    /// <param name="deploymentId">검증과 정규화를 마친 저장소 key입니다.</param>
    /// <param name="cancellationToken">lock 진입 전에 조회를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>최신 Event 또는 아직 기록이 없음을 뜻하는 null이 즉시 든 ValueTask를 반환합니다.</returns>
    public ValueTask<DeploymentProgressEvent?> GetLatestAsync(
        string deploymentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_deployments.TryGetValue(deploymentId, out var state) ||
                state.Events.Count == 0)
            {
                return ValueTask.FromResult<DeploymentProgressEvent?>(null);
            }

            // ^1은 컬렉션의 끝에서 첫 번째, 즉 마지막 원소를 가리키는 index-from-end 문법입니다.
            return ValueTask.FromResult<DeploymentProgressEvent?>(state.Events[^1]);
        }
    }

    /// <summary>
    /// 기대한 EventId 확인, 저장, live fan-out을 한 lock 안에서 수행해 lost update와 replay 경계 누락을 막습니다.
    /// </summary>
    /// <param name="progressEvent">Strategy가 만든 다음 불변 Event입니다.</param>
    /// <param name="expectedLastEventId">호출자가 마지막으로 읽은 EventId이며 첫 추가이면 0입니다.</param>
    /// <param name="cancellationToken">lock 진입 전에 추가를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>추가 성공 또는 최신 값이 달라졌다는 충돌 결과가 즉시 든 ValueTask를 반환합니다.</returns>
    public ValueTask<DeploymentProgressAppendResult> TryAppendAsync(
        DeploymentProgressEvent progressEvent,
        long expectedLastEventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progressEvent);
        if (expectedLastEventId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedLastEventId),
                "기대한 마지막 EventId는 0 이상이어야 합니다.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _deployments.TryGetValue(progressEvent.DeploymentId, out var state);
            var currentLastEventId = state is null || state.Events.Count == 0
                ? 0
                : state.Events[^1].EventId;

            if (currentLastEventId != expectedLastEventId)
            {
                return ValueTask.FromResult(
                    new DeploymentProgressAppendResult(
                        DeploymentProgressAppendStatus.Conflict));
            }

            if (progressEvent.EventId != checked(expectedLastEventId + 1))
            {
                throw new InvalidOperationException(
                    "Strategy가 만든 EventId는 기대한 마지막 EventId보다 정확히 1 커야 합니다.");
            }

            if (state is not null &&
                state.Events.Count > 0 &&
                state.Events[^1].IsTerminal)
            {
                throw new InvalidOperationException(
                    "종료 Event 뒤에는 새 Event를 저장할 수 없습니다.");
            }

            if (state is null)
            {
                state = new DeploymentState();
                _deployments.Add(progressEvent.DeploymentId, state);
            }

            state.Events.Add(progressEvent);

            // ToArray snapshot은 순회 도중 느린 구독자를 Dictionary에서 제거해도 열거자가 깨지지 않게 합니다.
            foreach (var pair in state.Subscribers.ToArray())
            {
                if (!pair.Value.Channel.Writer.TryWrite(progressEvent))
                {
                    state.Subscribers.Remove(pair.Key);
                    pair.Value.Channel.Writer.TryComplete(
                        new InvalidOperationException(
                            "SSE 소비 속도가 bounded Channel 용량보다 느려 연결을 종료합니다."));
                    // Unregister는 실행 중인 callback을 기다리지 않아 lock 안에서 교착 상태를 만들지 않습니다.
                    pair.Value.CancellationRegistration.Unregister();
                }
            }

            if (progressEvent.IsTerminal)
            {
                // terminal Event는 먼저 Channel에 들어갔으므로 Reader가 이를 모두 읽은 다음 자연스럽게 끝납니다.
                foreach (var subscriber in state.Subscribers.Values)
                {
                    subscriber.Channel.Writer.TryComplete();
                    subscriber.CancellationRegistration.Unregister();
                }

                state.Subscribers.Clear();
            }

            return ValueTask.FromResult(
                new DeploymentProgressAppendResult(
                    DeploymentProgressAppendStatus.Appended));
        }
    }

    /// <summary>
    /// lock 안에서 replay 배열을 복사하고 live Channel을 등록하여 두 구간 사이에 빈틈이 없는 stream을 만듭니다.
    /// </summary>
    /// <param name="deploymentId">검증과 정규화를 마친 저장소 key입니다.</param>
    /// <param name="afterEventId">클라이언트가 이미 받은 마지막 EventId이며 처음이면 0입니다.</param>
    /// <param name="cancellationToken">취소 시 등록한 구독자와 Channel을 즉시 정리할 신호입니다.</param>
    /// <returns>EventId 순서의 replay+live stream, 또는 미존재를 뜻하는 null이 즉시 든 ValueTask를 반환합니다.</returns>
    public ValueTask<DeploymentProgressStream?> OpenStreamAsync(
        string deploymentId,
        long afterEventId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (afterEventId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(afterEventId),
                "재생 기준 EventId는 0 이상이어야 합니다.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_deployments.TryGetValue(deploymentId, out var state) ||
                state.Events.Count == 0)
            {
                return ValueTask.FromResult<DeploymentProgressStream?>(null);
            }

            // LINQ의 Where와 ToArray는 lock 시점에 존재한 누락 Event만 별도 snapshot으로 고정합니다.
            var replay = state.Events
                .Where(progressEvent => progressEvent.EventId > afterEventId)
                .ToArray();
            var channel = CreateSubscriberChannel();

            if (state.Events[^1].IsTerminal)
            {
                channel.Writer.TryComplete();
                var completedStream = new DeploymentProgressStream(
                    ReadSubscribedEventsAsync(
                        replay,
                        channel.Reader,
                        deploymentId,
                        afterEventId,
                        subscriptionId: 0,
                        cancellationToken));
                return ValueTask.FromResult<DeploymentProgressStream?>(completedStream);
            }

            var subscriptionId = checked(++_nextSubscriberId);
            var subscriber = new Subscriber(channel);
            state.Subscribers.Add(subscriptionId, subscriber);

            // Register의 lambda는 요청 취소 시 repository와 구독 key를 기억했다가 Channel을 완료하고 Dictionary에서 제거합니다.
            subscriber.CancellationRegistration = cancellationToken.Register(
                () => CancelSubscription(
                    deploymentId,
                    subscriptionId,
                    cancellationToken));

            // 이미 취소된 token은 Register 호출 중 callback을 동기 실행할 수 있습니다. 반환된 등록도 즉시 해제해 참조가 남지 않게 합니다.
            if (cancellationToken.IsCancellationRequested)
            {
                subscriber.CancellationRegistration.Unregister();
            }

            var stream = new DeploymentProgressStream(
                ReadSubscribedEventsAsync(
                    replay,
                    channel.Reader,
                    deploymentId,
                    afterEventId,
                    subscriptionId,
                    cancellationToken));
            return ValueTask.FromResult<DeploymentProgressStream?>(stream);
        }
    }

    /// <summary>
    /// 설정한 용량을 넘으면 대기 모드로 들어가는 구독자별 bounded Channel을 만듭니다.
    /// Repository는 lock 안에서 기다릴 수 없으므로 TryWrite 실패 시 해당 느린 구독만 종료합니다.
    /// </summary>
    /// <returns>단일 Reader와 논리적으로 직렬화된 Writer에 최적화한 새 Channel을 반환합니다.</returns>
    private Channel<DeploymentProgressEvent> CreateSubscriberChannel()
    {
        return Channel.CreateBounded<DeploymentProgressEvent>(
            new BoundedChannelOptions(_subscriberCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });
    }

    /// <summary>
    /// 고정 replay를 먼저 내보내고 같은 lock 경계 뒤 Channel에 들어온 live Event를 이어서 내보냅니다.
    /// </summary>
    /// <param name="replay">stream을 연 순간 afterEventId 뒤에 있던 Event snapshot입니다.</param>
    /// <param name="reader">이후 Event를 순서대로 받는 구독자 전용 Channel Reader입니다.</param>
    /// <param name="deploymentId">열거가 조기 종료될 때 구독자를 찾을 저장소 key입니다.</param>
    /// <param name="afterEventId">미래 EventId가 들어와도 그보다 작은 live Event를 보내지 않을 하한입니다.</param>
    /// <param name="subscriptionId">live 구독 key이며 terminal replay 전용 stream이면 0입니다.</param>
    /// <param name="cancellationToken">열거와 HTTP 연결 종료를 함께 중단할 신호입니다.</param>
    /// <returns>replay와 live Event를 EventId 순서로 한 건씩 비동기 반환하는 열거를 반환합니다.</returns>
    private async IAsyncEnumerable<DeploymentProgressEvent> ReadSubscribedEventsAsync(
        IReadOnlyList<DeploymentProgressEvent> replay,
        ChannelReader<DeploymentProgressEvent> reader,
        string deploymentId,
        long afterEventId,
        long subscriptionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            foreach (var progressEvent in replay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // yield return은 전체 목록을 한꺼번에 만들지 않고 호출자가 다음 항목을 요청할 때 한 건만 전달합니다.
                yield return progressEvent;
            }

            // await foreach는 Channel에 값이 없을 때 thread를 막지 않고 기다리며, 완료되면 반복을 자연스럽게 끝냅니다.
            await foreach (var progressEvent in reader.ReadAllAsync(cancellationToken))
            {
                // 다른 stream에서 온 미래 Last-Event-ID여도 그것보다 작은 live ID를 보내지 않아 "이 ID 뒤부터" 계약을 지킵니다.
                if (progressEvent.EventId > afterEventId)
                {
                    yield return progressEvent;
                }
            }
        }
        finally
        {
            // 소비자가 break로 일찍 나가도 async iterator의 finally가 실행되어 live 구독이 남지 않습니다.
            if (subscriptionId != 0)
            {
                RemoveSubscription(deploymentId, subscriptionId);
            }
        }
    }

    /// <summary>
    /// 호출자 취소 callback에서 구독자를 원자적으로 제거하고 대기 중 Reader에 취소 완료를 알립니다.
    /// </summary>
    /// <param name="deploymentId">구독이 속한 배포 저장소 key입니다.</param>
    /// <param name="subscriptionId">제거할 live 구독 key입니다.</param>
    /// <param name="cancellationToken">OperationCanceledException에 연결할 원래 취소 신호입니다.</param>
    /// <returns>반환값 없이 구독과 Channel 수명만 종료합니다.</returns>
    private void CancelSubscription(
        string deploymentId,
        long subscriptionId,
        CancellationToken cancellationToken)
    {
        Subscriber? removed = null;

        lock (_gate)
        {
            if (_deployments.TryGetValue(deploymentId, out var state) &&
                state.Subscribers.Remove(subscriptionId, out var subscriber))
            {
                removed = subscriber;
            }
        }

        if (removed is not null)
        {
            removed.Channel.Writer.TryComplete(
                new OperationCanceledException(cancellationToken));
            removed.CancellationRegistration.Unregister();
        }
    }

    /// <summary>
    /// 소비자가 취소 없이 열거를 그만둔 경우에도 live 구독자를 제거하고 Channel을 정상 완료합니다.
    /// </summary>
    /// <param name="deploymentId">구독이 속한 배포 저장소 key입니다.</param>
    /// <param name="subscriptionId">제거할 live 구독 key입니다.</param>
    /// <returns>반환값 없이 구독과 Channel 수명만 종료합니다.</returns>
    private void RemoveSubscription(string deploymentId, long subscriptionId)
    {
        Subscriber? removed = null;

        lock (_gate)
        {
            if (_deployments.TryGetValue(deploymentId, out var state) &&
                state.Subscribers.Remove(subscriptionId, out var subscriber))
            {
                removed = subscriber;
            }
        }

        if (removed is not null)
        {
            removed.Channel.Writer.TryComplete();
            removed.CancellationRegistration.Unregister();
        }
    }

    /// <summary>
    /// 배포 하나의 append-only Event 기록과 현재 live 구독자를 같은 lock 아래 묶습니다.
    /// </summary>
    private sealed class DeploymentState
    {
        // []는 C# collection expression으로, 선언된 List/Dictionary 형식에 맞는 빈 컬렉션을 간결하게 만듭니다.
        public List<DeploymentProgressEvent> Events { get; } = [];

        public Dictionary<long, Subscriber> Subscribers { get; } = [];
    }

    /// <summary>
    /// 구독자 한 명의 bounded Channel과 취소 callback 등록을 함께 보관합니다.
    /// </summary>
    private sealed class Subscriber
    {
        /// <summary>
        /// live Event를 전달할 Channel을 받아 구독자 상태를 초기화합니다.
        /// </summary>
        /// <param name="channel">이 구독자만 사용하는 bounded Channel입니다.</param>
        /// <returns>생성자는 구독자 상태를 초기화하므로 별도 반환값은 없습니다.</returns>
        public Subscriber(Channel<DeploymentProgressEvent> channel)
        {
            Channel = channel;
        }

        public Channel<DeploymentProgressEvent> Channel { get; }

        public CancellationTokenRegistration CancellationRegistration { get; set; }
    }
}
