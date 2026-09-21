using System.Security.Claims;
using DocumentAccessApi.Domain;
using Microsoft.AspNetCore.Authorization;

namespace DocumentAccessApi.Security;

/// <summary>
/// 문자열 오타가 보안 규칙을 바꾸지 않도록 역할 이름을 한곳에 모읍니다.
/// </summary>
public static class DocumentRoles
{
    public const string Viewer = "Viewer";
    public const string Editor = "Editor";
    public const string Admin = "Admin";
}

/// <summary>
/// endpoint에 적용할 정책 이름을 한곳에 모읍니다.
/// </summary>
public static class DocumentPolicies
{
    public const string Creator = "DocumentCreator";
}

/// <summary>
/// 현재 사용자가 특정 문서를 읽을 수 있는지 판단해 달라는 marker requirement입니다.
/// 상태가 필요 없어서 모든 요청이 같은 인스턴스를 공유할 수 있습니다.
/// </summary>
public sealed class DocumentReadRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 새 객체 생성을 반복하지 않도록 공유할 requirement 한 개를 제공합니다.
    /// </summary>
    public static DocumentReadRequirement Instance { get; } = new();

    /// <summary>
    /// 상태 없는 requirement가 Instance를 통해서만 공유되도록 외부 생성을 막습니다.
    /// </summary>
    private DocumentReadRequirement()
    {
    }
}

/// <summary>
/// 문서 소유자 또는 Admin만 읽기를 허용하는 리소스 기반 Authorization Handler입니다.
/// </summary>
public sealed class DocumentReadAuthorizationHandler
    : AuthorizationHandler<DocumentReadRequirement, ProjectDocument>
{
    /// <summary>
    /// 사용자의 claim과 실제 문서의 OwnerId를 비교해 읽기 requirement의 성공 여부를 표시합니다.
    /// </summary>
    /// <param name="context">인증된 사용자와 지금까지의 인가 판단 상태입니다.</param>
    /// <param name="requirement">평가할 문서 읽기 요구사항입니다.</param>
    /// <param name="resource">Application Service가 먼저 불러온 실제 문서입니다.</param>
    /// <returns>동기 판단이 끝났으므로 이미 완료된 Task를 반환합니다.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentReadRequirement requirement,
        ProjectDocument resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
        {
            // claim만 수동으로 넣은 미인증 identity나 안정 ID 없는 Admin도 fail-closed로 아무 requirement도 성공시키지 않습니다.
            return Task.CompletedTask;
        }

        var isOwner = string.Equals(userId, resource.OwnerId, StringComparison.Ordinal);

        if (isOwner || context.User.IsInRole(DocumentRoles.Admin))
        {
            // Succeed는 이 handler가 requirement를 만족했다고 Authorization Service에 알립니다.
            context.Succeed(requirement);
        }

        // 다른 handler가 같은 requirement를 만족할 수 있는 조합을 허용하려고 context.Fail()은 호출하지 않습니다. Succeed가 없으면 최종 판단은 실패입니다.

        return Task.CompletedTask;
    }
}
