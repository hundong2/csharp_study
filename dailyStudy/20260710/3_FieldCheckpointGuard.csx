using System;
using System.Threading;

public sealed class StorageCheckpointController
{
    private int _isFlushing;

    public int IsFlushing
    {
        get => Volatile.Read(ref _isFlushing);
        set => Interlocked.Exchange(ref _isFlushing, value);
    }

    public bool TryAcquireFlushGate()
    {
        return Interlocked.CompareExchange(ref _isFlushing, 1, 0) == 0;
    }

    public void ReleaseFlushGate()
    {
        Interlocked.Exchange(ref _isFlushing, 0);
    }
}

var controller = new StorageCheckpointController();
Console.WriteLine($"[Checkpoint] Flush Gate Run 1: {controller.TryAcquireFlushGate()}");
Console.WriteLine($"[Checkpoint] Flush Gate Run 2: {controller.TryAcquireFlushGate()}");

controller.ReleaseFlushGate();
Console.WriteLine($"[Checkpoint] Flush Gate Run 3: {controller.TryAcquireFlushGate()}");
