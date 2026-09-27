using System.Globalization;
using System.Text;
using ReportTimeoutApi.Application;
using ReportTimeoutApi.Domain;
using Microsoft.AspNetCore.Http.Timeouts;

namespace ReportTimeoutApi.Presentation;

/// <summary>
/// HTTP 입력을 Application Service 호출로 바꾸고 Domain 결과를 HTTP 응답으로 매핑합니다.
/// </summary>
public static class ReportEndpoints
{
    /// <summary>
    /// 보고서 다운로드와 health endpoint를 애플리케이션 route table에 등록합니다.
    /// </summary>
    /// <param name="endpoints">Minimal API endpoint를 등록할 route builder입니다.</param>
    /// <returns>추가 등록을 이어 갈 수 있도록 같은 route builder를 반환합니다.</returns>
    // 첫 인수의 this는 이 static 메서드를 endpoints.MapReportEndpoints()처럼 호출하게 하는 extension method 문법입니다.
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // var는 오른쪽 MapGroup 결과로 형식이 분명할 때 타입 이름 반복을 줄입니다.
        var reportGroup = endpoints.MapGroup("/reports");

        reportGroup
            .MapGet("/{customerId}", HandleGenerateReportAsync)
            .WithName("GenerateInvoiceReport")
            // WithRequestTimeout은 이 endpoint에 이름 있는 150ms 정책을 연결합니다.
            .WithRequestTimeout(TimeoutNames.ReportGeneration);

        endpoints
            .MapGet("/health", HandleHealth)
            .WithName("Health")
            // health는 서비스 생존 여부를 빠르게 답해야 하므로 전역 timeout 정책 적용을 명시적으로 끕니다.
            .DisableRequestTimeout();

        return endpoints;
    }

    /// <summary>
    /// route/query 입력으로 보고서를 생성하고 성공 파일 또는 일관된 Problem Details를 반환합니다.
    /// </summary>
    /// <param name="customerId">route의 고객 번호입니다.</param>
    /// <param name="format">csv/json query이며 생략하면 csv입니다.</param>
    /// <param name="simulateMs">timeout 재현용 지연 query 원문이며 생략하면 25ms입니다.</param>
    /// <param name="service">DI가 제공하는 보고서 Application Service입니다.</param>
    /// <param name="context">현재 요청과 RequestAborted 취소 신호를 가진 HTTP 문맥입니다.</param>
    /// <returns>성공 시 다운로드 파일, 입력 오류 시 400, 데이터가 없으면 404로 완료되는 Task를 반환합니다.</returns>
    // async/await는 Repository I/O를 기다리는 동안 요청 스레드를 점유하지 않게 합니다.
    private static async Task<IResult> HandleGenerateReportAsync(
        string customerId,
        string? format,
        string? simulateMs,
        ReportApplicationService service,
        HttpContext context)
    {
        // out var는 helper가 반환하는 성공 여부와 함께 두 출력 값의 지역 변수를 선언합니다.
        if (!TryParseSimulatedLatency(simulateMs, out var effectiveLatency, out var parseError))
        {
            // !는 바로 위의 실패 분기에서 parseError가 반드시 있다는 사실을 nullable 분석기에 알려 줍니다.
            return MapError(parseError!);
        }

        var result = await service.GenerateAsync(
            customerId,
            format,
            effectiveLatency,
            context.RequestAborted);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var document = result.Value!;
        var bytes = Encoding.UTF8.GetBytes(document.Content);
        return Results.File(bytes, document.ContentType, document.FileName);
    }

    /// <summary>
    /// query 문자열을 문화권에 영향받지 않는 정수 밀리초로 바꾸고 생략 시 기본값을 적용합니다.
    /// </summary>
    /// <param name="rawValue">HTTP query에서 받은 원문이며 null 또는 공백이면 기본값을 뜻합니다.</param>
    /// <param name="milliseconds">성공 시 25 또는 파싱된 정수를 전달할 출력 인수입니다.</param>
    /// <param name="error">실패 시 일관된 Problem Details로 바꿀 오류를 전달할 출력 인수입니다.</param>
    /// <returns>정수 변환에 성공하거나 값이 생략되었으면 true, 그 외에는 false를 반환합니다.</returns>
    private static bool TryParseSimulatedLatency(
        string? rawValue,
        out int milliseconds,
        out DomainError? error)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            milliseconds = 25;
            error = null;
            return true;
        }

        // TryParse는 잘못된 query에서 framework의 빈 400을 내지 않고 우리 Problem Details 계약을 유지하게 합니다.
        if (int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out milliseconds))
        {
            error = null;
            return true;
        }

        milliseconds = 0;
        error = new DomainError(
            "report.delay.invalid",
            "simulateMs는 정수여야 합니다.");
        return false;
    }

    /// <summary>
    /// 예상된 Domain 오류 코드를 API의 400 또는 404 Problem Details 계약으로 바꿉니다.
    /// </summary>
    /// <param name="error">Application Service가 반환한 안정적인 오류 코드와 설명입니다.</param>
    /// <returns>오류 코드에 맞는 상태와 확장 필드를 가진 Problem Details 응답을 반환합니다.</returns>
    private static IResult MapError(DomainError error)
    {
        // switch 식은 오류 코드에 따라 상태를 값으로 선택합니다. _는 앞 조건에 없는 나머지 모든 코드입니다.
        // 각 arm의 =>는 왼쪽 패턴이 맞을 때 오른쪽 값을 결과로 선택한다는 뜻입니다.
        var statusCode = error.Code switch
        {
            "report.customer.not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        // 조건 연산자 condition ? A : B는 조건이 참이면 A, 거짓이면 B를 선택합니다.
        var title = statusCode == StatusCodes.Status404NotFound
            ? "보고서 데이터를 찾을 수 없습니다."
            : "보고서 요청이 올바르지 않습니다.";

        // object?는 확장 값이 여러 참조 형식을 담고 null도 허용한다는 뜻이며, ["code"]는 Dictionary index initializer입니다.
        // statusCode:처럼 이름을 붙인 named argument는 인수 순서보다 각 값의 의미를 먼저 보이게 합니다.
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code
            });
    }

    /// <summary>
    /// 프로세스가 요청을 받을 수 있음을 아주 짧은 JSON으로 알려 줍니다.
    /// </summary>
    /// <returns>항상 HTTP 200과 status=ok 객체를 반환합니다.</returns>
    private static IResult HandleHealth()
    {
        // 익명 형식 new { ... }은 이 응답에서만 필요한 JSON 모양을 별도 class 선언 없이 만듭니다.
        return Results.Ok(new { status = "ok" });
    }
}
