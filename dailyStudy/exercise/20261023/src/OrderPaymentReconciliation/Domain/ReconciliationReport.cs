using System.Collections.Immutable;

namespace OrderPaymentReconciliation.Domain;

/// <summary>
/// 한 번 계산된 대사 행을 변경 불가능한 snapshot으로 보관하는 불변 보고서입니다.
/// </summary>
public sealed record ReconciliationReport
{
    /// <summary>
    /// 이미 복사된 불변 행 모음을 저장합니다.
    /// </summary>
    /// <param name="lines">외부 수정이 불가능한 대사 행 snapshot입니다.</param>
    private ReconciliationReport(ImmutableArray<ReconciliationLine> lines)
    {
        Lines = lines;
    }

    /// <summary>항상 OrderId와 PaymentId 순서로 정렬된 불변 대사 행입니다.</summary>
    public ImmutableArray<ReconciliationLine> Lines { get; }

    /// <summary>C# 14 확장 속성을 LINQ predicate에서 사용해 검토 대상 수를 계산합니다.</summary>
    // =>는 getter 본문이 식 하나일 때 중괄호와 return을 줄이는 식 본문 문법입니다.
    public int ReviewCount => Lines.Count(line => line.NeedsReview);

    /// <summary>
    /// 지연 실행될 수 있는 IEnumerable을 OrderId·PaymentId 순서로 정렬하고 불변 snapshot으로 복사합니다.
    /// </summary>
    /// <param name="lines">보고서에 넣을 대사 행 sequence입니다.</param>
    /// <returns>결정적으로 정렬되고 호출 뒤 원본 collection 변경에도 영향받지 않는 보고서를 반환합니다.</returns>
    public static ReconciliationReport Create(IEnumerable<ReconciliationLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        // public factory 자체가 정렬과 snapshot 복사를 모두 책임져 어느 호출 경로에서도 같은 불변식을 보장합니다.
        var sortedSnapshot = lines
            .OrderBy(line => line.OrderId, StringComparer.Ordinal)
            .ThenBy(line => line.PaymentId ?? string.Empty, StringComparer.Ordinal)
            .ToImmutableArray();
        return new ReconciliationReport(sortedSnapshot);
    }

    /// <summary>
    /// 요청한 상태와 같은 행이 몇 개인지 계산합니다.
    /// </summary>
    /// <param name="status">개수를 알고 싶은 대사 상태입니다.</param>
    /// <returns>해당 상태인 행의 개수를 반환합니다.</returns>
    public int Count(ReconciliationStatus status)
    {
        // =>는 짧은 변환/판정 함수를 값처럼 LINQ에 전달하는 lambda 문법입니다.
        return Lines.Count(line => line.Status == status);
    }
}
