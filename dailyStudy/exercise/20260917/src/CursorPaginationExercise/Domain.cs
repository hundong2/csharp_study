namespace CursorPaginationExercise;

// 주문 상태를 정해진 이름으로 표현하면 문자열 오타로 필터가 틀어지는 일을 줄일 수 있습니다.
public enum OrderStatus
{
    Pending,
    Paid,
    Shipped
}

// record는 생성 후 값을 바꾸지 않는 데이터 묶음입니다. 페이지를 읽는 도중 주문 정보가 바뀌는 실수를 막습니다.
// 생성자는 Id(주문의 고유 번호), CreatedAtUtc(UTC 생성 시각), CustomerId(고객 번호),
// Status(현재 상태), Total(주문 금액)을 받아 한 주문 객체를 반환합니다.
public sealed record Order(long Id, DateTimeOffset CreatedAtUtc, string CustomerId, OrderStatus Status, decimal Total);

// record struct는 두 값을 가진 작은 값 형식입니다. 커서를 문자열 대신 타입으로 표현하여 키의 순서를 혼동하지 않게 합니다.
// 생성자는 CreatedAtUtc(마지막으로 본 주문의 UTC 시각)와 Id(그 주문의 번호)를 받아 커서 값을 반환합니다.
public readonly record struct OrderCursor(DateTimeOffset CreatedAtUtc, long Id);

// 물음표(?)는 값이 없을 수도 있음을 뜻합니다. 첫 페이지에는 After가 없고, 상태 필터는 선택 사항입니다.
// 생성자는 PageSize(원하는 주문 수), After(직전 페이지의 마지막 키), Status(조회할 상태)를 받아 요청 객체를 반환합니다.
public sealed record OrderQuery(int PageSize, OrderCursor? After = null, OrderStatus? Status = null);

// NextCursor가 null이면 다음 페이지가 없습니다. IReadOnlyList는 호출자가 페이지의 항목을 바꾸지 못하게 하는 읽기 전용 계약입니다.
// 생성자는 Items(이번 페이지 주문들)와 NextCursor(다음 페이지를 요청할 때 쓸 키)를 받아 결과 페이지를 반환합니다.
public sealed record OrderPage(IReadOnlyList<Order> Items, OrderCursor? NextCursor);

// 예상 가능한 잘못된 입력은 예외 대신 Result로 돌려주어 호출자가 사용자 메시지를 만들 수 있게 합니다.
// `where T : class`는 성공 값 T를 참조 형식으로 제한해, 실패할 때 값이 없음을 null로 표현할 수 있게 합니다.
public sealed class Result<T> where T : class
{
    // 생성자는 value(성공 시 데이터)와 error(실패 시 이유)를 저장하고 Result 객체를 반환합니다.
    // private은 성공과 실패가 섞인 객체를 외부에서 임의로 만들지 못하게 합니다.
    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public string? Error { get; }
    // =>는 값을 계산해 돌려주는 짧은 속성 문법입니다. 실패 이유가 없을 때만 성공입니다.
    public bool IsSuccess => Error == null;

    // 이 메서드는 value(성공한 작업의 데이터)를 받아 성공 Result를 반환합니다.
    // static은 Result 객체를 미리 만들지 않아도 이 메서드를 호출할 수 있다는 뜻입니다.
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    // 이 메서드는 error(사용자에게 설명할 실패 이유)를 받아 실패 Result를 반환합니다.
    public static Result<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new Result<T>(null, error);
    }
}
