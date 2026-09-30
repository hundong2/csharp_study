using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WorkshopContractApi.Application;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Presentation;

/// <summary>
/// 워크숍 등록 use case를 HTTP 계약과 OpenAPI metadata에 연결합니다.
/// </summary>
public static class RegistrationEndpoints
{
    /// <summary>
    /// 등록 생성과 단건 조회 endpoint를 같은 route group에 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>다른 endpoint 등록을 이어갈 수 있도록 같은 builder를 반환합니다.</returns>
    // 첫 매개변수의 this는 endpoints.MapWorkshopRegistrationEndpoints()처럼 호출할 수 있는 확장 메서드 문법입니다.
    public static IEndpointRouteBuilder MapWorkshopRegistrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/registrations")
            .WithTags("Workshop registrations");

        group.MapPost(string.Empty, CreateAsync)
            .WithName("CreateWorkshopRegistration")
            .WithSummary("워크숍 좌석 예약 생성")
            .WithDescription("입력을 검증하고 회차·좌석 충돌을 확인한 뒤 새 예약을 만듭니다.")
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json");

        group.MapGet("/{registrationId}", GetByIdAsync)
            .WithName("GetWorkshopRegistration")
            .WithSummary("워크숍 예약 단건 조회")
            .WithDescription("정규화된 예약 ID로 이미 생성된 예약을 조회합니다.")
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");

        return endpoints;
    }

    /// <summary>
    /// JSON 요청을 Application 명령으로 바꾸고 성공·검증·미존재·충돌을 명시적인 typed HTTP 결과로 매핑합니다.
    /// </summary>
    /// <param name="request">Minimal API가 JSON에서 역직렬화한 요청 DTO입니다. 개별 문자열은 Domain에서도 다시 검증합니다.</param>
    /// <param name="service">DI가 제공한 워크숍 등록 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료가 자동 연결된 요청 취소 신호입니다.</param>
    /// <returns>Created 또는 ProblemHttpResult 두 결과 형식만 허용하는 compile-time union을 반환합니다. 정확한 오류 상태는 switch와 테스트가 보장합니다.</returns>
    private static async Task<Results<Created<RegistrationResponse>, ProblemHttpResult>> CreateAsync(
        CreateRegistrationRequest request,
        WorkshopRegistrationService service,
        CancellationToken cancellationToken)
    {
        var command = new RegisterWorkshopCommand(
            request.RegistrationId,
            request.SessionId,
            request.AttendeeId,
            request.SeatNumber,
            request.AttendeeTier);
        var result = await service.RegisterAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            var response = RegistrationResponse.FromDomain(result.Value!);
            return TypedResults.Created(
                $"/registrations/{response.RegistrationId}",
                response);
        }

        var error = result.Error!;
        // union은 Created와 ProblemHttpResult 이외의 결과 형식을 막습니다. ProblemHttpResult 내부 상태는 switch와 테스트로 제한합니다.
        return error.Code switch
        {
            "registration.id.invalid" or
            "registration.session_id.invalid" or
            "registration.attendee_id.invalid" or
            "registration.seat.out_of_range" or
            "registration.tier.invalid" or
            "workshop.seat.out_of_range" => ToProblemResult(
                error,
                StatusCodes.Status400BadRequest,
                "/registrations"),
            "workshop.session.not_found" => ToProblemResult(
                error,
                StatusCodes.Status404NotFound,
                "/registrations"),
            "registration.id.duplicate" or
            "registration.seat.taken" => ToProblemResult(
                error,
                StatusCodes.Status409Conflict,
                "/registrations"),
            _ => throw new InvalidOperationException($"HTTP 매핑이 없는 오류 코드입니다: {error.Code}")
        };
    }

    /// <summary>
    /// route의 예약 ID를 조회하고 200, 400, 404 중 하나의 typed 결과로 변환합니다.
    /// </summary>
    /// <param name="registrationId">URL route에서 받은 아직 검증하지 않은 예약 식별자입니다.</param>
    /// <param name="service">DI가 제공한 워크숍 등록 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료가 자동 연결된 요청 취소 신호입니다.</param>
    /// <returns>예약 본문, 잘못된 ID, 미존재 중 하나를 반환합니다.</returns>
    private static async Task<Results<Ok<RegistrationResponse>, ProblemHttpResult>> GetByIdAsync(
        string registrationId,
        WorkshopRegistrationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FindAsync(registrationId, cancellationToken);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(RegistrationResponse.FromDomain(result.Value!));
        }

        var error = result.Error!;
        return error.Code switch
        {
            "registration.id.invalid" => ToProblemResult(
                error,
                StatusCodes.Status400BadRequest,
                $"/registrations/{registrationId}"),
            "registration.not_found" => ToProblemResult(
                error,
                StatusCodes.Status404NotFound,
                $"/registrations/{registrationId}"),
            _ => throw new InvalidOperationException($"HTTP 매핑이 없는 오류 코드입니다: {error.Code}")
        };
    }

    /// <summary>
    /// Domain Error를 상태 코드와 안정 code 확장을 가진 표준 Problem Details 본문으로 바꿉니다.
    /// </summary>
    /// <param name="error">Application Service가 반환한 예상 가능한 오류입니다.</param>
    /// <param name="statusCode">HTTP 400, 404, 409 중 매핑할 상태 코드입니다.</param>
    /// <param name="instance">오류가 발생한 요청 경로입니다.</param>
    /// <returns>application/problem+json으로 실행될 ProblemHttpResult를 반환합니다.</returns>
    private static ProblemHttpResult ToProblemResult(Error error, int statusCode, string instance)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "요청 값을 확인하세요.",
                StatusCodes.Status404NotFound => "요청한 대상을 찾을 수 없습니다.",
                StatusCodes.Status409Conflict => "현재 상태와 요청이 충돌합니다.",
                _ => "요청을 처리할 수 없습니다."
            },
            Detail = error.Message,
            Instance = instance,
            Type = statusCode switch
            {
                StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
                _ => "about:blank"
            }
        };
        problem.Extensions["code"] = error.Code;
        // 이 endpoint의 오류 schema는 field를 필수로 문서화하므로 전체 요청 오류는 request로 표현합니다.
        problem.Extensions["field"] = error.Field ?? "request";

        // ProblemHttpResult는 일반 JSON 결과와 달리 RFC Problem Details 전용 media type을 사용합니다.
        return TypedResults.Problem(problem);
    }
}
