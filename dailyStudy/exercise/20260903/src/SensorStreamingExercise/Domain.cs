// enum은 허용되는 심각도를 정해 문자열 오타와 정의되지 않은 상태를 막습니다.
enum AlertSeverity
{
    Warning,
    Critical
}

// record는 값 중심 데이터를 간결하게 표현하고, 생성 뒤 속성을 바꾸지 못하게 해 상태 추적을 쉽게 합니다.
// sealed는 다른 형식이 상속으로 이 데이터의 의미를 바꾸지 못하게 막습니다.
// 형식 이름 뒤 괄호는 생성자 입력과 읽기 전용 속성을 함께 선언하는 positional record 문법입니다.
// double?의 ?는 습도 값이 실제로 없을 수 있음을 형식에 표시하여 null 처리를 빼먹지 않게 합니다.
// double은 연속적인 센서 측정에, DateTimeOffset은 UTC 기준 시각과 지역 offset을 함께 보존하는 데 적합합니다.
sealed record SensorReading(
    string SensorId,
    DateTimeOffset CapturedAt,
    double TemperatureCelsius,
    double? HumidityPercent,
    long Sequence);

// 경고도 불변 record로 전달하여 저장 전후에 같은 경고가 몰래 바뀌는 일을 막습니다.
sealed record SensorAlert(
    string AlertId,
    string SensorId,
    long Sequence,
    AlertSeverity Severity,
    string Reason,
    DateTimeOffset DetectedAt);

// IReadOnlyList<T>는 호출자가 요약 안의 목록을 우연히 수정하지 못하게 하는 읽기 전용 계약입니다.
sealed record MonitoringSummary(
    int ReadingCount,
    IReadOnlyList<SensorAlert> NewAlerts)
{
    // 속성 이름 뒤 =>는 식 하나를 곧바로 반환하는 식 본문이고, Count 안의 alert =>는 조건 함수를 만드는 람다입니다.
    // LINQ Count는 변경 가능한 카운터 없이 어떤 항목을 세는지 집계 의도를 직접 드러냅니다.
    public int NewAlertCount => NewAlerts.Count;
    public int WarningCount => NewAlerts.Count(alert => alert.Severity == AlertSeverity.Warning);
    public int CriticalCount => NewAlerts.Count(alert => alert.Severity == AlertSeverity.Critical);
}

// Result<T>는 입력 오류나 저장 충돌처럼 호출자가 처리할 수 있는 예상 실패를 예외와 분리합니다.
// T는 성공할 때 담을 값의 형식을 호출하는 쪽이 정하는 제네릭 형식 매개변수입니다.
sealed class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }

    // Error가 null이면 실패 메시지가 없다는 뜻이므로 성공으로 판단합니다.
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 성공값과 오류를 내부에 보관하되 외부가 잘못된 조합을 직접 만들지 못하게 합니다.
    /// </summary>
    /// <param name="value">성공이면 실제 값, 실패이면 T의 기본값입니다.</param>
    /// <param name="error">성공이면 null, 실패이면 비어 있지 않은 오류 설명입니다.</param>
    /// <remarks>private 생성자는 반환값이 없으며 아래 Success와 Failure 팩터리만 호출할 수 있습니다.</remarks>
    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 성공 값을 Result 상자에 넣어 호출자가 성공과 실패를 같은 형식으로 다루게 합니다.
    /// </summary>
    /// <param name="value">성공했을 때 호출자에게 전달할 실제 값입니다.</param>
    /// <returns>Value에는 입력값을, Error에는 null을 담은 성공 Result를 반환합니다.</returns>
    public static Result<T> Success(T value)
    {
        // 성공인데 값이 null인 모순을 런타임에서도 막아 IsSuccess라면 Value가 있다는 불변식을 지킵니다.
        ArgumentNullException.ThrowIfNull(value);

        // new(...)는 반환 형식 Result<T>가 이미 알려져 있어 생성할 형식 이름을 생략한 target-typed new 문법입니다.
        return new(value, null);
    }

    /// <summary>
    /// 예상 가능한 실패 설명을 Result 상자에 넣어 호출자가 if로 처리하게 합니다.
    /// </summary>
    /// <param name="error">화면 표시나 상위 계층 판단에 사용할, 비어 있지 않은 실패 설명입니다.</param>
    /// <returns>Value에는 기본값을, Error에는 입력 설명을 담은 실패 Result를 반환합니다.</returns>
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("실패 설명은 비어 있을 수 없습니다.", nameof(error));
        }

        return new(default, error);
    }
}
