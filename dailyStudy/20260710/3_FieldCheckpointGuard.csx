/*
문제: 동시에 하나의 플러시 작업만 진입하도록 CAS 가드를 구현하세요.
*/

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

    public bool TryAcquireFlushGate() => Interlocked.CompareExchange(ref _isFlushing, 1, 0) == 0;
    public void ReleaseFlushGate() => Interlocked.Exchange(ref _isFlushing, 0);
}

var controller = new StorageCheckpointController();
Console.WriteLine($"[Checkpoint] Flush Gate Run 1: {controller.TryAcquireFlushGate()}");
Console.WriteLine($"[Checkpoint] Flush Gate Run 2: {controller.TryAcquireFlushGate()}");

/*
실행 결과:
[Checkpoint] Flush Gate Run 1: True
[Checkpoint] Flush Gate Run 2: False
*/

