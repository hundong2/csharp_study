using System;
using System.Threading;

public sealed class TelemetryTrafficShedder
{
    private long _activeConnections;

    public long ActiveConnections
    {
        get => Volatile.Read(ref _activeConnections);
        set => Interlocked.Exchange(ref _activeConnections, value);
    }

    public long IncrementTicket()
    {
        // 원문에는 IncremenetTicket 오타가 있었고, 실습 파일에서는 IncrementTicket으로 정리했습니다.
        return Interlocked.Increment(ref _activeConnections);
    }

    public void DecrementTicket()
    {
        Interlocked.Decrement(ref _activeConnections);
    }
}

var shedder = new TelemetryTrafficShedder();
Console.WriteLine($"[Shedder Base] Ticket Issued 1: {shedder.IncrementTicket()}");
Console.WriteLine($"[Shedder Base] Ticket Issued 2: {shedder.IncrementTicket()}");
shedder.DecrementTicket();
Console.WriteLine($"[Shedder Base] Current Remaining Active Load: {shedder.ActiveConnections}");

/*
실행 결과:
[Shedder Base] Ticket Issued 1: 1
[Shedder Base] Ticket Issued 2: 2
[Shedder Base] Current Remaining Active Load: 1
*/

