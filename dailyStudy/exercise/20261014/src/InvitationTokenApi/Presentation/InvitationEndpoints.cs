using System.Diagnostics;
using InvitationTokenApi.Application;
using InvitationTokenApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace InvitationTokenApi.Presentation;

/// <summary>
/// 프로젝트 초대 use case를 세 HTTP endpoint 계약에 연결합니다.
/// 학습 범위를 좁히려고 인증·인가를 생략했으므로 그대로 인터넷에 공개하면 안 되며, 운영에서는 사용자와 프로젝트 권한을 먼저 검증해야 합니다.
/// </summary>
public static class InvitationEndpoints
{
    /// <summary>
    /// 초대 생성, 조회, 사용 endpoint를 /invitations route group에 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>다른 endpoint 등록을 이어갈 수 있도록 같은 builder를 반환합니다.</returns>
    // 첫 매개변수의 this는 app.MapInvitationEndpoints()처럼 읽게 하는 확장 메서드 문법입니다.
    public static IEndpointRouteBuilder MapInvitationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/invitations")
            .WithTags("Project invitations");

        group.MapPost(string.Empty, CreateAsync)
            .WithName("CreateProjectInvitation")
            .Produces<CreateInvitationResponse>(StatusCodes.Status201Created)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json");

        group.MapGet("/{invitationId}", GetAsync)
            .WithName("GetProjectInvitation")
            .Produces<InvitationDetailsResponse>(StatusCodes.Status200OK)
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");

        group.MapPost("/redeem", RedeemAsync)
            .WithName("RedeemProjectInvitation")
            .Produces<RedeemInvitationResponse>(StatusCodes.Status200OK)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json");

        return endpoints;
    }

    /// <summary>
    /// 생성 JSON을 Application 명령으로 바꾸고 201, 400, 409 결과로 매핑합니다.
    /// </summary>
    /// <param name="request">Minimal API가 JSON에서 읽은 입력이며 null body일 수도 있습니다.</param>
    /// <param name="service">DI가 제공하는 초대 Application Service입니다.</param>
    /// <param name="httpContext">Location과 Problem Details 요청 정보를 가진 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>생성 응답 또는 표준 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> CreateAsync(
        CreateInvitationRequest? request,
        InvitationApplicationService service,
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

        var result = await service.CreateAsync(
            new CreateInvitationCommand(
                request.InvitationId,
                request.ProjectId,
                request.Email,
                request.Role),
            cancellationToken);
        if (result.IsSuccess)
        {
            var created = result.Value!;
            var response = new CreateInvitationResponse(
                created.Invitation.InvitationId,
                created.Invitation.ProjectId,
                created.Invitation.Email,
                created.Invitation.Role.ToString(),
                created.Token,
                created.Invitation.ExpiresAtUtc);
            return TypedResults.Created(
                $"/invitations/{created.Invitation.InvitationId}",
                response);
        }

        var error = result.Error!;
        var statusCode = error.Code == "invitation.duplicate"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return ToProblem(error, statusCode, httpContext);
    }

    /// <summary>
    /// route ID로 초대를 조회하고 토큰을 제외한 200 응답 또는 404를 만듭니다.
    /// </summary>
    /// <param name="invitationId">URL에서 받은 아직 검증하지 않은 초대 ID입니다.</param>
    /// <param name="service">DI가 제공하는 초대 Application Service입니다.</param>
    /// <param name="httpContext">Problem Details 요청 경로와 trace ID를 가진 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>조회 응답 또는 404 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> GetAsync(
        string invitationId,
        InvitationApplicationService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(invitationId, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(InvitationDetailsResponse.FromDomain(result.Value!))
            : ToProblem(
                result.Error!,
                StatusCodes.Status404NotFound,
                httpContext);
    }

    /// <summary>
    /// 토큰을 Application Service에서 검증·사용하고 200, 400, 409로 매핑합니다.
    /// </summary>
    /// <param name="request">외부에서 받은 불투명 토큰 JSON이며 null body일 수도 있습니다.</param>
    /// <param name="service">DI가 제공하는 초대 Application Service입니다.</param>
    /// <param name="httpContext">Problem Details 요청 경로와 trace ID를 가진 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>최소 성공 응답 또는 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> RedeemAsync(
        RedeemInvitationRequest? request,
        InvitationApplicationService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ToProblem(
                new Error(
                    "invitation.token.invalid_or_expired",
                    "초대 토큰이 올바르지 않거나 만료되었습니다.",
                    "token"),
                StatusCodes.Status400BadRequest,
                httpContext);
        }

        var result = await service.RedeemAsync(
            new RedeemInvitationCommand(request.Token),
            cancellationToken);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(
                RedeemInvitationResponse.FromDomain(result.Value!));
        }

        var error = result.Error!;
        var statusCode = error.Code == "invitation.already_redeemed"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return ToProblem(error, statusCode, httpContext);
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
                StatusCodes.Status400BadRequest => "초대 토큰 또는 입력을 확인하세요.",
                StatusCodes.Status404NotFound => "요청한 초대를 찾을 수 없습니다.",
                StatusCodes.Status409Conflict => "현재 초대 상태와 요청이 충돌합니다.",
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
