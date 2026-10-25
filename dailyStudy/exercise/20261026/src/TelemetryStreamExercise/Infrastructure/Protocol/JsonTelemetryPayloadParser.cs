using System.Text.Json;
using System.Text.Json.Serialization;
using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Infrastructure.Protocol;

/// <summary>
/// UTF-8 JSON payload를 transport 계약으로 읽은 뒤 Domain factory에 업무 검증을 위임합니다.
/// JSON 구문 오류와 Domain 규칙 오류를 다른 ErrorKind로 유지하는 것이 핵심입니다.
/// </summary>
public sealed class JsonTelemetryPayloadParser : ITelemetryPayloadParser
{
    // JsonSerializerDefaults.Web은 camelCase JSON 이름과 web에서 흔한 비교 규칙을 적용합니다.
    // target-typed new는 왼쪽 JsonSerializerOptions 형식이 분명해 생성자 형식 이름을 생략한 문법입니다.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // 알 수 없는 field를 조용히 무시하면 송신자 오타를 놓치므로 protocol 오류로 드러냅니다.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // 같은 property가 두 번 오면 어느 값을 믿을지 모호하므로 wire 계약 위반으로 거절합니다.
        AllowDuplicateProperties = false
    };

    /// <summary>
    /// UTF-8 JSON 한 개를 역직렬화하고 TelemetryReading의 불변식을 검사합니다.
    /// </summary>
    /// <param name="payload">frame 길이로 정확히 잘라 낸 JSON UTF-8 byte입니다.</param>
    /// <returns>유효한 Domain 객체, malformed JSON Protocol 오류, Domain Business 오류 중 하나를 반환합니다.</returns>
    public Result<TelemetryReading> Parse(ReadOnlyMemory<byte> payload)
    {
        try
        {
            // var는 오른쪽 Deserialize 반환 형식이 분명할 때 compiler가 정적 형식을 추론하게 하는 문법입니다.
            var contract = JsonSerializer.Deserialize<TelemetryContract>(
                payload.Span,
                SerializerOptions);

            if (contract is null)
            {
                return Result<TelemetryReading>.Failure(
                    Error.Protocol("payload.json_null", "JSON null은 telemetry 객체가 아닙니다."));
            }

            return TelemetryReading.Create(
                contract.EventId,
                contract.DeviceId,
                contract.CapturedAt,
                contract.TemperatureCelsius);
        }
        catch (JsonException)
        {
            // JsonException만 예상 입력 실패로 바꿉니다. programmer bug나 메모리 오류까지 삼키면 진짜 장애를 숨기게 됩니다.
            return Result<TelemetryReading>.Failure(
                Error.Protocol("payload.malformed_json", "payload가 telemetry JSON 계약에 맞지 않습니다."));
        }
    }

    /// <summary>
    /// JSON property를 먼저 담는 transport 전용 record입니다. nullable string은 누락을 Domain 검증 단계까지 정직하게 전달합니다.
    /// </summary>
    /// <param name="EventId">JSON의 eventId이며 누락될 수 있으므로 nullable입니다.</param>
    /// <param name="DeviceId">JSON의 deviceId이며 누락될 수 있으므로 nullable입니다.</param>
    /// <param name="CapturedAt">JSON의 capturedAt offset 시각이며 누락 여부를 구별하도록 nullable입니다.</param>
    /// <param name="TemperatureCelsius">JSON의 temperatureCelsius 숫자이며 누락 여부를 구별하도록 nullable입니다.</param>
    private sealed record TelemetryContract(
        string? EventId,
        string? DeviceId,
        DateTimeOffset? CapturedAt,
        decimal? TemperatureCelsius);
}
