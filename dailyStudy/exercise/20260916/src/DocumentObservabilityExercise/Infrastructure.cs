using System.Collections.Concurrent;

namespace DocumentObservabilityExercise;

/// <summary>
/// 학습용 문서 변환 Adapter입니다.
/// 실제 운영에서는 외부 변환 API나 전문 라이브러리가 이 Port를 구현할 수 있습니다.
/// </summary>
public sealed class SimpleDocumentConverter : IDocumentConverter
{
    /// <summary>
    /// 간단한 Markdown 표시를 걷어 낸 뒤 plain 또는 upper 문자열을 만듭니다.
    /// request는 검증된 원문과 형식, cancellationToken은 중단 신호이며,
    /// 변환 문서 또는 예측 가능한 정책 거절 Result를 반환합니다.
    /// </summary>
    /// <param name="request">변환할 원문과 허용된 출력 형식을 가진 요청입니다.</param>
    /// <param name="cancellationToken">실행 전에 확인할 호출자 취소 신호입니다.</param>
    /// <returns>변환된 문서 또는 샘플 정책에 따른 거절입니다.</returns>
    public Task<OperationResult<ConvertedDocument>> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // 이 표식은 외부 변환 엔진이 "지원하지 않는 문법"을 Result로 알리는 상황을 결정적으로 재현합니다.
        // 호출자가 내용을 고쳐 다시 요청할 수 있으므로 예외보다 예상 실패 값이 더 알맞습니다.
        if (request.SourceText.Contains("[[unsupported]]", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                OperationResult.Failure<ConvertedDocument>(
                    "conversion.syntax_unsupported",
                    "지원하지 않는 문서 문법이 포함되어 있습니다."));
        }

        var normalizedLines = request.SourceText
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim().TrimStart('#').Trim())
            .Where(line => line.Length > 0);

        var plainText = string.Join(" ", normalizedLines);

        // switch 식은 하나의 입력을 여러 경우에 대응시켜 결과 값 하나를 만드는 문법입니다.
        // 앞선 Request 검증으로 두 값만 올 수 있지만, 기본 분기는 계약 위반을 조용히 숨기지 않습니다.
        var convertedContent = request.TargetFormat switch
        {
            "plain" => plainText,
            "upper" => plainText.ToUpperInvariant(),
            _ => throw new InvalidOperationException("검증되지 않은 출력 형식이 전달되었습니다."),
        };

        var document = new ConvertedDocument(convertedContent, request.TargetFormat);
        return Task.FromResult(OperationResult.Success(document));
    }
}

/// <summary>
/// 완료 영수증을 프로세스 메모리에 저장하는 Repository Adapter입니다.
/// 학습과 테스트에는 결정적이지만 프로세스가 끝나면 데이터가 사라지므로 운영 저장소가 아닙니다.
/// </summary>
public sealed class InMemoryConversionReceiptRepository : IConversionReceiptRepository
{
    private readonly ConcurrentDictionary<string, ConversionReceipt> _receipts =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 작업 ID가 중복되지 않을 때 영수증을 메모리에 추가합니다.
    /// receipt는 저장할 결과, cancellationToken은 저장 전 확인할 취소 신호이며 반환값은 없습니다.
    /// 중복 키는 데이터 계약 위반이므로 덮어쓰지 않고 예외로 알려 기존 사실을 보호합니다.
    /// </summary>
    /// <param name="receipt">원문 없이 정책상 승인된 메타데이터만 가진 완료 영수증입니다.</param>
    /// <param name="cancellationToken">호출자가 저장을 중단했는지 알리는 신호입니다.</param>
    public Task SaveAsync(
        ConversionReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_receipts.TryAdd(receipt.JobId, receipt))
        {
            throw new InvalidOperationException($"작업 '{receipt.JobId}'의 영수증이 이미 저장되어 있습니다.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 현재 저장된 영수증을 작업 ID 순서의 복사본으로 반환합니다.
    /// 파라미터는 없고 읽기 전용 목록을 반환하며, 내부 ConcurrentDictionary를 직접 노출하지 않습니다.
    /// </summary>
    /// <returns>작업 ID 기준으로 정렬된 영수증 snapshot입니다.</returns>
    public IReadOnlyList<ConversionReceipt> GetSnapshot()
    {
        return _receipts.Values
            .OrderBy(receipt => receipt.JobId, StringComparer.Ordinal)
            .ToArray();
    }
}
