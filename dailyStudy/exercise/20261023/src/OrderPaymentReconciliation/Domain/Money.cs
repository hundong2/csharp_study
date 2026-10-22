using System.Globalization;

namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 금액과 통화를 항상 함께 다뤄 서로 다른 통화의 숫자를 실수로 비교하지 않게 하는 불변 값 객체입니다.
/// 생성 규칙을 Create에 모아 모든 Money 인스턴스가 같은 불변식을 지키게 합니다.
/// </summary>
// record는 값 중심 equality를 제공하는 C# 형식이며, 이 예제는 get-only property로 변경도 제한합니다.
public sealed record Money
{
    /// <summary>
    /// 검증을 통과한 값만 내부 생성자로 들어오게 해 잘못된 상태 생성을 막습니다.
    /// </summary>
    /// <param name="amount">0 이상이고 소수 둘째 자리까지인 금액입니다.</param>
    /// <param name="currency">영문 대문자 세 글자로 정규화된 통화 코드입니다.</param>
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// 결제 단위의 숫자 금액입니다. decimal은 이진 부동소수점 오차를 피해야 하는 금액 계산에 적합합니다.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// KRW, USD처럼 비교 기준을 명확히 하는 세 글자 통화 코드입니다.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// 원시 금액과 nullable 통화 문자열을 검증하고 정규화된 Money를 만듭니다.
    /// </summary>
    /// <param name="amount">0 이상이며 소수 둘째 자리보다 정밀하지 않아야 하는 금액입니다.</param>
    /// <param name="currency">앞뒤 공백을 제거한 뒤 영문 세 글자여야 하는 통화 코드입니다.</param>
    /// <returns>유효하면 Money, 잘못되면 어떤 입력을 고쳐야 하는지 담은 Error를 반환합니다.</returns>
    public static Result<Money> Create(decimal amount, string? currency)
    {
        // string?의 ?는 외부 입력 currency가 null일 가능성을 compiler 분석에 포함합니다.
        if (amount < 0m)
        {
            return Result<Money>.Failure(
                new Error("money.amount_negative", "금액은 0 이상이어야 합니다.", "amount"));
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return Result<Money>.Failure(
                new Error("money.amount_scale", "금액은 소수 둘째 자리까지만 입력할 수 있습니다.", "amount"));
        }

        // ?.는 currency가 null이면 Trim을 호출하지 않고 null을 유지합니다.
        // 이후 ToUpperInvariant를 적용해 " krw "와 "KRW"를 같은 도메인 값으로 정규화합니다.
        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            return Result<Money>.Failure(
                new Error("money.currency_required", "통화 코드는 필수입니다.", "currency"));
        }

        // lambda 식은 각 문자가 A부터 Z 사이인지 검사하는 작은 함수를 Any에 전달합니다.
        if (normalizedCurrency.Length != 3
            || normalizedCurrency.Any(character => character is < 'A' or > 'Z'))
        {
            return Result<Money>.Failure(
                new Error("money.currency_invalid", "통화 코드는 영문 세 글자여야 합니다.", "currency"));
        }

        return Result<Money>.Success(new Money(amount, normalizedCurrency));
    }

    /// <summary>
    /// 현재 문화권과 관계없이 항상 점을 소수점으로 쓰는 학습용 표시 문자열을 만듭니다.
    /// </summary>
    /// <returns>예: 35000.00 KRW 형식의 문자열을 반환합니다.</returns>
    public override string ToString()
    {
        return $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
    }
}
