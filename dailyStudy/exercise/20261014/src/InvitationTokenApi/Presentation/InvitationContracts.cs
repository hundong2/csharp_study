using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Presentation;

/// <summary>
/// 초대 생성 JSON body의 외부 계약입니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="InvitationId">클라이언트가 정한 3~40자 초대 ID입니다.</param>
/// <param name="ProjectId">초대 대상 프로젝트 ID입니다.</param>
/// <param name="Email">초대받을 사용자 이메일입니다.</param>
/// <param name="Role">Member 또는 Admin 권한입니다.</param>
public sealed record CreateInvitationRequest(
    string? InvitationId,
    string? ProjectId,
    string? Email,
    string? Role);

/// <summary>
/// 생성 성공 때 토큰과 만료 시각을 한 번 전달하는 JSON 계약입니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="InvitationId">정규화된 초대 ID입니다.</param>
/// <param name="ProjectId">정규화된 프로젝트 ID입니다.</param>
/// <param name="Email">정규화된 초대 대상 이메일입니다.</param>
/// <param name="Role">검증된 Member 또는 Admin 권한입니다.</param>
/// <param name="Token">클라이언트가 안전하게 보관해야 할 불투명 토큰입니다.</param>
/// <param name="ExpiresAtUtc">토큰을 더 이상 사용할 수 없는 UTC 시각입니다.</param>
public sealed record CreateInvitationResponse(
    string InvitationId,
    string ProjectId,
    string Email,
    string Role,
    string Token,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// 초대 조회 성공 JSON 계약이며 발급 토큰은 다시 노출하지 않습니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="InvitationId">정규화된 초대 ID입니다.</param>
/// <param name="ProjectId">정규화된 프로젝트 ID입니다.</param>
/// <param name="Email">정규화된 초대 대상 이메일입니다.</param>
/// <param name="Role">Member 또는 Admin 권한입니다.</param>
/// <param name="Status">Issued 또는 Redeemed 상태입니다.</param>
/// <param name="ExpiresAtUtc">토큰 만료 UTC 시각입니다.</param>
/// <param name="RedeemedAtUtc">사용 완료 UTC 시각이며 아직 사용 전이면 null입니다.</param>
public sealed record InvitationDetailsResponse(
    string InvitationId,
    string ProjectId,
    string Email,
    string Role,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RedeemedAtUtc)
{
    /// <summary>
    /// 내부 Domain Model을 조회 전용 HTTP DTO로 복사합니다.
    /// </summary>
    /// <param name="invitation">검증된 불변 초대 snapshot입니다.</param>
    /// <returns>토큰을 포함하지 않는 조회 응답을 반환합니다.</returns>
    public static InvitationDetailsResponse FromDomain(ProjectInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new InvitationDetailsResponse(
            invitation.InvitationId,
            invitation.ProjectId,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.Status.ToString(),
            invitation.ExpiresAtUtc,
            invitation.RedeemedAtUtc);
    }
}

/// <summary>
/// 토큰 사용 JSON body의 외부 계약입니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Token">발급 응답에서 받은 불투명 초대 토큰입니다.</param>
public sealed record RedeemInvitationRequest(string? Token);

/// <summary>
/// 토큰 사용 성공 응답이며 이메일과 토큰을 의도적으로 다시 내보내지 않습니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="InvitationId">사용 완료된 초대 ID입니다.</param>
/// <param name="ProjectId">권한을 받을 프로젝트 ID입니다.</param>
/// <param name="Role">부여할 Member 또는 Admin 권한입니다.</param>
/// <param name="RedeemedAtUtc">원자적 상태 전이에 기록된 UTC 시각입니다.</param>
public sealed record RedeemInvitationResponse(
    string InvitationId,
    string ProjectId,
    string Role,
    DateTimeOffset RedeemedAtUtc)
{
    /// <summary>
    /// 사용 완료된 Domain Model에서 최소 성공 응답만 선택합니다.
    /// </summary>
    /// <param name="invitation">RedeemedAtUtc가 반드시 있는 사용 완료 초대입니다.</param>
    /// <returns>토큰과 이메일을 제외한 사용 성공 응답을 반환합니다.</returns>
    public static RedeemInvitationResponse FromDomain(ProjectInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        // ?? throw는 왼쪽 값이 null이면 즉시 예외를 던져 성공 응답에 잘못된 기본 시각이 들어가지 않게 합니다.
        return new RedeemInvitationResponse(
            invitation.InvitationId,
            invitation.ProjectId,
            invitation.Role.ToString(),
            invitation.RedeemedAtUtc ??
                throw new InvalidOperationException("사용 완료 시각이 없는 초대입니다."));
    }
}

/// <summary>
/// OpenAPI metadata에서 Problem Details 확장 필드까지 설명할 오류 응답 모양입니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Type">HTTP 오류 종류를 설명하는 URI입니다.</param>
/// <param name="Title">오류 분류의 짧은 제목입니다.</param>
/// <param name="Status">HTTP 상태 코드입니다.</param>
/// <param name="Detail">사용자에게 안전한 구체 설명입니다.</param>
/// <param name="Instance">오류가 발생한 요청 경로입니다.</param>
/// <param name="Code">클라이언트가 분기할 안정 오류 코드입니다.</param>
/// <param name="Field">문제와 관련된 입력 필드입니다.</param>
/// <param name="TraceId">서버 로그와 요청을 연결할 진단 ID입니다.</param>
public sealed record ApiProblemResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string Field,
    string TraceId);
