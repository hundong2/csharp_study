using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Application;

/// <summary>
/// 입력 검증, Repository 조회, 포맷 Strategy 선택을 한 유스케이스 순서로 조정합니다.
/// </summary>
public sealed class ReportApplicationService
{
    private readonly IReportRepository _repository;
    private readonly IReadOnlyDictionary<string, IReportFormatterStrategy> _formatters;

    /// <summary>
    /// Port 구현을 주입받고 Strategy 키를 빠르게 찾을 읽기 전용 사전으로 준비합니다.
    /// </summary>
    /// <param name="repository">청구 데이터를 읽을 Repository Port 구현입니다.</param>
    /// <param name="formatters">csv/json 문서를 만들 Strategy 구현 모음입니다.</param>
    /// <returns>생성자는 서비스 의존성을 저장하며 별도 값을 반환하지 않습니다.</returns>
    public ReportApplicationService(
        IReportRepository repository,
        IEnumerable<IReportFormatterStrategy> formatters)
    {
        _repository = repository;
        // lambda의 =>는 각 formatter에서 키를 꺼내는 짧은 익명 함수이며, ToDictionary는 이름으로 찾을 사전을 만듭니다.
        // 같은 키가 두 개면 즉시 예외가 나서 잘못된 DI 구성을 숨기지 않습니다.
        _formatters = formatters.ToDictionary(
            formatter => formatter.FormatName,
            StringComparer.OrdinalIgnoreCase);

        // var는 컬렉션 원소가 string임이 분명할 때 타입 이름 반복을 줄입니다.
        foreach (var supportedFormat in ReportRequest.SupportedFormats)
        {
            if (!_formatters.ContainsKey(supportedFormat))
            {
                // 요청이 Repository를 읽기 전에 제품 허용 목록과 DI 등록의 불일치를 빠르게 드러냅니다.
                // $"..." 문자열 보간은 중괄호 안의 값을 오류 문장에 안전하게 끼워 넣습니다.
                throw new InvalidOperationException(
                    $"'{supportedFormat}' 보고서 Strategy가 DI에 등록되지 않았습니다.");
            }
        }
    }

    /// <summary>
    /// 원시 입력을 검증하고, 데이터를 읽은 뒤 선택한 형식의 보고서를 생성합니다.
    /// </summary>
    /// <param name="customerId">route에서 전달된 고객 번호입니다.</param>
    /// <param name="format">query에서 전달된 출력 형식이며 null이면 csv입니다.</param>
    /// <param name="simulatedLatencyMilliseconds">Repository가 기다릴 교육용 지연 시간입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 중단과 request timeout을 아래 계층까지 전달하는 신호입니다.</param>
    /// <returns>생성된 문서 또는 예상된 입력·미존재 오류를 담아 완료되는 Task를 반환합니다.</returns>
    // async는 메서드가 await를 사용할 수 있게 하고, string?은 입력이 없을 수 있음을 나타냅니다.
    public async Task<Result<ReportDocument>> GenerateAsync(
        string? customerId,
        string? format,
        int simulatedLatencyMilliseconds,
        CancellationToken cancellationToken)
    {
        var requestResult = ReportRequest.Create(
            customerId,
            format,
            simulatedLatencyMilliseconds);
        if (!requestResult.IsSuccess)
        {
            // !는 위 분기로 Error가 반드시 존재한다는 불변식을 nullable 분석기에 알려 주는 null-forgiving 연산자입니다.
            return Result<ReportDocument>.Failure(requestResult.Error!);
        }

        var request = requestResult.Value!;

        // await는 대기하는 동안 요청 스레드를 붙잡지 않습니다. 같은 token을 그대로 넘겨야 timeout이 실제 작업을 멈춥니다.
        var lines = await _repository.LoadInvoiceLinesAsync(
            request.CustomerId,
            request.SimulatedLatency,
            cancellationToken);

        // I/O가 완료되는 바로 그 순간 deadline이 지날 수 있으므로 404 같은 업무 분기보다 취소를 먼저 확정합니다.
        cancellationToken.ThrowIfCancellationRequested();

        if (lines.Count == 0)
        {
            return Result<ReportDocument>.Failure(new DomainError(
                "report.customer.not_found",
                $"고객 '{request.CustomerId}'의 청구 데이터를 찾을 수 없습니다."));
        }

        // out var는 조회 성공 여부와 함께 찾은 Strategy 지역 변수를 선언합니다.
        if (!_formatters.TryGetValue(request.Format, out var formatter))
        {
            // 사용자가 고칠 입력 오류가 아니라 Composition Root의 누락이므로 조용한 Result 대신 예외로 빠르게 드러냅니다.
            throw new InvalidOperationException(
                $"'{request.Format}' 보고서 Strategy가 DI에 등록되지 않았습니다.");
        }

        var document = formatter.Format(request.CustomerId, lines, cancellationToken);

        // 짧은 동기 포맷 중 deadline이 지난 경우에도 늦은 성공을 반환하지 않도록 응답 직전에 다시 확인합니다.
        cancellationToken.ThrowIfCancellationRequested();
        return Result<ReportDocument>.Success(document);
    }
}
