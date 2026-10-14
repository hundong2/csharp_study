using System.Diagnostics;
using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using DeploymentSseApi.Application;
using DeploymentSseApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DeploymentSseApi.Presentation;

/// <summary>
/// 배포 진행 게시와 SSE 구독 use case를 HTTP 계약에 연결합니다.
/// 학습 범위를 좁히려고 인증·인가를 생략했으므로 운영 공개 전에는 배포 권한을 반드시 검증해야 합니다.
/// </summary>
public static class DeploymentEndpoints
{
    private const string ProgressEventType = "deployment-progress";

    /// <summary>
    /// 배포 진행 게시와 SSE 구독 endpoint를 같은 route에 HTTP method별로 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>다른 endpoint 등록을 이어갈 수 있도록 같은 builder를 반환합니다.</returns>
    // 첫 매개변수의 this는 app.MapDeploymentEndpoints()처럼 읽게 하는 확장 메서드 문법입니다.
    public static IEndpointRouteBuilder MapDeploymentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/deployments/{deploymentId}/events")
            .WithTags("Deployment progress");

        group.MapPost(string.Empty, PublishAsync)
            .WithName("PublishDeploymentProgress")
            .Accepts<PublishDeploymentProgressRequest>("application/json")
            .Produces<DeploymentProgressResponse>(StatusCodes.Status201Created)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json");

        group.MapGet(string.Empty, OpenStreamAsync)
            .WithName("StreamDeploymentProgress")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");

