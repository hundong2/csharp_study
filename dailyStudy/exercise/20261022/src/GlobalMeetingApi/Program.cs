using System.Diagnostics;
using GlobalMeetingApi.Application;
using GlobalMeetingApi.Application.Ports;
using GlobalMeetingApi.Infrastructure;
using GlobalMeetingApi.Presentation;
using GlobalMeetingApi.SelfTesting;
using Microsoft.AspNetCore.Diagnostics;

namespace GlobalMeetingApi;

/// <summary>
/// 시간대 Adapter, Strategy, Application Service, Repository, HTTP endpoint를 조립하는 Composition Root입니다.
/// 구체 구현 선택을 이 진입점 한곳에 모아 Domain/Application이 객체 생성과 실행 환경 교체 책임을 지지 않게 합니다.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// static 진입점만 가진 Composition Root가 실수로 객체화되지 않게 막습니다.
    /// </summary>
    private Program()
    {
    }

    /// <summary>
    /// `--demo`, `--self-test`, 또는 Kestrel 서버 실행 중 하나를 선택합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 실행 mode와 Kestrel 설정 인수입니다.</param>
    /// <returns>서버·self-test 성공은 0, demo의 예상 실패는 1을 담은 Task를 반환합니다.</returns>
    // async/await는 thread를 붙잡지 않고 비동기 작업의 완료를 기다리는 C# 문법입니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        if (args.Contains("--demo", StringComparer.Ordinal))
        {
            return await RunDemoAsync();
        }

        // await using은 서버가 끝날 때 비동기 자원 정리까지 기다립니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 서비스 등록, 오류 middleware, endpoint를 조립한 아직 시작하지 않은 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <param name="timeProvider">테스트가 시스템 시계를 교체할 때 넘기는 선택적 시간 공급자입니다.</param>
    /// <returns>실행 또는 통합 테스트에 사용할 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(
        string[] args,
        TimeProvider? timeProvider = null)
    {
        // var는 오른쪽 factory 반환 형식이 분명할 때 compiler가 정적 형식을 추론하게 합니다.
        var builder = WebApplication.CreateBuilder(args);
        // lambda는 설정 객체를 받아 나중에 실행할 작은 함수를 값처럼 전달합니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        // ??는 테스트용 시간이 null이면 운영 시스템 시계를 선택합니다. Port→구현 등록은 DIP와 대역 교체 지점을 드러냅니다.
        builder.Services.AddSingleton(timeProvider ?? TimeProvider.System);
        builder.Services.AddSingleton<IAmbiguousTimePolicy, RequireExplicitOffsetPolicy>();
        builder.Services.AddSingleton<ILocalTimeResolver, TimeZoneInfoLocalTimeResolver>();
        // Singleton Repository의 공유 Dictionary는 Adapter 내부 lock으로 보호됩니다.
        builder.Services.AddSingleton<IMeetingRepository, InMemoryMeetingRepository>();
        builder.Services.AddSingleton<ScheduleMeetingService>();

        var app = builder.Build();
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                StatusCodeSelector = SelectExceptionStatusCode
            });
        // endpoint가 본문 없이 만든 404, 405, 415도 AddProblemDetails가 JSON 문제 응답으로 바꿉니다.
        app.UseStatusCodePages();
        app.MapGet(
            "/",
            () => TypedResults.Ok(
                new
                {
                    service = "GlobalMeetingApi",
                    stableTarget = "C# 14 / net10.0",
                    timeZoneExamples = new[] { "Asia/Seoul", "America/New_York", "Etc/UTC" }
                }));
        app.MapMeetingEndpoints();
        return app;
    }

    /// <summary>
    /// 내일의 서울 지역 시각을 만들어 실제 운영 객체를 거치는 한 건의 성공 흐름을 보여 줍니다.
    /// </summary>
    /// <returns>예약 성공은 0, 예상된 demo 실패는 1을 반환합니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        var timeProvider = TimeProvider.System;
        var resolver = new TimeZoneInfoLocalTimeResolver(new RequireExplicitOffsetPolicy());
        var service = new ScheduleMeetingService(
            resolver,
            new InMemoryMeetingRepository(),
            timeProvider);
        var seoulZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        var localTomorrow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow().AddDays(1),
            seoulZone);
        var command = new ScheduleMeetingCommand(
            "MEET-DEMO-001",
            "USER-DEMO",
            "서울 팀 데모 회의",
            DateOnly.FromDateTime(localTomorrow.DateTime),
            TimeOnly.FromDateTime(localTomorrow.DateTime),
            seoulZone.Id,
            localTomorrow.Offset,
            60);

        var result = await service.ScheduleAsync(command, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Console.WriteLine($"[DEMO:FAILURE] {result.Error.Code}: {result.Error.Message}");
            return 1;
        }

        var meeting = result.Value;
        Console.WriteLine(
            $"[DEMO:SUCCESS] {meeting.MeetingId} / " +
            $"{meeting.LocalTime.Date} {meeting.LocalTime.StartTime} {meeting.ResolvedTime.TimeZoneId} / " +
            $"UTC {meeting.Slot.StartsAtUtc:O}");
        return 0;
    }

    /// <summary>
    /// JSON binding의 올바른 4xx는 보존하고 예상하지 못한 예외만 500으로 분류합니다.
    /// </summary>
    /// <param name="exception">요청 pipeline 밖으로 전파된 예외입니다.</param>
    /// <returns>공개 가능한 4xx 상태 또는 안전한 500 상태를 반환합니다.</returns>
    private static int SelectExceptionStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // is는 예외 형식과 4xx 범위를 함께 확인하고, and는 두 관계 패턴을 묶습니다.
        // ?: 삼항 연산자는 조건이 참이면 공개 가능한 4xx를, 아니면 안전한 500을 선택합니다.
        return exception is BadHttpRequestException badRequest
            && badRequest.StatusCode is >= StatusCodes.Status400BadRequest
                and < StatusCodes.Status500InternalServerError
                    ? badRequest.StatusCode
                    : StatusCodes.Status500InternalServerError;
    }

    /// <summary>
    /// framework 오류에 안정적인 code와 traceId를 넣고 500 내부 정보는 공개하지 않습니다.
    /// </summary>
    /// <param name="context">현재 HTTP 요청과 작성 중인 Problem Details를 가진 문맥입니다.</param>
    /// <returns>전달받은 문제 객체를 제자리에서 수정하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = context.ProblemDetails;
        // ??는 왼쪽 nullable 값이 null일 때만 오른쪽 값을 사용합니다.
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        // ??=는 기존 instance가 null일 때만 현재 요청 경로를 대입합니다.
        problem.Instance ??= context.HttpContext.Request.Path;
        // switch 식은 상태 코드 하나를 안정적인 문자열 하나로 바꾸며, 각 => arm이 값별 결과를 나타냅니다.
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
        // ?.는 Activity가 없으면 Id 접근 대신 null을 만들고, ??가 그때 HTTP trace identifier를 사용합니다.
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            problem.Title = "서버에서 요청을 처리하지 못했습니다.";
            problem.Detail = "잠시 후 다시 시도하고, 문제가 계속되면 traceId와 함께 문의하세요.";
        }
    }
}
