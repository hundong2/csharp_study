using System.Diagnostics;
using EquipmentRentalApi.Application;
using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Infrastructure;
using EquipmentRentalApi.Presentation;
using Microsoft.AspNetCore.Diagnostics;

namespace EquipmentRentalApi;

/// <summary>
/// HTTP, Application Service, Strategy, Repository Adapter를 조립하고 실행하는 Composition Root입니다.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// static 진입점만 쓰는 Composition Root가 실수로 객체화되지 않게 막습니다.
    /// </summary>
    private Program()
    {
    }

    /// <summary>
    /// --demo이면 한 건의 로컬 흐름을 실행하고, 아니면 Kestrel HTTP 서버를 시작합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 demo, URL, 환경 명령줄 인수입니다.</param>
    /// <returns>정상 종료 코드 0을 담아 완료되는 Task를 반환합니다.</returns>
    // async는 await로 비동기 종료를 기다리는 진입점이며, 최종 종료 코드는 Task<int>에 담깁니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--demo", StringComparer.Ordinal))
        {
            // await는 실행 thread를 붙잡지 않고 demo 작업이 끝날 때까지 비동기로 기다립니다.
            await RunDemoAsync();
            return 0;
        }

        // await using은 서버 종료 때 WebApplication이 가진 비동기 자원까지 빠뜨리지 않고 정리합니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 서비스 등록, 오류 middleware, HTTP endpoint를 조립한 아직 시작하지 않은 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <returns>운영 실행과 WebApplicationFactory 테스트가 함께 사용할 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(string[] args)
    {
        // var는 오른쪽 factory 반환 형식이 분명할 때 compiler가 정확한 형식을 추론하도록 합니다.
        var builder = WebApplication.CreateBuilder(args);

        // lambda는 설정 객체를 받아 나중에 실행할 작은 함수를 값처럼 전달하는 문법입니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        builder.Services.AddSingleton(TimeProvider.System);
        // Singleton Adapter의 공유 Dictionary는 내부 lock으로 보호되어야 합니다.
        builder.Services.AddSingleton<IEquipmentRentalRepository, InMemoryEquipmentRentalRepository>();
        // 같은 Port의 여러 구현을 등록하면 Application Service가 Strategy 모음으로 주입받습니다.
        builder.Services.AddSingleton<IRentalPolicy, LaptopRentalPolicy>();
        builder.Services.AddSingleton<IRentalPolicy, CameraRentalPolicy>();
        builder.Services.AddSingleton<CreateRentalService>();

        var app = builder.Build();
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                // 올바른 framework 4xx는 보존하고 예상 밖 예외만 안전한 500 Problem Details로 통일합니다.
                StatusCodeSelector = SelectExceptionStatusCode
            });
        // endpoint가 본문을 만들지 않은 404, 405, 415도 AddProblemDetails를 통해 JSON 문제 응답으로 만듭니다.
        app.UseStatusCodePages();
        app.MapGet(
            "/",
            () => TypedResults.Ok(
                new
                {
                    service = "EquipmentRentalApi",
                    categories = new[] { "laptop", "camera" },
                    testRunner = "Microsoft.Testing.Platform"
                }));
        app.MapRentalEndpoints();
        return app;
    }

    /// <summary>
    /// 실제 네트워크 없이 운영 객체를 수동 조립해 한 건의 성공 흐름을 콘솔에 보여 줍니다.
    /// </summary>
    /// <returns>예약 생성과 출력이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RunDemoAsync()
    {
        var repository = new InMemoryEquipmentRentalRepository();
        // [a, b] collection expression은 원소 두 개로 배열을 만드는 C# 12+ 간결 문법입니다.
        IRentalPolicy[] policies = [new LaptopRentalPolicy(), new CameraRentalPolicy()];
        var service = new CreateRentalService(repository, policies, TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow();
        var command = new CreateRentalCommand(
            "RES-DEMO-001",
            "EQ-LAPTOP-007",
            "USER-100",
            "laptop",
            "신입 개발자 온보딩 실습",
            now.AddHours(1),
            now.AddDays(2));

        var result = await service.CreateAsync(command, CancellationToken.None);
        if (result.IsSuccess)
        {
            var reservation = result.Value;
            Console.WriteLine(
                $"[DEMO:SUCCESS] {reservation.ReservationId} / " +
                $"{reservation.EquipmentId} / {reservation.Window.StartsAtUtc:O}");
            return;
        }

        Console.WriteLine($"[DEMO:FAILURE] {result.Error.Code}: {result.Error.Message}");
    }

    /// <summary>
    /// JSON binding 같은 framework의 유효한 4xx 예외는 보존하고 예상 밖 예외만 500으로 분류합니다.
    /// </summary>
    /// <param name="exception">요청 pipeline 밖으로 전파되어 exception handler가 받은 예외입니다.</param>
    /// <returns>BadHttpRequestException의 안전한 400~499 상태 또는 그 밖의 경우 500을 반환합니다.</returns>
    private static int SelectExceptionStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // is 패턴은 예외 형식을 확인하면서 badRequest 변수를 만들고, 관계 패턴은 공개 가능한 4xx 범위만 허용합니다.
        return exception is BadHttpRequestException badRequest
            && badRequest.StatusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
                ? badRequest.StatusCode
                : StatusCodes.Status500InternalServerError;
    }

    /// <summary>
    /// framework가 만드는 오류 응답에 code, field, traceId를 넣고 500 내부 정보를 안전하게 바꿉니다.
    /// </summary>
    /// <param name="context">현재 HTTP 요청과 작성 중인 Problem Details를 가진 framework 문맥입니다.</param>
    /// <returns>전달된 문제 객체를 제자리에서 수정하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = context.ProblemDetails;
        // ??는 왼쪽 nullable 값이 null일 때만 오른쪽 응답 상태를 고릅니다.
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        problem.Instance ??= context.HttpContext.Request.Path;
        // switch 식은 상태 코드를 공개용 오류 코드 하나로 빠짐없이 변환해 code 변수에 넣습니다.
        var code = statusCode switch
        {
            StatusCodes.Status400BadRequest => "http.bad_request",
            StatusCodes.Status404NotFound => "http.not_found",
            StatusCodes.Status405MethodNotAllowed => "http.method_not_allowed",
            StatusCodes.Status415UnsupportedMediaType => "http.unsupported_media_type",
            StatusCodes.Status500InternalServerError => "http.internal_error",
            _ => "http.error"
        };
        problem.Extensions.TryAdd("code", code);
        problem.Extensions.TryAdd("field", "http");
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            problem.Title = "서버에서 요청을 처리하지 못했습니다.";
            problem.Detail = "잠시 후 다시 시도하고, 문제가 계속되면 traceId와 함께 관리자에게 문의하세요.";
        }
    }
}
