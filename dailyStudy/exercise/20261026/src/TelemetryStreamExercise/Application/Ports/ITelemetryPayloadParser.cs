using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// frame payload의 JSON 표현을 Domain Model로 바꾸는 Strategy 계약입니다.
/// framing과 JSON/domain 검증을 분리하면 각 실패 경계와 테스트가 선명해집니다.
/// </summary>
public interface ITelemetryPayloadParser
{
    /// <summary>
    /// 한 frame의 UTF-8 JSON을 읽고 Domain 생성 규칙까지 적용합니다.
    /// </summary>
    /// <param name="payload">길이 prefix를 제외한 JSON byte입니다.</param>
    /// <returns>유효한 telemetry 또는 Protocol/Business 오류 Result를 반환합니다.</returns>
    Result<TelemetryReading> Parse(ReadOnlyMemory<byte> payload);
}
