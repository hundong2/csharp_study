using System;
using System.Threading;

public sealed class HighTrafficShedder
{
    private int _systemLoadStatus;

    public int SystemLoadStatus
    {
        get => Volatile.Read(ref _systemLoadStatus);
        set => Interlocked.Exchange(ref _systemLoadStatus, value);
    }

    public bool TryAcquireEntry()
    {
        // CompareExchange(ref location, value, comparand):
        // - location이 comparand와 같으면 value로 바꿉니다.
        // - 반환값은 교체 전 값입니다. 0이었다면 진입 성공입니다.
        return Interlocked.CompareExchange(ref _systemLoadStatus, 1, 0) == 0;
    }

    public void ResetGate() => Interlocked.Exchange(ref _systemLoadStatus, 0);
}

var shedder = new HighTrafficShedder();
Console.WriteLine($"[Shedder] Entry 1: {shedder.TryAcquireEntry()} | Status: {shedder.SystemLoadStatus}");
Console.WriteLine($"[Shedder] Entry 2: {shedder.TryAcquireEntry()} | Status: {shedder.SystemLoadStatus}");

/*
실행 결과:
[Shedder] Entry 1: True | Status: 1
[Shedder] Entry 2: False | Status: 1
*/

