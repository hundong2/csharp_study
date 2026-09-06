// Domain 계층은 콘솔, 캐시, 데이터베이스 같은 기술을 모르고 업무에서 쓰는 값과 실패 의미만 정의합니다.

// enum은 가능한 오류 종류를 정해 문자열 오타를 막고, 호출자가 실패 원인별로 분기하게 합니다.
enum ErrorKind
{
    Validation,
    NotFound,
    Dependency
}

// record는 데이터 중심 형식에 값 동등성(value equality)을 제공합니다.
// 같은 Kind와 Message를 가진 오류를 같은 값으로 비교할 수 있고, 생성 뒤 내용을 바꾸지 않아 실패 의미가 안정적입니다.
/// <summary>
/// 예상 가능한 실패의 종류와 사람이 읽을 설명을 하나의 불변 값으로 묶습니다.
/// </summary>
/// <param name="Kind">호출자가 검증 실패, 미발견, 의존성 실패를 구분할 수 있는 분류입니다.</param>
/// <param name="Message">화면이나 로그에 전달할 구체적인 실패 설명입니다.</param>
/// <remarks>record의 괄호는 주 생성자이며 두 값을 초기화하고 별도 반환값은 없습니다.</remarks>
sealed record DomainError(ErrorKind Kind, string Message);

// 가격이 원본 저장소에서 왔는지 캐시에서 왔는지를 제한된 값으로 표현합니다.
enum PriceOrigin
{
    Repository,
    Cache
}

// SKU는 단순 문자열이 아니라 형식 규칙을 스스로 지키는 값 객체(Value Object)입니다.
// sealed는 상속으로 검증 규칙이 우회되는 일을 막고, record는 Code가 같으면 같은 SKU로 비교하게 합니다.
sealed record ProductSku
{
    // get만 있는 속성은 생성 뒤 바꿀 setter가 없어 값 객체의 불변성을 지킵니다.
    public string Code { get; }

    /// <summary>
    /// 이미 정규화와 검증이 끝난 상품 코드를 불변 SKU 값 객체에 저장합니다.
    /// </summary>
    /// <param name="code">대문자·숫자·하이픈 규칙을 통과한 상품 코드입니다.</param>
    /// <remarks>생성자는 값을 보관할 뿐 반환값이 없으며 외부에서는 Create를 통해서만 호출됩니다.</remarks>
    private ProductSku(string code)
    {
        Code = code;
    }

    /// <summary>
    /// 사용자가 입력한 문자열을 공백 제거·대문자화하고 안전한 상품 코드인지 검증합니다.
    /// </summary>
    /// <param name="code">예: laptop-15이며, 입력하지 않은 경우도 검증하려고 null을 허용합니다.</param>
    /// <returns>형식이 맞으면 ProductSku 성공 Result, 아니면 검증 오류를 담은 실패 Result를 반환합니다.</returns>
    /// <remarks>string?의 ?는 null 가능성을 표시하고, Result&lt;ProductSku&gt;는 성공값 형식을 지정하는 제네릭 문법입니다.</remarks>
    public static Result<ProductSku> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<ProductSku>.Failure(
                new DomainError(ErrorKind.Validation, "상품 코드는 비어 있을 수 없습니다."));
        }

        // var는 오른쪽 결과가 string임이 분명할 때 형식 이름 반복만 줄입니다. 실행 중 형식이 바뀌는 dynamic과 다릅니다.
        var normalized = code.Trim().ToUpperInvariant();

        // `is < 3 or > 20`은 값이 두 범위 중 하나에 속하는지 읽기 좋게 묶은 관계 패턴과 or 패턴입니다.
        if (normalized.Length is < 3 or > 20)
        {
            return Result<ProductSku>.Failure(
                new DomainError(ErrorKind.Validation, "상품 코드는 3~20자여야 합니다."));
        }

        // ||는 왼쪽 또는 오른쪽 조건 중 하나만 true여도 전체 조건을 true로 만드는 논리 OR 연산자입니다.
        if (normalized.StartsWith('-') || normalized.EndsWith('-'))
        {
            return Result<ProductSku>.Failure(
                new DomainError(ErrorKind.Validation, "상품 코드의 처음과 끝에는 하이픈을 쓸 수 없습니다."));
        }

        // LINQ의 Any는 조건을 어기는 문자가 하나라도 있는지 검사합니다.
        // character => ...는 문자 하나를 받아 bool을 돌려주는 람다이며, 여기서는 허용 문자 규칙을 가까이에 둡니다.
        if (normalized.Any(character =>
                !char.IsAsciiLetterUpper(character) &&
                !char.IsAsciiDigit(character) &&
                character != '-'))
        {
            return Result<ProductSku>.Failure(
                new DomainError(ErrorKind.Validation, "상품 코드는 영문, 숫자, 하이픈만 사용할 수 있습니다."));
        }

        return Result<ProductSku>.Success(new ProductSku(normalized));
    }

    /// <summary>
    /// SKU를 화면이나 로그에 사용할 때 내부의 정규화된 코드 문자열로 표현합니다.
    /// </summary>
    /// <returns>예: LAPTOP-15처럼 검증된 상품 코드 문자열을 반환합니다.</returns>
    /// <remarks>override는 record가 상속받은 ToString 동작을 이 값 객체에 맞게 교체한다는 뜻입니다.</remarks>
    public override string ToString()
    {
        return Code;
    }
}

