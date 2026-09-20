using Microsoft.AspNetCore.Mvc;
using RequestAdmissionApi.Application;
using RequestAdmissionApi.Application.Ports;
using RequestAdmissionApi.Domain;
using RequestAdmissionApi.Infrastructure;
using RequestAdmissionApi.RateLimiting;
using RequestAdmissionApi.SelfTesting;

namespace RequestAdmissionApi;

/// <summary>
/// 애플리케이션의 Composition Root입니다. 여기에서만 구체 구현을 인터페이스에 연결하고 HTTP 파이프라인을 조립합니다.
/// </summary>
public static class Program
{
    /// <summary>
    /// 명령줄을 해석해 self-test 또는 웹 서버를 실행합니다.
    /// <paramref name="args"/>는 <c>--self-test</c>나 웹 서버 옵션을 담으며 성공 시 0, self-test 실패 시 1을 반환합니다.
    /// <c>async</c>와 <c>await</c>는 비동기 작업을 기다리는 동안 스레드를 붙잡지 않고, <c>Task&lt;int&gt;</c>는 미래의 종료 코드를 나타냅니다.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTestRunner.RunAsync();
        }

        // var는 오른쪽 생성 식에서 타입이 명확할 때 중복 표기를 줄이며, 실제 타입은 WebApplicationBuilder입니다.
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder.Services);

        var app = builder.Build();
        ValidateConfiguration(app.Services);
        ConfigurePipeline(app);

        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 웹 서버가 포트를 열기 전에 Application Service를 한 번 구성해 renderer 누락·중복 같은 배포 오류를 확인합니다.
    /// <paramref name="services"/>는 빌드가 끝난 DI 컨테이너이며, 검증에 성공하면 반환값 없이 끝나고 실패하면 구성 예외를 그대로 발생시킵니다.
    /// </summary>
    private static void ValidateConfiguration(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // using var는 이 메서드가 끝날 때 임시 DI scope를 반드시 정리해 scoped 객체의 수명 경계를 지킵니다.
        using var scope = services.CreateScope();
        // _는 반환 객체 자체는 쓰지 않고 생성 과정의 구성 검증만 필요하다는 discard 표기입니다.
        _ = scope.ServiceProvider.GetRequiredService<ReportApplicationService>();
    }

    /// <summary>
    /// 애플리케이션 포트와 구체 어댑터, 시계, rate-limit policy를 DI 컨테이너에 연결합니다.
    /// <paramref name="services"/>는 등록 대상이며 반환값은 없습니다.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // DI는 생성 책임을 Composition Root로 모아 서비스 코드가 구현 선택을 모르도록 합니다.
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IReportRepository, InMemoryReportRepository>();
        services.AddSingleton<IReportRenderer, PlainTextReportRenderer>();
        services.AddScoped<ReportApplicationService>();
        services.AddProblemDetails();
        services.AddReportConcurrencyRateLimiting();
    }

    /// <summary>
    /// 미들웨어 순서와 HTTP endpoint를 설정합니다.
    /// <paramref name="app"/>은 빌드된 웹 애플리케이션이며 반환값은 없습니다.
    /// </summary>
    private static void ConfigurePipeline(WebApplication app)
    {
        // 예외 처리기는 뒤쪽 pipeline의 예상 밖 오류를 500 ProblemDetails로 바꾸므로 가장 바깥쪽에 둡니다.
        app.UseExceptionHandler();
        // endpoint-specific rate-limit metadata를 읽으려면 routing이 먼저 현재 endpoint를 선택해야 합니다.
        app.UseRouting();
        app.UseRateLimiter();

        // /health에는 RequireRateLimiting을 붙이지 않아 운영 상태 점검이 보고서 부하와 독립적으로 동작합니다.
        app.MapGet("/health", GetHealth);

        // 생성 응답의 Location을 실제로 따라갈 수 있도록 읽기 endpoint를 제공합니다.
        app.MapGet("/reports/{id:guid}", GetReportAsync);

        // named policy는 비싼 POST에만 명시적으로 적용됩니다.
        app.MapPost("/reports", CreateReportAsync)
            .RequireRateLimiting(ReportRateLimitPolicy.PolicyName);
    }

    /// <summary>
    /// 프로세스가 요청을 받을 수 있다는 간단한 상태를 반환합니다.
    /// 파라미터는 없으며 rate limit이 적용되지 않은 200 JSON 응답을 반환합니다.
    /// </summary>
    private static IResult GetHealth()
    {
        // new { ... }는 이 응답에서만 쓰는 작은 모양을 이름 없는 익명 타입으로 만드는 문법입니다.
        return Results.Ok(new { status = "healthy" });
    }

    /// <summary>
    /// 저장된 보고서를 ID로 조회하고 HTTP 응답으로 변환합니다.
    /// <paramref name="id"/>는 URL의 보고서 ID, <paramref name="service"/>는 조회 유스케이스,
    /// <paramref name="cancellationToken"/>은 연결 중단 신호이며 있으면 200, 없으면 404 Problem Details를 반환합니다.
    /// </summary>
    private static async Task<IResult> GetReportAsync(
        Guid id,
        ReportApplicationService service,
        CancellationToken cancellationToken)
    {
        var report = await service.FindByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "보고서를 찾을 수 없습니다.",
                detail: $"{id} ID의 보고서가 현재 프로세스 메모리에 없습니다.",
                type: "https://www.rfc-editor.org/rfc/rfc9110#name-404-not-found");
        }

        return Results.Ok(ToHttpResponse(report));
    }

    /// <summary>
    /// HTTP DTO를 애플리케이션 명령으로 바꾸고 보고서 생성 결과를 HTTP 응답으로 변환합니다.
    /// <paramref name="request"/>는 JSON 본문, <paramref name="service"/>는 DI가 제공한 유스케이스,
    /// <paramref name="cancellationToken"/>은 연결 중단 신호이며 201 성공 또는 400 Problem Details를 반환합니다.
    /// </summary>
    private static async Task<IResult> CreateReportAsync(
        CreateReportHttpRequest request,
        ReportApplicationService service,
        CancellationToken cancellationToken)
    {
        // ?.는 Rows가 null이면 Select를 실행하지 않고 null을 유지합니다. => 람다는 각 HTTP 행을 도메인 초안으로 바꿉니다.
        // JSON 배열의 null 요소도 500 예외 대신 도메인이 설명할 400 검증 오류로 그대로 전달합니다.
        var rows = request.Rows?
            .Select(row => row is null ? null : new ReportRowDraft(row.Label, row.Value))
            .ToArray();
        var command = new CreateReportCommand(request.Title, request.Format, rows);
        var result = await service.CreateAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            var report = result.Value;
            var response = ToHttpResponse(report);
            return Results.Created($"/reports/{report.Id}", response);
        }

        var error = result.Error
            ?? throw new InvalidOperationException("실패 Result에는 ApplicationError가 있어야 합니다.");
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: error.Message,
            detail: string.Join(" ", error.Details),
            type: "https://www.rfc-editor.org/rfc/rfc9110#name-400-bad-request",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["errors"] = error.Details,
            });
    }

    /// <summary>
    /// 내부 보고서 값을 안정적인 HTTP 응답 DTO로 바꿉니다.
    /// <paramref name="report"/>는 검증·저장된 보고서이며 wire format 문자열을 포함한 응답을 반환합니다.
    /// </summary>
    private static ReportHttpResponse ToHttpResponse(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new ReportHttpResponse(
            report.Id,
            report.Title,
            ToWireFormat(report.Format),
            report.Content,
            report.CreatedAtUtc);
    }

    /// <summary>
    /// 내부 enum을 안정적인 HTTP 문자열로 변환합니다.
    /// <paramref name="format"/>은 검증된 도메인 형식이며 API 계약 문자열을 반환합니다.
    /// </summary>
    private static string ToWireFormat(ReportFormat format)
    {
        return format switch
        {
            // switch 식은 값에 따른 변환을 간결하게 표현하고, 새 enum이 추가되면 이 지점을 검토하게 합니다.
            ReportFormat.PlainText => "text",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "지원하지 않는 보고서 형식입니다."),
        };
    }
}

