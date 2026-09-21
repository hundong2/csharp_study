using System.Security.Claims;
using DocumentAccessApi.Application;
using DocumentAccessApi.Application.Ports;
using DocumentAccessApi.Infrastructure;
using DocumentAccessApi.Presentation;
using DocumentAccessApi.Security;
using DocumentAccessApi.SelfTesting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

// top-level statements는 별도 Main 메서드 선언 없이도 위에서 아래로 시작 코드를 읽게 해 주는 C# 문법입니다.
// var는 오른쪽 식으로 타입이 명확할 때 컴파일러가 타입을 추론하게 하여 중복 표기를 줄입니다.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
builder.Services.AddSingleton<DocumentApplicationService>();

// Authentication은 “누구인가”를 확인합니다. scheme 이름은 어떤 handler가 신원을 만들지 선택하는 열쇠입니다.
builder.Services
    .AddAuthentication(DemoHeaderAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, DemoHeaderAuthenticationHandler>(
        DemoHeaderAuthenticationDefaults.Scheme,
        configureOptions: null);

// fallback policy는 AllowAnonymous를 명시한 endpoint 외에는 기본적으로 인증을 요구하는 secure-by-default 장치입니다.
// policy => ... 는 이름 없는 함수(lambda)입니다. framework가 전달한 policy를 구성하며 별도 값을 반환하지 않습니다.
builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(DocumentPolicies.Creator, policy =>
    {
        policy.RequireAuthenticatedUser();
        // 세 requirement는 모두 만족해야 하는 AND 조건입니다. RequireRole 안의 Editor/Admin 두 값만 OR 조건입니다.
        policy.RequireClaim(ClaimTypes.NameIdentifier);
        policy.RequireRole(DocumentRoles.Editor, DocumentRoles.Admin);
    });

builder.Services.AddSingleton<IAuthorizationHandler, DocumentReadAuthorizationHandler>();

var app = builder.Build();

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    return await SelfTestRunner.RunAsync(app.Services);
}

app.UseExceptionHandler();

// 순서가 중요합니다. Authentication이 HttpContext.User를 만든 뒤 Authorization이 그 claim으로 정책을 평가합니다.
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", DocumentEndpoints.GetHealth)
    .AllowAnonymous();

app.MapPost("/documents", DocumentEndpoints.CreateAsync)
    .RequireAuthorization(DocumentPolicies.Creator);

app.MapGet("/documents/{id:guid}", DocumentEndpoints.FindAsync);

// top-level await는 비동기 서버가 종료될 때까지 thread를 막지 않고 기다린 뒤 Main의 종료 코드 0을 반환합니다.
await app.RunAsync();
return 0;
