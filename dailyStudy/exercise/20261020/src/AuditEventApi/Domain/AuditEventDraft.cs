using System.Collections.Immutable;
using System.Net;

namespace AuditEventApi.Domain;

/// <summary>
/// 외부 문자열을 정규화하고 이벤트 종류별 불변식을 통과한, 아직 저장 전인 감사 이벤트를 나타냅니다.
/// </summary>
/// <remarks>
/// HTTP 형식과 무관한 Domain 팩터리에서 검증하므로 콘솔, 메시지 큐, 테스트가 호출해도 같은 규칙이 적용됩니다.
/// 생성 뒤 바뀌지 않는 값만 노출해 검증 완료 상태가 나중에 훼손되지 않게 합니다.
/// </remarks>
// sealed record는 값 중심 비교를 제공하면서 상속으로 검증 규칙이 우회되지 않게 막는 불변 데이터 형식입니다.
public sealed record AuditEventDraft
{
    /// <summary>사용자 로그인 이벤트에 쓰는 안정적인 종류 문자열입니다.</summary>
    public const string UserSignedInKind = "user-signed-in";

    /// <summary>문서 변경 이벤트에 쓰는 안정적인 종류 문자열입니다.</summary>
    public const string DocumentChangedKind = "document-changed";

    /// <summary>권한 변경 이벤트에 쓰는 안정적인 종류 문자열입니다.</summary>
    public const string PermissionChangedKind = "permission-changed";

    /// <summary>
    /// 검증이 끝난 값만 초안이 될 수 있도록 생성자를 private으로 감춥니다.
    /// </summary>
    /// <param name="eventId">정규화한 중복 판별 이벤트 식별자입니다.</param>
    /// <param name="kind">허용 목록에서 선택된 이벤트 종류입니다.</param>
    /// <param name="occurredAtUtc">UTC로 확인된 실제 사건 발생 시각입니다.</param>
    /// <param name="actorId">양끝 공백을 제거한 행위자 식별자입니다.</param>
    /// <param name="summary">민감한 원문 대신 사람이 읽을 수 있도록 만든 요약입니다.</param>
    /// <param name="details">이벤트 종류별로 검증한 불변 세부 값입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 검증 완료 초안을 초기화합니다.</returns>
    private AuditEventDraft(
        string eventId,
        string kind,
        DateTimeOffset occurredAtUtc,
        string actorId,
        string summary,
        ImmutableDictionary<string, string> details)
    {
        EventId = eventId;
        Kind = kind;
        OccurredAtUtc = occurredAtUtc;
        ActorId = actorId;
        Summary = summary;
        Details = details;
    }

    /// <summary>재전송된 같은 사건을 판별하는 정규화된 식별자입니다.</summary>
    public string EventId { get; }

    /// <summary>허용된 세 이벤트 종류 중 하나입니다.</summary>
    public string Kind { get; }

    /// <summary>사건이 실제로 발생한 UTC 시각입니다.</summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>사건을 일으킨 사용자 또는 시스템 식별자입니다.</summary>
    public string ActorId { get; }

    /// <summary>API 응답과 운영 기록에서 안전하게 읽을 수 있는 짧은 설명입니다.</summary>
    public string Summary { get; }

    /// <summary>이벤트 종류에 따라 검증된 값만 가진 변경 불가능한 세부 정보입니다.</summary>
    public ImmutableDictionary<string, string> Details { get; }

