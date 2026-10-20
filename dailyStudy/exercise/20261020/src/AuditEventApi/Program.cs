using System.Diagnostics;
using System.Text.Json.Serialization;
using AuditEventApi.Application;
using AuditEventApi.Application.Ports;
using AuditEventApi.Infrastructure;
using AuditEventApi.Presentation;
using AuditEventApi.SelfTesting;
using AuditEventApi.Serialization;
using Microsoft.AspNetCore.Diagnostics;

namespace AuditEventApi;

/// <summary>
/// HTTP, source-generated JSON 계약, Application, Port, Adapter를 한곳에서 조립하는 Composition Root입니다.
/// </summary>
/// <remarks>
/// 이 프로젝트는 인증·인가 package를 넣지 않은 로컬 학습용 예제입니다.
/// 신뢰할 수 없는 네트워크에 그대로 배포하면 누구나 감사 이벤트를 기록하고 조회할 수 있으므로 외부 배포에 사용하면 안 됩니다.
/// </remarks>
public static class Program
{
    /// <summary>
    /// --self-test 인수면 내부 회귀 검증을 실행하고, 아니면 Kestrel HTTP 서버를 실행합니다.
    /// </summary>
    /// <param name="args">dotnet run 뒤에 전달된 자체 테스트, URL, 환경 인수입니다.</param>
    /// <returns>정상 완료면 0, 자체 테스트 실패면 1인 프로세스 종료 코드를 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        // await using은 서버가 끝날 때 WebApplication의 비동기 자원을 빠뜨리지 않고 정리합니다.
        // var는 오른쪽 BuildApplication 반환값에서 app의 형식을 추론하며, 형식 검사를 없애는 dynamic과 다릅니다.
        await using var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// 엄격한 JSON 입력 정책, DI 등록, middleware, endpoint를 조립한 WebApplication을 만듭니다.
    /// </summary>
    /// <param name="args">Kestrel과 configuration에 전달할 명령줄 인수입니다.</param>
    /// <returns>아직 시작하지 않은 완성된 WebApplication을 반환합니다.</returns>
    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // lambda 식의 options => ...는 framework가 나중에 호출할 설정 작업을 값처럼 전달하는 문법입니다.
        builder.Services.AddProblemDetails(
            options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        builder.WebHost.ConfigureKestrel(
            options =>
            {
                // 뒤쪽에 놓인 JSON type 판별자를 찾을 때 serializer가 앞선 속성을 임시 보관할 수 있으므로
                // 큰 요청을 이용한 memory 고갈을 줄이기 위해 이 학습 API의 전체 요청 본문을 32 KiB로 제한합니다.
                options.Limits.MaxRequestBodySize = 32 * 1024;
            });
        builder.Services.ConfigureHttpJsonOptions(
            options =>
            {
                var json = options.SerializerOptions;
                // TypeInfoResolverChain의 첫 resolver로 생성된 metadata를 넣어 등록한 API 계약은 reflection보다 먼저 처리합니다.
                json.TypeInfoResolverChain.Insert(0, AuditJsonContext.Default);
                // 아래 다섯 옵션은 .NET 10 JsonSerializerOptions.Strict가 묶어 제공하는 핵심 입력 안전 정책입니다.
                json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                json.AllowDuplicateProperties = false;
                json.PropertyNameCaseInsensitive = false;
                json.RespectNullableAnnotations = true;
                json.RespectRequiredConstructorParameters = true;
                // JSON 객체는 속성 순서에 의미가 없으므로 type이 첫 속성이 아니어도 허용합니다.
                // 이 옵션은 type을 찾을 때 앞선 내용을 buffering할 수 있어 위 Kestrel 본문 상한과 반드시 함께 사용합니다.
                json.AllowOutOfOrderMetadataProperties = true;
            });

        builder.Services.AddSingleton(TimeProvider.System);
        // Singleton 메모리 Repository의 공유 상태는 ConcurrentDictionary.TryAdd의 원자적 연산으로 보호합니다.
        builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
        foreach (var mapper in AuditEventMappers.CreateDefault())
        {
            // 같은 Port의 여러 Strategy를 등록하면 Application Service가 IEnumerable로 모두 주입받습니다.
            builder.Services.AddSingleton<IAuditEventMapper>(mapper);
        }

        builder.Services.AddSingleton<IngestAuditEventService>();

        var app = builder.Build();
        // JSON binding과 Kestrel이 의도적으로 만든 4xx를 ExceptionHandler가 500으로 바꾸지 않도록 상태 선택 규칙을 연결합니다.
        // 객체 초기화 구문은 new로 만든 옵션의 속성을 중괄호 안에서 바로 지정해 올바른 middleware overload를 선택하게 합니다.
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                // 메서드 이름을 값처럼 넘기는 method group 문법으로, 예외마다 아래 선택 함수를 호출하게 합니다.
                StatusCodeSelector = SelectExceptionStatusCode
            });
        app.UseStatusCodePages();
        app.MapGet("/", GetServiceStatus);
        // 중요: 이 route group에는 인증·인가가 없습니다. localhost 학습 외의 환경에서는 인증 정책을 추가한 뒤 공개해야 합니다.
        app.MapAuditEventEndpoints();
        return app;
    }

    /// <summary>
    /// HTTP 요청 자체의 유효한 4xx 예외는 보존하고, 예상 밖 서버 예외는 안전한 500으로 분류합니다.
    /// </summary>
    /// <param name="exception">요청 pipeline 밖으로 전파되어 ExceptionHandler가 처리할 예외입니다.</param>
    /// <returns>BadHttpRequestException의 유효한 4xx 상태 또는 그 밖의 경우 500을 반환합니다.</returns>
    private static int SelectExceptionStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // is 패턴은 예외 형식을 확인하며 변수를 만들고, 관계 패턴은 외부에 내보내도 되는 400~499 범위만 허용합니다.
        // 조건 연산자는 조건이 참이면 원래 4xx를, 거짓이면 내부 오류를 뜻하는 500을 고릅니다.
        return exception is BadHttpRequestException badRequest
            && badRequest.StatusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
                ? badRequest.StatusCode
                : StatusCodes.Status500InternalServerError;
    }

    /// <summary>
    /// 서비스가 실행 중인지와 공개 입력에 적용한 JSON 안전 정책을 알려 줍니다.
    /// </summary>
    /// <returns>서비스 이름, 허용한 이벤트 종류, JSON 정책을 가진 200 OK 결과를 반환합니다.</returns>
    private static IResult GetServiceStatus()
    {
        return TypedResults.Ok(
            new ServiceStatusResponse(
                "AuditEventApi",
                SourceGeneratedJson: true,
                AuditEventMappers.SupportedKinds,
                StrictInput: true));
    }

    /// <summary>
    /// framework가 만든 오류 응답에도 안전한 code, field, traceId를 일관되게 추가합니다.
    /// </summary>
    /// <param name="context">현재 요청과 작성 중인 Problem Details를 가진 framework 문맥입니다.</param>
    /// <returns>객체를 제자리에서 보완하므로 반환값은 없습니다.</returns>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        // ??는 왼쪽 nullable 값이 null일 때만 오른쪽 기본값을 선택하는 null 병합 연산자입니다.
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = statusCode;
        // ??=는 왼쪽 값이 null일 때만 오른쪽 값을 대입해 framework가 이미 만든 instance를 덮어쓰지 않습니다.
        problem.Instance ??= context.HttpContext.Request.Path;
        // switch 식은 statusCode와 일치하는 한 항목의 안정적인 공개 오류 코드를 선택합니다.
        var code = statusCode switch
        {
            StatusCodes.Status400BadRequest => "http.bad_request",
            StatusCodes.Status404NotFound => "http.not_found",
            StatusCodes.Status405MethodNotAllowed => "http.method_not_allowed",
            StatusCodes.Status413PayloadTooLarge => "http.payload_too_large",
            StatusCodes.Status415UnsupportedMediaType => "http.unsupported_media_type",
            StatusCodes.Status500InternalServerError => "http.internal_error",
            _ => "http.error"
        };
        problem.Extensions.TryAdd("code", code);
        problem.Extensions.TryAdd("field", "http");
        problem.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            // framework나 예외 처리기가 Detail을 이미 넣었더라도 5xx에서는 내부 예외·경로가 새지 않도록 안전한 문구로 교체합니다.
            problem.Title = "서버에서 요청을 처리하지 못했습니다.";
            problem.Detail = "잠시 후 다시 시도하고 문제가 계속되면 traceId와 함께 관리자에게 문의해 주세요.";
        }
        else if (string.IsNullOrWhiteSpace(problem.Detail))
        {
            problem.Detail = statusCode == StatusCodes.Status400BadRequest
                ? "JSON 형식, type 판별자, 필수 필드와 중복·알 수 없는 속성을 확인해 주세요."
                : "요청을 처리할 수 없습니다.";
        }
    }
}
