using System.Collections.Immutable;

namespace AuditEventApi.Domain;

/// <summary>
/// 검증과 수신 시각 부여가 끝나 Repository에 저장할 수 있는 불변 감사 이벤트입니다.
/// </summary>
/// <remarks>
/// 이 형식은 setter를 노출하지 않습니다. 감사 기록이 저장된 뒤 바뀌면 추적 신뢰성이 깨지기 때문입니다.
/// </remarks>
// sealed record는 값 중심 비교를 제공하되 상속으로 검증 완료 상태를 바꾸지 못하게 막는 데이터 형식입니다.
public sealed record AuditEvent
{
    /// <summary>
    /// 검증 완료 초안과 API 수신 시각으로 최종 감사 이벤트를 초기화합니다.
    /// </summary>
    /// <param name="eventId">정규화된 중복 판별 이벤트 식별자입니다.</param>
    /// <param name="kind">허용 목록에 있는 이벤트 종류입니다.</param>
    /// <param name="occurredAtUtc">사건이 실제로 발생한 UTC 시각입니다.</param>
    /// <param name="actorId">사건을 일으킨 행위자 식별자입니다.</param>
    /// <param name="summary">사람이 읽을 수 있는 안전한 요약입니다.</param>
    /// <param name="receivedAtUtc">이 서비스가 요청을 받은 UTC 시각입니다.</param>
    /// <param name="details">종류별로 검증된 불변 세부 정보입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 새 감사 이벤트를 초기화합니다.</returns>
    private AuditEvent(
        string eventId,
        string kind,
        DateTimeOffset occurredAtUtc,
        string actorId,
        string summary,
        DateTimeOffset receivedAtUtc,
        ImmutableDictionary<string, string> details)
    {
        EventId = eventId;
        Kind = kind;
        OccurredAtUtc = occurredAtUtc;
        ActorId = actorId;
        Summary = summary;
        ReceivedAtUtc = receivedAtUtc;
        Details = details;
    }

    /// <summary>생산자가 부여하고 Domain이 정규화한 중복 판별 이벤트 식별자입니다.</summary>
    public string EventId { get; }

    /// <summary>user-signed-in, document-changed, permission-changed 중 하나입니다.</summary>
    public string Kind { get; }

    /// <summary>사건이 원래 시스템에서 실제로 발생한 UTC 시각입니다.</summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>사건을 일으킨 사용자 또는 시스템의 식별자입니다.</summary>
    public string ActorId { get; }

    /// <summary>민감한 원문 대신 응답과 운영 기록에 사용할 짧은 설명입니다.</summary>
    public string Summary { get; }

    /// <summary>이 API가 이벤트를 받아 최종 객체를 만든 UTC 시각입니다.</summary>
    public DateTimeOffset ReceivedAtUtc { get; }

    /// <summary>종류별로 정규화와 검증을 끝낸 변경 불가능한 세부 정보입니다.</summary>
    public ImmutableDictionary<string, string> Details { get; }

    /// <summary>
    /// 검증 완료 초안에 교체 가능한 시계가 제공한 수신 시각을 더해 최종 불변 이벤트를 만듭니다.
    /// </summary>
    /// <param name="draft">Domain 팩터리 검증을 이미 통과한 이벤트 초안입니다.</param>
    /// <param name="receivedAtUtc">Application Service가 TimeProvider에서 읽은 UTC 수신 시각입니다.</param>
    /// <returns>Repository에 안전하게 저장할 수 있는 완성된 AuditEvent를 반환합니다.</returns>
    public static AuditEvent Create(AuditEventDraft draft, DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (receivedAtUtc.Offset != TimeSpan.Zero)
        {
            // Domain 내부 사용법이 잘못된 programmer error는 사용자 입력 오류와 달리 즉시 예외로 드러냅니다.
            throw new ArgumentException("수신 시각은 UTC의 +00:00 오프셋이어야 합니다.", nameof(receivedAtUtc));
        }

        return new AuditEvent(
            draft.EventId,
            draft.Kind,
            draft.OccurredAtUtc,
            draft.ActorId,
            draft.Summary,
            receivedAtUtc,
            draft.Details);
    }
}
