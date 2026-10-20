using EquipmentRentalApi.Application;
using EquipmentRentalApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentRentalApi.Presentation;

/// <summary>
/// HTTP 계약을 Application 명령과 Result에 연결하는 얇은 Presentation 계층입니다.
/// </summary>
public static class RentalEndpoints
{
    /// <summary>
    /// 장비 대여 생성과 조회 endpoint를 route builder에 등록합니다.
    /// </summary>
    /// <param name="endpoints">Program이 만든 route 등록 대상입니다.</param>
    /// <returns>다른 endpoint 등록을 이어 갈 수 있도록 같은 route builder를 반환합니다.</returns>
    public static IEndpointRouteBuilder MapRentalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // this가 첫 parameter 앞에 붙은 위 메서드는 endpoints.MapRentalEndpoints()처럼 읽히는 확장 메서드입니다.
        endpoints.MapPost("/rentals", CreateRentalAsync);
        endpoints.MapGet("/rentals/{reservationId}", GetRentalAsync);
        return endpoints;
    }

    /// <summary>
    /// JSON 요청을 Application 명령으로 바꾸고 성공은 201, 예상 실패는 400 또는 409로 변환합니다.
    /// </summary>
    /// <param name="request">ASP.NET Core JSON binder가 만든 nullable 요청 DTO입니다.</param>
    /// <param name="service">DI Container가 제공한 장비 대여 Application Service입니다.</param>
    /// <param name="cancellationToken">HTTP 연결이 끝나면 자동으로 취소되는 요청 token입니다.</param>
    /// <returns>HTTP 상태와 JSON 본문을 표현하는 IResult를 담은 Task를 반환합니다.</returns>
    // async는 Repository까지 이어지는 비동기 작업을 await하고 최종 HTTP 결과를 Task에 담는다는 뜻입니다.
    private static async Task<IResult> CreateRentalAsync(
        CreateRentalRequest request,
        CreateRentalService service,
        CancellationToken cancellationToken)
    {
        if (request.StartsAt is null)
        {
            return ToProblem(
                new Error("rental.required", "startsAt은 필수입니다.", "startsAt"),
                StatusCodes.Status400BadRequest);
        }

        if (request.EndsAt is null)
        {
            return ToProblem(
                new Error("rental.required", "endsAt은 필수입니다.", "endsAt"),
                StatusCodes.Status400BadRequest);
        }

        // var는 오른쪽 생성 결과의 명확한 형식을 compiler가 추론하게 합니다.
        // .Value는 위 null 검사 뒤 nullable struct 안의 실제 DateTimeOffset을 꺼냅니다.
        var command = new CreateRentalCommand(
            request.ReservationId,
            request.EquipmentId,
            request.BorrowerId,
            request.Category,
            request.Purpose,
            request.StartsAt.Value,
            request.EndsAt.Value);
        // await는 요청 thread를 붙잡지 않고 Application 작업이 끝날 때까지 비동기로 기다립니다.
        var result = await service.CreateAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error;
            // is ... or ... 패턴은 두 업무 오류 코드 중 하나인지 검사하고, ?:는 그 결과로 HTTP 상태를 고릅니다.
            var statusCode = error.Code is
                "rental.duplicate_reservation_id" or "rental.equipment_unavailable"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
            return ToProblem(error, statusCode);
        }

        var response = RentalResponse.FromDomain(result.Value);
        return TypedResults.Created($"/rentals/{response.ReservationId}", response);
    }

    /// <summary>
    /// URL의 예약 ID를 조회해 있으면 200, 없으면 안정적인 404 Problem Details를 반환합니다.
    /// </summary>
    /// <param name="reservationId">route에서 추출한 예약 식별자입니다.</param>
    /// <param name="service">DI Container가 제공한 조회 기능 포함 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 전달하는 취소 신호입니다.</param>
    /// <returns>예약 응답 또는 404 문제 응답을 담은 Task를 반환합니다.</returns>
    private static async Task<IResult> GetRentalAsync(
        string reservationId,
        CreateRentalService service,
        CancellationToken cancellationToken)
    {
        var reservation = await service.FindAsync(reservationId, cancellationToken);
        return reservation is null
            ? ToProblem(
                new Error("rental.not_found", "예약을 찾을 수 없습니다.", "reservationId"),
                StatusCodes.Status404NotFound)
            : TypedResults.Ok(RentalResponse.FromDomain(reservation));
    }

    /// <summary>
    /// Domain/Application 오류를 RFC Problem Details 모양과 안정적인 확장 필드로 바꿉니다.
    /// </summary>
    /// <param name="error">외부에 공개해도 되는 예상 오류입니다.</param>
    /// <param name="statusCode">오류 의미에 맞는 400, 404, 409 HTTP 상태입니다.</param>
    /// <returns>code와 field를 포함한 Problem Details IResult를 반환합니다.</returns>
    private static IResult ToProblem(Error error, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Results.Problem(
            statusCode: statusCode,
            title: "장비 대여 요청을 처리할 수 없습니다.",
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["field"] = error.Field
            });
    }
}
