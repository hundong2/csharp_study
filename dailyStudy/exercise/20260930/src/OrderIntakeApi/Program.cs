using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Routing;
using OrderIntakeApi.Application;
using OrderIntakeApi.Application.Ports;
using OrderIntakeApi.ErrorHandling;
using OrderIntakeApi.Infrastructure;
using OrderIntakeApi.Presentation;
using OrderIntakeApi.SelfTesting;

namespace OrderIntakeApi;

/// <summary>
/// 프로세스 진입점이자 DI, middleware, endpoint를 조립하는 Composition Root입니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// --self-test이면 회귀 검증을 실행하고, 아니면 실제 HTTP 서버를 실행합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 URL, 환경, 자체 테스트 인수입니다.</param>
    /// <returns>정상 완료 시 0, 자체 테스트 실패 시 1인 프로세스 종료 코드를 반환합니다.</returns>
    // async는 이 메서드가 await를 사용하며 완료 결과를 Task<int>로 돌려준다는 뜻입니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 예외가 나도 WebApplication을 DisposeAsync로 정리하고, var는 오른쪽 값에서 형식을 추론합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// Domain/Application Port와 Adapter, 오류 middleware, endpoint를 한곳에서 연결합니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <param name="useEphemeralLoopbackPort">자체 테스트에서 OS가 빈 loopback 포트를 고르게 할지 나타냅니다.</param>
    /// <param name="environmentName">Development와 Production 동작을 결정할 선택적 환경 이름입니다.</param>
    /// <param name="enableDemoEndpoints">Development에서도 명시적으로 장애 주입 route를 켤지 나타냅니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    // string?와 bool?의 ?는 호출자가 값을 생략해 환경 설정이나 기본값에 맡길 수 있음을 nullable 분석기에 알립니다.
    public static WebApplication BuildApplication(
        string[] args,
        bool useEphemeralLoopbackPort = false,
        string? environmentName = null,
        bool? enableDemoEndpoints = null)
    {
        // var는 오른쪽 생성식으로 형식이 분명할 때 같은 형식 이름을 반복하지 않게 합니다.
        var builderOptions = new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environmentName
        };
        var builder = WebApplication.CreateBuilder(builderOptions);

        if (useEphemeralLoopbackPort)
        {
            builder.WebHost.ConfigureKestrel(ConfigureEphemeralTestEndpoint);
            builder.Logging.ClearProviders();
        }

        // custom writer를 AddProblemDetails보다 먼저 등록해 unsupported Accept에서도 같은 JSON 계약을 보장합니다.
        builder.Services.AddSingleton<IProblemDetailsWriter, JsonProblemDetailsWriter>();
        // CustomizeProblemDetails는 Result, exception handler, status code pages가 만든 모든 오류에 같은 추적 필드를 붙입니다.
        // options =>는 이름 없는 짧은 함수(lambda)로 framework 옵션 객체를 설정합니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        // 환경별 기본값 차이를 없애 malformed JSON을 항상 같은 BadHttpRequestException → 400 경계로 보냅니다.
        builder.Services.Configure<RouteHandlerOptions>(
            options => options.ThrowOnBadRequest = true);

        builder.Services.AddSingleton<DemoProductCatalog>();
        // factory lambda는 같은 singleton 객체를 구체 형식과 Port 두 관점에서 공유하게 합니다.
        builder.Services.AddSingleton<IProductCatalog>(
            services => services.GetRequiredService<DemoProductCatalog>());
        builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        builder.Services.AddSingleton<IShippingFeePolicy, ThresholdShippingFeePolicy>();
        builder.Services.AddSingleton<PlaceOrderService>();

        var app = builder.Build();

        // 예외 처리 middleware는 아래 endpoint에서 올라오는 예외를 잡아야 하므로 route 실행보다 먼저 둡니다.
        app.UseExceptionHandler();
        // StatusCodePages는 본문 없는 404 같은 상태도 같은 Problem Details 계약으로 채웁니다.
        app.UseStatusCodePages();

        // () =>는 parameter 없는 익명 함수이고, new { ... }는 상태 확인에만 쓸 익명 응답 형식을 만듭니다.
        app.MapGet("/", () => Results.Ok(new { service = "order-intake", status = "ready" }));
        app.MapOrderEndpoints();

        // ??는 왼쪽 값이 null일 때만 오른쪽 configuration 값을 선택합니다.
        var demoEndpointsEnabled = enableDemoEndpoints ??
            builder.Configuration.GetValue<bool>("DemoEndpoints:Enabled");
        if (app.Environment.IsDevelopment() && demoEndpointsEnabled)
        {
            // 환경 이름과 명시적 opt-in이 모두 맞아야 운영에 위험한 장애 주입 route가 생깁니다.
            app.MapDemoCatalogEndpoints();
        }

        return app;
    }

    /// <summary>
    /// 모든 Problem Details에 instance, 안정 코드, 분산 추적용 traceId를 빠짐없이 추가합니다.
    /// </summary>
    /// <param name="context">현재 HTTP 문맥과 작성 중인 ProblemDetails를 함께 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var httpContext = context.HttpContext;

        // ??=는 왼쪽 값이 null일 때만 오른쪽 경로를 대입해 명시적으로 정한 instance를 보존합니다.
        problem.Instance ??= httpContext.Request.Path;
        var statusCode = problem.Status ?? httpContext.Response.StatusCode;
        problem.Type ??= DefaultTypeFor(statusCode);
        problem.Title ??= DefaultTitleFor(statusCode);
        problem.Detail ??= DefaultDetailFor(statusCode);
        problem.Extensions.TryAdd("code", DefaultCodeFor(statusCode));
        // ?.는 현재 Activity가 있을 때만 Id를 읽고, ??는 없으면 ASP.NET Core 요청 식별자를 선택합니다.
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? httpContext.TraceIdentifier);
    }

    /// <summary>
    /// 별도 코드가 없는 framework 오류 상태를 안정적인 기본 코드로 바꿉니다.
    /// </summary>
    /// <param name="statusCode">Problem Details의 HTTP 상태 코드입니다.</param>
    /// <returns>클라이언트가 분기할 기본 오류 코드 문자열을 반환합니다.</returns>
    private static string DefaultCodeFor(int statusCode)
    {
        // switch expression과 관계 pattern은 상태 코드 하나를 안정적인 기본 오류 코드 하나로 대응시킵니다.
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "http.bad_request",
            StatusCodes.Status404NotFound => "http.not_found",
            StatusCodes.Status409Conflict => "http.conflict",
            StatusCodes.Status503ServiceUnavailable => "http.unavailable",
            _ when statusCode >= 500 => "server.unexpected",
            _ => "http.error"
        };
    }

    /// <summary>
    /// 별도 type이 없는 오류에 HTTP 의미를 설명하는 RFC 9110 URI를 제공합니다.
    /// </summary>
    /// <param name="statusCode">Problem Details의 HTTP 상태 코드입니다.</param>
    /// <returns>알려진 상태의 RFC section URI, 그 밖에는 about:blank를 반환합니다.</returns>
    private static string DefaultTypeFor(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
            _ => "about:blank"
        };
    }

    /// <summary>
    /// 별도 title이 없는 framework 오류에 짧은 안전한 제목을 제공합니다.
    /// </summary>
    /// <param name="statusCode">Problem Details의 HTTP 상태 코드입니다.</param>
    /// <returns>사용자가 오류 종류를 알아볼 수 있는 제목을 반환합니다.</returns>
    private static string DefaultTitleFor(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "요청을 확인하세요.",
            StatusCodes.Status404NotFound => "요청한 대상을 찾을 수 없습니다.",
            StatusCodes.Status409Conflict => "현재 상태와 요청이 충돌합니다.",
            StatusCodes.Status503ServiceUnavailable => "서비스를 잠시 사용할 수 없습니다.",
            _ when statusCode >= 500 => "서버에서 요청을 처리하지 못했습니다.",
            _ => "요청을 처리할 수 없습니다."
        };
    }

    /// <summary>
    /// 별도 detail이 없는 framework 오류에 내부 정보를 노출하지 않는 행동 안내를 제공합니다.
    /// </summary>
    /// <param name="statusCode">Problem Details의 HTTP 상태 코드입니다.</param>
    /// <returns>클라이언트가 다음 행동을 정할 수 있는 안전한 설명을 반환합니다.</returns>
    private static string DefaultDetailFor(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "요청 형식과 값을 확인하세요.",
            StatusCodes.Status404NotFound => "URL 또는 식별자를 확인하세요.",
            StatusCodes.Status409Conflict => "최신 상태를 확인한 뒤 다시 시도하세요.",
            StatusCodes.Status503ServiceUnavailable => "잠시 후 다시 시도하세요.",
            _ when statusCode >= 500 => "traceId와 함께 운영 담당자에게 문의하세요.",
            _ => "요청을 다시 확인하세요."
        };
    }

    /// <summary>
    /// 테스트 서버가 고정 포트와 충돌하지 않도록 loopback의 사용 가능한 임시 포트를 요청합니다.
    /// </summary>
    /// <param name="options">Kestrel 수신 주소를 구성할 옵션입니다.</param>
    /// <returns>옵션만 변경하므로 반환값은 없습니다.</returns>
    private static void ConfigureEphemeralTestEndpoint(KestrelServerOptions options)
    {
        options.Listen(IPAddress.Loopback, 0);
    }
}
