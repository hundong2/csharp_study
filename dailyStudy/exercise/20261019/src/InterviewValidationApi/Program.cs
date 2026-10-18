using System.Diagnostics;
using InterviewValidationApi.Application;
using InterviewValidationApi.Application.Ports;
using InterviewValidationApi.Infrastructure;
using InterviewValidationApi.Presentation;
using InterviewValidationApi.SelfTesting;

namespace InterviewValidationApi;

/// <summary>
/// 프로세스 진입점이자 Validation, HTTP, Application, Port, Adapter를 연결하는 Composition Root입니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// --self-test 인수면 회귀 검증을 실행하고, 아니면 Kestrel HTTP 서버를 실행합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 자체 테스트, URL, 환경 인수입니다.</param>
    /// <returns>정상 완료면 0, 자체 테스트 실패면 1인 프로세스 종료 코드를 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 서버가 끝날 때 WebApplication의 비동기 자원을 빠뜨리지 않고 정리합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// .NET 10 자동 검증, Problem Details, DI, middleware, endpoint를 조립한 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        // AddValidation은 source generator가 만든 metadata와 endpoint filter를 등록해 handler 전에 요청을 검증합니다.
        builder.Services.AddValidation();

        // Singleton Repository의 공유 상태는 Adapter 내부 lock으로 보호합니다. Port 주입은 테스트 대역 교체를 쉽게 합니다.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IInterviewRepository, InMemoryInterviewRepository>();
        builder.Services.AddSingleton<IInterviewBufferPolicy, RemoteInterviewBufferPolicy>();
        builder.Services.AddSingleton<IInterviewBufferPolicy, OnsiteInterviewBufferPolicy>();
        builder.Services.AddSingleton<ScheduleInterviewService>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapGet("/", GetServiceStatus);
        app.MapInterviewEndpoints();
        return app;
    }

    /// <summary>
    /// 서비스가 실행 중인지와 공개 가능한 검증 범위를 간단한 JSON으로 알려 줍니다.
    /// </summary>
    /// <returns>서비스명과 자동 검증 사용 여부를 가진 200 OK 결과를 반환합니다.</returns>
    private static IResult GetServiceStatus()
    {
        return TypedResults.Ok(
            new
            {
                service = "InterviewValidationApi",
                automaticValidation = true,
                minimumLeadMinutes = 15,
                maximumLeadDays = 180
            });
    }

    /// <summary>
    /// framework가 만든 오류 응답에도 안전한 code, field, traceId를 일관되게 추가합니다.
    /// </summary>
    /// <param name="context">현재 요청과 작성 중인 Problem Details를 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        problem.Instance ??= context.HttpContext.Request.Path;

        var isValidationProblem = problem is HttpValidationProblemDetails;
        problem.Extensions.TryAdd(
            "code",
            isValidationProblem
                ? "validation.failed"
                : statusCode switch
                {
                    StatusCodes.Status404NotFound => "http.not_found",
                    StatusCodes.Status405MethodNotAllowed => "http.method_not_allowed",
                    StatusCodes.Status415UnsupportedMediaType => "http.unsupported_media_type",
                    StatusCodes.Status500InternalServerError => "http.internal_error",
                    _ => "http.error"
                });
        problem.Extensions.TryAdd("field", isValidationProblem ? "request" : "http");
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (string.IsNullOrWhiteSpace(problem.Detail))
        {
            problem.Detail = isValidationProblem
                ? "요청 필드의 검증 오류를 확인해 주세요."
                : "요청을 처리할 수 없습니다.";
        }
    }
}
