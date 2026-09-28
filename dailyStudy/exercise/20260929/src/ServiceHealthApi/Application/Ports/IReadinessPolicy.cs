using ServiceHealthApi.Domain;

namespace ServiceHealthApi.Application.Ports;

/// <summary>
/// 여러 의존성 관찰을 Healthy, Degraded, Unhealthy 중 하나로 합치는 Strategy 계약입니다.
/// </summary>
public interface IReadinessPolicy
{
    /// <summary>
    /// 필수/선택 규칙에 따라 전체 준비 상태를 계산합니다.
    /// </summary>
    /// <param name="observations">이번 health check에서 얻은 의존성 관찰 목록입니다.</param>
    /// <returns>최종 준비 단계와 판정 근거의 불변 스냅샷을 반환합니다.</returns>
    ReadinessDecision Evaluate(IReadOnlyList<DependencyObservation> observations);
}
