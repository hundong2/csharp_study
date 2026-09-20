using RequestAdmissionApi.Domain;

namespace RequestAdmissionApi.Application.Ports;

/// <summary>
/// 보고서 저장소의 추상 포트입니다. 저장 기술을 애플리케이션에서 분리해 메모리, 데이터베이스,
/// 테스트 대역을 DI로 자유롭게 교체할 수 있게 합니다.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// 완성된 보고서를 저장합니다.
    /// <paramref name="report"/>는 저장할 불변 객체, <paramref name="cancellationToken"/>은 중단 신호이며 반환값은 없습니다.
    /// </summary>
    ValueTask SaveAsync(Report report, CancellationToken cancellationToken);

    /// <summary>
    /// 식별자로 보고서를 조회합니다.
    /// <paramref name="id"/>는 찾을 고유 식별자, <paramref name="cancellationToken"/>은 중단 신호이며
    /// 존재하면 보고서를, 없으면 null을 반환합니다.
    /// </summary>
    ValueTask<Report?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
