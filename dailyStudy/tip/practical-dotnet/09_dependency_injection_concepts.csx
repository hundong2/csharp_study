using System;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

public sealed class ReportService
{
    private readonly IClock _clock;

    // 생성자 주입:
    // 필요한 의존성을 생성자에서 받습니다.
    public ReportService(IClock clock)
    {
        _clock = clock;
    }

    public string BuildReport()
    {
        return $"Report generated at {_clock.Now:O}";
    }
}

IClock clock = new SystemClock();
var service = new ReportService(clock);

Console.WriteLine(service.BuildReport());
