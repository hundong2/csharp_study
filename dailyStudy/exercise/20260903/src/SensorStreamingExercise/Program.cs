// 코드 읽는 순서: Program → Domain → Application → Infrastructure → SelfTests입니다.
// top-level statement는 별도의 Main 메서드를 쓰지 않아도 이 파일의 문장을 위에서부터 실행하게 하는 안정 문법입니다.

// var는 오른쪽 값으로 정적 형식을 추론합니다. dynamic과 달리 컴파일할 때 형식이 고정됩니다.
var readingStream = new InMemorySensorStream(
    DemoReadings.Create(),
    TimeSpan.FromMilliseconds(5));

// 인터페이스 형식으로 변수를 선언하면 Composition Root만 구체 구현을 알고, 서비스는 계약에만 의존합니다.
IAnomalyRule anomalyRule = new TemperatureHumidityRule(
    warningTemperature: 70.0,
    criticalTemperature: 80.0,
    warningHumidity: 90.0);
IAlertRepository alertRepository = new InMemoryAlertRepository();
IAuditLog auditLog = new ConsoleAuditLog();

// Composition Root는 실행에 필요한 구현을 한곳에서 생성하고 생성자에 주입합니다.
// 이 조립 지점 덕분에 운영 구현을 가짜 구현으로 교체해 Application Service를 독립적으로 테스트할 수 있습니다.
var service = new MonitorSensorsService(readingStream, anomalyRule, alertRepository, auditLog);

// args는 프로그램 뒤에 입력한 명령줄 인수입니다. if는 조건이 참인 경우에만 중괄호 안을 실행합니다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    // await는 비동기 작업이 끝날 때까지 스레드를 붙잡지 않고 기다렸다가 다음 줄을 이어서 실행합니다.
    await SelfTests.RunAsync();
    return;
}

// CancellationToken은 호출자가 중단을 요청하는 통로입니다. 데모는 끝까지 실행하므로 None을 전달합니다.
var result = await service.ExecuteAsync(CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"처리 실패: {result.Error}");
    return;
}

// 성공 분기를 확인했으므로 !로 Value가 null이 아님을 nullable 분석기에 알려 줍니다.
var summary = result.Value!;
Console.WriteLine(
    $"처리 {summary.ReadingCount}건, 새 경고 {summary.NewAlertCount}건 " +
    $"(주의 {summary.WarningCount}건, 심각 {summary.CriticalCount}건)");

// foreach는 컬렉션의 각 원소를 하나씩 꺼냅니다. 비동기 원소에는 Application.cs의 await foreach를 사용합니다.
foreach (var alert in summary.NewAlerts)
{
    // $"...{값}..."은 문자열 보간으로, 값을 읽기 쉬운 문자열 안에 넣습니다.
    Console.WriteLine($"- {alert.AlertId}: {alert.Severity} ({alert.Reason})");
}