    /// <summary>
    /// 로그인 감사 입력의 공통 값, IP 주소, 성공 여부를 검증하고 정규화합니다.
    /// </summary>
    /// <param name="eventId">생산자가 보낸 중복 판별 이벤트 식별자 원문입니다.</param>
    /// <param name="occurredAtUtc">생산자가 기록한 사건 발생 시각입니다.</param>
    /// <param name="actorId">로그인을 시도한 행위자 식별자 원문입니다.</param>
    /// <param name="ipAddress">IPv4 또는 IPv6 주소 문자열입니다.</param>
    /// <param name="succeeded">로그인 성공 여부이며 null은 값 누락을 뜻합니다.</param>
    /// <returns>모든 규칙을 통과한 초안 또는 첫 번째 검증 오류를 반환합니다.</returns>
    public static Result<AuditEventDraft> CreateUserSignedIn(
        string? eventId,
        DateTimeOffset? occurredAtUtc,
        string? actorId,
        string? ipAddress,
        bool? succeeded)
    {
        // var는 오른쪽 반환값으로 지역 변수 형식을 추론합니다. 여기서는 Result<CommonFields>로 compile됩니다.
        var commonResult = CreateCommon(eventId, occurredAtUtc, actorId);
        if (!commonResult.IsSuccess)
        {
            // !는 바로 위 성공 검사 덕분에 Error가 null이 아님을 compiler에 알려 주는 null-forgiving 연산자입니다.
            return Result<AuditEventDraft>.Failure(commonResult.Error!);
        }

        // ?.는 ipAddress가 null이면 Trim을 호출하지 않고 null을 그대로 전달해 안전하게 누락 검증으로 이어지게 합니다.
        var normalizedIpAddress = ipAddress?.Trim();
        // out var는 TryParse가 성공했을 때 만든 IPAddress를 새 지역 변수 parsedAddress에 담습니다.
        if (!IPAddress.TryParse(normalizedIpAddress, out var parsedAddress))
        {
            return Result<AuditEventDraft>.Failure(
                new Error("audit.invalid_ip_address", "ipAddress에는 올바른 IPv4 또는 IPv6 주소가 필요합니다.", "ipAddress"));
        }

        if (succeeded is null)
        {
            return Result<AuditEventDraft>.Failure(
                new Error("audit.missing_sign_in_result", "user-signed-in 이벤트에는 succeeded 값이 필요합니다.", "succeeded"));
        }

        var common = commonResult.Value!;
        // 조건 ? 참값 : 거짓값은 bool 결과에 맞는 문자열 하나를 고르는 조건 연산자입니다.
        var normalizedSucceeded = succeeded.Value ? "true" : "false";
        // ImmutableDictionary는 만든 뒤 항목을 추가하거나 바꿀 수 없어 검증 완료 상태를 외부가 훼손하지 못합니다.
        var details = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("ipAddress", parsedAddress.ToString())
            .Add("succeeded", normalizedSucceeded);

        // $ 문자열은 중괄호 안의 검증 완료 값을 문장에 넣는 문자열 보간 문법입니다.
        var outcome = succeeded.Value ? "성공" : "실패";
        var summary = $"사용자 '{common.ActorId}'의 로그인이 {outcome}했습니다.";
        return Result<AuditEventDraft>.Success(
            new AuditEventDraft(
                common.EventId,
                UserSignedInKind,
                common.OccurredAtUtc,
                common.ActorId,
                summary,
                details));
    }

    /// <summary>
    /// 문서 변경 감사 입력의 공통 값, 문서 식별자, 안전한 변경 요약을 검증하고 정규화합니다.
    /// </summary>
    /// <param name="eventId">생산자가 보낸 중복 판별 이벤트 식별자 원문입니다.</param>
    /// <param name="occurredAtUtc">생산자가 기록한 사건 발생 시각입니다.</param>
    /// <param name="actorId">문서를 변경한 행위자 식별자 원문입니다.</param>
    /// <param name="documentId">변경된 문서의 업무 식별자 원문입니다.</param>
    /// <param name="changeSummary">문서 본문이나 비밀을 넣지 않은 짧은 변경 설명입니다.</param>
    /// <returns>모든 규칙을 통과한 초안 또는 첫 번째 검증 오류를 반환합니다.</returns>
    public static Result<AuditEventDraft> CreateDocumentChanged(
        string? eventId,
        DateTimeOffset? occurredAtUtc,
        string? actorId,
        string? documentId,
        string? changeSummary)
    {
        var commonResult = CreateCommon(eventId, occurredAtUtc, actorId);
        if (!commonResult.IsSuccess)
        {
            return Result<AuditEventDraft>.Failure(commonResult.Error!);
        }

        var normalizedDocumentId = documentId?.Trim();
        if (ContainsControlCharacters(documentId)
            || string.IsNullOrWhiteSpace(normalizedDocumentId)
            || normalizedDocumentId.Length > 120)
        {
            return Result<AuditEventDraft>.Failure(
                new Error(
                    "audit.invalid_document_id",
                    "documentId는 제어문자 없이 공백이 아닌 1~120자여야 합니다.",
                    "documentId"));
        }

        var normalizedChangeSummary = changeSummary?.Trim();
        // is < 3 or > 200은 길이가 두 허용 경계 밖인지 한눈에 표현하는 관계·논리 패턴입니다.
        if (ContainsControlCharacters(changeSummary)
            || string.IsNullOrWhiteSpace(normalizedChangeSummary)
            || normalizedChangeSummary.Length is < 3 or > 200)
        {
            return Result<AuditEventDraft>.Failure(
                new Error(
                    "audit.invalid_change_summary",
                    "changeSummary는 제어문자 없이 양끝 공백 제거 후 3~200자여야 합니다.",
                    "changeSummary"));
        }

        var common = commonResult.Value!;
        var details = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("documentId", normalizedDocumentId)
            .Add("changeSummary", normalizedChangeSummary);
        var summary = $"문서 '{normalizedDocumentId}' 변경: {normalizedChangeSummary}";

        return Result<AuditEventDraft>.Success(
            new AuditEventDraft(
                common.EventId,
                DocumentChangedKind,
                common.OccurredAtUtc,
                common.ActorId,
                summary,
                details));
    }

