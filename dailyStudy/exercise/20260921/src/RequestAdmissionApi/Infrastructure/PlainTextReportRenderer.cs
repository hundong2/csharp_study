using System.Globalization;
using RequestAdmissionApi.Application.Ports;
using RequestAdmissionApi.Domain;

namespace RequestAdmissionApi.Infrastructure;

/// <summary>
/// 검증된 보고서를 읽기 쉬운 일반 텍스트로 만드는 Strategy 어댑터입니다.
/// 실제 시스템의 비싼 파일/네트워크 I/O를 보여 주기 위해 짧은 지연을 의도적으로 흉내 냅니다.
/// </summary>
public sealed class PlainTextReportRenderer : IReportRenderer
{
    private static readonly TimeSpan SimulatedExternalIoDelay = TimeSpan.FromMilliseconds(250);

    public ReportFormat Format => ReportFormat.PlainText;

    /// <summary>
    /// 요청 행들을 일반 텍스트 보고서로 비동기 렌더링합니다.
    /// <paramref name="request"/>는 검증된 입력, <paramref name="cancellationToken"/>은 지연과 작업을 취소하는 신호이며
    /// 완성된 텍스트를 반환합니다.
    /// </summary>
    public async ValueTask<string> RenderAsync(
        ReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Task.Delay는 학습용 외부 I/O 시뮬레이션입니다. 운영에서는 실제 렌더링/스토리지 호출로 교체합니다.
        await Task.Delay(SimulatedExternalIoDelay, cancellationToken);

        // Select는 각 도메인 행을 출력 문자열로 투영하며 원본 컬렉션을 바꾸지 않습니다.
        var lines = request.Rows.Select(
            row => $"- {row.Label}: {row.Value.ToString("0.##", CultureInfo.InvariantCulture)}");

        return $"{request.Title}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }
}
