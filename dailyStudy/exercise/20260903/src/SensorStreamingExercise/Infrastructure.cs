using System.Runtime.CompilerServices;

static class DemoReadings
{
    /// <summary>
    /// 정상·온도 주의·온도 심각·습도 주의 분기를 모두 지나도록 데모 측정값을 순서대로 만듭니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 예제 안에 고정된 재현 가능한 측정값을 사용합니다.</remarks>
    /// <returns>호출자가 foreach로 요청할 때마다 다음 측정값 하나를 만드는 지연 실행 IEnumerable을 반환합니다.</returns>
    public static IEnumerable<SensorReading> Create()
    {
        var startedAt = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.FromHours(9));

        // yield return은 전체 목록을 먼저 만들지 않고, 반복자가 다음 값을 요구할 때 여기까지 실행해 한 건을 건넵니다.
        yield return new SensorReading("SENSOR-A", startedAt, 65.0, 45.0, 1);
        yield return new SensorReading("SENSOR-A", startedAt.AddSeconds(1), 72.0, 50.0, 2);
        yield return new SensorReading("SENSOR-B", startedAt.AddSeconds(2), 82.5, null, 1);
        yield return new SensorReading("SENSOR-C", startedAt.AddSeconds(3), 60.0, 95.0, 1);
        yield return new SensorReading("SENSOR-C", startedAt.AddSeconds(4), 55.0, 40.0, 2);
    }
}

// Adapter는 메모리 데이터를 IAsyncEnumerable로 바꾸어 실제 메시지 브로커나 장치 스트림처럼 보이게 합니다.
sealed class InMemorySensorStream : IReadingStream
{
    private readonly IEnumerable<SensorReading> _readings;
    private readonly TimeSpan _delayPerReading;

    /// <summary>
    /// 지연 실행 입력과 원소 사이 지연 시간을 받아 한 건씩 흘려보내는 인메모리 센서 스트림을 만듭니다.
    /// </summary>
    /// <param name="readings">스트림에서 순서대로 제공할 원본 측정값입니다.</param>
    /// <param name="delayPerReading">실제 I/O 대기를 흉내 낼 각 측정값 사이의 지연 시간입니다.</param>
    /// <remarks>생성자는 별도 반환값 없이 지연 실행 원본과 지연 설정을 가진 Adapter 객체를 초기화합니다.</remarks>
    public InMemorySensorStream(
        IEnumerable<SensorReading> readings,
        TimeSpan delayPerReading)
    {
        // nullable 분석은 컴파일 시 실수를 줄이고, ThrowIfNull은 외부 호출이 실제 null을 넘긴 경우 즉시 원인을 알립니다.
        ArgumentNullException.ThrowIfNull(readings);

        if (delayPerReading < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayPerReading),
                "지연 시간은 0 이상이어야 합니다.");
        }

        // 여기서 열거하거나 ToArray로 복사하지 않아, ReadAllAsync가 요청받을 때부터 원본을 한 건씩 생산합니다.
        _readings = readings;
        _delayPerReading = delayPerReading;
    }

    /// <summary>
    /// 저장된 측정값을 지연 시간에 맞춰 한 건씩 비동기로 생산하고 취소 요청에 즉시 협력합니다.
    /// </summary>
    /// <param name="cancellationToken">대기와 반복을 중단하라는 소비자 쪽 취소 신호입니다.</param>
    /// <returns>소비자가 await foreach로 당겨 갈 때마다 다음 측정값을 제공하는 비동기 스트림을 반환합니다.</returns>
    public async IAsyncEnumerable<SensorReading> ReadAllAsync(
        // EnumeratorCancellation은 이 매개변수가 await foreach 소비자의 취소 토큰을 받는 자리임을 컴파일러에 알립니다.
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var reading in _readings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_delayPerReading > TimeSpan.Zero)
            {
                // Task.Delay에 토큰을 전달해야 대기 중에도 종료 요청에 빠르게 반응할 수 있습니다.
                await Task.Delay(_delayPerReading, cancellationToken);
            }

            // async iterator의 yield return은 준비된 한 건만 소비자에게 보내고 다음 요청 전까지 실행을 멈춥니다.
            yield return reading;
        }
    }
}

sealed class InMemoryAlertRepository : IAlertRepository
{
    // new(...)는 왼쪽 Dictionary 형식이 이미 알려져 있어 생성할 형식 이름을 생략한 target-typed new입니다.
    private readonly Dictionary<string, SensorAlert> _saved = new(StringComparer.Ordinal);

    /// <summary>
    /// 경고 ID를 멱등성 키로 사용해 새 경고만 저장하고, 재전송과 충돌을 구분합니다.
    /// </summary>
    /// <param name="alert">저장할 경고이며 AlertId가 같은 요청은 같은 논리 작업으로 봅니다.</param>
    /// <param name="cancellationToken">저장 전에 중단을 요청할 수 있는 취소 신호입니다.</param>
    /// <returns>새 저장이면 true, 완전히 같은 재전송이면 false, 같은 ID의 다른 내용이면 실패 Result를 반환합니다.</returns>
    public Task<Result<bool>> SaveAsync(
        SensorAlert alert,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // out var existing은 사전 검색이 성공했을 때 기존 값을 새 지역 변수로 함께 꺼냅니다.
        if (_saved.TryGetValue(alert.AlertId, out var existing))
        {
            // 조건 ? 참일 때 값 : 거짓일 때 값 형태의 조건 연산자는 두 결과 중 하나를 고릅니다.
            // record의 ==는 참조 주소가 아니라 모든 positional 속성값이 같은지 비교합니다.
            var duplicateResult = existing == alert
                ? Result<bool>.Success(false)
                : Result<bool>.Failure("같은 경고 ID에 다른 내용이 이미 저장되어 있습니다.");

            // Task.FromResult는 즉시 계산한 값을 완료된 Task로 감싸 비동기 Repository 계약과 맞춥니다.
            return Task.FromResult(duplicateResult);
        }

        _saved.Add(alert.AlertId, alert);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleAuditLog : IAuditLog
{
    /// <summary>
    /// 새 경고의 식별자와 심각도만 콘솔에 남겨 처리 추적은 가능하게 하고 원본 센서값의 과도한 노출은 줄입니다.
    /// </summary>
    /// <param name="alert">Repository에 새로 저장된 경고입니다.</param>
    /// <returns>반환값은 없으며 한 줄의 감사 로그를 출력합니다.</returns>
    public void Recorded(SensorAlert alert)
    {
        Console.WriteLine(
            $"[audit] alert={alert.AlertId} sensor={alert.SensorId} severity={alert.Severity}");
    }
}
