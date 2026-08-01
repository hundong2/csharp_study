/*
문제: 스트리밍 버퍼 쓰기 구간에 동시에 하나의 작업만 들어가도록 가드를 구현하세요.
*/

using System;
using System.Threading;

public sealed class StreamBufferController
{
    private int _isBufferLocked;

    public int IsBufferLocked
    {
        get => Volatile.Read(ref _isBufferLocked);
        set => Interlocked.Exchange(ref _isBufferLocked, value);
    }

    public bool TryLockBuffer() => Interlocked.CompareExchange(ref _isBufferLocked, 1, 0) == 0;
    public void ReleaseBuffer() => Interlocked.Exchange(ref _isBufferLocked, 0);
}

var controller = new StreamBufferController();
Console.WriteLine($"[Buffer Lock] Run 1: {controller.TryLockBuffer()}");
Console.WriteLine($"[Buffer Lock] Run 2: {controller.TryLockBuffer()}");

/*
실행 결과:
[Buffer Lock] Run 1: True
[Buffer Lock] Run 2: False
*/

