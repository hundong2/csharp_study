using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application;

/// <summary>
/// HTTP 입력을 framework 형식과 분리해 초대 생성 use case에 전달합니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="InvitationId">사용자가 정한 초대 식별자입니다.</param>
/// <param name="ProjectId">초대할 프로젝트 식별자입니다.</param>
/// <param name="Email">초대받을 사용자의 이메일입니다.</param>
/// <param name="Role">부여할 Member 또는 Admin 권한 문자열입니다.</param>
public sealed record CreateInvitationCommand(
    string? InvitationId,
    string? ProjectId,
    string? Email,
    string? Role);

/// <summary>
/// 불투명 토큰 하나를 사용하려는 use case 입력입니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Token">클라이언트가 보관했다가 다시 보낸 보호된 토큰입니다.</param>
public sealed record RedeemInvitationCommand(string? Token);

/// <summary>
/// 생성된 Domain 초대와 클라이언트에게 한 번 전달할 토큰을 묶습니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Invitation">저장소에 기록된 불변 초대입니다.</param>
/// <param name="Token">Data Protection으로 기밀성과 무결성을 보호한 문자열입니다.</param>
public sealed record CreatedInvitation(ProjectInvitation Invitation, string Token);

/// <summary>
/// 토큰 안에 보호해 둘 최소 계약이며 비밀번호나 원문 key를 담지 않습니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Version">나중에 payload 모양이 바뀔 때 구분할 형식 버전입니다.</param>
/// <param name="InvitationId">저장된 초대를 찾을 식별자입니다.</param>
/// <param name="ProjectId">토큰과 저장소 기록이 같은 프로젝트인지 확인할 값입니다.</param>
/// <param name="Role">토큰 발급 시점에 승인한 권한입니다.</param>
/// <param name="ExpiresAtUtc">토큰을 더 이상 받아들이지 않을 UTC 시각입니다.</param>
public sealed record InvitationTokenPayload(
    int Version,
    string InvitationId,
    string ProjectId,
    ProjectRole Role,
    DateTimeOffset ExpiresAtUtc);
