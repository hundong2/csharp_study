using System.Globalization;

namespace InventoryWatcherExercise.Domain;

// record는 각 구성 요소의 Equals 결과를 조합해 값 동등성을 제공하는 C# 문법이다.
// 단, 컬렉션 내용까지 자동으로 순서 비교하지는 않는다. 여기서는 Result와 재고 한 건처럼 스칼라 값 중심의 객체에 사용한다.
/// <summary>
/// 사용자가 입력한 값으로 객체를 만들었을 때의 성공 또는 실패를 예외 없이 표현합니다.
/// 성공이면 <see cref="Value"/>가 있고, 실패이면 <see cref="Error"/>가 있습니다.
/// </summary>
/// <typeparam name="T">성공했을 때 담을 null이 아닌 값의 형식입니다.</typeparam>
// `where T : notnull`은 T를 null이 될 수 없는 형식으로 제한하는 generic constraint다.
// 성공 Result가 실제 값 하나를 반드시 가진다는 계약을 컴파일 단계에서도 돕기 위해 사용한다.
public sealed record Result<T>
    where T : notnull
{
    // T?의 물음표는 실패 결과에는 값이 없을 수 있음을 컴파일러에도 알려 주는 Nullable 문법이다.
    // 성공 여부와 값의 존재 여부를 함께 모델링하면 null 실수를 줄일 수 있다.
    private readonly T? _value;

    /// <summary>
    /// 성공/실패 상태를 모순 없이 한곳에서 조립합니다. 외부에서는 팩터리 메서드만 사용하며 값을 반환하지 않는 생성자입니다.
    /// </summary>
    /// <param name="isSuccess">작업이 성공했으면 <see langword="true"/>, 실패했으면 <see langword="false"/>입니다.</param>
    /// <param name="value">성공 결과에 담을 값이며, 실패일 때는 없습니다.</param>
    /// <param name="error">실패 이유이며, 성공일 때는 없습니다.</param>
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>
    /// 결과가 성공인지 알려 줍니다.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 결과가 실패인지 알려 줍니다.
    /// </summary>
    // `=>`는 짧은 getter의 계산 결과를 바로 반환하는 expression-bodied 문법이다.
    // 성공 상태의 반대라는 관계를 한눈에 보이게 하려고 사용한다.
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 성공 결과의 값을 반환합니다. 성공을 확인하지 않고 읽는 것은 프로그래머 계약 위반이므로 예외가 발생합니다.
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                // 사용자 입력 실패 자체는 Result로 표현하지만, 실패 결과에서 값을 꺼내는 개발 실수는 예외로 즉시 알린다.
                throw new InvalidOperationException("실패한 Result에는 성공 값이 없습니다.");
            }

            // !는 null 아님을 컴파일러에 확인해 주는 null-forgiving 연산자다.
            // Success 팩터리가 null을 막으므로 이 지점의 값은 실제로 null이 아니다.
            return _value!;
        }
    }

    /// <summary>
    /// 실패 원인을 반환하며 성공 결과일 때는 <see langword="null"/>입니다.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// null이 아닌 값을 담은 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="value">호출자에게 돌려줄 성공 값입니다.</param>
    /// <returns>주어진 값을 가진 성공 결과를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, null);
    }

    /// <summary>
    /// 사용자에게 설명할 수 있는 오류 메시지를 담은 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="error">무엇이 잘못되었는지 설명하는 비어 있지 않은 메시지입니다.</param>
    /// <returns>오류 메시지를 가진 실패 결과를 반환합니다.</returns>
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            // 비어 있는 오류 메시지는 사용자 문제가 아니라 Result를 잘못 조립한 개발자 계약 오류다.
            // nameof는 변수 이름을 문자열로 안전하게 얻어, 이름을 바꿔도 예외의 매개변수명이 함께 바뀌게 한다.
            throw new ArgumentException("실패 결과에는 오류 메시지가 필요합니다.", nameof(error));
        }

        // default는 T의 기본값을 뜻한다. 실패 상태에는 성공값이 없고 Value getter가 접근을 막으므로 이 자리를 채우는 데만 쓴다.
        return new Result<T>(false, default, error);
    }
}

/// <summary>
/// 재고 수량이 보충 기준과 비교해 어느 상태인지 나타냅니다.
/// 정확한 임계값은 Strategy가 정하며, 기본 Strategy는 아래 설명의 수량 규칙을 사용합니다.
/// 숫자가 클수록 더 긴급하도록 값을 배치해 정렬 규칙도 명확하게 만듭니다.
/// </summary>
public enum StockLevel
{
    /// <summary>Strategy가 보충할 필요가 없다고 판정한 정상 상태입니다.</summary>
    Healthy = 0,

