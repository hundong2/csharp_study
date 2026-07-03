using System;
using System.Threading;

public sealed class CloudThrottlerGauge
{
    private int _concurrentToken;

    public int ConcurrentToken
    {
        get => Volatile.Read(ref _concurrentToken);
        set => Interlocked.Exchange(ref _concurrentToken, value);
    }

    public bool TryEnterRoute() => Interlocked.CompareExchange(ref _concurrentToken, 1, 0) == 0;

    public void ExitRoute() => Interlocked.Exchange(ref _concurrentToken, 0);
}

var gauge = new CloudThrottlerGauge();

Console.WriteLine($"[Route Guard] First Enter: {gauge.TryEnterRoute()} | Active Signal: {gauge.ConcurrentToken}");
Console.WriteLine($"[Route Guard] Second Enter: {gauge.TryEnterRoute()} | Active Signal: {gauge.ConcurrentToken}");

gauge.ExitRoute();
Console.WriteLine($"[Route Guard] After Exit: {gauge.ConcurrentToken}");

/*
실행 결과:
[Route Guard] First Enter: True | Active Signal: 1
[Route Guard] Second Enter: False | Active Signal: 1
[Route Guard] After Exit: 0
*/
