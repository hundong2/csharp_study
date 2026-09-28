using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;

namespace ServiceHealthApi.Application;

/// <summary>
/// 필수 의존성 장애는 Unhealthy, 선택 의존성만의 장애는 Degraded로 판정합니다.
/// </summary>
public sealed class RequiredOptionalReadinessPolicy : IReadinessPolicy
{
    /// <summary>
    /// 필수 장애를 가장 우선하고, 그다음 선택 장애, 마지막으로 전체 정상을 판정합니다.
    /// </summary>
    /// <param name="observations">Application Service가 수집한 관찰 목록입니다.</param>
    /// <returns>우선순위 규칙을 적용한 준비 상태와 복사된 관찰 목록을 반환합니다.</returns>
    public ReadinessDecision Evaluate(IReadOnlyList<DependencyObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
        {
            throw new InvalidOperationException("준비 상태를 판단하려면 의존성 관찰이 하나 이상 필요합니다.");
        }

        if (observations.Any(observation =>
                !Enum.IsDefined(observation.Importance) ||
                !Enum.IsDefined(observation.Condition)))
        {
            throw new InvalidOperationException("정의되지 않은 dependency enum은 준비 완료로 처리할 수 없습니다.");
        }

        // LINQ Any는 목록을 직접 반복하는 구현보다 "조건에 맞는 항목이 하나라도 있는가"라는 의도를 드러냅니다.
        var requiredUnavailable = observations.Any(
            observation => observation.Importance == DependencyImportance.Required &&
                observation.Condition == DependencyCondition.Unavailable);
        var optionalUnavailable = observations.Any(
            observation => observation.Importance == DependencyImportance.Optional &&
                observation.Condition == DependencyCondition.Unavailable);

        // switch 식은 위에서 계산한 두 bool 조합을 빠짐없이 결과 하나로 바꿉니다.
        var level = (requiredUnavailable, optionalUnavailable) switch
        {
            (true, _) => ReadinessLevel.Unhealthy,
            (false, true) => ReadinessLevel.Degraded,
            _ => ReadinessLevel.Healthy
        };

        return ReadinessDecision.Create(level, observations);
    }
}