    /// <summary>Strategy가 보충이 필요하다고 판정한 낮은 재고 상태입니다.</summary>
    Low = 1,

    /// <summary>Strategy가 즉시 확인해야 한다고 판정한 가장 긴급한 상태입니다.</summary>
    Critical = 2,
}

/// <summary>
/// 감시 대상 상품 한 건을 나타내는 불변 도메인 모델입니다.
/// 생성 뒤 값이 바뀌지 않아 여러 계층이 같은 상품을 안전하게 공유할 수 있습니다.
/// </summary>
public sealed record InventoryItem
{
    /// <summary>학습 예제가 허용하는 수량의 상한으로, 계산 overflow와 비현실적인 입력을 막습니다.</summary>
    // 숫자 리터럴의 밑줄은 자릿수를 읽기 쉽게 할 뿐 실제 값에는 영향을 주지 않는다.
    public const int MaximumQuantity = 1_000_000;

    /// <summary>보충 후 수량이 상한 안에 남도록 허용하는 재주문 기준의 상한입니다.</summary>
    public const int MaximumReorderPoint = MaximumQuantity - 1;

    private const int MaximumSkuLength = 32;
    private const int MaximumNameLength = 80;

    /// <summary>
    /// 검증과 정규화가 끝난 재고 값을 보관합니다. 직접 값을 반환하지 않고 새 객체를 초기화하는 생성자입니다.
    /// </summary>
    /// <param name="sku">공백이 제거되고 대문자로 정규화된 상품 식별자입니다.</param>
    /// <param name="name">화면에 표시할 상품 이름입니다.</param>
    /// <param name="quantity">현재 보유 수량입니다.</param>
    /// <param name="reorderPoint">이 수량 이하일 때 보충이 필요한 기준값입니다.</param>
    private InventoryItem(string sku, string name, int quantity, int reorderPoint)
    {
        Sku = sku;
        Name = name;
        Quantity = quantity;
        ReorderPoint = reorderPoint;
    }

    /// <summary>상품을 유일하게 구분하는 정규화된 코드입니다.</summary>
    public string Sku { get; }

    /// <summary>사람이 읽을 수 있는 상품 이름입니다.</summary>
    public string Name { get; }

    /// <summary>현재 창고에 있는 수량입니다.</summary>
    public int Quantity { get; }

    /// <summary>재주문을 시작해야 하는 수량 기준입니다.</summary>
    public int ReorderPoint { get; }

    /// <summary>
    /// 외부 입력을 검사하고, 유효하면 불변 재고 객체를 만듭니다.
    /// 입력 실수는 정상적인 업무 실패이므로 예외 대신 <see cref="Result{T}"/>로 반환합니다.
    /// </summary>
    /// <param name="sku">사용자가 입력한 상품 식별자입니다.</param>
    /// <param name="name">사용자가 입력한 상품 이름입니다.</param>
    /// <param name="quantity">사용자가 입력한 현재 수량입니다.</param>
    /// <param name="reorderPoint">사용자가 입력한 재주문 기준 수량입니다.</param>
    /// <returns>검증 성공 시 재고 객체, 실패 시 초보자도 고칠 수 있는 오류 메시지를 반환합니다.</returns>
    public static Result<InventoryItem> Create(
        string? sku,
        string? name,
        int quantity,
        int reorderPoint)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Result<InventoryItem>.Failure("SKU는 비어 있을 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<InventoryItem>.Failure("상품 이름은 비어 있을 수 없습니다.");
        }

        if (quantity < 0 || quantity > MaximumQuantity)
        {
            // `$"...{값}..."`은 글 안에 값을 넣는 문자열 보간 문법이고, `:N0`는 천 단위 구분 기호로 표시한다.
            return Result<InventoryItem>.Failure(
                $"현재 수량은 0~{MaximumQuantity:N0} 사이여야 합니다.");
        }

        if (reorderPoint < 0 || reorderPoint > MaximumReorderPoint)
        {
            return Result<InventoryItem>.Failure(
                $"재주문 기준은 0~{MaximumReorderPoint:N0} 사이여야 합니다.");
        }

        // var는 오른쪽 값으로 지역 변수 형식을 추론하는 문법이다. 실제 형식은 여전히 string으로 고정된다.
        var normalizedSku = sku.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();

        if (normalizedSku.Length > MaximumSkuLength || !HasOnlySafeSkuCharacters(normalizedSku))
        {
            return Result<InventoryItem>.Failure(
                $"SKU는 {MaximumSkuLength}자 이하의 영문 대문자, 숫자, '-'와 '_'만 사용할 수 있습니다.");
        }

