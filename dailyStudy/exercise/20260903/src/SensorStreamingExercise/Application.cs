// I로 시작하는 interface는 구현이 지켜야 할 동작의 약속입니다. Application은 바깥 기술 대신 이 계약을 압니다.
// Task<T>는 미래에 한 번 완료될 비동기 결과이고, IAsyncEnumerable<T>는 여러 결과가 하나씩 준비되는 비동기 스트림입니다.
interface IReadingStream
{
    /// <summary>
    /// 준비되는 센서 측정값을 한꺼번에 메모리에 모으지 않고 시간 순서대로 제공합니다.
    /// </summary>
    /// <param name="cancellationToken">호출자가 읽기를 중단하고 싶을 때 전달하는 취소 신호입니다.</param>
    /// <returns>각 원소를 비동기로 받을 수 있는 IAsyncEnumerable 센서 스트림을 반환합니다.</returns>
    IAsyncEnumerable<SensorReading> ReadAllAsync(CancellationToken cancellationToken);
}

// Strategy 계약은 바뀌기 쉬운 이상 감지 규칙을 처리 순서에서 분리해 새 정책 추가 시 서비스 수정을 줄입니다(OCP).
interface IAnomalyRule
{
    /// <summary>
    /// 센서 측정값 한 건이 정상인지 판단하고, 이상이면 저장할 경고를 만듭니다.
    /// </summary>
    /// <param name="reading">검증을 마친 센서 측정값 한 건입니다.</param>
    /// <returns>이상이면 SensorAlert를, 정상이면 값이 없다는 뜻의 null을 반환합니다.</returns>
    SensorAlert? Evaluate(SensorReading reading);
}

// Repository 계약은 저장 방식의 세부사항을 핵심 흐름 밖으로 밀어내 메모리와 DB 구현을 교체하게 합니다(DIP).
interface IAlertRepository
{
    /// <summary>
    /// 경고를 중복 없이 저장하고 같은 경고의 재처리인지 알려 줍니다.
    /// </summary>
    /// <param name="alert">저장할 불변 경고입니다.</param>
    /// <param name="cancellationToken">저장 작업 중단 요청을 전달하는 취소 신호입니다.</param>
    /// <returns>새 저장이면 true, 같은 경고의 재처리면 false인 성공 Result를, 충돌이면 실패 Result를 반환합니다.</returns>
    Task<Result<bool>> SaveAsync(SensorAlert alert, CancellationToken cancellationToken);
}

interface IAuditLog
{
    /// <summary>
    /// 새로 저장된 경고의 최소 식별 정보만 감사 기록으로 남깁니다.
    /// </summary>
    /// <param name="alert">이미 저장에 성공한 경고입니다.</param>
    /// <returns>반환값은 없으며 감사 기록이라는 부수 효과만 수행합니다.</returns>
    void Recorded(SensorAlert alert);
}

// Application Service는 읽기 → 검증 → 판단 → 저장 → 감사의 유스케이스 순서만 조정합니다(SRP).
sealed class MonitorSensorsService
{
    private readonly IReadingStream _readingStream;
    private readonly IAnomalyRule _anomalyRule;
    private readonly IAlertRepository _alertRepository;
    private readonly IAuditLog _auditLog;

    /// <summary>
    /// 센서 모니터링 유스케이스에 필요한 네 협력 객체를 외부에서 받아 보관합니다.
    /// </summary>
    /// <param name="readingStream">측정값을 순차 제공하는 입력 Port입니다.</param>
    /// <param name="anomalyRule">정상과 이상을 구분하는 Strategy입니다.</param>
    /// <param name="alertRepository">경고를 멱등하게 저장하는 Repository Port입니다.</param>
    /// <param name="auditLog">새 경고 기록을 남기는 감사 Port입니다.</param>
    /// <remarks>생성자는 별도 반환값 없이 네 의존성을 가진 MonitorSensorsService 객체를 초기화합니다.</remarks>
    public MonitorSensorsService(
        IReadingStream readingStream,
        IAnomalyRule anomalyRule,
        IAlertRepository alertRepository,
        IAuditLog auditLog)
    {
        _readingStream = readingStream;
        _anomalyRule = anomalyRule;
        _alertRepository = alertRepository;
        _auditLog = auditLog;
    }

