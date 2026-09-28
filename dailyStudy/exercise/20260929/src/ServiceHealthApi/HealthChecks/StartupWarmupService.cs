using Microsoft.Extensions.Options;

namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// host가 뜬 뒤 비동기 warm-up을 수행하고 startup 신호를 완료 상태로 바꾸는 BackgroundService입니다.
/// </summary>
public sealed class StartupWarmupService : BackgroundService
{
    private readonly StartupSignal _signal;
    private readonly StartupWarmupOptions _options;

    /// <summary>
    /// singleton 신호와 시작 시 검증된 옵션을 주입받습니다.
    /// </summary>
    /// <param name="signal">startup health check와 공유할 완료 신호입니다.</param>
    /// <param name="options">warm-up 지연 시간이 든 옵션 wrapper입니다.</param>
    /// <returns>생성자는 background service를 초기화하며 별도 반환값은 없습니다.</returns>
    public StartupWarmupService(
        StartupSignal signal,
        IOptions<StartupWarmupOptions> options)
    {
        _signal = signal;
        _options = options.Value;
    }

    /// <summary>
    /// 구성된 warm-up 시간을 취소 가능하게 기다린 뒤 startup 준비 완료를 표시합니다.
    /// </summary>
    /// <param name="stoppingToken">host 종료 시 대기를 즉시 중단하라는 신호입니다.</param>
    /// <returns>warm-up 완료 또는 정상 종료 요청 때 끝나는 Task를 반환합니다.</returns>
    // async/await는 warm-up 대기 중 thread를 점유하지 않고, 완료 뒤 같은 비동기 흐름을 이어가게 합니다.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_options.DelayMilliseconds, stoppingToken);
            _signal.MarkReady();
        }
        // catch when은 host 종료 token이 원인인 취소만 골라 정상 종료로 처리합니다.
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 정상 종료 중인 취소는 장애가 아니므로 다시 던지지 않습니다.
        }
    }
}
