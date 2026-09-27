using System.Globalization;
using System.Text;
using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Infrastructure;

/// <summary>
/// 청구 항목을 스프레드시트에서 열기 쉬운 CSV 문서로 바꾸는 Strategy Adapter입니다.
/// </summary>
public sealed class CsvReportFormatterStrategy : IReportFormatterStrategy
{
    // =>는 한 식의 값을 바로 반환하는 식 본문 getter입니다.
    /// <summary>Strategy 선택 키입니다.</summary>
    public string FormatName => "csv";

    /// <summary>
    /// 헤더와 청구 행, 합계를 RFC 4180 스타일의 CSV 문자열로 만듭니다.
    /// </summary>
    /// <param name="customerId">파일 이름에 포함할 검증된 고객 번호입니다.</param>
    /// <param name="lines">CSV 행으로 변환할 청구 항목들입니다.</param>
    /// <param name="cancellationToken">행 변환 도중 timeout을 관찰할 취소 신호입니다.</param>
    /// <returns>text/csv 본문과 파일 이름을 담은 ReportDocument를 반환합니다.</returns>
    public ReportDocument Format(
        string customerId,
        IReadOnlyList<InvoiceLine> lines,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("invoiceNumber,description,amount");

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder
                .Append(EscapeCsv(line.InvoiceNumber))
                .Append(',')
                .Append(EscapeCsv(line.Description))
                .Append(',')
                // InvariantCulture는 실행 PC 언어가 달라도 소수점과 숫자 표현을 안정적으로 유지합니다.
                .Append(line.Amount.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        // LINQ Sum은 각 금액을 선택해 합계를 계산하며, 데이터 처리 의도를 반복문보다 직접 보여 줍니다.
        var total = lines.Sum(line => line.Amount);
        builder
            .Append("TOTAL,,")
            .Append(total.ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine();

        return new ReportDocument(
            $"invoice-report-{customerId.ToLowerInvariant()}.csv",
            "text/csv; charset=utf-8",
            builder.ToString(),
            lines.Count);
    }

    /// <summary>
    /// 쉼표·따옴표·줄바꿈이 있는 값을 따옴표로 감싸 CSV 열 경계가 깨지지 않게 합니다.
    /// </summary>
    /// <param name="value">CSV 한 칸에 넣을 원문 문자열입니다.</param>
    /// <returns>필요한 경우 따옴표를 두 번 이스케이프한 안전한 CSV 필드를 반환합니다.</returns>
    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\n') &&
            !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
