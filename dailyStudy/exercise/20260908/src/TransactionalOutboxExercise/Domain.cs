// 파일 범위 네임스페이스는 아래 파일의 모든 타입을 같은 이름 공간에 넣되,
// 중괄호 한 단계를 줄여 초보자가 핵심 코드에 더 쉽게 집중하게 합니다.
namespace DailyStudy.TransactionalOutbox;

/// <summary>
/// 실패 이유를 기계가 구분할 코드와 사람이 읽을 메시지로 함께 표현합니다.
/// <c>record</c>는 값이 같으면 같은 오류로 비교할 수 있는 불변 데이터에 알맞습니다.
/// </summary>
/// <param name="Code">프로그램이 분기하거나 테스트에서 확인할 안정적인 오류 코드입니다.</param>
/// <param name="Message">학습자나 사용자에게 보여 줄 이해하기 쉬운 설명입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 값을 돌려주는 메서드가 아니므로 반환값이 없습니다.</remarks>
public sealed record Error(string Code, string Message);

/// <summary>
/// 값이 필요 없는 작업의 성공 또는 예상 가능한 실패를 예외 없이 표현합니다.
/// 입력 오류나 중복처럼 호출자가 처리할 수 있는 실패에 Result를 쓰면 정상 흐름이 선명해집니다.
/// </summary>
public sealed class Result
{
    // 물음표(?)는 실패가 없을 때 Error가 null일 수 있음을 컴파일러에 알립니다.
    private readonly Error? _error;

    /// <summary>
    /// Result를 성공 또는 실패 상태로 만듭니다.
    /// </summary>
    /// <param name="isSuccess">성공이면 true, 실패이면 false입니다.</param>
    /// <param name="error">실패 이유이며 성공일 때는 null입니다.</param>
    /// <remarks>생성된 Result를 반환하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>작업이 성공했는지를 알려 줍니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>작업이 실패했는지를 성공 값의 반대로 계산합니다.</summary>
    // =>는 오른쪽 식 하나의 결과를 바로 돌려주는 "식 본문" 문법이고,
    // !는 bool 값을 뒤집는 논리 NOT이므로 성공이 아니면 실패라는 뜻입니다.
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 실패 이유를 돌려줍니다. 성공 Result에서 읽는 것은 프로그래밍 실수이므로 예외를 냅니다.
    /// </summary>
    // 조건 ? A : B는 조건이 참이면 A, 거짓이면 B를 고르는 조건 연산자입니다.
    public Error Error => IsFailure
        // 느낌표(!)는 실패 상태에서는 _error가 반드시 존재한다는 불변식을 컴파일러에 알려 줍니다.
        ? _error!
        : throw new InvalidOperationException("성공 Result에는 오류가 없습니다.");

    /// <summary>
    /// 값이 필요 없는 성공 Result를 만듭니다.
    /// </summary>
    /// <returns>성공 상태인 새 Result를 반환합니다.</returns>
    // new(...)의 타입은 메서드 반환형 Result로 분명하므로 클래스 이름을 생략한 target-typed new입니다.
    public static Result Success() => new(true, null);

    /// <summary>
    /// 호출자가 처리할 수 있는 실패 Result를 만듭니다.
    /// </summary>
    /// <param name="error">실패 코드와 설명입니다.</param>
    /// <returns>주어진 오류를 가진 실패 Result를 반환합니다.</returns>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }
}

/// <summary>
/// 성공 값 또는 예상 가능한 실패 중 하나를 담습니다.
/// 제네릭 <c>T</c> 덕분에 주문, 영수증 등 서로 다른 성공 값을 같은 규칙으로 다룰 수 있습니다.
/// </summary>
/// <typeparam name="T">성공했을 때 돌려줄 값의 타입입니다.</typeparam>
public sealed class Result<T>
{
    // 제약 없는 T의 T?는 참조 타입 대입 시 null 가능성을 표시하지만 int 같은 값 타입을 Nullable<int>로 만들지는 않습니다.
    // 실패일 때는 default(T)가 들어갈 수 있으며, 그 값이 유효한지는 IsSuccess로 판단합니다.
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// 성공 값과 실패 오류 중 알맞은 하나를 보관합니다.
    /// </summary>
    /// <param name="isSuccess">성공 여부입니다.</param>
    /// <param name="value">성공 값이며 실패일 때는 기본값입니다.</param>
    /// <param name="error">실패 이유이며 성공일 때는 null입니다.</param>
    /// <remarks>생성된 Result를 반환하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>성공 값을 안전하게 읽을 수 있는 상태인지 알려 줍니다.</summary>
    public bool IsSuccess { get; }

