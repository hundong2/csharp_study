using System;
using System.Threading;

public sealed class StreamingThrottler
{
    private int _isBufferFull;

    public int IsBufferFull
    {
        get => Volatile.Read(ref _isBufferFull);
        set => Interlocked.Exchange(ref _isBufferFull, value);
    }

    public bool TryAcquireStreamGate()
    {
        // 0이면 비어 있음, 1이면 사용 중이라고 약속합니다.
        // CompareExchange는 이 약속을 원자적으로 지켜 줍니다.
        return Interlocked.CompareExchange(ref _isBufferFull, 1, 0) == 0;
    }

    public void ReleaseStreamGate()
    {
        Interlocked.Exchange(ref _isBufferFull, 0);
    }
}

var throttler = new StreamingThrottler();
Console.WriteLine($"[Backpressure] Acquire Stream Gate 1: {throttler.TryAcquireStreamGate()}");
Console.WriteLine($"[Backpressure] Acquire Stream Gate 2: {throttler.TryAcquireStreamGate()}");

throttler.ReleaseStreamGate();
Console.WriteLine($"[Backpressure] Acquire Stream Gate 3: {throttler.TryAcquireStreamGate()}");
