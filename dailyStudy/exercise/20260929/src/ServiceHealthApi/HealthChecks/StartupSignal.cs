namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// warm-up이 한 번 완료되면 프로세스가 종료될 때까지 준비 완료 상태를 유지하는 thread-safe 신호입니다.
/// </summary>
public sealed class StartupSignal
{
    private int _isReady;

    /// <summary>
    /// 여러 thread에서 최신 준비 상태를 안전하게 읽습니다.
    /// </summary>
    public bool IsReady => Volatile.Read(ref _isReady) == 1;

    /// <summary>
    /// warm-up 완료 상태를 원자적으로 기록합니다. 여러 번 호출해도 결과는 같습니다.
    /// </summary>
    /// <returns>상태만 변경하므로 반환값은 없습니다.</returns>
    public void MarkReady()
    {
        // Interlocked.Exchange는 두 thread가 동시에 호출해도 중간 상태 없이 값을 한 번에 바꿉니다.
        Interlocked.Exchange(ref _isReady, 1);
    }
}

/// <summary>
/// 시작 warm-up 시간을 코드 변경 없이 구성할 수 있게 보관합니다.
/// </summary>
public sealed class StartupWarmupOptions
{
    /// <summary>
    /// 외부 연결·캐시 준비를 흉내 내는 대기 시간(ms)입니다.
    /// </summary>
    public int DelayMilliseconds { get; set; } = 250;
}
