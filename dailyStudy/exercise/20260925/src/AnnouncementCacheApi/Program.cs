using System.Net;
using System.Globalization;
using AnnouncementCacheApi.Application;
using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;
using AnnouncementCacheApi.Infrastructure;
using AnnouncementCacheApi.Presentation;
using AnnouncementCacheApi.SelfTesting;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AnnouncementCacheApi;

// Program은 구현 객체를 만들고 연결하는 Composition Root입니다.
// 조립 책임을 한곳에 모으면 Domain과 Application은 ASP.NET Core나 메모리 저장소를 몰라도 됩니다.
public static class Program
{
    /// <summary>
    /// 명령행 옵션에 따라 자체 테스트를 실행하거나 HTTP API 호스트를 시작합니다.
    /// </summary>
    /// <param name="args"><c>--self-test</c> 또는 일반 ASP.NET Core 실행 옵션을 담은 명령행 인수입니다.</param>
    /// <returns>자체 테스트의 종료 코드 또는 웹 호스트가 종료될 때 완료되는 성공 코드 0을 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        // async/await는 스레드를 붙잡아 두지 않고 테스트나 서버 종료를 기다리기 위해 사용합니다.
        // LINQ Contains 확장 메서드는 배열을 순회하며 대소문자를 무시하고 self-test 옵션을 찾습니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTestRunner.RunAsync();
        }

        // var는 오른쪽 BuildApplication 결과가 WebApplication임이 분명할 때 형식 이름 반복을 줄입니다.
        var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 서비스 등록, 미들웨어, 엔드포인트를 한 번에 조립한 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">ASP.NET Core 설정에 전달할 명령행 인수입니다.</param>
    /// <param name="useEphemeralLoopbackPort">true이면 자체 테스트가 충돌 없는 임시 로컬 포트를 선택하게 합니다.</param>
    /// <param name="configureServices">null이 아니면 테스트가 기본 등록 뒤 일부 의존성을 교체하는 callback입니다.</param>
    /// <returns>아직 시작하지 않아 호출자가 실행 시점을 제어할 수 있는 WebApplication을 반환합니다.</returns>
    // Action<T>?의 ?는 호출할 코드 묶음이 없을 때 null도 허용하는 nullable delegate임을 뜻합니다.
    public static WebApplication BuildApplication(
        string[] args,
        bool useEphemeralLoopbackPort = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (useEphemeralLoopbackPort)
        {
            // 메서드 그룹을 전달하면 같은 모양의 lambda를 새로 만들지 않고 테스트용 포트 설정을 분리할 수 있습니다.
            builder.WebHost.ConfigureKestrel(ConfigureEphemeralTestEndpoint);
            builder.Logging.ClearProviders();
        }

        // DI는 상위 계층이 new로 구체 구현을 고정하지 않게 해 SOLID의 의존성 역전과 테스트 교체를 돕습니다.
        builder.Services.AddSingleton<IAnnouncementRepository, InMemoryAnnouncementRepository>();
        builder.Services.AddSingleton<IAnnouncementLocalizationStrategy, KoreanAnnouncementLocalizationStrategy>();
        builder.Services.AddSingleton<IAnnouncementLocalizationStrategy, EnglishAnnouncementLocalizationStrategy>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AnnouncementApplicationService>();
        builder.Services.AddSingleton<AnnouncementFeedCacheGeneration>();
        builder.Services.AddOutputCache(ConfigureOutputCache);

        // 테스트만 Repository를 교체할 수 있게 하되, 운영 Composition Root의 기본 선택은 그대로 둡니다.
        // ?.는 왼쪽 delegate가 null이 아닐 때만 Invoke를 호출하는 null-conditional 연산자입니다.
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        // Output Cache 미들웨어가 endpoint의 CacheOutput 메타데이터를 읽어 응답 저장과 재사용을 담당합니다.
        app.UseOutputCache();
        app.MapAnnouncementEndpoints();
        return app;
    }

    /// <summary>
    /// 자체 테스트 서버가 운영 포트와 충돌하지 않도록 루프백 주소의 사용 가능한 포트를 요청합니다.
    /// </summary>
    /// <param name="options">Kestrel이 수신 주소를 구성할 때 사용하는 옵션입니다.</param>
    /// <returns>구성만 변경하므로 반환값은 없습니다.</returns>
    private static void ConfigureEphemeralTestEndpoint(KestrelServerOptions options)
    {
        options.Listen(IPAddress.Loopback, 0);
    }

    /// <summary>
    /// 애플리케이션에서 사용할 이름 있는 Output Cache 정책을 등록합니다.
    /// </summary>
    /// <param name="options">이름과 정책을 보관하는 ASP.NET Core Output Cache 옵션입니다.</param>
    /// <returns>정책을 등록할 뿐 값을 만들지 않으므로 반환값은 없습니다.</returns>
    private static void ConfigureOutputCache(OutputCacheOptions options)
    {
        options.AddPolicy(CacheNames.AnnouncementFeedPolicy, ConfigureAnnouncementFeedPolicy);
    }

    /// <summary>
    /// 공지 목록 캐시의 5분 TTL, 정규화 변형 키, 일괄 무효화 태그, 자원 잠금을 설정합니다.
    /// </summary>
    /// <param name="policy">여러 캐시 규칙을 연쇄적으로 추가할 정책 빌더입니다.</param>
    /// <returns>전달받은 빌더를 구성할 뿐 별도 값을 반환하지 않습니다.</returns>
    private static void ConfigureAnnouncementFeedPolicy(OutputCachePolicyBuilder policy)
    {
        // 자원 잠금은 같은 cold key 요청 여러 개 중 하나만 원본을 읽게 해 stampede를 줄입니다.
        policy
            .Expire(TimeSpan.FromMinutes(5))
            // 기본값인 "모든 raw query" 변형을 비운 뒤, 아래 custom 값만 cache key에 넣습니다.
            .SetVaryByQuery(Array.Empty<string>())
            .VaryByValue(CreateAnnouncementFeedVaryValue)
            .Tag(CacheTags.AnnouncementFeed)
            .SetLocking(true);
    }

    /// <summary>
    /// 원문 표현이 달라도 업무 의미가 같은 category·언어를 같은 값으로 정규화하고 쓰기 세대를 더합니다.
    /// </summary>
    /// <param name="context">query, header, DI singleton을 읽을 현재 HTTP 요청 문맥입니다.</param>
    /// <returns>Output Cache key에 추가할 고정 이름과 충돌 없는 복합 값을 반환합니다.</returns>
    private static KeyValuePair<string, string> CreateAnnouncementFeedVaryValue(HttpContext context)
    {
        var rawCategory = context.Request.Query["category"].ToString();
        string categoryKey;
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            categoryKey = "none";
        }
        else
        {
            var categoryResult = Announcement.NormalizeCategory(rawCategory);
            // 조건 연산자 ?:는 검증 성공과 실패의 key 모양을 한 식에서 선택합니다.
            // valid/invalid 접두사는 잘못된 입력이 정상 응답의 key와 절대 충돌하지 않게 합니다.
            categoryKey = categoryResult.IsSuccess
                ? $"valid:{categoryResult.Value}"
                : "invalid";
        }

        var language = LanguagePreferenceParser.Parse(context.Request.Headers.AcceptLanguage);
        var generation = context.RequestServices
            .GetRequiredService<AnnouncementFeedCacheGeneration>()
            .Current
            .ToString(CultureInfo.InvariantCulture);

        // string.Join은 구분자와 세 필드를 순서대로 합쳐 key 구조를 눈으로 확인하기 쉽게 합니다.
        var compositeValue = string.Join('|', generation, categoryKey, language);
        return KeyValuePair.Create("announcement-feed-shape", compositeValue);
    }
}
