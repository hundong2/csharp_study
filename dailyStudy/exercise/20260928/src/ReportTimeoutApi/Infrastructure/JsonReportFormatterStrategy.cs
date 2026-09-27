using System.Text.Json;
using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Infrastructure;

/// <summary>
/// 청구 항목을 기계가 읽기 쉬운 JSON 문서로 바꾸는 Strategy Adapter입니다.
/// </summary>
public sealed class JsonReportFormatterStrategy : IReportFormatterStrategy
{
    // target-typed new는 왼쪽 JsonSerializerOptions 타입이 분명해서 생성자 이름 반복을 줄이는 문법입니다.
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // =>는 한 식의 값을 바로 반환하는 식 본문 getter입니다.
    /// <summary>Strategy 선택 키입니다.</summary>
    public string FormatName => "json";

    /// <summary>
    /// 고객 번호, 청구 항목, 합계를 camelCase JSON 문서로 직렬화합니다.
    /// </summary>
    /// <param name="customerId">문서와 파일 이름에 포함할 검증된 고객 번호입니다.</param>
    /// <param name="lines">JSON 배열로 변환할 청구 항목들입니다.</param>
    /// <param name="cancellationToken">직렬화 시작 전에 timeout을 확인할 취소 신호입니다.</param>
    /// <returns>application/json 본문과 파일 이름을 담은 ReportDocument를 반환합니다.</returns>
    public ReportDocument Format(
        string customerId,
        IReadOnlyList<InvoiceLine> lines,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 익명 형식 new { ... }은 이 메서드에서만 필요한 JSON 모양을 별도 class 없이 정의합니다.
        var payload = new
        {
            customerId,
            lineCount = lines.Count,
            total = lines.Sum(line => line.Amount),
            lines
        };

        var content = JsonSerializer.Serialize(payload, SerializerOptions);
        return new ReportDocument(
            $"invoice-report-{customerId.ToLowerInvariant()}.json",
            "application/json; charset=utf-8",
            content,
            lines.Count);
    }
}
