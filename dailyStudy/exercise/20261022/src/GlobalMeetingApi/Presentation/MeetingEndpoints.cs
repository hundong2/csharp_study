using GlobalMeetingApi.Application;
using GlobalMeetingApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GlobalMeetingApi.Presentation;

/// <summary>
/// HTTP DTO와 상태 코드를 Application 결과로 변환하는 얇은 Presentation 계층입니다.
/// </summary>
public static class MeetingEndpoints
{
    /// <summary>
    /// 회의 생성과 조회 endpoint를 `/meetings` 그룹에 등록합니다.
    /// </summary>
    /// <param name="endpoints">WebApplication이 제공하는 endpoint route builder입니다.</param>
    /// <returns>다른 endpoint 등록을 이어 갈 수 있도록 같은 builder를 반환합니다.</returns>
    // 첫 매개변수의 this는 인스턴스 메서드처럼 호출할 수 있게 하는 extension method 문법입니다.
    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var meetings = endpoints.MapGroup("/meetings");
        meetings.MapPost("/", ScheduleAsync);
        meetings.MapGet("/{meetingId}", GetAsync);
        return endpoints;
    }

    /// <summary>
    /// JSON request를 명령으로 바꾸고 성공은 201, 입력 오류는 400, 충돌은 409로 응답합니다.
    /// </summary>
    /// <param name="request">JSON에서 바인딩된 외부 회의 입력입니다.</param>
    /// <param name="service">DI가 제공한 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>Application 결과에 맞는 HTTP 응답이 완료되는 Task를 반환합니다.</returns>
    private static async Task<IResult> ScheduleAsync(
        ScheduleMeetingRequest request,
        ScheduleMeetingService service,
        CancellationToken cancellationToken)
    {
        if (request.LocalDate is null)
        {
            return ToProblem(
                new Error("local_date.required", "localDate가 필요합니다.", "localDate"),
                StatusCodes.Status400BadRequest);
        }

        if (request.LocalStartTime is null)
        {
            return ToProblem(
                new Error(
                    "local_start_time.required",
                    "localStartTime이 필요합니다.",
                    "localStartTime"),
                StatusCodes.Status400BadRequest);
        }

        var offsetResult = UtcOffsetParser.Parse(request.PreferredUtcOffset);
        if (!offsetResult.IsSuccess)
        {
            return ToProblem(offsetResult.Error, StatusCodes.Status400BadRequest);
        }

        var command = new ScheduleMeetingCommand(
            request.MeetingId,
            request.OrganizerId,
            request.Title,
            request.LocalDate.Value,
            request.LocalStartTime.Value,
            request.TimeZoneId,
            offsetResult.Value.Value,
            request.DurationMinutes);
        var result = await service.ScheduleAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            var response = MeetingResponse.FromDomain(result.Value);
            return TypedResults.Created($"/meetings/{response.MeetingId}", response);
        }

        // or pattern은 두 충돌 code를 한 조건으로 묶고, ?:는 참이면 409, 아니면 400을 선택합니다.
        var statusCode = result.Error.Code is
            "meeting_id.duplicate" or "organizer.time_conflict"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
        return ToProblem(result.Error, statusCode);
    }

    /// <summary>
    /// URL의 예약 ID를 조회하고 찾으면 200, 없으면 404로 응답합니다.
    /// </summary>
    /// <param name="meetingId">route에서 받은 회의 ID입니다.</param>
    /// <param name="service">DI가 제공한 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>조회 결과에 맞는 HTTP 응답이 완료되는 Task를 반환합니다.</returns>
    private static async Task<IResult> GetAsync(
        string meetingId,
        ScheduleMeetingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(meetingId, cancellationToken);
        // ?: 삼항 연산자는 Result 성공 여부에 맞춰 200과 404 응답 하나를 고릅니다.
        return result.IsSuccess
            ? TypedResults.Ok(MeetingResponse.FromDomain(result.Value))
            : ToProblem(result.Error, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Domain/Application 오류를 RFC Problem Details 모양의 안전한 HTTP 본문으로 바꿉니다.
    /// </summary>
    /// <param name="error">안정적인 코드, 설명, 필드, 선택적 허용값을 가진 오류입니다.</param>
    /// <param name="statusCode">400, 404, 409 중 응답할 HTTP 상태 코드입니다.</param>
    /// <returns>클라이언트가 고칠 정보를 가진 Problem 응답을 반환합니다.</returns>
    private static IResult ToProblem(Error error, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = error.Code,
            ["field"] = error.Field
        };
        if (error.AllowedValues is not null)
        {
            extensions["allowedValues"] = error.AllowedValues;
        }

        // switch 식의 각 => arm은 상태 코드에 맞는 공개 제목 하나를 선택합니다.
        return TypedResults.Problem(
            statusCode: statusCode,
            title: statusCode switch
            {
                StatusCodes.Status404NotFound => "회의를 찾을 수 없습니다.",
                StatusCodes.Status409Conflict => "회의를 저장할 수 없습니다.",
                _ => "회의 입력을 확인하세요."
            },
            detail: error.Message,
            extensions: extensions);
    }
}
