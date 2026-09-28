using System.Diagnostics;
using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;

namespace ServiceHealthApi.Application;

/// <summary>
/// 모든 dependency probe를 실행하고 정책 Strategy에 최종 판정을 맡기는 Application Service입니다.
/// </summary>
public sealed class ReadinessApplicationService
{
    private readonly IReadOnlyList<IDependencyProbe> _probes;
    private readonly IReadinessPolicy _policy;

    /// <summary>
    /// DI가 제공한 probe와 정책을 검증하고 재사용 가능한 불변 목록으로 보관합니다.
    /// </summary>
    /// <param name="probes">Infrastructure Adapter를 가리키는 Port 구현 모음입니다.</param>
    /// <param name="policy">필수/선택 관찰을 합칠 Strategy입니다.</param>
    /// <returns>생성자는 서비스를 초기화하며 별도 반환값은 없습니다.</returns>
    public ReadinessApplicationService(
        IEnumerable<IDependencyProbe> probes,
        IReadinessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(policy);

        var probeArray = probes.OrderBy(probe => probe.Name, StringComparer.Ordinal).ToArray();
        if (probeArray.Length == 0)
        {
            throw new InvalidOperationException("dependency probe가 하나 이상 등록되어야 합니다.");
        }

        if (probeArray.Any(probe => probe.Timeout <= TimeSpan.Zero))
        {
            throw new InvalidOperationException("모든 dependency probe timeout은 0보다 커야 합니다.");
        }

        // ?.는 중복 group이 있을 때만 Key를 읽고, 없으면 duplicateName에 null을 둡니다.
        var duplicateName = probeArray
            .GroupBy(probe => probe.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        // is not null은 nullable 문자열에 실제 값이 있는지 확인하는 pattern입니다.
        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"dependency probe 이름이 중복되었습니다: {duplicateName}");
        }

        _probes = Array.AsReadOnly(probeArray);
        _policy = policy;
    }

    /// <summary>
    /// 모든 probe를 동시에 한 번씩 실행하고 같은 취소 신호를 전파한 뒤 정책으로 상태를 합칩니다.
    /// </summary>
    /// <param name="cancellationToken">클라이언트 중단 또는 health check timeout을 모든 probe에 전달합니다.</param>
    /// <returns>모든 관찰과 정책 판정이 끝나면 최종 ReadinessDecision을 반환합니다.</returns>
    public async Task<ReadinessDecision> CheckAsync(CancellationToken cancellationToken)
    {
        // Select의 lambda는 각 Port 호출을 Task 값으로 바꾸고, ToArray는 실제 실행할 작업 목록을 고정합니다.
        var observationTasks = _probes
            .Select(probe => ObserveWithTimeoutAsync(probe, cancellationToken))
            .ToArray();

        // await는 I/O 대기 중 thread를 붙잡지 않습니다. WhenAll은 probe를 직렬이 아니라 동시에 기다립니다.
        var observations = await Task.WhenAll(observationTasks);
        return _policy.Evaluate(observations);
    }

    /// <summary>
    /// probe별 시간 예산을 적용하고, 그 예산만 끝난 경우 중요도가 든 Unavailable 관찰로 바꿉니다.
    /// </summary>
    /// <param name="probe">실행할 Infrastructure Port 구현과 개별 timeout 설정입니다.</param>
    /// <param name="cancellationToken">전체 health 요청이나 호출자가 취소할 때는 그대로 다시 던질 상위 신호입니다.</param>
    /// <returns>정상 관찰 또는 중요도와 timeout 코드가 든 불가용 관찰을 반환합니다.</returns>
    private static async Task<DependencyObservation> ObserveWithTimeoutAsync(
        IDependencyProbe probe,
        CancellationToken cancellationToken)
    {
        // using var는 성공/예외 어느 경로에서도 linked source의 timer와 등록 자원을 method 끝에서 정리합니다.
        // linked source는 상위 취소와 probe 자체 timeout 중 먼저 온 신호를 하나의 token으로 합칩니다.
        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellation.CancelAfter(probe.Timeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await probe.ObserveAsync(probeCancellation.Token);
        }
        // catch when은 개별 budget이 원인이고 상위 요청은 살아 있는 취소만 골라 Domain 관찰로 바꿉니다.
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            probeCancellation.IsCancellationRequested)
        {
            stopwatch.Stop();
            var result = DependencyObservation.Create(
                probe.Name,
                probe.Importance,
                DependencyCondition.Unavailable,
                stopwatch.Elapsed,
                $"{probe.Name}.timeout");

            return result.Value ?? throw new InvalidOperationException(
                result.Error?.Message ?? "timeout 관찰 값을 만들 수 없습니다.");
        }
    }
}