// ProductPrice는 SKU, 금액, 통화를 함께 움직이게 하여 단위가 빠진 숫자가 돌아다니는 오류를 줄입니다.
sealed record ProductPrice
{
    public ProductSku Sku { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>
    /// 검증된 SKU, 양수 금액, 세 글자 통화 코드를 하나의 불변 가격으로 보관합니다.
    /// </summary>
    /// <param name="sku">가격이 속한 검증된 상품 코드입니다.</param>
    /// <param name="amount">0보다 큰 가격입니다.</param>
    /// <param name="currency">대문자 세 글자로 정규화된 통화 코드입니다.</param>
    /// <remarks>생성자는 값을 초기화하며 반환값이 없고, Create만 이 생성자를 호출합니다.</remarks>
    private ProductPrice(ProductSku sku, decimal amount, string currency)
    {
        Sku = sku;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// 원본 저장소에서 읽은 가격 데이터가 업무 규칙을 만족하는지 확인하고 ProductPrice를 만듭니다.
    /// </summary>
    /// <param name="sku">가격의 주인인 SKU이며 null이면 프로그래머의 조립 오류입니다.</param>
    /// <param name="amount">금융 계산에 사용할 decimal 가격이며 0보다 커야 합니다.</param>
    /// <param name="currency">예: krw처럼 대소문자가 섞일 수 있는 통화 코드입니다.</param>
    /// <returns>유효한 가격 또는 잘못된 원본 데이터를 설명하는 실패 Result를 반환합니다.</returns>
    public static Result<ProductPrice> Create(ProductSku sku, decimal amount, string? currency)
    {
        // ThrowIfNull은 복구 가능한 사용자 입력이 아니라 개발자가 잘못 연결한 객체를 빠르게 드러냅니다.
        ArgumentNullException.ThrowIfNull(sku);

        if (amount <= 0m)
        {
            // 숫자 뒤 m은 이 상수를 금융 계산에 적합한 decimal로 해석하라는 접미사입니다.
            return Result<ProductPrice>.Failure(
                new DomainError(ErrorKind.Dependency, "원본 가격은 0보다 커야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<ProductPrice>.Failure(
                new DomainError(ErrorKind.Dependency, "원본 가격의 통화 코드가 없습니다."));
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 ||
            !normalizedCurrency.All(character => char.IsAsciiLetterUpper(character)))
        {
            return Result<ProductPrice>.Failure(
                new DomainError(ErrorKind.Dependency, "통화 코드는 영문 세 글자여야 합니다."));
        }

        return Result<ProductPrice>.Success(
            new ProductPrice(sku, amount, normalizedCurrency));
    }
}

// 조회 결과에는 가격뿐 아니라 이번 호출의 출처와 가격의 신선도 기준 시각도 남겨 동작을 관찰할 수 있게 합니다.
/// <summary>
/// 조회된 상품 가격과 이번 호출의 출처, 가격의 신선도 기준 시각을 하나의 불변 결과로 묶습니다.
/// </summary>
/// <param name="Price">호출자에게 전달할 검증된 상품 가격입니다.</param>
/// <param name="Origin">이번 호출이 원본 저장소를 읽었는지 캐시를 읽었는지 나타냅니다.</param>
/// <param name="FreshAsOfUtc">가격이 원본에서 확인되어 신선하다고 볼 기준 UTC 시각이며, Decorator를 거쳐도 보존됩니다.</param>
/// <remarks>record의 주 생성자는 세 값을 초기화하며 별도 반환값은 없습니다.</remarks>
sealed record PriceLookup(
    ProductPrice Price,
    PriceOrigin Origin,
    DateTimeOffset FreshAsOfUtc);

/// <summary>
/// 캐시 hit 수, inner Provider 조회 시도 수, 현재 캐시 항목 수를 한 번에 보여 주는 불변 통계입니다.
/// </summary>
/// <param name="HitCount">유효한 캐시 항목을 재사용한 횟수입니다.</param>
/// <param name="InnerLoadCount">cache miss 뒤 감싼 IPriceProvider를 호출한 횟수이며 성공과 실패를 모두 셉니다.</param>
/// <param name="EntryCount">현재 프로세스 메모리에 남아 있는 캐시 항목 수입니다.</param>
/// <remarks>record의 주 생성자는 통계값을 초기화하며 별도 반환값은 없습니다.</remarks>
sealed record CacheStatistics(int HitCount, int InnerLoadCount, int EntryCount);

// Result<T>는 예상 가능한 실패를 예외 대신 값으로 전달합니다.
// 예외는 잘못된 DI 조립이나 취소처럼 정상 업무 분기로 보기 어려운 상황에 남겨 둡니다.
sealed class Result<T>
{
    // T?와 DomainError?의 ?는 성공 또는 실패 한쪽 값이 없을 수 있음을 컴파일러의 null 분석에 알립니다.
    public T? Value { get; }
    public DomainError? Problem { get; }

    // =>는 식 하나의 값을 바로 반환하는 식 본문이고, is null은 null 패턴 검사입니다.
    public bool IsSuccess => Problem is null;

    /// <summary>
    /// 성공값과 실패 정보를 함께 보관하면서 외부가 모순된 조합을 만들지 못하게 합니다.
    /// </summary>
    /// <param name="value">성공 시 실제 값이며 실패 시 T의 기본값입니다.</param>
    /// <param name="problem">실패 시 오류이며 성공 시 null입니다.</param>
    /// <remarks>private 생성자는 반환값이 없고 Success와 Failure 팩터리만 호출할 수 있습니다.</remarks>
    private Result(T? value, DomainError? problem)
    {
        Value = value;
        Problem = problem;
    }

    /// <summary>
    /// 정상 결과를 성공 Result로 감싸 성공과 실패의 반환 형식을 통일합니다.
    /// </summary>
    /// <param name="value">호출자에게 전달할 성공값입니다.</param>
    /// <returns>성공값과 null 오류를 가진 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null);
    }

    /// <summary>
    /// 호출자가 처리할 수 있는 예상 실패를 실패 Result로 감쌉니다.
    /// </summary>
    /// <param name="problem">실패의 종류와 설명입니다.</param>
    /// <returns>기본 성공값과 오류 정보를 가진 Result를 반환합니다.</returns>
    public static Result<T> Failure(DomainError problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        // default는 T가 어떤 형식이든 그 형식의 기본값을 만들며 실패 Result에서는 성공값을 사용하지 않습니다.
        return new Result<T>(default, problem);
    }
}
