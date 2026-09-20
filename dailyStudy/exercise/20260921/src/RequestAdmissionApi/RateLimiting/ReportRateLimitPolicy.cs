using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RequestAdmissionApi.RateLimiting;

/// <summary>
/// 비싼 보고서 API를 데모 클라이언트별로 격리하는 named rate-limit policy를 구성합니다.
/// 알려진 키만 유지해 공격자가 무한한 partition을 만들어 메모리를 늘리지 못하게 합니다.
/// </summary>
public static class ReportRateLimitPolicy
{
    public const string PolicyName = "report-concurrency";
    public const string ClientHeaderName = "X-Demo-Client";
    public const string AnonymousPartition = "anonymous";

    /// <summary>
    /// ASP.NET Core 서비스 컬렉션에 보고서 전용 동시성 제한 정책과 429 응답 처리를 등록합니다.
    /// <paramref name="services"/>는 Composition Root의 DI 컨테이너이며 같은 컬렉션을 반환해 연속 등록을 지원합니다.
    /// 첫 파라미터의 this는 이 메서드를 services.AddReportConcurrencyRateLimiting()처럼 호출하게 하는 확장 메서드 문법입니다.
    /// </summary>
    public static IServiceCollection AddReportConcurrencyRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            // 이 람다는 HTTP 요청마다 bounded partition key를 고르고, 각 key별 limiter는 프레임워크가 재사용합니다.
            // static 람다는 바깥 변수를 캡처하지 않음을 나타내고, _는 전달된 partition key를 쓰지 않는다는 discard 표기입니다.
            options.AddPolicy<string>(PolicyName, httpContext =>
                RateLimitPartition.GetConcurrencyLimiter(
                    ResolvePartitionKey(httpContext),
                    static _ => CreateLimiterOptions()));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectedProblemAsync;
        });

        return services;
    }

    /// <summary>
    /// HTTP 헤더에서 클라이언트 값을 읽어 제한된 partition key로 정규화합니다.
    /// <paramref name="httpContext"/>는 현재 요청이며 alpha, beta 또는 anonymous 중 하나를 반환합니다.
    /// </summary>
    public static string ResolvePartitionKey(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var rawClient = httpContext.Request.Headers[ClientHeaderName].FirstOrDefault();
        return NormalizeClient(rawClient);
    }

    /// <summary>
    /// 임의의 클라이언트 문자열을 메모리 사용량이 제한된 세 partition 중 하나로 바꿉니다.
    /// <paramref name="client"/>는 헤더 원문이며 알려진 alpha/beta는 각각 반환하고 나머지는 anonymous를 반환합니다.
    /// </summary>
    public static string NormalizeClient(string? client)
    {
        // ?.는 client가 null이면 뒤 연산을 건너뛰고 null을 유지해 NullReferenceException을 막습니다.
        var normalized = client?.Trim().ToLowerInvariant();
        // `is ... or ...`는 둘 중 하나와 맞는지 검사하고, `조건 ? 참 : 거짓` 삼항 연산자는 반환할 값을 고릅니다.
        return normalized is "alpha" or "beta" ? normalized : AnonymousPartition;
    }

    /// <summary>
    /// 웹 서버와 self-test가 같은 설정을 공유하도록 동시성 limiter 옵션을 만듭니다.
    /// 파라미터는 없으며 동시 실행 1개, 대기 1개, 오래 기다린 순서의 새 옵션을 반환합니다.
    /// </summary>
    public static ConcurrencyLimiterOptions CreateLimiterOptions()
    {
        return new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        };
    }

    /// <summary>
    /// HTTP 서버 없이 partition 동작을 결정적으로 검증할 수 있는 limiter를 만듭니다.
    /// 파라미터는 없으며 문자열 클라이언트를 alpha/beta/anonymous로 격리하는 새 limiter를 반환합니다.
    /// </summary>
    public static PartitionedRateLimiter<string> CreatePartitionedLimiter()
    {
        return PartitionedRateLimiter.Create<string, string>(client =>
            RateLimitPartition.GetConcurrencyLimiter(
                NormalizeClient(client),
                static _ => CreateLimiterOptions()));
    }

    /// <summary>
    /// 제한을 넘은 HTTP 요청에 RFC Problem Details 모양의 JSON을 기록합니다.
    /// <paramref name="rejectionContext"/>는 거절된 요청 정보, <paramref name="cancellationToken"/>은 응답 중단 신호이며
    /// 응답 기록이 끝나는 시점을 나타내는 ValueTask를 반환합니다.
    /// </summary>
    private static async ValueTask WriteRejectedProblemAsync(
        OnRejectedContext rejectionContext,
        CancellationToken cancellationToken)
    {
        var httpContext = rejectionContext.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "보고서 생성 요청이 너무 많습니다.",
            Detail = "같은 제한 파티션에서는 1개를 실행하고 1개만 기다릴 수 있습니다. 잠시 후 다시 시도하세요.",
            Type = "https://www.rfc-editor.org/rfc/rfc6585#section-4",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["partition"] = ResolvePartitionKey(httpContext);

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
