using System.Diagnostics;
using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;

namespace ServiceHealthApi.Infrastructure;

/// <summary>
/// 실제 DB/API 대신 정상, 장애, 지연을 결정적으로 재현하는 교육용 dependency probe Adapter입니다.
/// </summary>
public sealed class ConfigurableDependencyProbe : IDependencyProbe
{
    // System.Threading.Lock은 lock 문 전용 동기화 객체입니다. 짧은 상태 복사만 보호하고 await는 잠금 밖에서 수행합니다.
    private readonly Lock _gate = new();
    private DependencyCondition _condition = DependencyCondition.Available;
    private TimeSpan _delay = TimeSpan.Zero;
    private int _probeCount;
    private int _canceledProbeCount;

    /// <summary>
    /// 고유 이름과 중요도를 가진 probe를 정상 상태로 만듭니다.
    /// </summary>
    /// <param name="name">DI와 health 응답에서 사용할 고유 이름입니다.</param>
    /// <param name="importance">요청 처리에 필수인지 선택인지 나타냅니다.</param>
    /// <param name="timeout">이 probe 한 번에 허용할 최대 시간입니다.</param>
    /// <returns>생성자는 새 probe를 만들며 별도 반환값은 없습니다.</returns>
    public ConfigurableDependencyProbe(
        string name,
        DependencyImportance importance,
        TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("probe 이름은 비워 둘 수 없습니다.", nameof(name));
        }

        if (!Enum.IsDefined(importance))
        {
            throw new ArgumentOutOfRangeException(nameof(importance), "정의되지 않은 dependency 중요도입니다.");
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "probe timeout은 0초 초과 2초 이하여야 합니다.");
        }

        Name = name.Trim().ToLowerInvariant();
        Importance = importance;
        Timeout = timeout;
    }

    /// <summary>
    /// 이 probe를 구분하는 고유 이름을 가져옵니다.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 준비 상태 정책에서 사용할 중요도를 가져옵니다.
    /// </summary>
    public DependencyImportance Importance { get; }

    /// <summary>
    /// Application Service가 적용할 개별 probe 시간 예산을 가져옵니다.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// 지금까지 시작한 probe 횟수를 thread-safe하게 읽습니다.
    /// </summary>
    public int ProbeCount => Volatile.Read(ref _probeCount);

    /// <summary>
    /// timeout이나 호출자 취소를 실제로 관찰한 횟수를 thread-safe하게 읽습니다.
    /// </summary>
    public int CanceledProbeCount => Volatile.Read(ref _canceledProbeCount);

    /// <summary>
    /// 학습과 자체 테스트를 위해 재설정 전까지 이어질 probe 상태와 지연을 원자적으로 바꿉니다.
    /// </summary>
    /// <param name="condition">재설정하기 전 모든 후속 호출에서 반환할 가용 상태입니다.</param>
    /// <param name="delay">실제 외부 I/O를 흉내 낼 0~2초 지연입니다.</param>
    /// <returns>설정만 변경하므로 반환값은 없습니다.</returns>
    public void Configure(DependencyCondition condition, TimeSpan delay)
    {
        if (!Enum.IsDefined(condition))
        {
            throw new ArgumentOutOfRangeException(nameof(condition), "정의되지 않은 dependency 상태입니다.");
        }

        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(2))
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "probe 지연은 0~2초여야 합니다.");
        }

        lock (_gate)
        {
            _condition = condition;
            _delay = delay;
        }
    }

    /// <summary>
    /// 현재 설정의 스냅샷을 읽고 지연을 취소 가능하게 기다린 뒤 Domain 관찰 값으로 변환합니다.
    /// </summary>
    /// <param name="cancellationToken">health check timeout을 모의 I/O까지 전달하는 신호입니다.</param>
    /// <returns>가용 또는 불가용 상태와 안전한 코드를 가진 관찰 값을 반환합니다.</returns>
    public async Task<DependencyObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        DependencyCondition condition;
        TimeSpan delay;

        lock (_gate)
        {
            condition = _condition;
            delay = _delay;
            _probeCount++;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (delay > TimeSpan.Zero)
            {
                // CancellationToken을 Task.Delay에 전달해야 timeout이 긴 대기를 실제로 중단시킬 수 있습니다.
                await Task.Delay(delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _canceledProbeCount);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }

        // condition ? a : b는 bool 조건에 따라 안전한 코드 suffix 두 값 중 하나를 고릅니다.
        var suffix = condition == DependencyCondition.Available ? "available" : "unavailable";
        var result = DependencyObservation.Create(
            Name,
            Importance,
            condition,
            stopwatch.Elapsed,
            $"{Name}.{suffix}");

        // ?? throw는 왼쪽이 null인 경우에만 구성 오류 예외를 던집니다. 정상 DI라면 Create 검증은 항상 성공합니다.
        return result.Value ?? throw new InvalidOperationException(
            result.Error?.Message ?? "dependency 관찰 값을 만들 수 없습니다.");
    }
}
