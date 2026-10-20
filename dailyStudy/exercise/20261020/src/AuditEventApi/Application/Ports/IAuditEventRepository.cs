using AuditEventApi.Domain;

namespace AuditEventApi.Application.Ports;

/// <summary>
/// Application이 Dictionary나 데이터베이스를 몰라도 감사 이벤트를 원자적으로 저장하고 조회하게 하는 Repository Port입니다.
/// </summary>
public interface IAuditEventRepository
{
    /// <summary>
    /// 같은 EventId가 없을 때만 확인과 추가를 하나의 원자적 연산으로 수행합니다.
    /// </summary>
    /// <param name="auditEvent">검증과 수신 시각 부여를 끝낸 불변 감사 이벤트입니다.</param>
    /// <param name="cancellationToken">호출자가 더 처리하지 않아도 될 때 작업 중단을 전달하는 신호입니다.</param>
    /// <returns>새로 저장했으면 true, 같은 EventId가 이미 있어 저장하지 않았으면 false를 반환합니다.</returns>
    ValueTask<bool> TryAddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>
    /// 정규화된 중복 판별 식별자로 저장된 감사 이벤트를 찾습니다.
    /// </summary>
    /// <param name="eventId">Domain 규칙과 같은 방식으로 정규화할 이벤트 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 조회 결과를 더 기다리지 않을 때 전달하는 신호입니다.</param>
    /// <returns>찾은 AuditEvent 또는 존재하지 않을 때 null을 담은 비동기 결과를 반환합니다.</returns>
    ValueTask<AuditEvent?> FindByEventIdAsync(string eventId, CancellationToken cancellationToken);
}
