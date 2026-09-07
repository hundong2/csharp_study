using System.Text.Json;

namespace DailyStudy.TransactionalOutbox;

/// <summary>
/// 주문 이벤트를 <see cref="System.Text.Json"/>으로 직렬화하는 Adapter입니다.
/// 외부 라이브러리 선택을 이 클래스에 가둬 Application 계층을 JSON 세부사항에서 분리합니다.
/// </summary>
public sealed class JsonOrderPlacedSerializer : IOrderPlacedSerializer
{
    /// <summary>
    /// 주문 생성 이벤트의 공개 속성을 JSON 문자열로 변환합니다.
    /// </summary>
    /// <param name="domainEvent">직렬화할 주문 생성 이벤트입니다.</param>
    /// <returns>Outbox 저장소에 넣을 JSON 문자열을 반환합니다.</returns>
    public string Serialize(OrderPlaced domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return JsonSerializer.Serialize(domainEvent);
    }
}

/// <summary>
/// 한 프로세스 안에서 주문과 Outbox 메시지를 함께 저장하는 교육용 DB 역할을 합니다.
/// 실제 운영에서는 같은 관계형 DB 트랜잭션으로 두 INSERT를 커밋해야 합니다.
/// </summary>
public sealed class InMemoryOutboxDatabase : IOrderUnitOfWork, IOutboxRepository
{
    private readonly object _gate = new();
    private StoreState _state = StoreState.Empty();
    private bool _failNextCommit;
    private bool _failNextMarkPublished;

    /// <summary>
    /// 주문과 메시지 Dictionary를 하나로 묶은 교체 가능한 저장소 스냅샷입니다.
    /// </summary>
    /// <param name="Orders">주문 ID별 불변 Order 모음입니다.</param>
    /// <param name="Messages">메시지 ID별 불변 OutboxMessage 모음입니다.</param>
    /// <remarks>괄호 속 매개변수로 private 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
    private sealed record StoreState(
        IReadOnlyDictionary<string, Order> Orders,
        IReadOnlyDictionary<Guid, OutboxMessage> Messages)
    {
        /// <summary>
        /// 주문과 메시지가 하나도 없는 첫 저장소 상태를 만듭니다.
        /// </summary>
        /// <returns>서로 독립된 빈 Dictionary 두 개를 가진 상태를 반환합니다.</returns>
        public static StoreState Empty()
        {
            return new StoreState(
                new Dictionary<string, Order>(StringComparer.Ordinal),
                new Dictionary<Guid, OutboxMessage>());
        }
    }

    /// <summary>
    /// 현재 저장된 주문 수를 잠금 안에서 읽어 일관된 값을 돌려줍니다.
    /// </summary>
    public int OrderCount
    {
        get
        {
            // lock은 한 번에 한 스레드만 상태를 읽거나 바꾸게 해 중간 상태 관찰과 경합을 막습니다.
            lock (_gate)
            {
                return _state.Orders.Count;
            }
        }
    }

    /// <summary>
    /// 현재 저장된 전체 Outbox 메시지 수를 잠금 안에서 읽습니다.
    /// </summary>
    public int OutboxCount
    {
        get
        {
            lock (_gate)
            {
                return _state.Messages.Count;
            }
        }
    }

    /// <summary>
    /// 다음 커밋 한 번이 상태 교체 직전에 실패하도록 예약합니다.
    /// </summary>
    /// <returns>예약만 수행하며 반환값은 없습니다.</returns>
    public void FailNextCommit()
    {
        lock (_gate)
        {
            _failNextCommit = true;
        }
    }

    /// <summary>
    /// 다음 완료 표시 한 번이 저장 전에 예외를 내도록 예약합니다.
    /// 외부 발행 성공과 DB 완료 표시 사이의 프로세스 장애 구간을 재현할 때 사용합니다.
    /// </summary>
    /// <returns>예약만 수행하며 반환값은 없습니다.</returns>
    public void FailNextMarkPublished()
    {
        lock (_gate)
        {
            _failNextMarkPublished = true;
        }
    }

