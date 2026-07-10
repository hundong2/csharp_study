using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class SelfHealingNetworkGateway
{
    public async Task RequestWithWatchdogAsync(string endpoint, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            // Task.Delay:
            // - 네트워크 I/O를 대신하는 비동기 작업 시뮬레이션입니다.
            // - CancellationToken을 넘기면 timeout 시 OperationCanceledException이 발생합니다.
            await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token);
            Console.WriteLine("[Watchdog Base] Connection Ingress Safe. Status: OK");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Watchdog Active] Stalled connection isolated and forcefully purged: Timeout");
        }
    }
}

var gateway = new SelfHealingNetworkGateway();
await gateway.RequestWithWatchdogAsync("local://service/delay/1", TimeSpan.FromSeconds(2));

/*
실행 결과:
[Watchdog Base] Connection Ingress Safe. Status: OK
*/

