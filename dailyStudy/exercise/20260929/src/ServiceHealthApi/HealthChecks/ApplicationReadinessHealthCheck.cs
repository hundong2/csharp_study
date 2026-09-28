using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServiceHealthApi.Application;
using ServiceHealthApi.Domain;

namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// framework와 무관한 Application 판정을 ASP.NET Core HealthCheckResult로 번역하는 Adapter입니다.
/// </summary>
public sealed class ApplicationReadinessHealthCheck : IHealthCheck
{
    private readonly ReadinessApplicationService _service;

    /// <summary>
    /// dependency probe와 정책을 조정하는 Application Service를 주입받습니다.
    /// </summary>
    /// <param name="service">Domain 준비 상태를 계산하는 유스케이스 서비스입니다.</param>
    /// <returns>생성자는 health check를 초기화하며 별도 반환값은 없습니다.</returns>
    public ApplicationReadinessHealthCheck(ReadinessApplicationService service)
    {
        _service = service;
    }

    /// <summary>
    /// 모든 의존성을 검사하고 Domain의 세 단계를 framework의 세 상태로 일대일 변환합니다.
    /// </summary>
    /// <param name="context">framework가 전달하는 등록 문맥입니다.</param>
    /// <param name="cancellationToken">등록 timeout과 요청 취소가 결합된 신호입니다.</param>
    /// <returns>안전한 코드만 description에 넣은 HealthCheckResult를 반환합니다.</returns>
    // async/await는 dependency I/O 동안 thread를 점유하지 않고 최종 Domain 판정을 기다립니다.
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = context;

        var decision = await _service.CheckAsync(cancellationToken);
        // switch 식은 Domain enum의 가능한 각 값을 framework 결과 하나로 빠짐없이 번역합니다.
        return decision.Level switch
        {
            ReadinessLevel.Healthy => HealthCheckResult.Healthy("dependencies.available"),
            ReadinessLevel.Degraded => HealthCheckResult.Degraded("dependencies.optional_unavailable"),
            ReadinessLevel.Unhealthy => HealthCheckResult.Unhealthy("dependencies.required_unavailable"),
            _ => throw new InvalidOperationException($"알 수 없는 준비 상태입니다: {decision.Level}")
        };
    }
}
