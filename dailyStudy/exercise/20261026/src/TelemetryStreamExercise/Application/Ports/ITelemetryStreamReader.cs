using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// 임의의 Stream을 frame 단위 telemetry 결과로 바꾸는 Strategy 계약입니다.
/// 파일, socket, 압축 해제 stream 등 입력 기술이 달라져도 Application Service 계약은 유지됩니다.
/// </summary>
public interface ITelemetryStreamReader
{
    /// <summary>
    /// stream을 끝까지 읽으며 각 frame의 성공 또는 예상 가능한 실패를 순서대로 전달합니다.
    /// </summary>
    /// <param name="source">길이 prefix frame들이 이어진 읽기 가능한 stream입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 보내는 취소 신호입니다.</param>
    /// <returns>frame 순서를 보존하는 비동기 Result sequence를 반환합니다.</returns>
    IAsyncEnumerable<Result<TelemetryReading>> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
