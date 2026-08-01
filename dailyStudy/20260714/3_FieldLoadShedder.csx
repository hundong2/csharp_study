/*
문제: 동시에 하나의 요청만 게이트를 획득하도록 부하 차단기를 구현하세요.
*/

using System;
using System.Threading;

public sealed class CloudConsensusShedder
{
    private int _isSheddingActive;

    public int IsSheddingActive
    {
        get => Volatile.Read(ref _isSheddingActive);
        set => Interlocked.Exchange(ref _isSheddingActive, value);
    }

    public bool TryAcquireGate() => Interlocked.CompareExchange(ref _isSheddingActive, 1, 0) == 0;
    public void DisengageGate() => Interlocked.Exchange(ref _isSheddingActive, 0);
}

var shedder = new CloudConsensusShedder();
Console.WriteLine($"[Shedder Guard] Gate Open Attempt 1: {shedder.TryAcquireGate()}");
Console.WriteLine($"[Shedder Guard] Gate Open Attempt 2: {shedder.TryAcquireGate()}");

/*
실행 결과:
[Shedder Guard] Gate Open Attempt 1: True
[Shedder Guard] Gate Open Attempt 2: False
*/