    /// <summary>
    /// 권한 변경 감사 입력의 공통 값, 대상 사용자, 권한 이름, 부여·회수 동작을 검증하고 정규화합니다.
    /// </summary>
    /// <param name="eventId">생산자가 보낸 중복 판별 이벤트 식별자 원문입니다.</param>
    /// <param name="occurredAtUtc">생산자가 기록한 사건 발생 시각입니다.</param>
    /// <param name="actorId">권한을 변경한 행위자 식별자 원문입니다.</param>
    /// <param name="targetUserId">권한이 바뀐 대상 사용자 식별자 원문입니다.</param>
    /// <param name="permission">변경한 권한 이름 원문입니다.</param>
    /// <param name="action">grant 또는 revoke 동작 문자열입니다.</param>
    /// <returns>모든 규칙을 통과한 초안 또는 첫 번째 검증 오류를 반환합니다.</returns>
    public static Result<AuditEventDraft> CreatePermissionChanged(
        string? eventId,
        DateTimeOffset? occurredAtUtc,
        string? actorId,
        string? targetUserId,
        string? permission,
        string? action)
    {
        var commonResult = CreateCommon(eventId, occurredAtUtc, actorId);
        if (!commonResult.IsSuccess)
        {
            return Result<AuditEventDraft>.Failure(commonResult.Error!);
        }

        var normalizedTargetUserId = targetUserId?.Trim();
        if (ContainsControlCharacters(targetUserId)
            || string.IsNullOrWhiteSpace(normalizedTargetUserId)
            || normalizedTargetUserId.Length > 80)
        {
            return Result<AuditEventDraft>.Failure(
                new Error(
                    "audit.invalid_target_user_id",
                    "targetUserId는 제어문자 없이 공백이 아닌 1~80자여야 합니다.",
                    "targetUserId"));
        }

        var normalizedPermission = permission?.Trim().ToLowerInvariant();
        if (ContainsControlCharacters(permission)
            || string.IsNullOrWhiteSpace(normalizedPermission)
            || normalizedPermission.Length > 80)
        {
            return Result<AuditEventDraft>.Failure(
                new Error(
                    "audit.invalid_permission",
                    "permission은 제어문자 없이 공백이 아닌 1~80자여야 합니다.",
                    "permission"));
        }

        // switch 식은 제한된 문자열만 통과시키고, 알 수 없는 값은 null로 바꾸어 아래 한곳에서 거절합니다.
        var normalizedAction = action?.Trim().ToLowerInvariant() switch
        {
            "grant" => "grant",
            "revoke" => "revoke",
            _ => null
        };

        if (normalizedAction is null)
        {
            return Result<AuditEventDraft>.Failure(
                new Error("audit.invalid_permission_action", "action은 grant 또는 revoke여야 합니다.", "action"));
        }

        var common = commonResult.Value!;
        var details = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("targetUserId", normalizedTargetUserId)
            .Add("permission", normalizedPermission)
            .Add("action", normalizedAction);
        var actionText = normalizedAction == "grant" ? "부여" : "회수";
        var summary = $"사용자 '{normalizedTargetUserId}'의 '{normalizedPermission}' 권한을 {actionText}했습니다.";

        return Result<AuditEventDraft>.Success(
            new AuditEventDraft(
                common.EventId,
                PermissionChangedKind,
                common.OccurredAtUtc,
                common.ActorId,
                summary,
                details));
    }