    /// <summary>실패 상태인지를 성공 값의 반대로 계산합니다.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 성공 값을 돌려줍니다. 실패 Result에서 읽으면 호출 순서가 잘못된 것이므로 예외를 냅니다.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("실패 Result에는 성공 값이 없습니다.");

    /// <summary>
    /// 실패 이유를 돌려줍니다. 성공 Result에서 읽으면 프로그래밍 실수이므로 예외를 냅니다.
    /// </summary>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("성공 Result에는 오류가 없습니다.");

    /// <summary>
    /// 주어진 값을 담은 성공 Result를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>성공 상태와 값을 담은 새 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 주어진 오류를 담은 실패 Result를 만듭니다.
    /// </summary>
    /// <param name="error">호출자가 처리할 수 있는 실패 코드와 설명입니다.</param>
    /// <returns>실패 상태와 오류를 담은 새 Result를 반환합니다.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        // default는 T가 무엇이든 그 타입의 기본값을 뜻하며, 실패 상태라 성공 값은 읽히지 않습니다.
        return new Result<T>(false, default, error);
    }
}

/// <summary>
/// 외부 입력을 검증한 뒤 만들어지는 주문 도메인 모델입니다.
/// 생성자를 숨기고 <see cref="Create"/>만 공개하여 잘못된 주문이 생기지 않게 합니다.
/// </summary>
public sealed class Order
{
    /// <summary>
    /// 검증이 끝난 값으로 주문을 만듭니다.
    /// </summary>
    /// <param name="id">중복 주문을 구분하는 주문 ID입니다.</param>
    /// <param name="customerId">주문 소유자를 나타내는 고객 ID입니다.</param>
    /// <param name="totalAmount">0보다 큰 주문 총액입니다.</param>
    /// <param name="createdAtUtc">주문이 생성된 UTC 시각입니다.</param>
    /// <remarks>생성된 Order를 초기화하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
    private Order(string id, string customerId, decimal totalAmount, DateTimeOffset createdAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>주문을 유일하게 구분하는 ID입니다.</summary>
    public string Id { get; }

    /// <summary>주문한 고객을 식별하는 ID입니다.</summary>
    public string CustomerId { get; }

    /// <summary>금액 오차를 피하려고 부동소수점 대신 decimal로 보관한 주문 총액입니다.</summary>
    public decimal TotalAmount { get; }

    /// <summary>서버와 지역이 달라도 같은 순간을 가리키도록 UTC로 저장한 생성 시각입니다.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// 외부 문자열과 금액을 검사하고 유효할 때만 주문을 만듭니다.
    /// </summary>
    /// <param name="id">공백이 아니어야 하는 주문 ID입니다.</param>
    /// <param name="customerId">공백이 아니어야 하는 고객 ID입니다.</param>
    /// <param name="totalAmount">0보다 커야 하는 주문 총액입니다.</param>
    /// <param name="createdAtUtc">주문 생성 UTC 시각입니다.</param>
    /// <returns>유효하면 Order, 아니면 고칠 수 있는 입력 오류를 담은 Result를 반환합니다.</returns>
    public static Result<Order> Create(
        string id,
        string customerId,
        decimal totalAmount,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<Order>.Failure(new Error("order.id_required", "주문 ID를 입력하세요."));
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result<Order>.Failure(new Error("order.customer_required", "고객 ID를 입력하세요."));
        }

        // 숫자 뒤 m은 이 리터럴이 금액 계산에 알맞은 decimal 타입임을 나타냅니다.
        if (totalAmount <= 0m)
        {
            return Result<Order>.Failure(new Error("order.amount_positive", "주문 금액은 0보다 커야 합니다."));
        }

        return Result<Order>.Success(
            new Order(id.Trim(), customerId.Trim(), totalAmount, createdAtUtc.ToUniversalTime()));
    }
}

