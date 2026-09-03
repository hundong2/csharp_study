// using은 다른 네임스페이스의 형식을 짧은 이름으로 쓰게 하는 문법입니다.
// ReadOnlyDictionary는 내부 Dictionary가 외부에서 수정되지 않도록 읽기 전용 창을 제공합니다.
using System.Collections.ObjectModel;

// enum은 가능한 오류 종류를 정해 문자열 오타를 막고, 호출자가 실패 원인별로 분기하게 합니다.
enum ErrorKind
{
    Validation,
    RemoteService,
    InvalidPayload,
    UnsupportedCurrency
}

// record는 값이 같으면 같은 것으로 비교하는 데이터 중심 형식입니다.
// 오류를 불변 값으로 전달하면 하위 계층에서 만든 실패 의미가 상위 계층에서 몰래 바뀌지 않습니다.
/// <summary>
/// 예상 가능한 실패의 분류와 사람이 읽을 설명을 하나의 불변 값으로 묶습니다.
/// </summary>
/// <param name="Kind">호출자가 실패 종류별로 분기할 수 있는 오류 분류입니다.</param>
/// <param name="Message">로그나 화면에 보여 줄 구체적인 실패 설명입니다.</param>
/// <remarks>record의 괄호는 주 생성자이며 값을 초기화하고 별도 반환값은 없습니다.</remarks>
sealed record DomainError(ErrorKind Kind, string Message);

// sealed는 다른 형식이 상속으로 규칙을 바꾸지 못하게 하고, record는 "USD"처럼 값 자체를 정체성으로 비교합니다.
sealed record Currency
{
    // get만 있는 속성은 생성 뒤 바꿀 setter가 없어 값 객체의 불변성을 지킵니다.
    public string Code { get; }

    /// <summary>
    /// 이미 검증된 ISO 통화 코드를 불변 Currency 값 객체에 저장합니다.
    /// </summary>
    /// <param name="code">대문자 세 글자로 검증이 끝난 통화 코드입니다.</param>
    /// <remarks>생성자는 객체만 초기화하므로 반환값이 없으며, 외부에서는 Create를 통해서만 호출됩니다.</remarks>
    private Currency(string code)
    {
        Code = code;
    }

    /// <summary>
    /// 사용자가 입력한 문자열을 공백 제거·대문자화하고 ISO 형식의 세 글자인지 검증합니다.
    /// </summary>
    /// <param name="code">예: usd 또는 KRW이며, 입력하지 않은 경우도 검증하려고 null을 허용합니다.</param>
    /// <returns>형식이 맞으면 Currency를 담은 성공 Result, 아니면 설명을 담은 실패 Result를 반환합니다.</returns>
    /// <remarks>string?의 ?는 null 가능성을 표시하고, Result&lt;Currency&gt;의 꺾쇠는 성공값 형식을 Currency로 정하는 제네릭 문법입니다.</remarks>
    public static Result<Currency> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<Currency>.Failure(
                new DomainError(ErrorKind.Validation, "통화 코드는 비어 있을 수 없습니다."));
        }

        // var는 오른쪽 값이 string임이 분명할 때 형식 이름의 반복만 생략합니다. 동적 형식은 아닙니다.
        var normalized = code.Trim().ToUpperInvariant();

        // LINQ의 All은 각 문자가 조건을 만족하는지 검사합니다. 규칙의 의도를 반복문보다 직접 드러냅니다.
        // letter => ...는 한 문자를 받아 bool을 돌려주는 짧은 익명 함수(람다)입니다.
        if (normalized.Length != 3 || !normalized.All(letter => char.IsAsciiLetter(letter)))
        {
            return Result<Currency>.Failure(
                new DomainError(ErrorKind.Validation, "통화 코드는 영문 세 글자여야 합니다."));
        }

        return Result<Currency>.Success(new Currency(normalized));
    }

    /// <summary>
    /// Currency를 화면이나 URL에 사용할 때 내부의 표준 통화 코드 문자열로 표현합니다.
    /// </summary>
    /// <returns>예: USD처럼 정규화된 세 글자 코드를 반환합니다.</returns>
    /// <remarks>override는 record가 상속받은 ToString 동작을 이 값 객체에 알맞은 표현으로 교체한다는 뜻입니다.</remarks>
    public override string ToString()
    {
        return Code;
    }
}

