using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using WebhookReceiverApi.Application;
using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Infrastructure;
using WebhookReceiverApi.Presentation;
using WebhookReceiverApi.SelfTesting;

namespace WebhookReceiverApi;

/// <summary>
/// 프로세스 진입점이자 HTTP, Application, Port, Adapter를 연결하는 Composition Root입니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// --self-test이면 회귀 검증을 실행하고, 아니면 HTTP 서버를 실행합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 URL, 환경, 자체 테스트 인수입니다.</param>
    /// <returns>자체 테스트 성공 또는 정상 서버 종료 시 0, 자체 테스트 실패 시 1인 프로세스 종료 코드를 반환합니다.</returns>
    // async는 메서드에서 await를 쓰게 하고, await는 비동기 작업 동안 thread를 붙잡지 않습니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 서버 종료 시 WebApplication의 비동기 자원 정리를 보장합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 설정 검증, DI 등록, middleware, endpoint 연결을 마친 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(string[] args)
    {
        // var는 오른쪽 값에서 WebApplicationBuilder라는 정적 형식을 추론합니다.
        var builder = WebApplication.CreateBuilder(args);

        // options =>는 이름 없는 짧은 함수(lambda)로 Problem Details 설정을 제자리에서 구성합니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);

        // ValidateOnStart는 첫 요청까지 잘못된 secret을 숨기지 않고 서버 시작 시 즉시 실패하게 합니다.
        builder.Services.AddOptions<WebhookSecurityOptions>()
            .Bind(builder.Configuration.GetSection(WebhookSecurityOptions.SectionName))
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Secret) >= 32,
                "WebhookSecurity:Secret은 UTF-8 기준 32바이트 이상이어야 합니다.")
            .ValidateOnStart();

        // DI와 DIP 덕분에 Application Service는 아래 구체 Adapter를 new로 직접 만들지 않아 교체와 테스트가 쉽습니다.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IWebhookSignatureVerifier, HmacSha256WebhookSignatureVerifier>();
        builder.Services.AddSingleton<IWebhookPayloadParser, SystemTextJsonWebhookPayloadParser>();
        builder.Services.AddSingleton<IWebhookReceiptRepository, InMemoryWebhookReceiptRepository>();
        builder.Services.AddSingleton<WebhookReceiverApplicationService>();

        var app = builder.Build();
        // ExceptionHandler는 예상 밖 예외의 stack trace를 숨기고 등록한 Problem Details writer로 안전한 500을 만듭니다.
        app.UseExceptionHandler();
        // StatusCodePages는 route 없음 같은 framework 오류도 본문 없는 응답 대신 Problem Details로 만듭니다.
        app.UseStatusCodePages();
        app.MapGet("/", GetServiceStatus);
        // 학습 범위를 좁히려고 조회 endpoint 인증은 생략했습니다. 운영에서는 관리자 권한 등 별도 인가가 필요합니다.
        app.MapWebhookEndpoints();
        return app;
    }

    /// <summary>
    /// 서비스가 실행 중인지와 공개 가능한 제한값을 보여 주는 상태 응답을 만듭니다.
    /// </summary>
    /// <returns>서비스명과 공개 가능한 설정만 가진 200 JSON 결과를 반환합니다.</returns>
    private static IResult GetServiceStatus()
    {
        return TypedResults.Ok(
            new
            {
                service = "WebhookReceiverApi",
                maxBodyBytes = WebhookSecurityOptions.MaxBodyBytes,
                allowedClockSkewSeconds = WebhookSecurityOptions.AllowedClockSkewSeconds
            });
    }

    /// <summary>
    /// framework가 만든 Problem Details에 안전한 code, field, traceId를 일관되게 추가합니다.
    /// </summary>
    /// <param name="context">현재 요청과 작성 중인 Problem Details를 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        // ??=는 왼쪽이 null일 때만 오른쪽 요청 경로를 대입합니다.
        problem.Instance ??= context.HttpContext.Request.Path;

        if (!problem.Extensions.ContainsKey("code"))
        {
            problem.Detail = statusCode switch
            {
                StatusCodes.Status404NotFound => "요청한 URL을 찾을 수 없습니다.",
                StatusCodes.Status405MethodNotAllowed => "이 URL에서 요청한 HTTP method를 지원하지 않습니다.",
                StatusCodes.Status500InternalServerError => "서버에서 요청을 완료하지 못했습니다.",
                _ => "요청을 처리할 수 없습니다."
            };
        }

        problem.Extensions.TryAdd(
            "code",
            statusCode switch
            {
                StatusCodes.Status404NotFound => "http.not_found",
                StatusCodes.Status405MethodNotAllowed => "http.method_not_allowed",
                StatusCodes.Status500InternalServerError => "http.internal_error",
                _ => "http.error"
            });
        problem.Extensions.TryAdd("field", "request");
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }
}
