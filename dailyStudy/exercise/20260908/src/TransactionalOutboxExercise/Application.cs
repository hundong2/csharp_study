namespace DailyStudy.TransactionalOutbox;

/// <summary>
/// 주문 생성 유스케이스가 받는 외부 입력입니다.
/// 위치 기반 <c>record</c>를 써서 짧고 불변인 명령 데이터로 표현합니다.
/// </summary>
/// <param name="OrderId">새 주문을 구분할 ID입니다.</param>
/// <param name="CustomerId">주문한 고객의 ID입니다.</param>
/// <param name="TotalAmount">주문의 총액입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
public sealed record CreateOrderCommand(string OrderId, string CustomerId, decimal TotalAmount);

/// <summary>
/// 주문과 Outbox 메시지가 함께 저장된 뒤 호출자에게 돌려주는 최소 결과입니다.
/// </summary>
/// <param name="OrderId">저장된 주문 ID입니다.</param>
/// <param name="MessageId">나중 발행과 중복 처리를 추적할 Outbox 메시지 ID입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
public sealed record OrderReceipt(string OrderId, Guid MessageId);

/// <summary>
/// Dispatcher 한 번의 실행에서 확인한 메시지 수와 성공·실패 수를 요약합니다.
/// </summary>
/// <param name="Scanned">이번 실행에서 읽은 미발행 메시지 수입니다.</param>
/// <param name="Published">발행과 완료 표시가 모두 끝난 수입니다.</param>
/// <param name="Failed">예상 가능한 발행 실패로 Pending에 남은 수입니다.</param>
/// <param name="FailureCodes">운영 지표나 테스트에서 분류할 실패 코드 모음입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
public sealed record DispatchReport(
    int Scanned,
    int Published,
    int Failed,
    IReadOnlyList<string> FailureCodes);