    /// <summary>
    /// 센서 스트림을 끝까지 한 건씩 읽고, 새 이상 경고만 저장하여 처리 요약을 만듭니다.
    /// </summary>
    /// <param name="cancellationToken">호출자가 전체 모니터링을 중단할 때 모든 I/O에 전파할 취소 신호입니다.</param>
    /// <returns>처리 건수와 새 경고 목록을 담은 성공 Result, 입력 오류·저장 충돌이면 실패 Result를 비동기로 반환합니다.</returns>
    public async Task<Result<MonitoringSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var readingCount = 0;
        var newAlerts = new List<SensorAlert>();

        // await foreach는 다음 원소가 비동기로 준비될 때까지 기다렸다가 한 건씩 처리합니다.
        // WithCancellation은 소비자 쪽 취소 신호도 반복기에 연결해 중단 요청이 생산자까지 거슬러 가게 합니다.
        await foreach (var reading in _readingStream
            .ReadAllAsync(cancellationToken)
            .WithCancellation(cancellationToken))
        {
            readingCount++;

            var validationError = ValidateReading(reading);
            // is not null 패턴은 nullable 값이 실제 문자열을 가진 경우만 실패 분기로 보냅니다.
            if (validationError is not null)
            {
                return Result<MonitoringSummary>.Failure(
                    $"{reading.SensorId}/{reading.Sequence} 입력 오류: {validationError}");
            }

            var alert = _anomalyRule.Evaluate(reading);
            if (alert is null)
            {
                continue;
            }

            var saved = await _alertRepository.SaveAsync(alert, cancellationToken);
            if (!saved.IsSuccess)
            {
                return Result<MonitoringSummary>.Failure(
                    $"{alert.AlertId} 저장 실패: {saved.Error}");
            }

            // bool Result의 Value는 true면 새 저장, false면 이미 처리한 같은 경고라는 뜻입니다.
            if (saved.Value)
            {
                newAlerts.Add(alert);
                _auditLog.Recorded(alert);
            }
        }

        if (readingCount == 0)
        {
            return Result<MonitoringSummary>.Failure("처리할 센서 측정값이 없습니다.");
        }

        return Result<MonitoringSummary>.Success(
            new MonitoringSummary(readingCount, newAlerts.AsReadOnly()));
    }

    /// <summary>
    /// 외부에서 들어온 측정값의 필수 식별자와 숫자 범위를 핵심 규칙 실행 전에 검사합니다.
    /// </summary>
    /// <param name="reading">검사할 센서 측정값 한 건입니다.</param>
    /// <returns>유효하면 null을, 잘못된 값이면 초보자도 이해할 수 있는 오류 설명을 반환합니다.</returns>
    private static string? ValidateReading(SensorReading reading)
    {
        if (string.IsNullOrWhiteSpace(reading.SensorId))
        {
            return "센서 ID가 비어 있습니다.";
        }

        if (reading.Sequence < 1)
        {
            return "순번은 1 이상이어야 합니다.";
        }

        if (reading.CapturedAt == default)
        {
            return "측정 시각이 필요합니다.";
        }

        if (!double.IsFinite(reading.TemperatureCelsius))
        {
            return "온도는 유한한 숫자여야 합니다.";
        }

        // nullable 습도는 null이면 측정하지 않은 상태입니다. is double 패턴으로 값이 있을 때만 꺼냅니다.
        // NaN과 무한대는 < 또는 > 비교가 기대와 다를 수 있어 IsFinite 검사도 반드시 함께 수행합니다.
        if (reading.HumidityPercent is double humidity &&
            (!double.IsFinite(humidity) || humidity is < 0 or > 100))
        {
            return "습도는 0~100 사이여야 합니다.";
        }

        return null;
    }
}

sealed class TemperatureHumidityRule : IAnomalyRule
{
    private readonly double _warningTemperature;
    private readonly double _criticalTemperature;
    private readonly double _warningHumidity;

