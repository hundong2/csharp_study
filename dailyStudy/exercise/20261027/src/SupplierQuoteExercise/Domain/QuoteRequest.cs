namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 한 상품에 필요한 수량과 납기일을 나타내는 불변 Domain Model입니다.
/// private 생성자와 get 전용 속성은 검증되지 않은 요청이 만들어지는 길을 막습니다.
/// </summary>
public sealed record QuoteRequest
{
    /// <summary>
    /// factory 검증을 통과한 견적 요청 값을 보관합니다.
    /// 세 파라미터로 속성을 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="sku">공백이 제거되고 대문자로 정규화된 상품 코드입니다.</param>
    /// <param name="quantity">구매하려는 양수 수량입니다.</param>
    /// <param name="needBy">물품이 도착해야 하는 마지막 날짜입니다.</param>
    private QuoteRequest(string sku, int quantity, DateOnly needBy)
    {
        Sku = sku;
        Quantity = quantity;
        NeedBy = needBy;
    }

    /// <summary>정규화된 상품 코드를 반환합니다.</summary>
    public string Sku { get; }

    /// <summary>구매하려는 수량을 반환합니다.</summary>
    public int Quantity { get; }

    /// <summary>허용 가능한 최종 납기일을 반환합니다.</summary>
    public DateOnly NeedBy { get; }

    /// <summary>
    /// 외부 입력을 검증하고 유효할 때만 QuoteRequest를 만듭니다.
    /// </summary>
    /// <param name="sku">사용자가 입력한 상품 코드이며 앞뒤 공백은 제거됩니다.</param>
    /// <param name="quantity">요청 수량이며 학습 예제에서는 1부터 10,000까지 허용합니다.</param>
    /// <param name="needBy">물품이 필요한 날짜입니다.</param>
    /// <returns>검증 성공 시 QuoteRequest, 실패 시 구체적인 업무 오류를 반환합니다.</returns>
    public static Result<QuoteRequest> Create(string? sku, int quantity, DateOnly needBy)
    {
        // ?.는 왼쪽 값이 null이면 메서드를 호출하지 않고 null을 만드는 null-conditional 문법입니다.
        // var는 오른쪽 식에서 형식이 분명할 때 compiler가 정적 형식을 추론하며 dynamic이 아닙니다.
        var normalizedSku = sku?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSku))
        {
            return Result<QuoteRequest>.Failure(
                Error.Business("quote_request.sku_required", "상품 코드는 비어 있을 수 없습니다."));
        }

        if (normalizedSku.Length > 32)
        {
            return Result<QuoteRequest>.Failure(
                Error.Business("quote_request.sku_too_long", "상품 코드는 32자 이하여야 합니다."));
        }

        // `is < 1 or > 10_000`은 값의 범위를 읽기 쉽게 표현하는 관계·논리 패턴입니다.
        if (quantity is < 1 or > 10_000)
        {
            return Result<QuoteRequest>.Failure(
                Error.Business("quote_request.quantity_out_of_range", "수량은 1부터 10,000까지여야 합니다."));
        }

        return Result<QuoteRequest>.Success(new QuoteRequest(normalizedSku, quantity, needBy));
    }
}
