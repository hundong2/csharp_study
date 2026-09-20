using RequestAdmissionApi.Domain;

namespace RequestAdmissionApi.Application.Ports;

/// <summary>
/// 보고서 형식별 렌더링 전략이 지켜야 할 포트입니다. 애플리케이션은 구체 구현을 모르므로
/// 텍스트 외 형식을 추가하거나 테스트 가짜 구현으로 교체하기 쉽습니다.
/// </summary>
public interface IReportRenderer
{
    ReportFormat Format { get; }

    /// <summary>
    /// 검증된 요청을 해당 전략의 본문으로 비동기 변환합니다.
    /// <paramref name="request"/>는 도메인 입력, <paramref name="cancellationToken"/>은 요청 중단 신호이며
    /// 완성된 문자열을 반환합니다.
    /// ValueTask는 구현이 즉시 결과를 만들 수 있을 때 Task 객체 할당을 피할 수 있는 비동기 반환 형식입니다.
    /// </summary>
    ValueTask<string> RenderAsync(ReportRequest request, CancellationToken cancellationToken);
}
