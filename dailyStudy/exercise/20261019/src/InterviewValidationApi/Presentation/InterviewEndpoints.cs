using InterviewValidationApi.Application;
using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Presentation;

/// <summary>
/// 면접 일정 HTTP 경로와 Application Service 사이의 얇은 변환 계층입니다.
/// </summary>
public static class InterviewEndpoints
{
    /// <summary>
    /// POST 생성과 GET 조회 endpoint를 현재 route builder에 등록합니다.
    /// </summary>
    /// <param name="endpoints">경로를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>추가 구성을 이어 갈 수 있도록 같은 builder를 반환합니다.</returns>
    // this 매개변수가 있는 정적 메서드는 app.MapInterviewEndpoints()처럼 호출 가능한 extension method가 됩니다.
    public static IEndpointRouteBuilder MapInterviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/interviews", ScheduleInterviewAsync)
            .Produces<InterviewResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        endpoints.MapGet("/interviews/{id:guid}", GetInterviewAsync)
            .Produces<InterviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    /// 자동 검증을 통과한 body를 command로 바꾸고 예약 결과를 201 또는 업무 오류로 변환합니다.
    /// </summary>
    /// <param name="request">binding과 .NET 10 자동 검증을 통과한 JSON 요청입니다.</param>
    /// <param name="service">DI로 받은 면접 일정 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결이 끊기면 ASP.NET Core가 전달하는 취소 신호입니다.</param>
    /// <returns>생성 성공 201 또는 Domain/Application 오류에 맞는 Problem Details를 반환합니다.</returns>
    private static async Task<IResult> ScheduleInterviewAsync(
        CreateInterviewRequest request,
        ScheduleInterviewService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ScheduleAsync(request.ToCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        var response = InterviewResponse.FromDomain(result.Value!);
        return TypedResults.Created($"/interviews/{response.Id}", response);
    }

    /// <summary>
    /// URL의 GUID로 일정을 조회하고 존재 여부를 200 또는 404로 표현합니다.
    /// </summary>
    /// <param name="id">route constraint가 GUID로 변환한 일정 식별자입니다.</param>
    /// <param name="service">저장 Port를 감싼 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트가 조회를 취소할 때 전달되는 신호입니다.</param>
    /// <returns>찾으면 200 응답, 없으면 404 Problem Details를 반환합니다.</returns>
    private static async Task<IResult> GetInterviewAsync(
        Guid id,
        ScheduleInterviewService service,
        CancellationToken cancellationToken)
    {
        var interview = await service.FindByIdAsync(id, cancellationToken);
        return interview is null
            ? CreateProblem(
                StatusCodes.Status404NotFound,
                new Error("interview.not_found", "요청한 면접 일정을 찾을 수 없습니다.", "id"))
            : TypedResults.Ok(InterviewResponse.FromDomain(interview));
    }

    /// <summary>
    /// 안정적인 오류 code에 따라 예상 실패를 400 또는 409 HTTP Problem Details로 바꿉니다.
    /// </summary>
    /// <param name="error">Application 또는 Domain에서 반환한 예상 실패입니다.</param>
    /// <returns>오류 성격에 맞는 상태 코드를 가진 Problem Details 결과를 반환합니다.</returns>
    private static IResult ToProblem(Error error)
    {
        var statusCode = error.Code == "interview.slot_conflict"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return CreateProblem(statusCode, error);
    }

    /// <summary>
    /// code와 field 확장 값을 포함한 일관된 Problem Details 결과를 만듭니다.
    /// </summary>
    /// <param name="statusCode">응답에 사용할 HTTP 상태 코드입니다.</param>
    /// <param name="error">detail, code, field에 반영할 예상 실패입니다.</param>
    /// <returns>안전한 공개 정보만 담은 Problem Details 결과를 반환합니다.</returns>
    private static IResult CreateProblem(int statusCode, Error error)
    {
        return TypedResults.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status409Conflict ? "일정 충돌" : "요청 오류",
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["field"] = error.Field
            });
    }
}
