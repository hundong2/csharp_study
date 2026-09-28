using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// warm-up 완료 신호를 ASP.NET Core HealthCheckResult로 바꾸는 Adapter입니다.
/// </summary>
public sealed class StartupHealthCheck : IHealthCheck
{
    private readonly StartupSignal _signal;

    /// <summary>
    /// background warm-up과 같은 singleton 신호를 주입받습니다.
    /// </summary>
    /// <param name="signal">현재 시작 완료 상태를 보관하는 신호입니다.</param>
    /// <returns>생성자는 health check를 초기화하며 별도 반환값은 없습니다.</returns>
    public StartupHealthCheck(StartupSignal signal)
    {
        _signal = signal;
    }

    /// <summary>
    /// warm-up이 끝났으면 Healthy, 아직이면 Unhealthy를 즉시 반환합니다.
    /// </summary>
    /// <param name="context">등록 이름과 실패 정책을 포함한 health check 실행 문맥입니다.</param>
    /// <param name="cancellationToken">호출 취소 신호이며 이 검사는 I/O가 없어 즉시 끝납니다.</param>
    /// <returns>startup.ready 또는 startup.warming_up 코드를 가진 완료된 Task를 반환합니다.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        _ = cancellationToken;

        // condition ? a : b는 bool 조건에 따라 두 값 중 하나를 고르는 conditional 연산자입니다.
        var result = _signal.IsReady
            ? HealthCheckResult.Healthy("startup.ready")
            : HealthCheckResult.Unhealthy("startup.warming_up");

        return Task.FromResult(result);
    }
}