    /// <summary>
    /// 세 이벤트 종류가 공유하는 식별자, UTC 시각, 행위자 규칙을 한 번만 정의해 일관되게 적용합니다.
    /// </summary>
    /// <param name="eventId">중복 판별 이벤트 식별자 원문입니다.</param>
    /// <param name="occurredAtUtc">사건 발생 시각 원문입니다.</param>
    /// <param name="actorId">행위자 식별자 원문입니다.</param>
    /// <returns>정규화된 공통 값 또는 첫 번째 공통 필드 오류를 반환합니다.</returns>
    private static Result<CommonFields> CreateCommon(
        string? eventId,
        DateTimeOffset? occurredAtUtc,
        string? actorId)
    {
        var normalizedEventId = eventId?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEventId)
            || normalizedEventId.Length > 80
            || !ContainsOnlyEventIdCharacters(normalizedEventId))
        {
            return Result<CommonFields>.Failure(
                new Error(
                    "audit.invalid_event_id",
                    "eventId는 영문자·숫자·하이픈·밑줄·마침표·콜론으로 된 1~80자여야 합니다.",
                    "eventId"));
        }

        if (occurredAtUtc is null || occurredAtUtc.Value.Offset != TimeSpan.Zero)
        {
            return Result<CommonFields>.Failure(
                new Error("audit.invalid_occurred_at", "occurredAtUtc는 +00:00 오프셋을 가진 UTC 시각이어야 합니다.", "occurredAtUtc"));
        }

        var normalizedActorId = actorId?.Trim();
        if (ContainsControlCharacters(actorId)
            || string.IsNullOrWhiteSpace(normalizedActorId)
            || normalizedActorId.Length > 80)
        {
            return Result<CommonFields>.Failure(
                new Error(
                    "audit.invalid_actor_id",
                    "actorId는 제어문자 없이 공백이 아닌 1~80자여야 합니다.",
                    "actorId"));
        }

        return Result<CommonFields>.Success(
            new CommonFields(normalizedEventId, occurredAtUtc.Value, normalizedActorId));
    }

    /// <summary>
    /// 이벤트 식별자가 저장·URL·로그에서 다루기 쉬운 제한된 문자만 사용하는지 확인합니다.
    /// </summary>
    /// <param name="value">null과 길이 검사를 이미 통과한 정규화 문자열입니다.</param>
    /// <returns>모든 문자가 허용 목록에 있으면 true, 하나라도 벗어나면 false를 반환합니다.</returns>
    private static bool ContainsOnlyEventIdCharacters(string value)
    {
        foreach (var character in value)
        {
            // is ... or ...는 한 값이 여러 허용 패턴 중 하나인지 읽기 쉽게 검사하는 패턴 매칭입니다.
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not ('-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 자유 입력 문자열에 줄바꿈이나 제어문자가 들어 있는지 확인해 감사 기록 한 줄의 경계를 지킵니다.
    /// </summary>
    /// <param name="value">정규화하기 전의 nullable 식별자 또는 설명 문자열입니다.</param>
    /// <returns>CR, LF, TAB 같은 제어문자가 하나라도 있으면 true, null이거나 모두 일반 문자이면 false를 반환합니다.</returns>
    private static bool ContainsControlCharacters(string? value)
    {
        if (value is null)
        {
            return false;
        }

        foreach (var character in value)
        {
            // char.IsControl은 CR/LF뿐 아니라 화면에 보이지 않아 로그의 열·행 경계를 속일 수 있는 제어문자도 찾습니다.
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 종류별 검증에서 공통으로 사용할 정규화 완료 값을 잠시 묶습니다.
    /// </summary>
    /// <param name="EventId">대문자로 정규화한 중복 판별 식별자입니다.</param>
    /// <param name="OccurredAtUtc">UTC 오프셋을 확인한 발생 시각입니다.</param>
    /// <param name="ActorId">양끝 공백을 제거한 행위자 식별자입니다.</param>
    private sealed record CommonFields(string EventId, DateTimeOffset OccurredAtUtc, string ActorId);
}
