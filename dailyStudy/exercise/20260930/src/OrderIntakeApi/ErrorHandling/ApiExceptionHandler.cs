using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderIntakeApi.Infrastructure;

namespace OrderIntakeApi.ErrorHandling;

/// <summary>
/// 처리되지 않은 예외를 안전하고 일관된 Problem Details HTTP 응답으로 바꾸는 마지막 오류 경계입니다.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    /// <summary>
    /// Problem Details writer와 구조화 logger를 DI로 받습니다.
    /// </summary>
    /// <param name="problemDetailsService">표준 오류 응답을 직렬화할 framework 서비스입니다.</param>
    /// <param name="logger">내부 진단을 남기되 응답과 분리할 logger입니다.</param>
    /// <returns>생성자는 handler를 초기화하므로 별도 반환값은 없습니다.</returns>
    public ApiExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService ??
            throw new ArgumentNullException(nameof(problemDetailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 요청 취소는 그대로 전파하고, 일시적 외부 장애와 알 수 없는 버그를 서로 다른 안전한 응답으로 처리합니다.
    /// </summary>
    /// <param name="httpContext">현재 요청, 응답, trace 식별자를 가진 ASP.NET Core 문맥입니다.</param>
    /// <param name="exception">pipeline 아래에서 처리되지 않고 올라온 예외입니다.</param>
    /// <param name="cancellationToken">오류 응답 작성 자체가 중단될 때의 신호입니다.</param>
    /// <returns>이 handler가 응답까지 책임졌으면 true, 다음 처리로 넘기면 false를 반환합니다.</returns>
    // ValueTask<bool>은 대개 짧게 끝나는 framework hook이 불필요한 Task 할당을 줄일 수 있게 한 비동기 반환형입니다.
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // 클라이언트가 연결을 끊어 생긴 취소는 서버 결함 500으로 바꾸지 않고 hosting layer가 마무리하게 둡니다.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var failure = Classify(exception);
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (failure.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            _logger.LogWarning(
                "상품 카탈로그 일시 장애를 처리했습니다. TraceId={TraceId}",
                traceId);
            // Retry-After는 클라이언트가 즉시 재시도 폭주를 만들지 않도록 초 단위 힌트를 줍니다.
            httpContext.Response.Headers.RetryAfter = "5";
        }
        else if (failure.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            // 예외 객체는 서버 로그에만 남기고 응답 detail에는 형식, stack trace, 내부 message를 복사하지 않습니다.
            _logger.LogError(
                exception,
                "예기치 않은 요청 오류를 처리했습니다. TraceId={TraceId}",
                traceId);
        }
        else
        {
            // 잘못된 JSON은 흔한 클라이언트 실패이므로 stack trace 없이 정보 수준으로만 남깁니다.
            _logger.LogInformation(
                "잘못된 HTTP 요청을 안전한 400으로 처리했습니다. TraceId={TraceId}",
                traceId);
        }

        httpContext.Response.StatusCode = failure.StatusCode;
        var problem = new ProblemDetails
        {
            Status = failure.StatusCode,
            Title = failure.Title,
            Detail = failure.SafeDetail,
            Instance = httpContext.Request.Path,
            Type = failure.TypeUri
        };
        problem.Extensions["code"] = failure.Code;

        // WriteAsync는 등록된 Problem Details writer와 전역 CustomizeProblemDetails 규칙을 재사용합니다.
        await _problemDetailsService.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem
            });

        // .NET 10에서는 true를 반환해 처리한 예외의 middleware 진단이 기본 억제되므로 이 handler가 한 번만 기록합니다.
        return true;
    }

    /// <summary>
    /// 내부 예외 형식을 공개 가능한 상태, 코드, 설명으로 분류합니다.
    /// </summary>
    /// <param name="exception">오류 경계까지 올라온 실제 예외입니다.</param>
    /// <returns>응답에 안전하게 사용할 FailureDescriptor를 반환합니다.</returns>
    private static FailureDescriptor Classify(Exception exception)
    {
        // type pattern switch는 예외 형식별 정책을 한 표처럼 모으고, 기본 분기로 새 예외도 500에 fail-closed합니다.
        return exception switch
        {
            BadHttpRequestException => new FailureDescriptor(
                StatusCodes.Status400BadRequest,
                "http.bad_request",
                "요청 본문을 읽을 수 없습니다.",
                "JSON 형식과 Content-Type을 확인하세요.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            ProductCatalogUnavailableException => new FailureDescriptor(
                StatusCodes.Status503ServiceUnavailable,
                "catalog.unavailable",
                "상품 정보를 잠시 사용할 수 없습니다.",
                "잠시 후 같은 요청을 다시 시도하세요.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.4"),
            _ => new FailureDescriptor(
                StatusCodes.Status500InternalServerError,
                "server.unexpected",
                "서버에서 요청을 처리하지 못했습니다.",
                "traceId와 함께 운영 담당자에게 문의하세요.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1")
        };
    }

    /// <summary>
    /// 예외 분류 뒤 응답에 허용할 값만 담는 내부 불변 자료입니다.
    /// </summary>
    /// <param name="StatusCode">HTTP 상태 코드입니다.</param>
    /// <param name="Code">클라이언트용 안정 오류 코드입니다.</param>
    /// <param name="Title">오류 종류를 설명하는 짧은 제목입니다.</param>
    /// <param name="SafeDetail">민감정보가 없는 행동 안내입니다.</param>
    /// <param name="TypeUri">HTTP 의미를 설명하는 RFC 9110 section URI입니다.</param>
    private sealed record FailureDescriptor(
        int StatusCode,
        string Code,
        string Title,
        string SafeDetail,
        string TypeUri);
}
