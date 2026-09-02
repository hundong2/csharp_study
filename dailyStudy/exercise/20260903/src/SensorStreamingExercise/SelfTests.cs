static class SelfTests
{
    /// <summary>
    /// 외부 테스트 패키지 없이 핵심 정책·스트리밍·멱등성·검증·취소 시나리오를 차례로 실행합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 테스트 입력은 각 검증 메서드가 직접 준비합니다.</remarks>
    /// <returns>모든 검증이 끝날 때 완료되는 Task를 반환하고, 실패하면 예외를 던집니다.</returns>
    public static async Task RunAsync()
    {
        // (이름, 실행 함수)는 튜플이고 Func<Task>는 매개변수 없이 Task를 돌려주는 메서드를 담는 대리자 형식입니다.
        // [ ... ] 컬렉션 식은 여러 테스트 항목을 배열로 간결하게 만듭니다.
        (string Name, Func<Task> Run)[] tests =
        [
            ("임계 온도는 심각 경고", RuleCreatesCriticalAlertAsync),
            ("서비스는 스트림 전체를 집계", ServiceProcessesStreamAsync),
            ("Repository는 같은 경고를 멱등 처리", RepositoryIsIdempotentAsync),
            ("잘못된 습도는 Result 실패", InvalidReadingReturnsFailureAsync),
            ("취소 신호는 생산자까지 전파", CancellationStopsStreamAsync)
        ];

        var passed = 0;
        foreach (var test in tests)
        {
            await test.Run();
            passed++;
            Console.WriteLine($"PASS: {test.Name}");
        }

        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
    }

    /// <summary>
    /// 심각 임계값과 같은 온도가 Critical 경고를 만드는지 Strategy만 떼어 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 메서드 안에서 경계값 입력을 준비합니다.</remarks>
    /// <returns>동기 검증을 마친 완료 Task를 반환하고, 결과가 다르면 예외를 던집니다.</returns>
    private static Task RuleCreatesCriticalAlertAsync()
    {
        var rule = CreateRule();
        var reading = new SensorReading(
            "TEST-A",
            new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero),
            80.0,
            30.0,
            1);

        var alert = rule.Evaluate(reading);

        Assert(alert is not null, "심각 온도에서 경고가 만들어져야 합니다.");
        AssertEqual(
            AlertSeverity.Critical,
            alert!.Severity,
            "80도는 Critical이어야 합니다.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 데모 비동기 스트림을 서비스가 모두 소비하고 주의 2건·심각 1건으로 집계하는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 메서드 안에서 실제 인메모리 Adapter들을 조립합니다.</remarks>
    /// <returns>서비스 실행과 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ServiceProcessesStreamAsync()
    {
        var enumerationStarted = false;

        // () => ...는 매개변수 없이 실행할 짧은 함수를 만들며, 여기서는 열거 시작 사실을 바깥 변수에 기록합니다.
        var readingStream = new InMemorySensorStream(
            CreateObservedReadings(() => enumerationStarted = true),
            TimeSpan.Zero);

        Assert(!enumerationStarted, "스트림 생성자에서 입력 전체를 미리 열거하면 안 됩니다.");

        var service = new MonitorSensorsService(
            readingStream,
            CreateRule(),
            new InMemoryAlertRepository(),
            new SilentAuditLog());

        var result = await service.ExecuteAsync(CancellationToken.None);

        Assert(result.IsSuccess, $"서비스가 성공해야 합니다: {result.Error}");
        Assert(enumerationStarted, "서비스가 실행되면 입력 열거가 시작되어야 합니다.");
        var summary = result.Value!;
        AssertEqual(5, summary.ReadingCount, "측정값 5건을 모두 읽어야 합니다.");
        AssertEqual(3, summary.NewAlertCount, "새 경고가 3건이어야 합니다.");
        AssertEqual(2, summary.WarningCount, "주의 경고가 2건이어야 합니다.");
        AssertEqual(1, summary.CriticalCount, "심각 경고가 1건이어야 합니다.");
    }

    /// <summary>
    /// 같은 경고를 두 번 저장해 첫 호출만 새 저장이고 두 번째는 안전한 재처리인지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 동일한 AlertId의 같은 record를 두 번 사용합니다.</remarks>
    /// <returns>두 저장 작업과 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RepositoryIsIdempotentAsync()
    {
        var repository = new InMemoryAlertRepository();
        var alert = new SensorAlert(
            "TEST-B:1",
            "TEST-B",
            1,
            AlertSeverity.Warning,
            "테스트 경고",
            new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        var first = await repository.SaveAsync(alert, CancellationToken.None);
        var second = await repository.SaveAsync(alert, CancellationToken.None);

        Assert(first.IsSuccess && first.Value, "첫 저장은 새 저장이어야 합니다.");
        Assert(second.IsSuccess && !second.Value, "같은 재저장은 중복으로 건너뛰어야 합니다.");
    }

    /// <summary>
    /// 허용 범위를 벗어난 습도와 NaN이 예외가 아닌 예상 가능한 Result 실패로 반환되는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 메서드 안에서 습도 120%와 NaN 입력을 준비합니다.</remarks>
    /// <returns>각 잘못된 입력의 서비스 실행과 실패 Result 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task InvalidReadingReturnsFailureAsync()
    {
        // double.NaN은 숫자 연산 결과가 유효한 수가 아님을 나타내며 범위 비교만으로는 놓칠 수 있습니다.
        double[] invalidHumidityValues = [120.0, double.NaN];

        foreach (var invalidHumidity in invalidHumidityValues)
        {
            SensorReading[] invalidReadings =
            [
                new SensorReading(
                    "TEST-C",
                    new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero),
                    25.0,
                    invalidHumidity,
                    1)
            ];

            var service = new MonitorSensorsService(
                new InMemorySensorStream(invalidReadings, TimeSpan.Zero),
                CreateRule(),
                new InMemoryAlertRepository(),
                new SilentAuditLog());

            var result = await service.ExecuteAsync(CancellationToken.None);

            Assert(!result.IsSuccess, $"잘못된 습도 {invalidHumidity}는 실패 Result여야 합니다.");
            // ?.는 Error가 null이면 Contains를 호출하지 않고 null을 돌려주는 null 조건부 연산자입니다.
            Assert(
                result.Error?.Contains("습도", StringComparison.Ordinal) == true,
                "오류 설명에 습도가 포함되어야 합니다.");
        }
    }

    /// <summary>
    /// 소비자가 취소하면 지연 중인 비동기 생산자가 OperationCanceledException으로 즉시 멈추는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 짧은 제한 시간을 가진 취소 토큰을 만듭니다.</remarks>
    /// <returns>취소 예외를 확인한 뒤 완료되는 Task를 반환하고, 취소되지 않으면 검증 예외를 던집니다.</returns>
    private static async Task CancellationStopsStreamAsync()
    {
        SensorReading[] readings =
        [
            new SensorReading(
                "TEST-D",
                new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero),
                72.0,
                40.0,
                1)
        ];

        var service = new MonitorSensorsService(
            new InMemorySensorStream(readings, TimeSpan.FromMilliseconds(200)),
            CreateRule(),
            new InMemoryAlertRepository(),
            new SilentAuditLog());

        // using은 테스트가 끝날 때 CancellationTokenSource가 가진 타이머 자원을 자동으로 정리합니다.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var canceled = false;

        try
        {
            await service.ExecuteAsync(cancellation.Token);
        }
        // catch 뒤 when은 우리가 보낸 취소 신호로 생긴 예외만 잡는 예외 필터입니다.
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            canceled = true;
        }

        Assert(canceled, "취소 신호가 비동기 생산자까지 전달되어야 합니다.");
    }

    /// <summary>
    /// 입력 열거가 실제로 시작되는 순간 callback을 호출한 뒤 데모 측정값을 한 건씩 전달합니다.
    /// </summary>
    /// <param name="onEnumerationStarted">첫 MoveNext 요청이 들어왔음을 테스트에 알리는 매개변수 없는 함수입니다.</param>
    /// <returns>열거 전에는 본문을 실행하지 않는 지연 실행 센서 측정값 시퀀스를 반환합니다.</returns>
    private static IEnumerable<SensorReading> CreateObservedReadings(
        Action onEnumerationStarted)
    {
        ArgumentNullException.ThrowIfNull(onEnumerationStarted);
        onEnumerationStarted();

        foreach (var reading in DemoReadings.Create())
        {
            yield return reading;
        }
    }

    /// <summary>
    /// 모든 테스트가 같은 임계값을 쓰도록 표준 이상 감지 Strategy를 한곳에서 만듭니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 고정된 학습용 임계값을 사용합니다.</remarks>
    /// <returns>주의 70도·심각 80도·주의 습도 90%로 설정한 IAnomalyRule 구현을 반환합니다.</returns>
    private static IAnomalyRule CreateRule()
    {
        return new TemperatureHumidityRule(70.0, 80.0, 90.0);
    }

    /// <summary>
    /// 조건이 거짓이면 현재 테스트를 즉시 실패시켜 원인을 메시지로 알려 줍니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검증 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 예외에 담을 이해하기 쉬운 설명입니다.</param>
    /// <returns>조건이 참이면 아무 값도 반환하지 않고 계속 진행합니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 예상값과 실제값을 제네릭 동등성 비교로 확인하고 다르면 현재 테스트를 실패시킵니다.
    /// </summary>
    /// <param name="expected">코드가 만들어야 하는 예상값입니다.</param>
    /// <param name="actual">실제로 코드가 만든 값입니다.</param>
    /// <param name="message">두 값이 다를 때 보여 줄 검증 설명입니다.</param>
    /// <returns>두 값이 같으면 아무 값도 반환하지 않고 계속 진행합니다.</returns>
    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        // EqualityComparer<T>.Default는 int, enum 등 T가 무엇이든 그 형식의 표준 같음 규칙을 사용합니다.
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} 예상={expected}, 실제={actual}");
        }
    }

    private sealed class SilentAuditLog : IAuditLog
    {
        /// <summary>
        /// 테스트 중 콘솔 출력 부수 효과를 없애 서비스 반환값 검증에만 집중하게 합니다.
        /// </summary>
        /// <param name="alert">서비스가 새로 저장한 경고이며 테스트 대역에서는 사용하지 않습니다.</param>
        /// <returns>아무 작업도 하지 않고 반환합니다.</returns>
        public void Recorded(SensorAlert alert)
        {
        }
    }
}