        if (normalizedName.Length > MaximumNameLength || HasUnsafeDisplayCharacter(normalizedName))
        {
            // 제어·줄 구분·양방향 제어 문자를 막으면 한 상품명이 로그의 줄이나 읽는 방향을 위조하는 위험도 줄어든다.
            return Result<InventoryItem>.Failure(
                $"상품 이름은 {MaximumNameLength}자 이하이고 제어·줄 구분·양방향 제어 문자를 포함할 수 없습니다.");
        }

        return Result<InventoryItem>.Success(
            new InventoryItem(normalizedSku, normalizedName, quantity, reorderPoint));
    }

    /// <summary>
    /// 정규화된 SKU의 모든 문자가 로그와 저장 키에 안전한 제한 문자 집합인지 확인합니다.
    /// </summary>
    /// <param name="sku">공백 제거와 대문자 변환을 마친 SKU입니다.</param>
    /// <returns>모든 문자가 영문 대문자, 숫자, 하이픈, 밑줄 중 하나이면 true를 반환합니다.</returns>
    private static bool HasOnlySafeSkuCharacters(string sku)
    {
        foreach (var character in sku)
        {
            if (!char.IsAsciiLetterUpper(character)
                && !char.IsAsciiDigit(character)
                && character != '-'
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 상품 이름에 제어 문자, Unicode 줄 구분자, 보이지 않는 양방향 제어 문자가 하나라도 있는지 확인합니다.
    /// </summary>
    /// <param name="value">검사할 상품 이름입니다.</param>
    /// <returns>로그 표시를 왜곡할 수 있는 문자를 찾으면 true, 모두 일반 표시 문자이면 false를 반환합니다.</returns>
    private static bool HasUnsafeDisplayCharacter(string value)
    {
        foreach (var character in value)
        {
            var category = char.GetUnicodeCategory(character);
            if (char.IsControl(character)
                || category == UnicodeCategory.LineSeparator
                || category == UnicodeCategory.ParagraphSeparator
                || IsBidirectionalControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 문자의 표시 방향을 바꿔 로그의 앞뒤를 속일 수 있는 Unicode 양방향 제어 문자인지 확인합니다.
    /// </summary>
    /// <param name="character">검사할 UTF-16 문자입니다.</param>
    /// <returns>양방향 표시 제어 문자이면 true, 일반 문자이면 false를 반환합니다.</returns>
    private static bool IsBidirectionalControl(char character)
    {
        return character == '\u061C'
            || character == '\u200E'
            || character == '\u200F'
            || (character >= '\u202A' && character <= '\u202E')
            || (character >= '\u2066' && character <= '\u2069');
    }
}

/// <summary>
/// 애플리케이션이 외부로 전달할 저재고 경고 한 건입니다.
/// 단계의 세부 임계값은 주입한 Strategy의 책임이므로 상품의 기본 ReorderPoint와 단계를 다시 대조하지 않습니다.
/// </summary>
public sealed record StockAlert
{
    /// <summary>
    /// 검증된 재고 항목과 정의된 위험 단계를 한 경고 값으로 묶습니다.
    /// item은 경고 대상, level은 Strategy의 판정 결과이며 생성자는 객체만 초기화하고 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="item">경고 대상인 null이 아닌 불변 재고 항목입니다.</param>
    /// <param name="level">Strategy가 판정한 정의된 재고 위험 단계입니다.</param>
    public StockAlert(InventoryItem item, StockLevel level)
    {
        // `?? throw`는 왼쪽 값이 null일 때 즉시 오른쪽 예외를 던져 잘못된 객체 생성을 막는 문법이다.
        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (!Enum.IsDefined(level) || level == StockLevel.Healthy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "경고에는 Low 또는 Critical 재고 단계가 필요합니다.");
        }

        Level = level;
    }

    /// <summary>경고 대상인 불변 재고 항목입니다.</summary>
    public InventoryItem Item { get; }

    /// <summary>Strategy가 판정한 Low 또는 Critical 재고 위험 단계입니다.</summary>
    public StockLevel Level { get; }

    /// <summary>
    /// Strategy의 판정과 별개로, 기본 ReorderPoint보다 한 개 많아지는 데 필요한 수량을 반환합니다.
    /// 사용자 정의 Strategy가 더 일찍 경고하면 이 값은 0일 수 있으므로 주문 권고량으로 해석하지 않습니다.
    /// </summary>
    public int BaseReorderShortfallQuantity
    {
        get
        {
            // 두 수량의 상한이 보충 후 결과도 MaximumQuantity 안에 남도록 제한하므로 int overflow가 없다.
            return Math.Max(0, Item.ReorderPoint - Item.Quantity + 1);
        }
    }
}
