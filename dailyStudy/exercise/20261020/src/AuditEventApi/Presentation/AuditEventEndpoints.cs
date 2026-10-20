using System.Diagnostics;
using AuditEventApi.Application;
using AuditEventApi.Domain;

namespace AuditEventApi.Presentation;

/// <summary>
/// HTTP 요청/응답과 Application command/Result 사이만 변환하는 얇은 Presentation 계층입니다.
/// </summary>
/// <remarks>인증·인가를 생략한 로컬 학습용 endpoint이므로 외부 네트워크에 그대로 공개하면 안 됩니다.</remarks>
public static class AuditEventEndpoints
{
    /// <summary>
    /// 감사 이벤트 생성과 조회 endpoint를 하나의 route group으로 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>추가 구성이 필요할 때 이어서 사용할 원래 endpoint builder를 반환합니다.</returns>
    // this가 첫 parameter 앞에 붙으면 app.MapAuditEventEndpoints()처럼 호출할 수 있는 extension method가 됩니다.
    public static IEndpointRouteBuilder MapAuditEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // var는 오른쪽 MapGroup 결과로 지역 변수 형식을 추론하며, compiler가 이후 호출의 형식 안전성을 계속 검사합니다.
        var group = endpoints.MapGroup("/audit-events").WithTags("Audit events");
        group.MapPost("/", IngestAsync);
        group.MapGet("/{eventId}", FindAsync);
        return endpoints;
    }

    /// <summary>
    /// 구체적인 다형성 요청을 안정적인 command로 바꾸고, 새 감사 이벤트를 한 번만 저장합니다.
    /// </summary>
    /// <param name="request">System.Text.Json이 type 판별자로 선택한 구체 요청 DTO입니다.</param>
    /// <param name="service">종류별 검증과 원자적 저장 순서를 조정할 Application Service입니다.</param>
    /// <param name="httpContext">Location과 안전한 Problem Details를 만들 현재 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 하위 계층까지 전달하는 취소 신호입니다.</param>
    /// <returns>저장 성공 시 201과 Location, 예상 실패 시 400 또는 409 결과를 반환합니다.</returns>
    private static async Task<IResult> IngestAsync(
        AuditEventRequest request,
        IngestAuditEventService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = ToCommand(request);
        // await는 비동기 저장이 끝날 때까지 thread를 막지 않고 현재 흐름의 다음 판단을 이어 줍니다.
        var result = await service.IngestAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!, httpContext);
        }

        var response = ToResponse(result.Value!);
        // $ 문자열은 검증된 EventId를 Location 경로에 넣는 문자열 보간 문법입니다.
        var location = $"/audit-events/{Uri.EscapeDataString(response.EventId)}";
        return TypedResults.Created(location, response);
    }

    /// <summary>
    /// URL의 이벤트 식별자를 정규화해 저장된 감사 이벤트 하나를 조회합니다.
    /// </summary>
    /// <param name="eventId">route에서 받은 생산자 이벤트 식별자입니다.</param>
    /// <param name="service">Repository Port를 통해 조회할 Application Service입니다.</param>
    /// <param name="httpContext">404 Problem Details에 요청 경로와 traceId를 넣을 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 더 기다리지 않을 때 전달하는 취소 신호입니다.</param>
    /// <returns>찾으면 200 응답, 없으면 안전한 404 Problem Details를 반환합니다.</returns>
    private static async Task<IResult> FindAsync(
        string eventId,
        IngestAuditEventService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var auditEvent = await service.FindByEventIdAsync(eventId, cancellationToken);
        if (auditEvent is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "감사 이벤트를 찾을 수 없습니다.",
                detail: "요청한 eventId와 일치하는 감사 이벤트가 없습니다.",
                instance: httpContext.Request.Path,
                extensions: CreateExtensions("audit.not_found", "eventId", httpContext));
        }

        return TypedResults.Ok(ToResponse(auditEvent));
    }

    /// <summary>
    /// 세 Presentation 하위 형식의 필드만 골라 framework에 의존하지 않는 Application command로 바꿉니다.
    /// </summary>
    /// <param name="request">다형성 역직렬화를 마친 허용 목록 안의 요청입니다.</param>
    /// <returns>공통 필드와 해당 종류의 선택 필드만 채운 command를 반환합니다.</returns>
    private static IngestAuditEventCommand ToCommand(AuditEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // switch 형식 패턴은 runtime 하위 형식을 확인하면서 각 형식 전용 property에 안전하게 접근합니다.
        return request switch
        {
            UserSignedInRequest signIn => new IngestAuditEventCommand(
                AuditEventDraft.UserSignedInKind,
                signIn.EventId,
                signIn.OccurredAtUtc,
                signIn.ActorId,
                IpAddress: signIn.IpAddress,
                Succeeded: signIn.Succeeded),
            DocumentChangedRequest document => new IngestAuditEventCommand(
                AuditEventDraft.DocumentChangedKind,
                document.EventId,
                document.OccurredAtUtc,
                document.ActorId,
                DocumentId: document.DocumentId,
                ChangeSummary: document.ChangeSummary),
            PermissionChangedRequest permission => new IngestAuditEventCommand(
                AuditEventDraft.PermissionChangedKind,
                permission.EventId,
                permission.OccurredAtUtc,
                permission.ActorId,
                TargetUserId: permission.TargetUserId,
                Permission: permission.Permission,
                Action: permission.Action),
            // 허용 목록과 source-generated 계약이 어긋난 경우는 사용자가 고칠 업무 오류가 아니라 구성 결함입니다.
            _ => throw new UnreachableException("등록되지 않은 감사 이벤트 요청 형식입니다.")
        };
    }

    /// <summary>
    /// 내부 Domain Model에서 공개하기로 정한 값만 안정적인 HTTP 응답 DTO로 복사합니다.
    /// </summary>
    /// <param name="auditEvent">검증과 저장을 마친 불변 Domain Model입니다.</param>
    /// <returns>내부 세부 Dictionary를 노출하지 않는 공개 응답을 반환합니다.</returns>
    private static AuditEventResponse ToResponse(AuditEvent auditEvent)
    {
        return new AuditEventResponse(
            auditEvent.EventId,
            auditEvent.Kind,
            auditEvent.OccurredAtUtc,
            auditEvent.ActorId,
            auditEvent.Summary,
            auditEvent.ReceivedAtUtc);
    }

    /// <summary>
    /// Application의 예상 실패를 내부 구현 정보가 없는 Problem Details HTTP 결과로 변환합니다.
    /// </summary>
    /// <param name="error">Domain 또는 Application이 반환한 안정적인 업무 오류입니다.</param>
    /// <param name="httpContext">요청 경로와 traceId를 제공하는 현재 HTTP 문맥입니다.</param>
    /// <returns>중복이면 409, 그 밖의 입력 오류면 400인 Problem Details를 반환합니다.</returns>
    private static IResult ToProblem(Error error, HttpContext httpContext)
    {
        var statusCode = error.Code == "audit.duplicate_event_id"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status409Conflict
                ? "감사 이벤트가 이미 존재합니다."
                : "감사 이벤트 값이 올바르지 않습니다.",
            detail: error.Message,
            instance: httpContext.Request.Path,
            extensions: CreateExtensions(error.Code, error.Field, httpContext));
    }

    /// <summary>
    /// 모든 수동 Problem Details에 같은 code, field, traceId 확장 값을 만듭니다.
    /// </summary>
    /// <param name="code">클라이언트가 분기할 안정적인 오류 코드입니다.</param>
    /// <param name="field">호출자가 확인할 입력 필드 또는 자원 이름입니다.</param>
    /// <param name="httpContext">현재 분산 trace 또는 요청 식별자를 제공하는 HTTP 문맥입니다.</param>
    /// <returns>Problem Details extensions에 바로 전달할 문자열-객체 Dictionary를 반환합니다.</returns>
    private static Dictionary<string, object?> CreateExtensions(
        string code,
        string field,
        HttpContext httpContext)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["field"] = field,
            ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };
    }
}
