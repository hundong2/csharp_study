using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DocumentAccessApi.Security;

/// <summary>
/// 학습용 인증 scheme와 header 이름입니다.
/// 이 방식은 header를 누구나 위조할 수 있으므로 운영 인증으로 사용하면 안 됩니다.
/// </summary>
public static class DemoHeaderAuthenticationDefaults
{
    public const string Scheme = "DemoHeader";
    public const string UserHeader = "X-Demo-User";
}

/// <summary>
/// 미리 정한 세 사용자만 ClaimsPrincipal로 바꾸는 학습용 Authentication Handler입니다.
/// 운영에서는 OIDC/JWT처럼 서명과 발급자를 검증하는 검증된 handler로 교체해야 합니다.
/// </summary>
public sealed class DemoHeaderAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // collection expression([ ... ])은 C# 12부터 간결하게 배열을 만드는 문법입니다.
    private static readonly IReadOnlyDictionary<string, DemoUser> Users =
        new Dictionary<string, DemoUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["alice"] = new DemoUser("user-alice", "Alice", [DocumentRoles.Editor]),
            ["bob"] = new DemoUser("user-bob", "Bob", [DocumentRoles.Viewer]),
            ["admin"] = new DemoUser("user-admin", "Admin", [DocumentRoles.Admin]),
        };

    /// <summary>
    /// ASP.NET Core가 handler를 만들 때 필요한 options, logger, URL encoder를 전달받습니다.
    /// </summary>
    /// <param name="options">scheme별 인증 설정을 현재 값으로 제공하는 monitor입니다.</param>
    /// <param name="logger">인증 과정의 운영 로그를 만들 logger factory입니다.</param>
    /// <param name="encoder">안전한 URL 출력을 위한 framework encoder입니다.</param>
    public DemoHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 요청 header를 읽어 알려진 데모 사용자의 ClaimsPrincipal을 만듭니다.
    /// </summary>
    /// <returns>header가 없으면 NoResult, 잘못됐으면 Fail, 알려진 사용자면 Success 결과를 반환합니다.</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DemoHeaderAuthenticationDefaults.UserHeader, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("X-Demo-User header는 정확히 한 개의 값이어야 합니다."));
        }

        // 위에서 값의 null/공백 여부를 검사했으므로 !로 컴파일러의 nullable 경고만 제거합니다.
        var requestedUser = values[0]!.Trim();
        if (!Users.TryGetValue(requestedUser, out var demoUser))
        {
            return Task.FromResult(AuthenticateResult.Fail("등록되지 않은 데모 사용자입니다."));
        }

        var claims = new List<Claim>
        {
            // NameIdentifier는 표시 이름이 바뀌어도 권한 비교에 사용할 안정적인 subject ID입니다.
            new(ClaimTypes.NameIdentifier, demoUser.SubjectId),
            new(ClaimTypes.Name, demoUser.DisplayName),
        };

        foreach (var role in demoUser.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 두 번째 authenticationType이 비어 있지 않으면 ClaimsIdentity.IsAuthenticated가 true가 됩니다.
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// 신원을 확인할 수 없는 요청에 로그인 redirect 대신 API용 401 Problem Details를 씁니다.
    /// </summary>
    /// <param name="properties">challenge를 호출한 endpoint의 인증 속성입니다.</param>
    /// <returns>응답 JSON 쓰기가 끝나는 시점을 나타내는 Task를 반환합니다.</returns>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // 401은 client가 사용할 인증 scheme을 알 수 있도록 RFC 9110의 WWW-Authenticate challenge도 함께 보내야 합니다.
        Response.Headers.WWWAuthenticate = $"{Scheme.Name} realm=\"DocumentAccessApi\"";

        // Results.Problem은 상태 코드뿐 아니라 RFC 9457 형식 JSON과 application/problem+json media type도 일관되게 설정합니다.
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "인증이 필요합니다.",
                detail: "이 endpoint에 유효한 인증 정보가 필요합니다.")
            .ExecuteAsync(Context);
    }

    /// <summary>
    /// 신원은 확인됐지만 정책을 만족하지 못한 요청에 403 Problem Details를 씁니다.
    /// </summary>
    /// <param name="properties">forbid를 호출한 endpoint의 인증 속성입니다.</param>
    /// <returns>응답 JSON 쓰기가 끝나는 시점을 나타내는 Task를 반환합니다.</returns>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "권한이 부족합니다.",
                detail: "현재 사용자는 이 endpoint의 정책을 만족하지 않습니다.")
            .ExecuteAsync(Context);
    }

    /// <summary>
    /// 데모 사용자의 변하지 않는 subject, 표시 이름, 역할 목록을 묶습니다.
    /// </summary>
    /// <param name="SubjectId">권한 비교에 사용하는 안정적인 사용자 식별자입니다.</param>
    /// <param name="DisplayName">사람에게 보여 줄 이름입니다.</param>
    /// <param name="Roles">정책 평가에 사용할 역할 claim 값들입니다.</param>
    private sealed record DemoUser(string SubjectId, string DisplayName, string[] Roles);
}
