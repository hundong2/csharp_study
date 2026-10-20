using System.Collections.Concurrent;
using AuditEventApi.Application.Ports;
using AuditEventApi.Domain;

namespace AuditEventApi.Infrastructure;

/// <summary>
/// 한 프로세스 안에서 ConcurrentDictionary로 EventId 중복 검사와 저장을 원자화하는 학습용 Repository Adapter입니다.
/// </summary>
/// <remarks>
/// 실제 운영에서는 데이터베이스의 UNIQUE 제약 조건이 같은 최종 방어선 역할을 해야 합니다.
/// 메모리 저장소는 프로세스를 재시작하면 비워지므로 영속 저장소를 대신하지 않습니다.
/// </remarks>
public sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    // ConcurrentDictionary는 여러 요청 thread가 동시에 읽고 추가해도 내부 상태가 손상되지 않는 동시성 컬렉션입니다.
    // new(...)는 왼쪽 필드 형식에서 생성할 형식을 알아내는 target-typed new 문법입니다.
    private readonly ConcurrentDictionary<string, AuditEvent> _events =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 자체 검증에서 중복 요청 뒤 실제 저장 건수를 확인할 수 있도록 현재 항목 수를 가져옵니다.
    /// </summary>
    /// <returns>현재 프로세스 메모리에 저장된 서로 다른 이벤트 수입니다.</returns>
    // =>는 중괄호와 return 없이 식 하나의 값을 바로 돌려주는 식 본문 문법입니다.
    public int Count => _events.Count;

    /// <summary>
    /// EventId가 아직 없을 때만 이벤트를 추가하며, 존재 확인과 추가를 분리하지 않습니다.
    /// </summary>
    /// <param name="auditEvent">검증 완료 후 변경할 수 없게 만든 감사 이벤트입니다.</param>
    /// <param name="cancellationToken">호출자가 작업 중단을 요청했는지 저장 직전에 확인할 신호입니다.</param>
    /// <returns>추가했으면 true, 같은 EventId가 이미 존재하면 false인 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<bool> TryAddAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        // TryAdd 하나가 원자적으로 확인과 추가를 수행합니다. ContainsKey 뒤 Add를 하면 그 사이에 다른 요청이 끼어들 수 있습니다.
        // var는 오른쪽 TryAdd 반환값으로 added가 bool임을 compiler가 추론하게 합니다.
        var added = _events.TryAdd(auditEvent.EventId, auditEvent);
        return ValueTask.FromResult(added);
    }

    /// <summary>
    /// 식별자를 Domain과 같은 대문자 규칙으로 정규화해 저장된 이벤트를 조회합니다.
    /// </summary>
    /// <param name="eventId">찾을 이벤트 식별자이며 양끝 공백과 대소문자는 구분하지 않습니다.</param>
    /// <param name="cancellationToken">호출자가 조회 중단을 요청했는지 확인할 신호입니다.</param>
    /// <returns>찾은 AuditEvent 또는 없을 때 null인 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<AuditEvent?> FindByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ValueTask.FromResult<AuditEvent?>(null);
        }

        var normalizedEventId = eventId.Trim().ToUpperInvariant();
        // out var는 찾은 값을 새 지역 변수 auditEvent에 담고, 삼항 연산자는 성공 여부에 따라 값 또는 null을 고릅니다.
        return ValueTask.FromResult(
            _events.TryGetValue(normalizedEventId, out var auditEvent)
                ? auditEvent
                : null);
    }
}