/// <summary>
/// 주문과 Outbox 메시지를 하나의 원자적 커밋으로 저장하는 트랜잭션 경계입니다.
/// Application 계층은 구체 DB 대신 이 추상화에 의존하므로 테스트용 저장소로 쉽게 바꿀 수 있습니다.
/// </summary>
public interface IOrderUnitOfWork
{
    /// <summary>
    /// 주문과 그 주문에서 생긴 Outbox 메시지를 둘 다 저장하거나 둘 다 저장하지 않습니다.
    /// </summary>
    /// <param name="order">검증을 통과한 주문입니다.</param>
    /// <param name="message">같은 주문에서 생긴 아직 미발행인 메시지입니다.</param>
    /// <param name="cancellationToken">호출자가 작업 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>커밋 성공 또는 중복·주입 실패 같은 예상 가능한 오류를 비동기로 반환합니다.</returns>
    Task<Result> CommitAsync(
        Order order,
        OutboxMessage message,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dispatcher가 미발행 메시지를 조회하고 발행 상태를 갱신하는 저장소 포트입니다.
/// 조회와 갱신을 인터페이스로 분리하여 외부 DB 없이 실패 경계를 테스트할 수 있습니다.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// 아직 성공 표시가 없는 메시지를 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">조회 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>호출 시점의 Pending 메시지 스냅샷을 비동기로 반환합니다.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 메시지 하나를 ID로 찾습니다.
    /// </summary>
    /// <param name="messageId">찾으려는 Outbox 메시지 ID입니다.</param>
    /// <param name="cancellationToken">조회 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>찾은 메시지 또는 존재하지 않을 때 null을 비동기로 반환합니다.</returns>
    Task<OutboxMessage?> FindAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// 외부 발행을 호출하기 직전에 시도 횟수를 1 올립니다.
    /// </summary>
    /// <param name="messageId">시작할 발행 시도의 메시지 ID입니다.</param>
    /// <param name="cancellationToken">갱신 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>갱신된 메시지를 비동기로 반환합니다.</returns>
    Task<OutboxMessage> StartAttemptAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// 외부 발행이 성공한 뒤 메시지를 완료 상태로 바꿉니다.
    /// </summary>
    /// <param name="messageId">완료 표시할 메시지 ID입니다.</param>
    /// <param name="publishedAtUtc">외부 발행이 성공했다고 기록할 UTC 시각입니다.</param>
    /// <param name="cancellationToken">갱신 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>상태 갱신이 끝날 때 완료되는 비동기 작업입니다.</returns>
    Task MarkPublishedAsync(
        Guid messageId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outbox 메시지를 외부 브로커로 보내는 포트입니다.
/// DIP에 따라 Application 계층은 콘솔용 가짜 발행기나 실제 Kafka/RabbitMQ 구현을 알 필요가 없습니다.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// 저장된 메시지 봉투를 외부 시스템에 한 번 전달하려고 시도합니다.
    /// </summary>
    /// <param name="message">발행할 ID, 이벤트 종류, 버전, JSON payload입니다.</param>
    /// <param name="cancellationToken">외부 호출 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>성공 또는 호출자가 재시도할 수 있는 예상 실패를 비동기로 반환합니다.</returns>
    Task<Result> PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// 도메인 이벤트를 저장 가능한 문자열로 바꾸는 직렬화 Strategy입니다.
/// 인터페이스가 JSON 라이브러리 세부사항을 Application Service 밖으로 밀어냅니다.
/// </summary>
public interface IOrderPlacedSerializer
{
    /// <summary>
    /// OrderPlaced 이벤트를 Outbox payload 문자열로 직렬화합니다.
    /// </summary>
    /// <param name="domainEvent">저장할 주문 생성 이벤트입니다.</param>
    /// <returns>나중에 외부로 발행할 JSON 문자열을 반환합니다.</returns>
    string Serialize(OrderPlaced domainEvent);
}

/// <summary>
/// 입력 검증, 도메인 객체 생성, 이벤트 직렬화, 원자 커밋 순서를 조정하는 Application Service입니다.
/// 이 클래스에는 DB나 브로커 구현을 직접 만들지 않아 유스케이스 테스트가 간단합니다.
/// </summary>
public sealed class OrderApplicationService
{
    private readonly IOrderUnitOfWork _unitOfWork;
    private readonly IOrderPlacedSerializer _serializer;

    /// <summary>
    /// 주문 유스케이스에 필요한 저장 경계와 직렬화 전략을 주입받습니다.
    /// </summary>
    /// <param name="unitOfWork">주문과 메시지를 함께 커밋할 추상화입니다.</param>
    /// <param name="serializer">도메인 이벤트를 JSON으로 바꿀 전략입니다.</param>
    /// <remarks>의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    public OrderApplicationService(
        IOrderUnitOfWork unitOfWork,
        IOrderPlacedSerializer serializer)
    {
        // ?? throw는 왼쪽 의존성이 null일 때 즉시 예외를 내며, nameof는 매개변수 이름을 안전하게 얻습니다.
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary>
    /// 명령을 검증하고 주문·이벤트 메시지를 만든 뒤 둘을 한 번에 저장합니다.
    /// </summary>
    /// <param name="command">주문 ID, 고객 ID, 금액을 담은 외부 입력입니다.</param>
    /// <param name="occurredAtUtc">테스트에서도 고정할 수 있는 주문 생성 UTC 시각입니다.</param>
    /// <param name="cancellationToken">저장 전후에 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>성공하면 주문/메시지 ID, 실패하면 입력 또는 커밋 오류를 비동기로 반환합니다.</returns>
    /// <remarks><c>async</c>는 메서드 안에서 await를 사용하고 결과를 Task로 돌려줄 수 있게 합니다.</remarks>
    public async Task<Result<OrderReceipt>> CreateAsync(
        CreateOrderCommand command,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // var는 오른쪽 결과 타입이 분명할 때 긴 타입명을 반복하지 않도록 하는 지역 변수 문법입니다.
        var orderResult = Order.Create(
            command.OrderId,
            command.CustomerId,
            command.TotalAmount,
            occurredAtUtc);

        if (orderResult.IsFailure)
        {
            return Result<OrderReceipt>.Failure(orderResult.Error);
        }

        var order = orderResult.Value;
        var domainEvent = new OrderPlaced(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.CreatedAtUtc);
        var payload = _serializer.Serialize(domainEvent);
        var message = OutboxMessage.Create(Guid.NewGuid(), domainEvent, payload);

        // await는 비동기 저장이 끝날 때까지 현재 스레드를 막아 두지 않고 기다린 뒤 결과를 이어서 처리합니다.
        // ConfigureAwait(false)는 콘솔/라이브러리 코드가 원래 실행 문맥으로 돌아올 필요가 없음을 명시합니다.
        var commitResult = await _unitOfWork
            .CommitAsync(order, message, cancellationToken)
            .ConfigureAwait(false);

        if (commitResult.IsFailure)
        {
            // 저장 실패와 취소가 겹치면 취소를 우선합니다. 성공 뒤에는 point of no return이므로 다시 검사하지 않습니다.
            cancellationToken.ThrowIfCancellationRequested();
            return Result<OrderReceipt>.Failure(commitResult.Error);
        }

        return Result<OrderReceipt>.Success(new OrderReceipt(order.Id, message.MessageId));
    }
}

/// <summary>
/// 저장된 Pending 메시지를 순서대로 외부에 보내고 성공한 것만 완료 표시하는 Application Service입니다.
/// 실패 메시지 하나가 다음 메시지까지 막지 않도록 예상 실패는 보고서에 모으고 계속 진행합니다.
/// </summary>
public sealed class OutboxDispatcher
{
    private readonly IOutboxRepository _repository;
    private readonly IEventPublisher _publisher;

    /// <summary>
    /// 메시지 저장소와 외부 발행 포트를 주입받아 Dispatcher를 구성합니다.
    /// </summary>
    /// <param name="repository">Pending 조회와 상태 갱신에 사용할 저장소입니다.</param>
    /// <param name="publisher">메시지를 외부 시스템에 보낼 구현입니다.</param>
    /// <remarks>의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    public OutboxDispatcher(IOutboxRepository repository, IEventPublisher publisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// 현재 Pending인 메시지를 오래된 순서대로 각각 한 번 발행하려고 시도합니다.
    /// </summary>
    /// <param name="publishedAtUtc">성공한 메시지에 기록할 UTC 완료 시각입니다.</param>
    /// <param name="cancellationToken">조회, 발행, 갱신 전 과정에 전달할 취소 신호입니다.</param>
    /// <returns>조회 수와 성공·실패 수, 실패 코드를 담은 실행 보고서를 비동기로 반환합니다.</returns>
    public async Task<DispatchReport> DispatchPendingAsync(
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = await _repository
            .GetPendingAsync(cancellationToken)
            .ConfigureAwait(false);

        // 저장소가 빈 스냅샷과 취소를 함께 돌려줘도 foreach가 없어 신호가 사라지지 않게 합니다.
        cancellationToken.ThrowIfCancellationRequested();

        // LINQ는 반복문을 직접 늘리지 않고 "발생 시각, 메시지 ID 순으로 정렬"이라는 의도를 표현합니다.
        // => 람다는 각 메시지에서 정렬 기준 값을 꺼내는 이름 없는 짧은 함수입니다.
        var pending = snapshot
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.MessageId)
            .ToArray();

        var published = 0;
        var failed = 0;
        var failureCodes = new List<string>();

        // foreach는 정렬된 배열의 메시지를 하나씩 순서대로 꺼내 같은 발행 절차를 적용합니다.
        foreach (var message in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attemptedMessage = await _repository
                .StartAttemptAsync(message.MessageId, cancellationToken)
                .ConfigureAwait(false);
            var publishResult = await _publisher
                .PublishAsync(attemptedMessage, cancellationToken)
                .ConfigureAwait(false);

            // 구현체가 취소 신호와 실패 Result를 동시에 돌려줘도 취소가 우선입니다.
            // 그렇지 않으면 마지막 메시지에서는 취소가 평범한 발행 실패로 삼켜질 수 있습니다.
            cancellationToken.ThrowIfCancellationRequested();

            if (publishResult.IsFailure)
            {
                failed++;
                failureCodes.Add(publishResult.Error.Code);
                // continue는 현재 반복만 끝냅니다. 한 건의 예상된 실패가 뒤 메시지 발행을 막지 않게 하는 선택입니다.
                continue;
            }

            // 외부 발행 뒤 완료 표시 사이에는 장애 구간이 남습니다. 그래서 정확히 한 번이 아니라
            // 같은 MessageId가 중복될 수 있는 at-least-once 전달이며 소비자의 멱등 처리가 필요합니다.
            await _repository
                .MarkPublishedAsync(message.MessageId, publishedAtUtc, cancellationToken)
                .ConfigureAwait(false);
            published++;
        }

        return new DispatchReport(pending.Length, published, failed, failureCodes);
    }
}
