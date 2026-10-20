using AuditEventApi.Application.Ports;
using AuditEventApi.Domain;

namespace AuditEventApi.Application;

/// <summary>
/// 실행 환경과 자체 테스트가 같은 기본 Strategy 구성을 재사용하게 하는 작은 팩터리입니다.
/// </summary>
public static class AuditEventMappers
{
    // collection expression은 목표 형식인 string[]에 맞춰 세 상수를 배열로 만드는 최신 C# 문법입니다.
    private static readonly string[] SupportedKindValues =
    [
        AuditEventDraft.UserSignedInKind,
        AuditEventDraft.DocumentChangedKind,
        AuditEventDraft.PermissionChangedKind
    ];

    /// <summary>
    /// 서비스 구성 검증과 상태 응답이 함께 사용할 공식 이벤트 종류 목록을 가져옵니다.
    /// </summary>
    /// <returns>외부에서 항목을 추가하거나 교체할 수 없는 세 종류의 읽기 전용 목록입니다.</returns>
    public static IReadOnlyList<string> SupportedKinds { get; } =
        Array.AsReadOnly(SupportedKindValues);

    /// <summary>
    /// 현재 허용한 세 이벤트 종류를 각각 담당하는 Mapper Strategy를 만듭니다.
    /// </summary>
    /// <returns>user-signed-in, document-changed, permission-changed 순서의 읽기 전용 Strategy 목록입니다.</returns>
    public static IReadOnlyList<IAuditEventMapper> CreateDefault()
    {
        // 공식 종류 목록과 같은 세 Strategy를 만들며, 서비스 생성자가 누락·중복·목록 밖 등록을 다시 검증합니다.
        return
        [
            new UserSignedInAuditEventMapper(),
            new DocumentChangedAuditEventMapper(),
            new PermissionChangedAuditEventMapper()
        ];
    }
}

/// <summary>
/// 로그인 command의 선택 필드를 로그인 전용 Domain 팩터리에 연결하는 Strategy입니다.
/// </summary>
public sealed class UserSignedInAuditEventMapper : IAuditEventMapper
{
    /// <summary>
    /// 이 Strategy가 담당하는 이벤트 종류를 가져옵니다.
    /// </summary>
    /// <returns>user-signed-in 고정 문자열입니다.</returns>
    // =>는 단일 식의 값을 바로 반환하는 식 본문 문법이며, 상수의 의미를 짧고 분명하게 보여 줍니다.
    public string SupportedKind => AuditEventDraft.UserSignedInKind;

    /// <summary>
    /// 로그인에 필요한 IP와 성공 여부를 골라 Domain이 공통 값과 함께 검증하게 합니다.
    /// </summary>
    /// <param name="command">로그인 세부 값을 포함한 안정적인 Application 입력입니다.</param>
    /// <returns>정규화한 로그인 초안 또는 필드별 검증 오류를 반환합니다.</returns>
    public Result<AuditEventDraft> Map(IngestAuditEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return AuditEventDraft.CreateUserSignedIn(
            command.EventId,
            command.OccurredAtUtc,
            command.ActorId,
            command.IpAddress,
            command.Succeeded);
    }
}

/// <summary>
/// 문서 변경 command의 선택 필드를 문서 변경 전용 Domain 팩터리에 연결하는 Strategy입니다.
/// </summary>
public sealed class DocumentChangedAuditEventMapper : IAuditEventMapper
{
    /// <summary>
    /// 이 Strategy가 담당하는 이벤트 종류를 가져옵니다.
    /// </summary>
    /// <returns>document-changed 고정 문자열입니다.</returns>
    public string SupportedKind => AuditEventDraft.DocumentChangedKind;

    /// <summary>
    /// 문서 식별자와 안전한 변경 요약을 골라 Domain이 공통 값과 함께 검증하게 합니다.
    /// </summary>
    /// <param name="command">문서 변경 세부 값을 포함한 안정적인 Application 입력입니다.</param>
    /// <returns>정규화한 문서 변경 초안 또는 필드별 검증 오류를 반환합니다.</returns>
    public Result<AuditEventDraft> Map(IngestAuditEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return AuditEventDraft.CreateDocumentChanged(
            command.EventId,
            command.OccurredAtUtc,
            command.ActorId,
            command.DocumentId,
            command.ChangeSummary);
    }
}

/// <summary>
/// 권한 변경 command의 선택 필드를 권한 변경 전용 Domain 팩터리에 연결하는 Strategy입니다.
/// </summary>
public sealed class PermissionChangedAuditEventMapper : IAuditEventMapper
{
    /// <summary>
    /// 이 Strategy가 담당하는 이벤트 종류를 가져옵니다.
    /// </summary>
    /// <returns>permission-changed 고정 문자열입니다.</returns>
    public string SupportedKind => AuditEventDraft.PermissionChangedKind;

    /// <summary>
    /// 대상 사용자, 권한, 동작을 골라 Domain이 공통 값과 함께 검증하게 합니다.
    /// </summary>
    /// <param name="command">권한 변경 세부 값을 포함한 안정적인 Application 입력입니다.</param>
    /// <returns>정규화한 권한 변경 초안 또는 필드별 검증 오류를 반환합니다.</returns>
    public Result<AuditEventDraft> Map(IngestAuditEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return AuditEventDraft.CreatePermissionChanged(
            command.EventId,
            command.OccurredAtUtc,
            command.ActorId,
            command.TargetUserId,
            command.Permission,
            command.Action);
    }
}
