using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// 내부 예외·연결 문자열을 숨기고 운영자가 필요한 최소 정보만 JSON으로 쓰는 응답 Adapter입니다.
/// </summary>
public static class HealthResponseWriter
{
    /// <summary>
    /// 전체 상태와 정렬된 검사 이름, 안전한 코드, 소요 시간만 응답으로 직렬화합니다.
    /// </summary>
    /// <param name="context">응답 헤더와 본문을 쓸 현재 HTTP 문맥입니다.</param>
    /// <param name="report">ASP.NET Core가 모든 등록 검사를 실행해 만든 보고서입니다.</param>
    /// <returns>JSON 본문 쓰기가 완료될 때 끝나는 Task를 반환합니다.</returns>
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";

        // LINQ OrderBy/Select는 report 항목을 결정적 이름순 공개 DTO로 바꿉니다.
        // entry =>는 항목 하나를 변환하는 이름 없는 짧은 함수(lambda)입니다.
        var checks = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new HealthCheckResponseItem(
                entry.Key,
                entry.Value.Status.ToString(),
                NormalizeSafeCode(
                    entry.Value.Description,
                    entry.Value.Exception is not null),
                Math.Round(entry.Value.Duration.TotalMilliseconds, 2)))
            .ToArray();

        var response = new HealthResponse(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks);

        // await는 JSON 쓰기 I/O가 끝날 때까지 thread를 점유하지 않고 기다립니다.
        // RequestAborted를 전달해 연결이 끊긴 뒤 불필요한 JSON 쓰기를 계속하지 않습니다.
        await context.Response.WriteAsJsonAsync(response, cancellationToken: context.RequestAborted);
    }

    /// <summary>
    /// 예외가 있거나 미리 허용하지 않은 description이면 일반 코드로 바꿔 민감정보 노출을 막습니다.
    /// </summary>
    /// <param name="description">각 health check가 공개 가능하다고 선언한 짧은 설명입니다.</param>
    /// <param name="hasException">framework report에 내부 예외 객체가 붙었는지 나타냅니다.</param>
    /// <returns>명시적으로 허용한 상태 코드 또는 대체 코드를 반환합니다.</returns>
    internal static string NormalizeSafeCode(string? description, bool hasException)
    {
        if (hasException || string.IsNullOrWhiteSpace(description))
        {
            return "health.check.failed";
        }

        var normalized = description.Trim().ToLowerInvariant();
        // 문자 모양만 검사하면 영숫자로 된 password도 통과합니다. 제품이 만든 알려진 코드만 allowlist로 공개합니다.
        return normalized switch
        {
            "startup.ready" => normalized,
            "startup.warming_up" => normalized,
            "dependencies.available" => normalized,
            "dependencies.optional_unavailable" => normalized,
            "dependencies.required_unavailable" => normalized,
            _ => "health.check.failed"
        };
    }
}

/// <summary>
/// health endpoint 전체 JSON 모양을 나타냅니다.
/// </summary>
/// <param name="Status">Healthy, Degraded, Unhealthy 중 전체 상태입니다.</param>
/// <param name="TotalDurationMs">모든 검사에 걸린 전체 시간(ms)입니다.</param>
/// <param name="Checks">이 endpoint에서 실제 실행된 검사 목록입니다.</param>
public sealed record HealthResponse(
    string Status,
    double TotalDurationMs,
    IReadOnlyList<HealthCheckResponseItem> Checks);

/// <summary>
/// 개별 health check의 공개 가능한 JSON 항목입니다.
/// </summary>
/// <param name="Name">등록 시 지정한 검사 이름입니다.</param>
/// <param name="Status">개별 검사의 세 단계 상태입니다.</param>
/// <param name="Code">예외 원문이 아닌 안전한 상태 코드입니다.</param>
/// <param name="DurationMs">개별 검사 소요 시간(ms)입니다.</param>
public sealed record HealthCheckResponseItem(
    string Name,
    string Status,
    string Code,
    double DurationMs);