/// <summary>
/// POST /reports JSON 본문입니다. Nullable 표기는 누락된 입력을 500 오류가 아니라 도메인 검증 오류로 다루기 위함입니다.
/// </summary>
/// <param name="Title">보고서 제목입니다.</param>
/// <param name="Format">현재 text만 허용하는 형식입니다.</param>
/// <param name="Rows">보고서 데이터 행들입니다.</param>
public sealed record CreateReportHttpRequest(
    string? Title,
    string? Format,
    IReadOnlyList<ReportRowHttpRequest?>? Rows);

/// <summary>
/// HTTP 입력 한 행을 나타냅니다.
/// </summary>
/// <param name="Label">행 이름입니다.</param>
/// <param name="Value">0 이상의 값입니다.</param>
public sealed record ReportRowHttpRequest(string? Label, decimal Value);

/// <summary>
/// 생성된 보고서를 클라이언트에 돌려주는 HTTP 응답입니다.
/// </summary>
/// <param name="Id">보고서 고유 식별자입니다.</param>
/// <param name="Title">정리된 제목입니다.</param>
/// <param name="Format">API 계약상의 형식 문자열입니다.</param>
/// <param name="Content">렌더링된 본문입니다.</param>
/// <param name="CreatedAtUtc">UTC 생성 시각입니다.</param>
public sealed record ReportHttpResponse(
    Guid Id,
    string Title,
    string Format,
    string Content,
    DateTimeOffset CreatedAtUtc);
