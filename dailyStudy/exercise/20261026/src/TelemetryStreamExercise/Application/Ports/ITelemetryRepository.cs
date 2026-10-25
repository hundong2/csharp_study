using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// Application이 구체 저장 기술을 모르고도 telemetry를 멱등하게 저장하도록 하는 Repository Port입니다.
/// </summary>
public interface ITelemetryRepository
{
    /// <summary>
    /// event ID가 처음일 때만 telemetry를 원자적으로 추가합니다.
    /// </summary>
    /// <param name="reading">검증이 끝난 불변 telemetry입니다.</param>
    /// <param name="cancellationToken">저장 결과가 더 필요 없다는 호출자 신호입니다.</param>
    /// <returns>신규 저장, 동일 중복, ID 충돌 중 하나를 비동기 결과로 반환합니다.</returns>
    ValueTask<RepositoryWriteStatus> StoreIfAbsentAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
