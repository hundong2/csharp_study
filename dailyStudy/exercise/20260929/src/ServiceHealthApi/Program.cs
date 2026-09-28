using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServiceHealthApi.Application;
using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;
using ServiceHealthApi.HealthChecks;
using ServiceHealthApi.Infrastructure;
using ServiceHealthApi.Presentation;
using ServiceHealthApi.SelfTesting;

namespace ServiceHealthApi;

/// <summary>
/// 프로세스 진입점이자 DI, health check, endpoint를 조립하는 Composition Root입니다.
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
            // await는 비동기 검증이 끝날 때까지 thread를 점유하지 않고 기다린 뒤 실제 int 결과를 꺼냅니다.
            return await SelfTestRunner.RunAsync();
        }

        // await using은 실행이나 종료 중 예외가 나도 WebApplication의 비동기 자원을 DisposeAsync로 정리합니다.
        // var는 오른쪽 BuildApplication 반환형이 분명할 때 지역 변수 형식 이름의 중복을 줄입니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// Domain/Application Port와 Adapter, background warm-up, health endpoint를 한곳에서 연결합니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <param name="useEphemeralLoopbackPort">자체 테스트에서 OS가 빈 loopback 포트를 고르게 할지 나타냅니다.</param>
    /// <param name="startupDelayMilliseconds">테스트가 startup 전 상태를 결정적으로 만들 때 덮어쓸 지연입니다.</param>
    /// <param name="environmentName">Development와 Production 동작을 결정할 선택적 host 환경 이름입니다.</param>
    /// <param name="enableDemoEndpoints">Development에서도 명시적으로 장애 주입 route를 켤지 나타냅니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    // int?, string?, bool?의 ?는 호출자가 값을 생략해 configuration/default에 맡길 수 있음을 nullable 분석기에 알립니다.
    public static WebApplication BuildApplication(
        string[] args,
        bool useEphemeralLoopbackPort = false,
        int? startupDelayMilliseconds = null,
        string? environmentName = null,
        bool? enableDemoEndpoints = null)
    {
        var builderOptions = new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environmentName
        };
        var builder = WebApplication.CreateBuilder(builderOptions);

        if (useEphemeralLoopbackPort)
        {
            // 메서드 그룹은 같은 모양의 lambda 대신 설정 메서드 자체를 callback으로 전달합니다.
            builder.WebHost.ConfigureKestrel(ConfigureEphemeralTestEndpoint);
            builder.Logging.ClearProviders();
        }

        // ??는 왼쪽 값이 null일 때만 다음 configuration 또는 마지막 기본값을 선택합니다.
        var configuredDelay = startupDelayMilliseconds ??
            builder.Configuration.GetValue<int?>("StartupWarmup:DelayMilliseconds") ??
            250;
        var demoEndpointsEnabled = enableDemoEndpoints ??
            builder.Configuration.GetValue<bool>("DemoDependencies:Enabled");

        // options =>는 이름 없는 짧은 함수(lambda)로, DI가 옵션 객체를 만들 때 실행할 설정 동작을 전달합니다.
        // 아래 Validate의 is >= ... and <= ...는 두 관계 pattern을 결합해 허용 범위를 읽기 쉽게 표현합니다.
        builder.Services
            .AddOptions<StartupWarmupOptions>()
            .Configure(options => options.DelayMilliseconds = configuredDelay)
            .Validate(
                options => options.DelayMilliseconds is >= 0 and <= 60_000,
                "StartupWarmup:DelayMilliseconds는 0~60000이어야 합니다.")
            .ValidateOnStart();

        builder.Services.AddSingleton<StartupSignal>();
        builder.Services.AddHostedService<StartupWarmupService>();

        // 같은 Port의 singleton Adapter를 여러 번 등록하면 IEnumerable<IDependencyProbe>로 모두 주입받을 수 있습니다.
        // `_ =>`의 _는 factory가 받는 IServiceProvider를 여기서는 쓰지 않는다는 discard 파라미터입니다.
        builder.Services.AddSingleton<IDependencyProbe>(
            _ => new ConfigurableDependencyProbe(
                "inventory",
                DependencyImportance.Required,
                TimeSpan.FromMilliseconds(150)));
        builder.Services.AddSingleton<IDependencyProbe>(
            _ => new ConfigurableDependencyProbe(
                "recommendations",
                DependencyImportance.Optional,
                TimeSpan.FromMilliseconds(100)));
        builder.Services.AddSingleton<IReadinessPolicy, RequiredOptionalReadinessPolicy>();
        builder.Services.AddSingleton<ReadinessApplicationService>();

        // [a, b]는 C# collection expression으로 두 tag를 framework가 요구하는 collection 형식에 맞춥니다.
        // failureStatus: 같은 이름 있는 인수는 뒤의 값이 무엇을 뜻하는지 호출 위치에서 보여 줍니다.
        builder.Services
            .AddHealthChecks()
            .AddCheck<StartupHealthCheck>(
                "startup",
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthTags.Startup, HealthTags.Ready],
                timeout: TimeSpan.FromMilliseconds(100))
            .AddCheck<ApplicationReadinessHealthCheck>(
                "application-readiness",
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthTags.Ready],
                timeout: TimeSpan.FromMilliseconds(500));

        var app = builder.Build();
        app.MapServiceHealthEndpoints();

        if (app.Environment.IsDevelopment() && demoEndpointsEnabled)
        {
            // 환경 이름만으로는 실수할 수 있어 Development와 명시적 opt-in이 모두 맞을 때만 route를 매핑합니다.
            app.MapDemoDependencyEndpoints();
        }

        return app;
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