    /// <summary>
    /// 주문과 Outbox 메시지를 복사본에 모두 반영한 뒤 상태 참조를 한 번만 교체합니다.
    /// </summary>
    /// <param name="order">저장할 검증 완료 주문입니다.</param>
    /// <param name="message">같은 주문에서 발생한 Pending 메시지입니다.</param>
    /// <param name="cancellationToken">상태 교체 전에 확인할 취소 신호입니다.</param>
    /// <returns>둘 다 저장했으면 성공, 중복이나 주입된 커밋 실패면 실패 Result를 반환합니다.</returns>
    public Task<Result> CommitAsync(
        Order order,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            // 잠금을 기다리는 동안 취소됐을 수도 있으므로 변경을 시작하기 전에 다시 확인합니다.
            cancellationToken.ThrowIfCancellationRequested();

            // 이 메서드는 Application Service 내부 경계이므로 Factory를 우회한 잘못된 메시지도
            // 실제 저장 직전에 거부합니다. 특히 MessageId는 소비자 중복 제거의 핵심 키입니다.
            // ||는 조건 중 하나만 참이어도 전체가 참이며, 참을 찾으면 뒤 조건을 실행하지 않는 단락 평가를 합니다.
            if (message.MessageId == Guid.Empty ||
                string.IsNullOrWhiteSpace(message.EventType) ||
                message.EventVersion <= 0 ||
                string.IsNullOrWhiteSpace(message.Payload))
            {
                throw new ArgumentException("Outbox 메시지 ID, 타입, 버전, payload가 유효해야 합니다.", nameof(message));
            }

            if (!string.Equals(message.AggregateId, order.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("주문과 Outbox 메시지의 AggregateId가 일치해야 합니다.", nameof(message));
            }

            // is not null은 nullable 값 안에 실제 완료 시각이 이미 들어 있음을 검사합니다.
            if (message.PublishedAtUtc is not null || message.AttemptCount != 0)
            {
                throw new ArgumentException("새 주문에는 아직 시도하지 않은 Pending 메시지만 저장할 수 있습니다.", nameof(message));
            }

            if (_state.Orders.ContainsKey(order.Id))
            {
                // 메모리 작업은 즉시 끝나지만 Task.FromResult로 비동기 저장소 인터페이스 모양을 맞춥니다.
                // $ 문자열 안의 {order.Id}는 실행 시 실제 ID 값으로 바뀌는 문자열 보간 문법입니다.
                return Task.FromResult(Result.Failure(
                    new Error("order.duplicate", $"주문 '{order.Id}'는 이미 저장되어 있습니다.")));
            }

            if (_state.Messages.ContainsKey(message.MessageId))
            {
                return Task.FromResult(Result.Failure(
                    new Error("outbox.duplicate", "같은 메시지 ID가 이미 저장되어 있습니다.")));
            }

            // 기존 Dictionary를 직접 바꾸지 않고 복사본을 준비하면 실패 시 원본 상태가 그대로 남습니다.
            // [key] = value 형태의 Dictionary index initializer로 복사본에 새 항목을 추가합니다.
            var nextOrders = new Dictionary<string, Order>(_state.Orders, StringComparer.Ordinal)
            {
                [order.Id] = order,
            };
            var nextMessages = new Dictionary<Guid, OutboxMessage>(_state.Messages)
            {
                [message.MessageId] = message,
            };

            if (_failNextCommit)
            {
                _failNextCommit = false;
                return Task.FromResult(Result.Failure(
                    new Error("storage.commit_failed", "주문과 메시지 커밋에 실패했습니다.")));
            }

            // 단 한 번의 참조 교체로 다른 독자는 이전 전체 상태 또는 새 전체 상태만 보게 됩니다.
            _state = new StoreState(nextOrders, nextMessages);

            // 커밋 뒤 취소를 다시 확인하면 실제 저장은 성공했는데 실패처럼 보일 수 있으므로 여기서는 확인하지 않습니다.
            return Task.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 아직 PublishedAtUtc가 없는 메시지만 호출 시점의 배열로 복사합니다.
    /// </summary>
    /// <param name="cancellationToken">조회 전에 확인할 취소 신호입니다.</param>
    /// <returns>외부에서 내부 Dictionary를 바꿀 수 없는 Pending 메시지 스냅샷을 반환합니다.</returns>
    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // "is null" 패턴은 nullable 값이 비어 있는 메시지만 안전하게 고릅니다.
            IReadOnlyList<OutboxMessage> pending = _state.Messages.Values
                .Where(message => message.PublishedAtUtc is null)
                .ToArray();
            return Task.FromResult(pending);
        }
    }