// Money도 불변 record로 만들어 금액과 통화가 서로 떨어지거나 생성 뒤 바뀌는 오류를 막습니다.
// decimal은 10진 소수를 정확히 표현해 이진 부동소수점보다 금액 계산에 적합합니다.
sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    /// <summary>
    /// 검증된 양수 금액과 통화를 한 덩어리의 불변 값 객체로 묶습니다.
    /// </summary>
    /// <param name="amount">0보다 큰 금액입니다.</param>
    /// <param name="currency">금액의 단위를 나타내는 검증된 통화입니다.</param>
    /// <remarks>생성자는 값을 보관할 뿐 반환값이 없으며, 외부에서는 Create로만 생성합니다.</remarks>
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// 금액이 양수인지와 통화 객체가 존재하는지를 확인한 뒤 Money를 만듭니다.
    /// </summary>
    /// <param name="amount">환산할 원래 금액이며 반드시 0보다 커야 합니다.</param>
    /// <param name="currency">금액의 기준 통화이며 null이면 프로그래머의 조립 오류입니다.</param>
    /// <returns>유효하면 Money 성공 Result, 0 이하이면 검증 실패 Result를 반환합니다.</returns>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (amount <= 0m)
        {
            return Result<Money>.Failure(
                new DomainError(ErrorKind.Validation, "환산 금액은 0보다 커야 합니다."));
        }

        return Result<Money>.Success(new Money(amount, currency));
    }
}

// 환산 요청은 원본 Money와 목표 Currency를 함께 전달하는 불변 명령 데이터입니다.
/// <summary>
/// 한 번의 환산에 필요한 검증된 원본 금액과 목표 통화를 묶습니다.
/// </summary>
/// <param name="Source">금액과 원본 통화를 함께 가진 Money 값 객체입니다.</param>
/// <param name="Target">환산 결과가 사용해야 할 목표 Currency 값 객체입니다.</param>
/// <remarks>record의 주 생성자는 두 값을 초기화하며 별도 반환값은 없습니다.</remarks>
sealed record ConversionRequest(Money Source, Currency Target);

// 환산 결과는 계산에 사용한 환율과 기준 날짜까지 보존하여 결과를 나중에 설명할 수 있게 합니다.
/// <summary>
/// 환산 전후 금액, 적용 환율, 데이터 기준일을 감사 가능한 불변 결과로 묶습니다.
/// </summary>
/// <param name="Source">서비스가 받은 원본 Money입니다.</param>
/// <param name="Converted">목표 통화로 계산된 Money입니다.</param>
/// <param name="AppliedRate">계산에 실제로 곱한 환율입니다.</param>
/// <param name="RateDate">Gateway 응답에 적힌 환율 기준 날짜입니다.</param>
/// <remarks>record의 주 생성자는 결과 값을 초기화하며 별도 반환값은 없습니다.</remarks>
sealed record ConversionReceipt(
    Money Source,
    Money Converted,
    decimal AppliedRate,
    DateOnly RateDate);

// ExchangeRateTable은 API DTO를 그대로 노출하지 않는 Domain Model입니다.
// 덕분에 외부 JSON 모양이 바뀌어도 업무 계층은 이 안정적인 모델만 의존합니다.
sealed class ExchangeRateTable
{
    private readonly IReadOnlyDictionary<Currency, decimal> _rates;

    public Currency BaseCurrency { get; }
    public DateOnly RateDate { get; }

    /// <summary>
    /// 검증과 복사가 끝난 환율 사전을 기준 통화·날짜와 함께 불변 표로 보관합니다.
    /// </summary>
    /// <param name="baseCurrency">모든 환율의 기준이 되는 통화입니다.</param>
    /// <param name="rateDate">환율 데이터가 적용되는 날짜입니다.</param>
    /// <param name="rates">외부에서 변경할 수 없도록 감싼 목표 통화별 환율 사전입니다.</param>
    /// <remarks>생성자는 표를 초기화하며 반환값은 없습니다. Create가 검증한 값만 전달합니다.</remarks>
    private ExchangeRateTable(
        Currency baseCurrency,
        DateOnly rateDate,
        IReadOnlyDictionary<Currency, decimal> rates)
    {
        BaseCurrency = baseCurrency;
        RateDate = rateDate;
        _rates = rates;
    }

