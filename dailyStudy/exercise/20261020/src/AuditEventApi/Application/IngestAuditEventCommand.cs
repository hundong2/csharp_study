namespace AuditEventApi.Application;

/// <summary>
/// HTTP의 상속 구조나 JSON 특성에 의존하지 않고 Application Service가 받을 수 있는 안정적인 입력입니다.
/// </summary>
/// <param name="Kind">user-signed-in, document-changed, permission-changed 중 요청한 종류입니다.</param>
/// <param name="EventId">재전송 중복을 판별할 생산자 이벤트 식별자 원문입니다.</param>
/// <param name="OccurredAtUtc">사건이 실제로 발생한 UTC 시각입니다.</param>
/// <param name="ActorId">사건을 일으킨 사용자 또는 시스템 식별자 원문입니다.</param>
/// <param name="IpAddress">로그인 이벤트의 접속 IP 주소이며 다른 종류에서는 null입니다.</param>
/// <param name="Succeeded">로그인 성공 여부이며 다른 종류에서는 null입니다.</param>
/// <param name="DocumentId">문서 변경 이벤트의 문서 식별자이며 다른 종류에서는 null입니다.</param>
/// <param name="ChangeSummary">문서 변경의 안전한 짧은 설명이며 다른 종류에서는 null입니다.</param>
/// <param name="TargetUserId">권한 변경 대상 사용자이며 다른 종류에서는 null입니다.</param>
/// <param name="Permission">부여하거나 회수한 권한 이름이며 다른 종류에서는 null입니다.</param>
/// <param name="Action">grant 또는 revoke 문자열이며 다른 종류에서는 null입니다.</param>
// nullable 선택 필드를 가진 하나의 command를 경계로 두면 Presentation 계약이 바뀌어도 유스케이스 입력은 안정적으로 유지됩니다.
// record는 생성자 인수로 값 묶음을 간결하게 선언하고 같은 값끼리 비교하는 기능을 자동으로 제공합니다.
public sealed record IngestAuditEventCommand(
    string? Kind,
    string? EventId,
    DateTimeOffset? OccurredAtUtc,
    string? ActorId,
    string? IpAddress = null,
    bool? Succeeded = null,
    string? DocumentId = null,
    string? ChangeSummary = null,
    string? TargetUserId = null,
    string? Permission = null,
    string? Action = null);