        return endpoints;
    }

    /// <summary>
    /// JSON 요청을 Domain draft로 바꾸고 Application Service 결과를 201, 400, 409로 매핑합니다.
    /// </summary>
    /// <param name="deploymentId">route에서 받은 아직 검증하지 않은 배포 식별자입니다.</param>
    /// <param name="request">Minimal API가 JSON에서 읽은 입력이며 body가 JSON null이면 null일 수 있습니다.</param>
    /// <param name="service">DI가 제공하는 배포 진행 Application Service입니다.</param>
    /// <param name="httpContext">Location과 Problem Details 요청 정보를 가진 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>생성 응답 또는 표준 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> PublishAsync(
        string deploymentId,
        PublishDeploymentProgressRequest? request,
        DeploymentProgressApplicationService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ToProblem(
                new Error("http.bad_request", "요청 JSON body가 필요합니다.", "request"),
                StatusCodes.Status400BadRequest,
                httpContext);
        }

        var result = await service.PublishAsync(
            new DeploymentProgressDraft(
                deploymentId,
                request.Stage,
                request.Percent,
                request.Message),
            cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var statusCode = error.Code is "deployment.transition.invalid" or
                "deployment.already_terminal" or
                "deployment.concurrent_update"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return ToProblem(error, statusCode, httpContext);
        }

        var response = ToResponse(result.Value!);
        var escapedDeploymentId = Uri.EscapeDataString(response.DeploymentId);
        return TypedResults.Created(
            $"/deployments/{escapedDeploymentId}/events",
            response);
    }

    /// <summary>
    /// Last-Event-ID 이후의 replay와 live Event를 .NET 10 SSE 결과로 변환합니다.
    /// </summary>
    /// <param name="deploymentId">route에서 받은 아직 검증하지 않은 배포 식별자입니다.</param>
    /// <param name="service">DI가 제공하는 배포 진행 Application Service입니다.</param>
    /// <param name="httpContext">재연결 헤더, 요청 경로, 연결 취소 신호를 가진 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>SSE stream 또는 400·404 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> OpenStreamAsync(
        string deploymentId,
        DeploymentProgressApplicationService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Headers indexer는 값이 없으면 빈 StringValues를 돌려주며 ToString은 이를 빈 문자열로 바꿉니다.
        var lastEventId = httpContext.Request.Headers["Last-Event-ID"].ToString();
        var result = await service.OpenStreamAsync(
            deploymentId,
            lastEventId,
            cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var statusCode = error.Code == "deployment.not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return ToProblem(error, statusCode, httpContext);
        }

        // TypedResults.ServerSentEvents는 SseItem의 id, event, retry, JSON data를 표준 text/event-stream framing으로 씁니다.
        return TypedResults.ServerSentEvents(
            ToSseItems(result.Value!.Events, cancellationToken));
    }

    /// <summary>
    /// Domain Event 비동기 열거를 HTTP에 공개할 SseItem 비동기 열거로 한 건씩 변환합니다.
    /// </summary>
    /// <param name="events">Repository가 replay와 live 구간을 원자적으로 연결한 Domain Event stream입니다.</param>
    /// <param name="cancellationToken">연결 종료 시 await foreach와 Repository 구독을 함께 멈출 신호입니다.</param>
    /// <returns>EventId, event type, 재연결 간격, JSON data를 가진 SSE 항목 stream을 반환합니다.</returns>
    // async iterator는 호출 순간 전체를 실행하지 않고, 소비자가 다음 항목을 요청할 때까지 작업을 미룹니다.
    private static async IAsyncEnumerable<SseItem<DeploymentProgressResponse>> ToSseItems(
        IAsyncEnumerable<DeploymentProgressEvent> events,
        // EnumeratorCancellation은 GetAsyncEnumerator에 전달된 취소 신호를 iterator 본문에서 보게 합니다.
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // await foreach는 다음 Event가 비동기로 도착할 때 thread를 막지 않고 기다립니다.
        await foreach (var progressEvent in events.WithCancellation(cancellationToken))
        {
            // yield return은 연결을 끝내지 않고 지금 준비된 항목 하나만 클라이언트로 보냅니다.
            yield return new SseItem<DeploymentProgressResponse>(
                ToResponse(progressEvent),
                ProgressEventType)
            {
                EventId = progressEvent.EventId.ToString(CultureInfo.InvariantCulture),
                ReconnectionInterval = TimeSpan.FromSeconds(2)
            };
        }
    }

    /// <summary>
    /// 내부 Domain Event를 POST와 SSE가 공유하는 공개 응답 DTO로 복사합니다.
    /// </summary>
    /// <param name="progressEvent">Strategy 검증과 저장을 마친 불변 Domain Event입니다.</param>
    /// <returns>HTTP에 공개해도 되는 필드만 가진 응답을 반환합니다.</returns>
    private static DeploymentProgressResponse ToResponse(
        DeploymentProgressEvent progressEvent)
    {
        return new DeploymentProgressResponse(
            progressEvent.EventId,
            progressEvent.DeploymentId,
            progressEvent.Stage.ToString(),
            progressEvent.Percent,
            progressEvent.Message,
            progressEvent.OccurredAtUtc,
            progressEvent.IsTerminal);
    }

    /// <summary>
    /// Domain 오류를 code, field, traceId가 있는 application/problem+json 결과로 바꿉니다.
    /// </summary>
    /// <param name="error">Application Service가 반환한 예상 가능한 오류입니다.</param>
    /// <param name="statusCode">HTTP 400, 404, 409 중 사용할 상태입니다.</param>
    /// <param name="httpContext">요청 경로와 추적 ID를 제공하는 현재 HTTP 문맥입니다.</param>
    /// <returns>ASP.NET Core가 표준 media type으로 실행할 ProblemHttpResult를 반환합니다.</returns>
    private static IResult ToProblem(
        Error error,
        int statusCode,
        HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "배포 진행 입력을 확인하세요.",
                StatusCodes.Status404NotFound => "요청한 배포 진행 기록을 찾을 수 없습니다.",
                StatusCodes.Status409Conflict => "현재 배포 상태와 요청이 충돌합니다.",
                _ => "요청을 처리할 수 없습니다."
            },
            Detail = error.Message,
            Instance = httpContext.Request.Path,
            Type = statusCode switch
            {
                StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
                _ => "about:blank"
            }
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["field"] = error.Field ?? "request";
        problem.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;
        return TypedResults.Problem(problem);
    }
}
