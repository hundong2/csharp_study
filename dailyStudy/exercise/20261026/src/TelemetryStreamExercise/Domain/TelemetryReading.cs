namespace TelemetryStreamExercise.Domain;

/// <summary>
/// 한 장치가 보낸 유효한 온도 측정값을 나타내는 불변 Domain Model입니다.
/// record를 사용해 같은 event ID와 같은 내용의 재전송을 값 equality로 비교할 수 있게 합니다.
/// </summary>
public sealed record TelemetryReading
{
    /// <summary>
    /// 이미 검증하고 정규화한 값만 보관해 잘못된 Domain 객체가 생기지 않게 합니다.
    /// </summary>
    /// <param name="eventId">멱등 처리의 key로 쓰는 event 식별자입니다.</param>
    /// <param name="deviceId">측정값을 보낸 장치 식별자입니다.</param>
    /// <param name="capturedAtUtc">UTC로 정규화한 측정 시각입니다.</param>
    /// <param name="temperatureCelsius">섭씨 온도 값입니다.</param>
    private TelemetryReading(
        string eventId,
        string deviceId,
        DateTimeOffset capturedAtUtc,
        decimal temperatureCelsius)
    {
        EventId = eventId;
        DeviceId = deviceId;
        CapturedAtUtc = capturedAtUtc;
        TemperatureCelsius = temperatureCelsius;
    }

    /// <summary>
    /// 재전송을 같은 요청으로 알아보기 위한 안정적인 식별자입니다.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// telemetry를 생산한 장치의 식별자입니다.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// 서로 다른 시간대 입력도 안전하게 비교하도록 UTC로 정규화한 측정 시각입니다.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; }

    /// <summary>
    /// 예제에서 허용하는 범위 안의 섭씨 온도입니다.
    /// </summary>
    public decimal TemperatureCelsius { get; }

    /// <summary>
    /// JSON 계약에서 읽은 값을 검증하고, 성공할 때만 불변 TelemetryReading을 만듭니다.
    /// </summary>
    /// <param name="eventId">비어 있지 않고 64자 이하여야 하는 멱등 key입니다.</param>
    /// <param name="deviceId">비어 있지 않고 64자 이하여야 하는 장치 ID입니다.</param>
    /// <param name="capturedAt">필수이며 송신자가 보낸 nullable offset 포함 측정 시각입니다.</param>
    /// <param name="temperatureCelsius">필수이며 -100도 이상 200도 이하인 nullable 섭씨 값입니다.</param>
    /// <returns>유효하면 Domain 객체, 그렇지 않으면 Business 오류를 담은 Result를 반환합니다.</returns>
    public static Result<TelemetryReading> Create(
        string? eventId,
        string? deviceId,
        DateTimeOffset? capturedAt,
        decimal? temperatureCelsius)
    {
        // ?.는 eventId가 null이면 Trim을 호출하지 않고 null을 유지하는 null-conditional 문법입니다.
        var normalizedEventId = eventId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEventId))
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.event_id_required", "eventId는 비어 있을 수 없습니다."));
        }

        if (normalizedEventId.Length > 64)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.event_id_too_long", "eventId는 64자 이하여야 합니다."));
        }

        var normalizedDeviceId = deviceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.device_id_required", "deviceId는 비어 있을 수 없습니다."));
        }

        if (normalizedDeviceId.Length > 64)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.device_id_too_long", "deviceId는 64자 이하여야 합니다."));
        }

        if (capturedAt is null)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.captured_at_required", "capturedAt은 실제 측정 시각이어야 합니다."));
        }

        var normalizedCapturedAt = capturedAt.Value;
        if (normalizedCapturedAt == default)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business(
                    "telemetry.captured_at_invalid",
                    "capturedAt은 DateTimeOffset의 최소 기본값보다 실제 측정 시각을 사용해야 합니다."));
        }

        // decimal?의 ?는 JSON에서 숫자 0과 property 누락을 구별하기 위해 값이 없을 수도 있음을 나타냅니다.
        if (temperatureCelsius is null)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.temperature_required", "temperatureCelsius는 반드시 필요합니다."));
        }

        var normalizedTemperature = temperatureCelsius.Value;
        // `is < ... or > ...`는 값이 두 허용 경계 바깥인지 읽기 좋게 표현하는 relational pattern입니다.
        if (normalizedTemperature is < -100m or > 200m)
        {
            return Result<TelemetryReading>.Failure(
                Error.Business("telemetry.temperature_out_of_range", "temperatureCelsius는 -100에서 200 사이여야 합니다."));
        }

        return Result<TelemetryReading>.Success(
            new TelemetryReading(
                normalizedEventId,
                normalizedDeviceId,
                normalizedCapturedAt.ToUniversalTime(),
                normalizedTemperature));
    }
}
