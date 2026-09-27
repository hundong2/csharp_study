using System.Net;
using System.Text;
using ReportTimeoutApi.Application;
using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Infrastructure;
using ReportTimeoutApi.Presentation;
using ReportTimeoutApi.SelfTesting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// 파일 범위 namespace는 파일 끝까지 같은 namespace를 적용하면서 중괄호 한 겹을 줄이는 문법입니다.
namespace ReportTimeoutApi;

/// <summary>
/// 구현 객체와 ASP.NET Core pipeline을 한곳에서 연결하는 Composition Root입니다.
/// </summary>
public static class Program
{
    // raw string literal(""")은 JSON 안의 큰따옴표를 반복해서 이스케이프하지 않고 그대로 적게 해 줍니다.
    private static readonly byte[] TimeoutProblemBody = Encoding.UTF8.GetBytes(
        """
        {"type":"https://httpstatuses.com/504","title":"보고서 생성 시간이 초과되었습니다.","status":504,"detail":"서버 제한 시간 안에 작업을 마치지 못했습니다. 더 작은 작업으로 나누거나 다시 시도하세요.","code":"request.timeout"}
        """);

    /// <summary>
    /// 명령행 옵션에 따라 자체 테스트를 실행하거나 HTTP 서버를 시작합니다.
    /// </summary>
    /// <param name="args"><c>--self-test</c> 또는 ASP.NET Core 실행 옵션을 담은 명령행 인수입니다.</param>
    /// <returns>자체 테스트 결과 코드 또는 서버가 정상 종료될 때의 0을 담아 완료되는 Task를 반환합니다.</returns>
    // async/await는 자체 테스트나 서버 종료를 기다리는 동안 호출 스레드를 막지 않습니다.
    // Task<int>는 비동기 작업이 끝나면 프로세스 종료 코드인 int를 제공한다는 generic 반환 형식입니다.
    public static async Task<int> Main(string[] args)
    {
        // Contains의 comparer 인수는 옵션 대소문자가 달라도 같은 뜻으로 처리합니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTestRunner.RunAsync();
        }

        // var는 오른쪽 BuildApplication 결과가 WebApplication임이 분명할 때 타입 이름 반복을 줄입니다.
        var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// DI 서비스, request timeout middleware, endpoint를 조립한 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">ASP.NET Core 설정에 전달할 명령행 인수입니다.</param>
    /// <param name="useEphemeralLoopbackPort">true이면 테스트가 충돌 없는 임시 loopback 포트를 요청합니다.</param>
    /// <param name="configureServices">테스트가 기본 등록 뒤 필요한 의존성만 교체할 선택 callback입니다.</param>
    /// <returns>아직 시작되지 않아 호출자가 수명 주기를 제어할 WebApplication을 반환합니다.</returns>
    // Action<T>?의 ?는 테스트용 callback이 없을 때 null도 허용한다는 nullable 표기입니다.
    // = false와 = null은 호출자가 인수를 생략했을 때 쓸 선택적 파라미터의 기본값입니다.
    public static WebApplication BuildApplication(
        string[] args,
        bool useEphemeralLoopbackPort = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (useEphemeralLoopbackPort)
        {
            // 메서드 그룹은 같은 모양의 lambda를 쓰지 않고 설정 메서드 자체를 callback으로 전달합니다.
            builder.WebHost.ConfigureKestrel(ConfigureEphemeralTestEndpoint);
            builder.Logging.ClearProviders();
        }

        // singleton Repository는 이 데모의 고정 데이터를 공유합니다. 실제 DB adapter는 보통 scoped 수명을 검토합니다.
        builder.Services.AddSingleton<IReportRepository, InMemoryReportRepository>();
        builder.Services.AddSingleton<IReportFormatterStrategy, CsvReportFormatterStrategy>();
        builder.Services.AddSingleton<IReportFormatterStrategy, JsonReportFormatterStrategy>();
        builder.Services.AddSingleton<ReportApplicationService>();
        builder.Services.AddRequestTimeouts(ConfigureRequestTimeouts);

        // ?.는 callback이 null이 아닐 때만 호출하는 null-conditional 연산자입니다.
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        // singleton을 서버 시작 전에 한 번 resolve해 Domain 허용 형식과 Strategy 등록 불일치를 fail-fast 검증합니다.
        // `_ =`는 반환 객체 자체는 쓰지 않되, resolve 과정의 생성자 검증은 실행하려는 discard 대입입니다.
        _ = app.Services.GetRequiredService<ReportApplicationService>();

        // middleware 순서는 중요합니다. endpoint 실행 전에 RequestAborted를 제한 시간 token과 연결합니다.
        app.UseRequestTimeouts();
        app.MapReportEndpoints();
        return app;
    }

    /// <summary>
    /// 자체 테스트 서버가 운영 포트와 충돌하지 않도록 loopback의 사용 가능한 임시 포트를 요청합니다.
    /// </summary>
    /// <param name="options">Kestrel 수신 주소를 구성할 옵션입니다.</param>
    /// <returns>옵션만 변경하므로 반환값은 없습니다.</returns>
    private static void ConfigureEphemeralTestEndpoint(KestrelServerOptions options)
    {
        options.Listen(IPAddress.Loopback, 0);
    }

    /// <summary>
    /// 일반 endpoint의 기본 제한과 보고서 endpoint의 짧은 이름 있는 정책을 등록합니다.
    /// </summary>
    /// <param name="options">전역 및 이름 있는 RequestTimeoutPolicy를 보관할 옵션입니다.</param>
    /// <returns>정책만 등록하므로 반환값은 없습니다.</returns>
    private static void ConfigureRequestTimeouts(RequestTimeoutOptions options)
    {
        // 객체 초기화자 { 속성 = 값 }은 객체를 만든 직후 설정할 속성을 한곳에 모아 보여 줍니다.
        options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(2),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = WriteTimeoutProblemAsync
        };

        options.AddPolicy(TimeoutNames.ReportGeneration, new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromMilliseconds(150),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = WriteTimeoutProblemAsync
        });
    }

    /// <summary>
    /// 제한 시간이 끝났을 때 클라이언트가 해석할 수 있는 고정 504 Problem Details를 씁니다.
    /// </summary>
    /// <param name="context">timeout middleware가 응답을 작성하도록 넘겨준 현재 HTTP 문맥입니다.</param>
    /// <returns>작은 JSON 본문 쓰기가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static Task WriteTimeoutProblemAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentLength = TimeoutProblemBody.Length;

        // RequestAborted는 이미 timeout 때문에 취소되었습니다. 작은 오류 응답 자체는 None으로 끝까지 써야 클라이언트가 원인을 받습니다.
        // Stream.WriteAsync는 ValueTask를 반환하므로 AsTask로 middleware delegate가 요구하는 Task 모양에 맞춥니다.
        return context.Response.Body
            .WriteAsync(TimeoutProblemBody, CancellationToken.None)
            .AsTask();
    }
}
