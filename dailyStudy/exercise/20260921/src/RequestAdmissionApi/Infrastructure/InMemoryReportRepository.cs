using System.Collections.Concurrent;
using RequestAdmissionApi.Application.Ports;
using RequestAdmissionApi.Domain;

namespace RequestAdmissionApi.Infrastructure;

/// <summary>
/// 학습 예제를 외부 데이터베이스 없이 실행하기 위한 메모리 저장소 어댑터입니다.
/// ConcurrentDictionary는 여러 HTTP 요청이 동시에 저장해도 내부 상태를 안전하게 보호합니다.
/// </summary>
public sealed class InMemoryReportRepository : IReportRepository
{
    private readonly ConcurrentDictionary<Guid, Report> reports = new();

    /// <summary>
    /// 보고서를 프로세스 메모리에 저장하거나 같은 식별자의 값을 교체합니다.
    /// <paramref name="report"/>는 저장할 보고서, <paramref name="cancellationToken"/>은 중단 신호이며 반환값은 없습니다.
    /// </summary>
    public ValueTask SaveAsync(Report report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        reports[report.Id] = report;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 식별자에 해당하는 보고서를 메모리에서 찾습니다.
    /// <paramref name="id"/>는 검색 키, <paramref name="cancellationToken"/>은 중단 신호이며 있으면 보고서, 없으면 null을 반환합니다.
    /// </summary>
    public ValueTask<Report?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        reports.TryGetValue(id, out var report);
        return ValueTask.FromResult(report);
    }
}