/// <summary>
/// 주문이 저장될 때 외부 시스템에 알려야 할 사실을 나타내는 불변 도메인 이벤트입니다.
/// </summary>
/// <param name="OrderId">이벤트가 발생한 주문 ID입니다.</param>
/// <param name="CustomerId">후속 처리가 참조할 고객 ID입니다.</param>
/// <param name="TotalAmount">주문의 총액입니다.</param>
/// <param name="OccurredAtUtc">이벤트가 발생한 UTC 시각입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
public sealed record OrderPlaced(
    string OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// DB에 저장했다가 나중에 발행할 이벤트 봉투(envelope)입니다.
/// 메시지 ID와 이벤트 버전은 소비자가 중복 처리와 스키마 변화를 다루는 기준이 됩니다.
/// </summary>
/// <param name="MessageId">재발행되어도 변하지 않는 메시지 고유 ID입니다.</param>
/// <param name="AggregateId">이 메시지를 만든 주문 ID입니다.</param>
/// <param name="EventType">소비자가 payload 종류를 찾는 이벤트 이름입니다.</param>
/// <param name="EventVersion">이벤트 payload 계약의 버전입니다.</param>
/// <param name="OccurredAtUtc">도메인 이벤트가 발생한 UTC 시각입니다.</param>
/// <param name="Payload">DB에 저장할 JSON 문자열입니다.</param>
/// <param name="PublishedAtUtc">아직 발행되지 않았으면 null, 성공 표시가 끝났으면 그 UTC 시각입니다.</param>
/// <param name="AttemptCount">발행을 시작한 횟수입니다.</param>
/// <remarks>괄호 속 매개변수로 주 생성자가 자동 생성되며, 생성자는 별도 반환값이 없습니다.</remarks>
public sealed record OutboxMessage(
    Guid MessageId,
    string AggregateId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAtUtc,
    string Payload,
    DateTimeOffset? PublishedAtUtc,
    int AttemptCount)
{
    /// <summary>
    /// 주문 이벤트와 직렬화된 JSON으로 아직 발행되지 않은 Outbox 메시지를 만듭니다.
    /// </summary>
    /// <param name="messageId">이 메시지를 재시도 동안 동일하게 식별할 ID입니다.</param>
    /// <param name="domainEvent">주문에서 발생한 불변 도메인 이벤트입니다.</param>
    /// <param name="payload">이벤트를 직렬화한 JSON입니다.</param>
    /// <returns>발행 시각은 null이고 시도 횟수는 0인 새 Outbox 메시지를 반환합니다.</returns>
    public static OutboxMessage Create(Guid messageId, OrderPlaced domainEvent, string payload)
    {
        // 잘못된 입력을 고칠 수 있는 Order.Create는 Result를 쓰지만, 빈 내부 메시지 ID는
        // 호출 코드가 지켜야 할 계약 위반이므로 조용히 업무 실패로 숨기지 않고 예외를 냅니다.
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("빈 메시지 ID는 사용할 수 없습니다.", nameof(messageId));
        }

        ArgumentNullException.ThrowIfNull(domainEvent);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Outbox payload가 비어 있습니다.", nameof(payload));
        }

        // EventVersion:처럼 이름을 붙인 인수는 값이 어떤 매개변수로 가는지 명확하게 보여 줍니다.
        return new OutboxMessage(
            messageId,
            domainEvent.OrderId,
            nameof(OrderPlaced),
            EventVersion: 1,
            domainEvent.OccurredAtUtc,
            payload,
            PublishedAtUtc: null,
            AttemptCount: 0);
    }

    /// <summary>
    /// 발행 시도가 시작됐음을 기록한 새 메시지 값을 만듭니다.
    /// </summary>
    /// <returns>기존 값은 바꾸지 않고 AttemptCount만 1 큰 복사본을 반환합니다.</returns>
    public OutboxMessage StartAttempt()
    {
        // record의 with 식은 원본을 바꾸지 않고 일부 속성만 바꾼 복사본을 만듭니다.
        // checked는 int 범위를 넘는 잘못된 시도 횟수를 조용히 감싸지 않고 예외로 드러냅니다.
        return this with { AttemptCount = checked(AttemptCount + 1) };
    }

    /// <summary>
    /// 외부 발행이 성공한 뒤 완료 시각을 기록한 새 메시지 값을 만듭니다.
    /// </summary>
    /// <param name="publishedAtUtc">외부 발행 성공을 기록하는 UTC 시각입니다.</param>
    /// <returns>PublishedAtUtc가 채워진 불변 복사본을 반환합니다.</returns>
    public OutboxMessage MarkPublished(DateTimeOffset publishedAtUtc)
    {
        return this with { PublishedAtUtc = publishedAtUtc.ToUniversalTime() };
    }
}
