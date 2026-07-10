using System;
using System.Threading;

public sealed class ThreadPoolShedder
{
    // C# 14의 field 키워드 예제는 현재 스크립트에서 지원되지 않을 수 있습니다.
    // 그래서 명시적 백킹 필드(_globalTicket)를 두고 Interlocked로 원자적 갱신을 수행합니다.
    private long _globalTicket;

    public long GlobalTicket
    {
        // Volatile.Read:
        // - 다른 스레드가 쓴 최신 값이 보이도록 읽기 순서를 보장합니다.
        get => Volatile.Read(ref _globalTicket);

        // Interlocked.Exchange:
        // - 값을 원자적으로 교체합니다. lock 블록 없이 단일 변수 갱신을 안전하게 처리합니다.
        set => Interlocked.Exchange(ref _globalTicket, value);
    }

    public long IssueTicket()
    {
        // Interlocked.Increment:
        // - 여러 스레드가 동시에 호출해도 중복 없는 증가값을 돌려줍니다.
        return Interlocked.Increment(ref _globalTicket);
    }
}

var shedder = new ThreadPoolShedder();
Console.WriteLine($"[Shedder] Issued Ticket 1: {shedder.IssueTicket()}");
Console.WriteLine($"[Shedder] Issued Ticket 2: {shedder.IssueTicket()}");

/*
실행 결과:
[Shedder] Issued Ticket 1: 1
[Shedder] Issued Ticket 2: 2
*/

