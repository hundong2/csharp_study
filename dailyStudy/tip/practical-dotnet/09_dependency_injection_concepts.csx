using System;

// interface:
// "현재 시간을 제공한다"는 기능만 약속하고, 실제 구현은 감춥니다.
// 이렇게 하면 테스트에서 가짜 시계를 넣을 수 있습니다.
public interface IClock
{
    DateTimeOffset Now { get; }
}

// SystemClock:
// 실제 시스템 시간을 반환하는 구현체입니다.
public sealed class SystemClock : IClock
{
    // expression-bodied property:
    // get만 있는 짧은 속성을 => 로 간단히 쓴 형태입니다.
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

// ReportService:
// 보고서를 만드는 비즈니스 서비스라고 가정합니다.
// 이 클래스는 DateTimeOffset.UtcNow를 직접 호출하지 않고 IClock에 의존합니다.
public sealed class ReportService
{
    // readonly:
    // 생성자에서 값을 넣은 뒤 다른 값으로 바꾸지 않겠다는 뜻입니다.
    private readonly IClock _clock;

    // 생성자 주입:
    // 필요한 의존성을 생성자에서 받습니다.
    // 실무 DI 컨테이너도 기본적으로 이런 생성자를 보고 객체를 만들어 줍니다.
    public ReportService(IClock clock)
    {
        _clock = clock;
    }

    public string BuildReport()
    {
        // :O 포맷:
        // ISO 8601 형식의 날짜/시간 문자열을 출력합니다.
        return $"Report generated at {_clock.Now:O}";
    }
}

// 구체 구현은 SystemClock이지만, 변수 타입은 IClock으로 둡니다.
// 이것이 "구현이 아니라 추상화에 의존한다"는 기본 형태입니다.
IClock clock = new SystemClock();
var service = new ReportService(clock);

Console.WriteLine(service.BuildReport());