    /// <summary>
    /// 메시지 ID로 저장된 불변 스냅샷을 찾습니다.
    /// </summary>
    /// <param name="messageId">찾을 메시지의 고유 ID입니다.</param>
    /// <param name="cancellationToken">조회 전에 확인할 취소 신호입니다.</param>
    /// <returns>메시지가 있으면 그 값, 없으면 null을 반환합니다.</returns>
    public Task<OutboxMessage?> FindAsync(Guid messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // out var는 TryGetValue가 찾은 값을 새 지역 변수 message에 동시에 선언해 받습니다.
            _state.Messages.TryGetValue(messageId, out var message);
            return Task.FromResult(message);
        }
    }

    /// <summary>
    /// 메시지를 불변 복사본으로 교체하여 발행 시작 횟수를 1 올립니다.
    /// </summary>
    /// <param name="messageId">시도를 시작할 메시지 ID입니다.</param>
    /// <param name="cancellationToken">상태 교체 전에 확인할 취소 신호입니다.</param>
    /// <returns>AttemptCount가 증가한 메시지를 반환합니다.</returns>
    public Task<OutboxMessage> StartAttemptAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_state.Messages.TryGetValue(messageId, out var current))
            {
                throw new InvalidOperationException($"Outbox 메시지 '{messageId}'를 찾을 수 없습니다.");
            }

            if (current.PublishedAtUtc is not null)
            {
                throw new InvalidOperationException("이미 완료된 메시지는 다시 시작할 수 없습니다.");
            }

            var attempted = current.StartAttempt();
            var nextMessages = new Dictionary<Guid, OutboxMessage>(_state.Messages)
            {
                [messageId] = attempted,
            };
            _state = new StoreState(_state.Orders, nextMessages);
            return Task.FromResult(attempted);
        }
    }

    /// <summary>
    /// 외부 발행 성공 뒤 메시지를 완료된 불변 복사본으로 교체합니다.
    /// </summary>
    /// <param name="messageId">완료 표시할 메시지 ID입니다.</param>
    /// <param name="publishedAtUtc">성공 기록에 넣을 UTC 시각입니다.</param>
    /// <param name="cancellationToken">상태 교체 전에 확인할 취소 신호입니다.</param>
    /// <returns>완료 상태가 저장될 때 끝나는 Task를 반환합니다.</returns>
    public Task MarkPublishedAsync(
        Guid messageId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_failNextMarkPublished)
            {
                _failNextMarkPublished = false;
                throw new InvalidOperationException("발행 성공 뒤 완료 표시 전에 장애가 발생했습니다.");
            }

            if (!_state.Messages.TryGetValue(messageId, out var current))
            {
                throw new InvalidOperationException($"Outbox 메시지 '{messageId}'를 찾을 수 없습니다.");
            }

            var nextMessages = new Dictionary<Guid, OutboxMessage>(_state.Messages)
            {
                [messageId] = current.MarkPublished(publishedAtUtc),
            };
            _state = new StoreState(_state.Orders, nextMessages);
            // 돌려줄 값이 없는 동기 메모리 갱신이 끝났으므로 이미 완료된 Task를 재사용합니다.
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// 성공·실패 순서를 미리 지정할 수 있는 결정적 가짜 외부 발행기입니다.
/// 네트워크 없이 재시도와 부분 실패를 빠르게 검증하는 테스트 더블 역할을 합니다.
/// </summary>
public sealed class ScriptedEventPublisher : IEventPublisher
{
    private readonly object _gate = new();
    private readonly Queue<bool> _shouldFail;
    // [] collection expression은 빈 List<Guid>를 목표 타입에 맞춰 간결하게 만듭니다.
    private readonly List<Guid> _attemptedMessageIds = [];
    private readonly List<Guid> _deliveredMessageIds = [];

    /// <summary>
    /// 호출별 실패 여부를 앞에서부터 소비하도록 발행기를 만듭니다.
    /// </summary>
    /// <param name="shouldFail">각 호출이 실패해야 하면 true이며, 값이 다 소진된 뒤에는 성공합니다.</param>
    /// <remarks>테스트용 스크립트를 저장하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    public ScriptedEventPublisher(IEnumerable<bool>? shouldFail = null)
    {
        // ??는 왼쪽 값이 null일 때만 오른쪽 빈 배열을 대신 사용하는 null 병합 연산자입니다.
        _shouldFail = new Queue<bool>(shouldFail ?? []);
    }

    /// <summary>
    /// 지금까지 외부 발행을 시도한 메시지 ID를 호출 순서대로 복사해 돌려줍니다.
    /// </summary>
    public IReadOnlyList<Guid> AttemptedMessageIds
    {
        get
        {
            lock (_gate)
            {
                return _attemptedMessageIds.ToArray();
            }
        }
    }

    /// <summary>
    /// 가짜 브로커가 수락한 메시지 ID를 복사해 돌려줍니다. 같은 ID가 둘 이상 있을 수 있습니다.
    /// </summary>
    public IReadOnlyList<Guid> DeliveredMessageIds
    {
        get
        {
            lock (_gate)
            {
                return _deliveredMessageIds.ToArray();
            }
        }
    }

    /// <summary>
    /// 메시지 ID를 기록하고 준비된 스크립트에 따라 성공 또는 일시 실패를 반환합니다.
    /// </summary>
    /// <param name="message">저장소에서 읽은 동일 ID의 Outbox 메시지입니다.</param>
    /// <param name="cancellationToken">발행을 시작하기 전에 확인할 취소 신호입니다.</param>
    /// <returns>가짜 브로커 수락 성공 또는 재시도 가능한 실패 Result를 반환합니다.</returns>
    public Task<Result> PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _attemptedMessageIds.Add(message.MessageId);

            // &&는 두 조건이 모두 참이어야 하며, 큐가 비었으면 오른쪽 Dequeue를 실행하지 않는 단락 평가를 합니다.
            var shouldFail = _shouldFail.Count > 0 && _shouldFail.Dequeue();
            if (shouldFail)
            {
                return Task.FromResult(Result.Failure(
                    new Error("publisher.transient", "가짜 브로커가 이번 발행을 거절했습니다.")));
            }

            _deliveredMessageIds.Add(message.MessageId);
            return Task.FromResult(Result.Success());
        }
    }
}