    /// <summary>
    /// Adapter가 전달한 환율 목록에 중복 통화나 0 이하 환율이 없는지 확인하고 안전한 표를 만듭니다.
    /// </summary>
    /// <param name="baseCurrency">API 응답이 사용하는 기준 통화입니다.</param>
    /// <param name="rateDate">API가 알려 준 환율 기준 날짜입니다.</param>
    /// <param name="rates">검증할 목표 통화와 환율 쌍의 열거 가능한 목록입니다.</param>
    /// <returns>검증된 ExchangeRateTable 또는 잘못된 응답을 설명하는 실패 Result를 반환합니다.</returns>
    public static Result<ExchangeRateTable> Create(
        Currency baseCurrency,
        DateOnly rateDate,
        IEnumerable<KeyValuePair<Currency, decimal>> rates)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);
        ArgumentNullException.ThrowIfNull(rates);

        // default(DateOnly)는 0001-01-01입니다. 최신 환율 날짜로는 의미가 없으므로 누락된 날짜처럼 거부합니다.
        if (rateDate == default)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(ErrorKind.InvalidPayload, "환율 기준 날짜가 없습니다."));
        }

        var copy = new Dictionary<Currency, decimal>();
        foreach (var pair in rates)
        {
            if (pair.Value <= 0m)
            {
                return Result<ExchangeRateTable>.Failure(
                    new DomainError(
                        ErrorKind.InvalidPayload,
                        $"{pair.Key.Code} 환율은 0보다 커야 합니다."));
            }

            if (!copy.TryAdd(pair.Key, pair.Value))
            {
                return Result<ExchangeRateTable>.Failure(
                    new DomainError(
                        ErrorKind.InvalidPayload,
                        $"{pair.Key.Code} 환율이 중복되었습니다."));
            }
        }

        if (copy.Count == 0)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(ErrorKind.InvalidPayload, "환율 목록이 비어 있습니다."));
        }

        // ReadOnlyDictionary는 복사본을 감싸 호출자가 환율을 바꾸지 못하게 합니다.
        return Result<ExchangeRateTable>.Success(
            new ExchangeRateTable(
                baseCurrency,
                rateDate,
                new ReadOnlyDictionary<Currency, decimal>(copy)));
    }

    /// <summary>
    /// 목표 통화에 대응하는 환율이 표에 있는지 확인합니다.
    /// </summary>
    /// <param name="target">찾으려는 목표 통화입니다.</param>
    /// <param name="rate">찾으면 환율이 기록되고, 찾지 못하면 decimal의 기본값 0이 기록됩니다.</param>
    /// <returns>목표 통화 환율을 찾았으면 true, 없으면 false를 반환합니다.</returns>
    public bool TryGetRate(Currency target, out decimal rate)
    {
        ArgumentNullException.ThrowIfNull(target);
        // out은 메서드가 bool 반환값과 별도로 찾은 환율을 호출자 변수에 써 주는 출력 매개변수 문법입니다.
        return _rates.TryGetValue(target, out rate);
    }
}

// Result<T>의 T는 성공값 형식을 호출자가 정하는 제네릭 매개변수입니다.
// 예상 가능한 검증/API 실패를 예외 대신 값으로 돌려주면 호출자가 실패 처리를 빠뜨리기 어렵습니다.
sealed class Result<T>
{
    // T?와 DomainError?의 ?는 실패 또는 성공 한쪽 값이 없을 수 있음을 정적 분석에 알립니다.
    public T? Value { get; }
    public DomainError? Problem { get; }

    // =>는 식 하나의 결과를 바로 반환하는 식 본문이고, "is null"은 null인지 안전하게 확인하는 패턴입니다.
    public bool IsSuccess => Problem is null;

    /// <summary>
    /// 성공값과 실패 정보를 한 상자에 보관하면서 외부가 모순된 조합을 만들지 못하게 합니다.
    /// </summary>
    /// <param name="value">성공 시 실제 값이며 실패 시 T의 기본값입니다.</param>
    /// <param name="problem">실패 시 오류 정보이며 성공 시 null입니다.</param>
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
    /// <param name="problem">오류 종류와 초보자도 이해할 수 있는 설명입니다.</param>
    /// <returns>기본 성공값과 오류 정보를 가진 Result를 반환합니다.</returns>
    public static Result<T> Failure(DomainError problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        // default는 T가 어떤 형식이든 그 형식의 기본값을 만들며, 실패 Result에서는 성공값을 사용하지 않습니다.
        return new Result<T>(default, problem);
    }
}
