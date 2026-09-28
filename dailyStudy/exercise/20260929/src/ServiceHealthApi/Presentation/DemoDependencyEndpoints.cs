using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;
using ServiceHealthApi.Infrastructure;

namespace ServiceHealthApi.Presentation;

/// <summary>
/// Development 환경에서만 dependency 장애와 지연을 재현하는 교육용 endpoint를 등록합니다.
/// </summary>
public static class DemoDependencyEndpoints
{
    /// <summary>
    /// probe 상태 변경과 호출 횟수 조회 endpoint를 route에 추가합니다.
    /// </summary>
    /// <param name="endpoints">Minimal API route를 추가할 endpoint builder입니다.</param>
    /// <returns>다른 endpoint를 계속 연결할 수 있도록 같은 builder를 반환합니다.</returns>
    // 파라미터 앞 this는 기존 endpoint builder에 이 mapping 동작을 붙여 보이게 하는 extension method 문법입니다.
    public static IEndpointRouteBuilder MapDemoDependencyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var demo = endpoints.MapGroup("/demo");
        demo.AddEndpointFilter(RequireLoopbackAsync);
        demo.MapPut(
            "/dependencies/{name}/{condition}",
            ConfigureDependency);
        demo.MapGet("/probes", GetProbeCounters);
        return endpoints;
    }

    /// <summary>
    /// 명시적으로 켠 Development 서버에서도 loopback 요청만 장애 주입 handler로 통과시킵니다.
    /// </summary>
    /// <param name="context">현재 HTTP 연결과 endpoint 인수가 든 filter 문맥입니다.</param>
    /// <param name="next">검사를 통과했을 때 다음 filter 또는 실제 handler를 호출할 delegate입니다.</param>
    /// <returns>원격 요청에는 존재를 숨기는 404, loopback 요청에는 다음 처리 결과를 반환합니다.</returns>
    // ValueTask는 filter 결과가 이미 있으면 별도 Task 할당 없이, 비동기면 기다릴 수 있게 반환하는 값 형식입니다.
    private static ValueTask<object?> RequireLoopbackAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var remoteAddress = context.HttpContext.Connection.RemoteIpAddress;
        if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress))
        {
            return ValueTask.FromResult<object?>(Results.NotFound());
        }

        return next(context);
    }

    /// <summary>
    /// 이름으로 probe를 찾아 다음 관찰의 상태와 지연 시간을 설정합니다.
    /// </summary>
    /// <param name="name">inventory 또는 recommendations probe 이름입니다.</param>
    /// <param name="condition">available 또는 unavailable 문자열입니다.</param>
    /// <param name="delayMs">timeout 실습에 사용할 선택적 지연 시간 문자열입니다.</param>
    /// <param name="probes">DI가 제공한 모든 dependency probe입니다.</param>
    /// <returns>성공 시 새 설정, 실패 시 code가 든 400/404 Problem Details를 반환합니다.</returns>
    // [FromServices]는 probes 같은 복합 형식을 JSON body가 아니라 DI 컨테이너에서 가져오라고 Minimal API에 알려 줍니다.
    // string?의 ?는 query를 생략하면 delayMs가 null일 수 있음을 nullable 분석기에 알립니다.
    private static IResult ConfigureDependency(
        string name,
        string condition,
        string? delayMs,
        [FromServices] IEnumerable<IDependencyProbe> probes)
    {
        var probe = FindConfigurableProbe(probes, name);
        if (probe is null)
        {
            return CreateProblem(
                StatusCodes.Status404NotFound,
                "dependency.not_found",
                "해당 이름의 교육용 dependency probe가 없습니다.");
        }

        // nullable enum은 허용 문자열이 아니면 null을 담아 검증 실패와 실제 enum 값을 구분합니다.
        // switch 식은 입력 문자열 한 값을 가능한 DependencyCondition 결과 하나로 바꿉니다.
        DependencyCondition? parsedCondition = condition.Trim().ToLowerInvariant() switch
        {
            "available" => DependencyCondition.Available,
            "unavailable" => DependencyCondition.Unavailable,
            _ => null
        };

        if (parsedCondition is null)
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "dependency.condition.invalid",
                "condition은 available 또는 unavailable이어야 합니다.");
        }

        var normalizedDelay = 0;
        // is not null pattern으로 query 존재를 확인하고, out은 변환된 정수를 기존 변수에 담게 합니다.
        // 직접 파싱해 framework의 빈 400이나 개발자 예외 페이지 대신 안정적인 code를 줍니다.
        if (delayMs is not null && !int.TryParse(
                delayMs,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out normalizedDelay))
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "dependency.delay.invalid",
                "delayMs는 정수여야 합니다.");
        }

        // is < 0 or > 2_000은 두 관계 pattern을 결합해 허용 범위 밖을 읽기 쉽게 표현합니다.
        if (normalizedDelay is < 0 or > 2_000)
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "dependency.delay.out_of_range",
                "delayMs는 0~2000이어야 합니다.");
        }

        probe.Configure(parsedCondition.Value, TimeSpan.FromMilliseconds(normalizedDelay));
        // new { ... }는 이 HTTP 응답에서만 쓸 작은 모양을 이름 없는 anonymous object로 만듭니다.
        return Results.Ok(new
        {
            probe.Name,
            Importance = probe.Importance.ToString(),
            Condition = parsedCondition.Value.ToString(),
            DelayMs = normalizedDelay
        });
    }

    /// <summary>
    /// liveness가 dependency probe를 호출하지 않는지 확인할 수 있도록 안전한 counter만 반환합니다.
    /// </summary>
    /// <param name="probes">DI가 제공한 모든 dependency probe입니다.</param>
    /// <returns>이름순 probe 호출/취소 횟수 배열을 HTTP 200으로 반환합니다.</returns>
    private static IResult GetProbeCounters(
        [FromServices] IEnumerable<IDependencyProbe> probes)
    {
        var counters = probes
            .OfType<ConfigurableDependencyProbe>()
            .OrderBy(probe => probe.Name, StringComparer.Ordinal)
            .Select(probe => new
            {
                probe.Name,
                probe.ProbeCount,
                probe.CanceledProbeCount
            })
            .ToArray();

        return Results.Ok(counters);
    }

    /// <summary>
    /// 전체 Port 구현 중 이름이 같은 교육용 Adapter를 찾습니다.
    /// </summary>
    /// <param name="probes">검색할 Port 구현 모음입니다.</param>
    /// <param name="name">대소문자를 무시하고 비교할 route 이름입니다.</param>
    /// <returns>일치하는 ConfigurableDependencyProbe 또는 없을 때 null을 반환합니다.</returns>
    private static ConfigurableDependencyProbe? FindConfigurableProbe(
        IEnumerable<IDependencyProbe> probes,
        string name)
    {
        return probes
            .OfType<ConfigurableDependencyProbe>()
            .SingleOrDefault(probe => string.Equals(
                probe.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 모든 demo 입력 오류를 같은 Problem Details 모양으로 만듭니다.
    /// </summary>
    /// <param name="statusCode">응답에 사용할 HTTP 상태 코드입니다.</param>
    /// <param name="code">클라이언트가 안정적으로 분기할 오류 코드입니다.</param>
    /// <param name="detail">사람이 이해할 수 있는 안전한 설명입니다.</param>
    /// <returns>code 확장 필드를 가진 Problem Details 결과를 반환합니다.</returns>
    private static IResult CreateProblem(int statusCode, string code, string detail)
    {
        return Results.Problem(
            statusCode: statusCode,
            title: "Dependency demo 요청을 처리할 수 없습니다.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code
            });
    }
}
