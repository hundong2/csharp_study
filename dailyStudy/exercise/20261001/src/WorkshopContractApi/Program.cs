using System.Net;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using WorkshopContractApi.Application;
using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Infrastructure;
using WorkshopContractApi.OpenApi;
using WorkshopContractApi.Presentation;
using WorkshopContractApi.SelfTesting;

namespace WorkshopContractApi;

/// <summary>
/// 프로세스 진입점이자 DI 등록, OpenAPI 생성, HTTP endpoint 연결을 한곳에서 조립하는 Composition Root입니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// --self-test 인수가 있으면 회귀 검증을 실행하고, 아니면 실제 HTTP 서버를 실행합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 URL, 환경, 자체 테스트 인수입니다.</param>
    /// <returns>정상 완료 시 0, 자체 테스트 실패 시 1인 프로세스 종료 코드를 반환합니다.</returns>
    // async는 메서드 안에서 await를 사용할 수 있게 합니다. 실제로 thread를 양보하는지는 기다리는 작업의 완료 상태에 달려 있습니다.
    public static async Task<int> Main(string[] args)
    {
        // LINQ Contains는 명령줄 배열에 정확히 같은 self-test 인수가 하나라도 있는지 검사합니다.
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 서버가 끝날 때 비동기 자원 정리를 보장하고, var는 오른쪽 값으로 정적 형식을 추론합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// OpenAPI 서비스, Application Service, Port와 Adapter, endpoint를 연결한 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <param name="useEphemeralLoopbackPort">테스트에서 OS가 비어 있는 loopback 포트를 고르게 할지 나타냅니다.</param>
    /// <param name="environmentName">Development 또는 Production 같은 선택적 환경 이름입니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    // string?의 ?는 호출자가 환경 이름을 생략해 기본 환경을 쓰게 할 수 있음을 nullable 분석기에 알립니다.
    public static WebApplication BuildApplication(
        string[] args,
        bool useEphemeralLoopbackPort = false,
        string? environmentName = null)
    {
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

        // AddProblemDetails는 binding, 404, 415 같은 framework 오류에도 같은 안전 필드를 보완합니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);

        // ASP.NET Core 기본값은 숫자 문자열도 허용하므로 JSON 숫자와 OpenAPI integer 계약을 맞추기 위해 엄격하게 읽습니다.
        builder.Services.ConfigureHttpJsonOptions(
            options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

        // AddOpenApi는 endpoint metadata를 읽어 OpenAPI 문서를 만드는 서비스입니다.
        // options =>는 이름 없는 짧은 함수(lambda)로 설정 객체를 구성하는 문법입니다.
        builder.Services.AddOpenApi(options =>
        {
            // generic <T>는 어떤 transformer 형식을 DI로 만들지 compile time에 명확히 지정합니다.
            options.AddDocumentTransformer<WorkshopApiDocumentTransformer>();
            options.AddOperationTransformer<WorkshopApiOperationTransformer>();
        });

        // singleton은 앱 전체가 한 instance를 공유하는 lifetime입니다. 상태를 공유하므로 Repository의 lock처럼 구현 자체가 thread-safe해야 합니다.
        builder.Services.AddSingleton<IWorkshopCatalog, DemoWorkshopCatalog>();
        builder.Services.AddSingleton<IRegistrationRepository, InMemoryRegistrationRepository>();
        builder.Services.AddSingleton<ITicketPricePolicy, StandardTicketPricePolicy>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkshopRegistrationService>();

        var app = builder.Build();

        // StatusCodePages는 route가 없는 404처럼 본문 없는 오류도 Problem Details로 채웁니다.
        app.UseStatusCodePages();

        // MapOpenApi가 기본 문서 이름 v1을 /openapi/v1.json으로 노출합니다.
        // 교육용으로 모든 환경에 열지만 운영에서는 인증을 걸거나 내부 환경에서만 노출해야 합니다.
        app.MapOpenApi();

        // 익명 객체는 준비 상태처럼 재사용하지 않을 아주 작은 응답에만 사용합니다.
        app.MapGet("/", () => Results.Ok(new { service = "workshop-contract", status = "ready" }))
            .ExcludeFromDescription();
        app.MapWorkshopRegistrationEndpoints();

        return app;
    }

    /// <summary>
    /// framework가 만든 Problem Details에 안전한 detail, 안정 code, 관련 field, 추적 ID를 일관되게 추가합니다.
    /// </summary>
    /// <param name="context">현재 HTTP 요청과 작성 중인 ProblemDetails를 함께 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        problem.Instance = context.HttpContext.Request.Path;

        // Application 오류는 endpoint가 이미 안전하고 구체적인 detail과 code를 넣었습니다.
        // framework 오류에만 일반 안내를 덮어써 JSON parser의 내부 예외 정보가 외부로 새지 않게 합니다.
        if (!problem.Extensions.ContainsKey("code"))
        {
            // switch expression은 statusCode 하나를 안전한 안내 문구 하나로 대응시켜 결과를 바로 만듭니다.
            problem.Detail = statusCode switch
            {
                StatusCodes.Status400BadRequest => "요청 JSON 형식과 값의 자료형을 확인하세요.",
                StatusCodes.Status404NotFound => "URL 또는 식별자를 확인하세요.",
                StatusCodes.Status415UnsupportedMediaType => "Content-Type을 application/json으로 보내세요.",
                _ => "요청을 처리할 수 없습니다."
            };
        }

        problem.Extensions.TryAdd(
            "code",
            statusCode switch
            {
                StatusCodes.Status400BadRequest => "http.bad_request",
                StatusCodes.Status404NotFound => "http.not_found",
                StatusCodes.Status415UnsupportedMediaType => "http.unsupported_media_type",
                _ => "http.error"
            });
        problem.Extensions.TryAdd("field", "request");
        // ?.는 현재 Activity가 있을 때만 Id를 읽고, ??는 없으면 ASP.NET Core 요청 ID를 선택합니다.
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 자체 테스트 서버가 고정 포트와 충돌하지 않도록 loopback의 사용 가능한 임시 포트를 요청합니다.
    /// </summary>
    /// <param name="options">Kestrel 수신 주소를 구성할 옵션입니다.</param>
    /// <returns>옵션을 제자리에서 바꾸므로 반환값은 없습니다.</returns>
    private static void ConfigureEphemeralTestEndpoint(KestrelServerOptions options)
    {
        options.Listen(IPAddress.Loopback, 0);
    }
}
