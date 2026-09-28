using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// liveness, startup, readiness URL과 서로 다른 검사 선택 규칙을 한곳에 매핑합니다.
/// </summary>
public static class HealthEndpointMappings
{
    /// <summary>
    /// 오케스트레이터가 사용할 세 health endpoint를 등록합니다.
    /// </summary>
    /// <param name="endpoints">Minimal API route를 추가할 endpoint builder입니다.</param>
    /// <returns>다른 endpoint를 계속 연결할 수 있도록 같은 builder를 반환합니다.</returns>
    // 파라미터 앞 this는 이 static 메서드를 endpoints.MapServiceHealthEndpoints()처럼 호출하게 하는 extension method 문법입니다.
    public static IEndpointRouteBuilder MapServiceHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // _ => false는 등록된 검사를 하나도 실행하지 않습니다. live는 프로세스가 HTTP에 답하는지만 확인해야 합니다.
        endpoints.MapHealthChecks("/health/live", CreateOptions(_ => false));
        endpoints.MapHealthChecks(
            "/health/startup",
            CreateOptions(registration => registration.Tags.Contains(HealthTags.Startup)));
        endpoints.MapHealthChecks(
            "/health/ready",
            CreateOptions(registration => registration.Tags.Contains(HealthTags.Ready)));

        return endpoints;
    }

    /// <summary>
    /// 검사 선택 조건, 상태 코드 매핑, 캐시 금지, 안전한 JSON writer를 공통 구성합니다.
    /// </summary>
    /// <param name="predicate">현재 URL에서 실행할 등록을 고르는 함수입니다.</param>
    /// <returns>MapHealthChecks에 전달할 완성된 옵션을 반환합니다.</returns>
    private static HealthCheckOptions CreateOptions(Func<HealthCheckRegistration, bool> predicate)
    {
        return new HealthCheckOptions
        {
            Predicate = predicate,
            ResponseWriter = HealthResponseWriter.WriteAsync,
            AllowCachingResponses = false,
            // 컬렉션 초기화자의 [key] = value 문법으로 Domain 상태와 HTTP 계약을 명시적으로 고정합니다.
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        };
    }
}
