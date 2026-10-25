using System.Collections.Concurrent;
using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Infrastructure;

/// <summary>
/// process memory 안에서 event ID별 telemetry를 보관하는 Repository Adapter입니다.
/// ConcurrentDictionary의 원자적 TryAdd로 동시에 같은 event가 와도 한 건만 처음 저장되게 합니다.
/// </summary>
public sealed class InMemoryTelemetryRepository : ITelemetryRepository
{
    // target-typed new는 왼쪽 dictionary 형식이 분명하므로 생성자 형식 이름을 생략한 문법입니다.
    private readonly ConcurrentDictionary<string, TelemetryReading> _readings =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 현재 저장된 서로 다른 event ID 개수입니다. demo와 self-test에서 멱등성을 관찰하는 용도입니다.
    /// </summary>
    // =>는 본문이 식 하나인 property를 간결하게 쓰는 식 본문 문법입니다.
    public int Count => _readings.Count;

    /// <summary>
    /// event ID가 처음이면 저장하고, 이미 있으면 기존 내용과 비교해 안전한 중복과 충돌을 구분합니다.
    /// </summary>
    /// <param name="reading">Domain 검증이 끝난 불변 telemetry입니다.</param>
    /// <param name="cancellationToken">저장 시도를 시작하지 말아야 할 때 보내는 취소 신호입니다.</param>
    /// <returns>Stored, Duplicate, Conflict 중 하나를 이미 완료된 ValueTask로 반환합니다.</returns>
    public ValueTask<RepositoryWriteStatus> StoreIfAbsentAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        cancellationToken.ThrowIfCancellationRequested();

        if (_readings.TryAdd(reading.EventId, reading))
        {
            return ValueTask.FromResult(RepositoryWriteStatus.Stored);
        }

        // out var는 TryGetValue가 찾은 값을 새 지역 변수 existing으로 선언해 바로 받는 문법입니다.
        if (!_readings.TryGetValue(reading.EventId, out var existing))
        {
            // 이 Adapter는 항목을 제거하지 않으므로 도달하면 구현 불변식이 깨진 programmer 오류입니다.
            throw new InvalidOperationException("저장 직후 기존 telemetry를 찾을 수 없습니다.");
        }

        // record의 값 equality 덕분에 같은 event ID뿐 아니라 payload 전체가 같은 재전송인지 확인합니다.
        var status = existing == reading
            ? RepositoryWriteStatus.Duplicate
            : RepositoryWriteStatus.Conflict;
        return ValueTask.FromResult(status);
    }
}
