// YARP (Yet Another Reverse Proxy) - Getting Started
// 참고: https://learn.microsoft.com/ko-kr/aspnet/core/fundamentals/servers/yarp/getting-started

var builder = WebApplication.CreateBuilder(args);

// ① YARP 리버스 프록시 서비스를 DI 컨테이너에 등록합니다.
//    appsettings.json 의 "ReverseProxy" 섹션에서 라우트·클러스터 설정을 읽어옵니다.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// ② 현재 로드된 라우트 정보를 콘솔에 출력합니다 (학습/디버깅 용도).
var proxyConfig = app.Services.GetRequiredService<Yarp.ReverseProxy.Configuration.IProxyConfigProvider>();
var config = proxyConfig.GetConfig();
app.Logger.LogInformation("=== YARP Routes ===");
foreach (var route in config.Routes)
    app.Logger.LogInformation("Route '{RouteId}' → Cluster '{ClusterId}', Path: {Path}",
        route.RouteId, route.ClusterId, route.Match.Path);

app.Logger.LogInformation("=== YARP Clusters ===");
foreach (var cluster in config.Clusters)
    foreach (var dest in cluster.Destinations ?? new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>())
        app.Logger.LogInformation("Cluster '{ClusterId}' → Destination '{DestId}': {Address}",
            cluster.ClusterId, dest.Key, dest.Value.Address);

// ③ YARP 미들웨어를 파이프라인에 추가합니다.
//    모든 매칭 요청은 appsettings.json 에서 지정한 업스트림 서버로 프록시됩니다.
app.MapReverseProxy(proxyPipeline =>
{
    // Deep Dive: 요청이 프록시되기 직전에 실행되는 커스텀 미들웨어 예시
    proxyPipeline.Use(async (context, next) =>
    {
        app.Logger.LogInformation("[YARP] Proxying {Method} {Path} → cluster",
            context.Request.Method, context.Request.Path);
        await next();
    });
});

app.Run();
