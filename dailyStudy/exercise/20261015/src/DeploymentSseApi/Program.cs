using System.Diagnostics;
using DeploymentSseApi.Application;
using DeploymentSseApi.Application.Ports;
using DeploymentSseApi.Infrastructure;
using DeploymentSseApi.Presentation;
using DeploymentSseApi.SelfTesting;

namespace DeploymentSseApi;

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
    // async는 메서드에서 await를 쓸 수 있게 하고, await는 비동기 작업이 끝날 때까지 thread를 붙잡지 않고 결과를 이어받습니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 서버 종료 때 비동기 자원 정리를 보장하고 var는 오른쪽 값에서 정적 형식을 추론합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 서비스 등록, middleware, endpoint 연결을 마친 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // options =>는 이름 없는 짧은 함수(lambda)로 Problem Details 설정 객체를 제자리에서 구성합니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);

        // positional record의 필수 생성자 인수가 JSON에서 빠졌을 때 int의 기본값 0으로 조용히 채우지 않고 400으로 거절합니다.
        // 특히 percent 누락과 사용자가 명시한 percent: 0은 의미가 다르므로 HTTP 경계에서 구분해야 합니다.
        builder.Services.ConfigureHttpJsonOptions(
            options => options.SerializerOptions.RespectRequiredConstructorParameters = true);

        // DI(의존성 주입)는 Service가 구체 class를 직접 만들지 않고 Composition Root가 Port와 Adapter 조합을 정하게 합니다.
        // 이는 SOLID의 DIP(의존성 역전 원칙)로, 상위 use case가 하위 구현 대신 interface에 의존해 교체와 테스트를 쉽게 합니다.
        builder.Services.AddSingleton<IDeploymentProgressRepository>(
            _ => new InMemoryDeploymentProgressRepository(subscriberCapacity: 8));
        builder.Services.AddSingleton<
            IDeploymentProgressTransitionStrategy,
            DeploymentProgressTransitionStrategy>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DeploymentProgressApplicationService>();

        var app = builder.Build();
        // StatusCodePages는 framework가 만든 본문 없는 404·415도 공통 Problem Details writer로 보냅니다.
        app.UseStatusCodePages();
        app.MapGet("/", GetServiceStatus);
        // 학습 API는 인증·인가를 생략했습니다. 운영 공개 전에는 게시 endpoint에 배포 권한을, 구독 endpoint에 조회 권한을 추가해야 합니다.
        app.MapDeploymentEndpoints();
        return app;
    }

    /// <summary>
    /// 서비스가 실행 중임을 보여 주는 최소 상태 응답을 만듭니다.
    /// </summary>
    /// <returns>service 값이 DeploymentSseApi인 200 JSON 결과를 반환합니다.</returns>
    private static IResult GetServiceStatus()
    {
        return TypedResults.Ok(new { service = "DeploymentSseApi" });
    }

    /// <summary>
    /// framework가 만든 Problem Details에 안전한 code, field, traceId를 일관되게 추가합니다.
    /// </summary>
    /// <param name="context">현재 요청과 작성 중인 Problem Details를 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        // ??는 왼쪽 값이 null일 때만 오른쪽 값을 선택합니다. 아래 ?.도 Activity가 null이면 Id 접근 대신 null을 만들어 다음 대안으로 넘깁니다.
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        // ??=는 왼쪽 값이 null일 때만 오른쪽 요청 경로를 대입해 endpoint가 정한 instance를 덮어쓰지 않습니다.
        problem.Instance ??= context.HttpContext.Request.Path;

        if (!problem.Extensions.ContainsKey("code"))
        {
            // switch expression은 상태 코드 하나를 안전한 설명 하나로 대응시키고 그 결과 값을 바로 반환합니다.
            problem.Detail = statusCode switch
            {
                StatusCodes.Status400BadRequest => "요청 JSON 형식과 값을 확인하세요.",
                StatusCodes.Status404NotFound => "요청한 URL을 찾을 수 없습니다.",
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
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }
}