    /// <summary>
    /// 온도·습도 임계값을 받아 재사용 가능한 이상 감지 Strategy를 만듭니다.
    /// </summary>
    /// <param name="warningTemperature">이 값 이상이면 주의 경고로 볼 섭씨 온도입니다.</param>
    /// <param name="criticalTemperature">이 값 이상이면 심각 경고로 볼 섭씨 온도이며 주의 값보다 커야 합니다.</param>
    /// <param name="warningHumidity">이 값 이상이면 주의 경고로 볼 상대 습도 백분율입니다.</param>
    /// <remarks>생성자는 별도 반환값 없이 검증된 임계값을 가진 Strategy 객체를 초기화합니다.</remarks>
    public TemperatureHumidityRule(
        double warningTemperature,
        double criticalTemperature,
        double warningHumidity)
    {
        // 잘못된 임계값은 정상적인 업무 실패가 아니라 앱 구성·코딩 오류이므로 생성 시 예외로 빠르게 알립니다.
        if (!double.IsFinite(warningTemperature))
        {
            throw new ArgumentOutOfRangeException(
                nameof(warningTemperature),
                "주의 온도는 유한한 숫자여야 합니다.");
        }

        if (!double.IsFinite(criticalTemperature) ||
            warningTemperature >= criticalTemperature)
        {
            throw new ArgumentOutOfRangeException(
                nameof(criticalTemperature),
                "심각 온도는 유한한 숫자이며 주의 온도보다 커야 합니다.");
        }

        if (!double.IsFinite(warningHumidity) || warningHumidity is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warningHumidity),
                "주의 습도는 0~100 사이여야 합니다.");
        }

        _warningTemperature = warningTemperature;
        _criticalTemperature = criticalTemperature;
        _warningHumidity = warningHumidity;
    }

    /// <summary>
    /// 측정값을 임계값과 비교해 심각·주의·정상 중 하나로 판단하고 이상 경고를 만듭니다.
    /// </summary>
    /// <param name="reading">Application Service에서 기본 검증을 마친 측정값입니다.</param>
    /// <returns>심각 또는 주의 상태면 경고를, 정상이면 null을 반환합니다.</returns>
    public SensorAlert? Evaluate(SensorReading reading)
    {
        var isCriticalTemperature = reading.TemperatureCelsius >= _criticalTemperature;
        var isWarningTemperature = reading.TemperatureCelsius >= _warningTemperature;

        // "is double humidity"는 nullable 값이 실제 숫자일 때 그 값을 humidity 변수로 꺼내는 형식 패턴입니다.
        var isWarningHumidity =
            reading.HumidityPercent is double humidity && humidity >= _warningHumidity;

        // 튜플 switch 식은 세 조건의 조합과 우선순위를 위에서 아래로 보여 줍니다. _는 남은 모든 경우입니다.
        AlertSeverity? severity = (
            isCriticalTemperature,
            isWarningTemperature,
            isWarningHumidity) switch
        {
            (true, _, _) => AlertSeverity.Critical,
            (false, true, _) => AlertSeverity.Warning,
            (false, false, true) => AlertSeverity.Warning,
            _ => null
        };

        if (severity is null)
        {
            return null;
        }

        // switch arm의 when은 앞 패턴이 맞은 뒤 추가 조건까지 참일 때만 그 결과를 선택하는 guard입니다.
        var reason = severity.Value switch
        {
            AlertSeverity.Critical => $"온도 {reading.TemperatureCelsius:F1}°C가 심각 기준 이상입니다.",
            AlertSeverity.Warning when isWarningTemperature =>
                $"온도 {reading.TemperatureCelsius:F1}°C가 주의 기준 이상입니다.",
            _ => $"습도 {reading.HumidityPercent:F1}%가 주의 기준 이상입니다."
        };

        // 센서 ID와 센서별 순번을 합친 안정적인 ID는 같은 측정값 재처리를 알아보는 멱등성 키입니다.
        var alertId = $"{reading.SensorId}:{reading.Sequence}";
        return new SensorAlert(
            alertId,
            reading.SensorId,
            reading.Sequence,
            severity.Value,
            reason,
            reading.CapturedAt);
    }
}
