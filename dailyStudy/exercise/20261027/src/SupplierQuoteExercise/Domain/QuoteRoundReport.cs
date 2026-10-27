using System.Collections.Immutable;

namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 한 차례의 병렬 견적 수집 결과와 선택된 최종 견적을 불변 snapshot으로 보관합니다.
/// ImmutableArray는 호출자가 원래 List를 바꿔도 저장된 report가 변하지 않게 합니다.
/// </summary>
public sealed class QuoteRoundReport
{
    /// <summary>
    /// 검증된 요청, 완료 순서 결과, 선택 견적의 snapshot을 보관합니다.
    /// 세 파라미터로 snapshot을 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="request">이 수집 회차의 원본 요청입니다.</param>
    /// <param name="completionOrder">gateway가 실제 완료된 순서의 불변 결과입니다.</param>
    /// <param name="selectedQuote">정책이 선택한 견적이며 선택 가능 항목이 없으면 null입니다.</param>
    private QuoteRoundReport(
        QuoteRequest request,
        ImmutableArray<QuoteAttempt> completionOrder,
        SupplierQuote? selectedQuote)
    {
        Request = request;
        CompletionOrder = completionOrder;
        SelectedQuote = selectedQuote;
    }

    /// <summary>이 수집 회차의 견적 요청을 반환합니다.</summary>
    public QuoteRequest Request { get; }

    /// <summary>각 공급업체 결과를 실제 완료된 순서로 반환합니다.</summary>
    public ImmutableArray<QuoteAttempt> CompletionOrder { get; }

    /// <summary>정책이 선택한 견적 또는 선택할 수 없을 때 null을 반환합니다.</summary>
    public SupplierQuote? SelectedQuote { get; }

    /// <summary>
    /// 성공한 공급업체 응답 수를 계산합니다.
    /// </summary>
    /// <returns>Outcome이 성공인 항목 개수를 반환합니다.</returns>
    public int CountSuccesses()
    {
        // LINQ Count는 조건을 만족하는 원소 수를 선언적으로 계산합니다.
        return CompletionOrder.Count(attempt => attempt.Outcome.IsSuccess);
    }

    /// <summary>
    /// 실패한 공급업체 응답 수를 계산합니다.
    /// </summary>
    /// <returns>Outcome이 실패인 항목 개수를 반환합니다.</returns>
    public int CountFailures()
    {
        return CompletionOrder.Count(attempt => !attempt.Outcome.IsSuccess);
    }

    /// <summary>
    /// 변경 가능한 완료 목록을 복사해 안전한 QuoteRoundReport snapshot을 만듭니다.
    /// </summary>
    /// <param name="request">이 회차를 시작한 견적 요청입니다.</param>
    /// <param name="completionOrder">공급업체 결과를 실제 완료 순서로 나열한 sequence입니다.</param>
    /// <param name="selectedQuote">선택 정책의 결과이며 선택 불가 시 null입니다.</param>
    /// <returns>외부 변경에 영향을 받지 않는 새 QuoteRoundReport를 반환합니다.</returns>
    public static QuoteRoundReport Create(
        QuoteRequest request,
        IEnumerable<QuoteAttempt> completionOrder,
        SupplierQuote? selectedQuote)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(completionOrder);

        var snapshot = completionOrder.ToImmutableArray();
        if (snapshot.IsDefaultOrEmpty)
        {
            throw new ArgumentException("완료 결과가 하나 이상 필요합니다.", nameof(completionOrder));
        }

        var supplierIds = new HashSet<string>(StringComparer.Ordinal);
        var successfulQuotes = new List<SupplierQuote>();
        foreach (var attempt in snapshot)
        {
            if (attempt is null)
            {
                throw new ArgumentException("완료 결과에 null을 넣을 수 없습니다.", nameof(completionOrder));
            }

            if (string.IsNullOrWhiteSpace(attempt.SupplierId))
            {
                throw new ArgumentException(
                    "완료 결과의 공급업체 ID는 비어 있거나 공백일 수 없습니다.",
                    nameof(completionOrder));
            }

            if (attempt.Outcome is null)
            {
                throw new ArgumentException("완료 결과의 Outcome은 null일 수 없습니다.", nameof(completionOrder));
            }

            if (!supplierIds.Add(attempt.SupplierId))
            {
                throw new ArgumentException(
                    $"공급업체 ID '{attempt.SupplierId}'의 완료 결과가 중복되었습니다.",
                    nameof(completionOrder));
            }

            if (!attempt.Outcome.IsSuccess)
            {
                continue;
            }

            if (!StringComparer.Ordinal.Equals(
                    attempt.SupplierId,
                    attempt.Outcome.Value.SupplierId))
            {
                throw new ArgumentException(
                    $"완료 결과 ID '{attempt.SupplierId}'와 성공 견적 ID "
                    + $"'{attempt.Outcome.Value.SupplierId}'가 다릅니다.",
                    nameof(completionOrder));
            }

            successfulQuotes.Add(attempt.Outcome.Value);
        }

        // `is not null` pattern은 선택 값이 실제로 있을 때만 성공 목록 포함 여부를 검사합니다.
        if (selectedQuote is not null && !successfulQuotes.Contains(selectedQuote))
        {
            throw new ArgumentException(
                "선택 견적은 이 회차의 성공 결과 중 하나여야 합니다.",
                nameof(selectedQuote));
        }

        return new QuoteRoundReport(request, snapshot, selectedQuote);
    }
}
