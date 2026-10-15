using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebhookReceiverApi.Application;
using WebhookReceiverApi.Domain;
using WebhookReceiverApi.Infrastructure;

namespace WebhookReceiverApi.Presentation;

/// <summary>
/// raw HTTP 요청을 Application 명령으로 바꾸고 use case 결과를 HTTP 계약으로 매핑합니다.
/// </summary>
public static class WebhookEndpoints
{
    private const string EventIdHeader = "X-Webhook-Id";
    private const string TimestampHeader = "X-Webhook-Timestamp";
    private const string SignatureHeader = "X-Webhook-Signature";

    /// <summary>
    /// 결제 웹훅 수신과 접수 결과 조회 endpoint를 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 ASP.NET Core endpoint builder입니다.</param>
    /// <returns>다른 endpoint 등록을 이어갈 수 있도록 같은 builder를 반환합니다.</returns>
    // 첫 매개변수의 this는 app.MapWebhookEndpoints()처럼 호출하게 하는 확장 메서드 문법입니다.
    public static IEndpointRouteBuilder MapWebhookEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/payments", ReceiveAsync)
            .WithName("ReceivePaymentWebhook")
            .WithTags("Payment webhooks")
            .Produces<WebhookReceiptResponse>(StatusCodes.Status202Accepted)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json");

        endpoints.MapGet("/webhooks/receipts/{eventId}", FindAsync)
            .WithName("FindWebhookReceipt")
            .WithTags("Payment webhooks")
            .Produces<WebhookReceiptResponse>(StatusCodes.Status200OK)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");

        return endpoints;
    }

    /// <summary>
    /// JSON media type, 단일 보안 헤더, body 크기를 확인한 뒤 Application Service에 접수를 요청합니다.
    /// </summary>
    /// <param name="httpContext">요청 headers/body와 응답 headers를 가진 현재 HTTP 문맥입니다.</param>
    /// <param name="service">DI가 제공하는 웹훅 수신 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>202 접수 응답 또는 표준 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> ReceiveAsync(
        HttpContext httpContext,
        WebhookReceiverApplicationService service,
        CancellationToken cancellationToken)
    {
        // var는 오른쪽 값에서 HttpRequest라는 정적 형식을 추론합니다.
        var request = httpContext.Request;
        // 결제 식별자와 금액이 든 응답을 브라우저·proxy가 재사용하지 않게 저장 금지를 알립니다.
        httpContext.Response.Headers.CacheControl = "no-store";
        if (!request.HasJsonContentType())
        {
            return ToProblem(
                new Error(
                    "http.unsupported_media_type",
                    "Content-Type을 application/json으로 보내세요.",
                    "Content-Type"),
                StatusCodes.Status415UnsupportedMediaType,
                httpContext);
        }

        var eventIdResult = ReadSingleRequiredHeader(request, EventIdHeader);
        if (!eventIdResult.IsSuccess)
        {
            return ToProblem(eventIdResult.Error!, StatusCodes.Status400BadRequest, httpContext);
        }

        var timestampResult = ReadSingleRequiredHeader(request, TimestampHeader);
        if (!timestampResult.IsSuccess)
        {
            return ToProblem(timestampResult.Error!, StatusCodes.Status400BadRequest, httpContext);
        }

        var signatureResult = ReadSingleRequiredHeader(request, SignatureHeader);
        if (!signatureResult.IsSuccess)
        {
            return ToProblem(signatureResult.Error!, StatusCodes.Status400BadRequest, httpContext);
        }

        var bodyResult = await ReadBodyAsync(
            request,
            WebhookSecurityOptions.MaxBodyBytes,
            cancellationToken);
        if (!bodyResult.IsSuccess)
        {
            var statusCode = bodyResult.Error!.Code == "webhook.body.too_large"
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status400BadRequest;
            return ToProblem(bodyResult.Error, statusCode, httpContext);
        }

        var result = await service.ReceiveAsync(
            new ReceiveWebhookCommand(
                eventIdResult.Value!,
                timestampResult.Value!,
                signatureResult.Value!,
                bodyResult.Value!),
            cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var statusCode = error.Code switch
            {
                "webhook.unauthorized" => StatusCodes.Status401Unauthorized,
                "webhook.replayed" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            if (statusCode == StatusCodes.Status401Unauthorized)
            {
                // WWW-Authenticate는 클라이언트에게 이 endpoint가 HMAC 인증을 요구한다는 표준 HTTP 힌트를 줍니다.
                httpContext.Response.Headers.WWWAuthenticate = "HMAC realm=\"webhooks\"";
            }

            return ToProblem(error, statusCode, httpContext);
        }

        var response = ToResponse(result.Value!);
        var escapedEventId = Uri.EscapeDataString(response.EventId);
        return TypedResults.Accepted(
            $"/webhooks/receipts/{escapedEventId}",
            response);
    }

    /// <summary>
    /// route의 event ID를 검증해 이미 접수한 웹훅 결과를 조회합니다.
    /// </summary>
    /// <param name="eventId">route에서 받은 아직 검증하지 않은 event ID 문자열입니다.</param>
    /// <param name="service">DI가 제공하는 웹훅 수신 Application Service입니다.</param>
    /// <param name="httpContext">Problem Details 경로와 trace ID를 제공하는 HTTP 문맥입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료와 연결된 취소 신호입니다.</param>
    /// <returns>200 접수 결과 또는 400·404 Problem Details를 실행할 IResult가 든 Task를 반환합니다.</returns>
    private static async Task<IResult> FindAsync(
        string eventId,
        WebhookReceiverApplicationService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        var result = await service.FindAsync(eventId, cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error!.Code == "webhook.not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return ToProblem(result.Error, statusCode, httpContext);
        }

        return TypedResults.Ok(ToResponse(result.Value!));
    }

    /// <summary>
    /// 보안 헤더가 정확히 하나인지 확인해 proxy와 framework 사이의 중복 값 해석 차이를 없앱니다.
    /// </summary>
    /// <param name="request">헤더를 읽을 현재 HTTP 요청입니다.</param>
    /// <param name="headerName">반드시 한 번만 존재해야 하는 헤더 이름입니다.</param>
    /// <returns>단일 문자열 값 또는 누락·중복 오류를 반환합니다.</returns>
    private static Result<string> ReadSingleRequiredHeader(
        HttpRequest request,
        string headerName)
    {
        // out var는 조회 성공 여부와 찾은 StringValues를 한 호출에서 함께 받습니다.
        if (!request.Headers.TryGetValue(headerName, out var values) ||
            values.Count == 0 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            return Result<string>.Failure(
                new Error(
                    "webhook.header.required",
                    $"{headerName} 헤더가 필요합니다.",
                    headerName));
        }

        if (values.Count != 1)
        {
            return Result<string>.Failure(
                new Error(
                    "webhook.header.ambiguous",
                    $"{headerName} 헤더는 정확히 하나만 보내야 합니다.",
                    headerName));
        }

        return Result<string>.Success(values[0]!);
    }

    /// <summary>
    /// request body를 제한된 크기까지만 비동기로 읽어 메모리 고갈 공격을 줄입니다.
    /// </summary>
    /// <param name="request">Content-Length와 body stream을 가진 HTTP 요청입니다.</param>
    /// <param name="maximumBytes">설정에서 검증된 허용 최대 바이트 수입니다.</param>
    /// <param name="cancellationToken">클라이언트가 연결을 끊으면 읽기를 멈추는 신호입니다.</param>
    /// <returns>원본 body byte 배열 또는 빈 body·크기 초과 오류를 반환합니다.</returns>
    private static async Task<Result<byte[]>> ReadBodyAsync(
        HttpRequest request,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > maximumBytes)
        {
            return BodyTooLarge(maximumBytes);
        }

        var buffer = new byte[Math.Min(4096, maximumBytes + 1)];
        // await using은 중간 반환이나 예외가 있어도 MemoryStream의 비동기 정리 경로를 보장합니다.
        await using var body = new MemoryStream();
        while (true)
        {
            var remainingProbeBytes = maximumBytes + 1 - (int)body.Length;
            var read = await request.Body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remainingProbeBytes)),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (body.Length > maximumBytes)
            {
                return BodyTooLarge(maximumBytes);
            }
        }

        if (body.Length == 0)
        {
            return Result<byte[]>.Failure(
                new Error(
                    "webhook.body.required",
                    "요청 JSON body가 필요합니다.",
                    "request"));
        }

        return Result<byte[]>.Success(body.ToArray());
    }

    /// <summary>
    /// 일관된 body 크기 초과 오류를 만듭니다.
    /// </summary>
    /// <param name="maximumBytes">오류 메시지에 안내할 최대 허용 바이트 수입니다.</param>
    /// <returns>webhook.body.too_large 오류가 든 실패 결과를 반환합니다.</returns>
    private static Result<byte[]> BodyTooLarge(int maximumBytes)
    {
        return Result<byte[]>.Failure(
            new Error(
                "webhook.body.too_large",
                $"요청 body는 {maximumBytes}바이트 이하여야 합니다.",
                "request"));
    }

    /// <summary>
    /// 내부 Domain 접수 결과를 안정적인 공개 HTTP 응답으로 복사합니다.
    /// </summary>
    /// <param name="receipt">Repository에 저장된 불변 접수 결과입니다.</param>
    /// <returns>비밀값과 raw body를 제외한 공개 응답을 반환합니다.</returns>
    private static WebhookReceiptResponse ToResponse(WebhookReceipt receipt)
    {
        return new WebhookReceiptResponse(
            receipt.EventId.Value,
            receipt.PaymentEvent.ToExternalType(),
            receipt.PaymentEvent.PaymentId,
            receipt.PaymentEvent.Amount,
            receipt.PaymentEvent.Currency,
            receipt.SignedAtUtc,
            receipt.ReceivedAtUtc,
            "accepted");
    }

    /// <summary>
    /// Domain/Application 오류를 RFC 9457 형태의 Problem Details HTTP 응답으로 바꿉니다.
    /// </summary>
    /// <param name="error">안전한 코드, 메시지, 필드를 가진 예상 오류입니다.</param>
    /// <param name="statusCode">오류 의미에 대응하는 HTTP 상태 코드입니다.</param>
    /// <param name="httpContext">요청 경로와 추적 식별자를 가진 HTTP 문맥입니다.</param>
    /// <returns>실행 시 application/problem+json을 쓰는 IResult를 반환합니다.</returns>
    private static IResult ToProblem(
        Error error,
        int statusCode,
        HttpContext httpContext)
    {
        // object?는 확장 값이 여러 형식 또는 null일 수 있음을 나타내고, 대괄호 index initializer가 key/value를 채웁니다.
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
            ["field"] = error.Field ?? "request",
            // ?.는 Activity가 없을 때 Id 접근 대신 null을 만들고 ??가 요청 trace ID를 대안으로 선택합니다.
            ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        return Results.Problem(
            statusCode: statusCode,
            title: GetProblemTitle(statusCode),
            detail: error.Message,
            instance: httpContext.Request.Path,
            extensions: extensions);
    }

    /// <summary>
    /// 상태 코드에 맞는 짧고 일관된 Problem Details 제목을 고릅니다.
    /// </summary>
    /// <param name="statusCode">응답으로 보낼 HTTP 상태 코드입니다.</param>
    /// <returns>클라이언트에게 보여 줄 안전한 한국어 제목을 반환합니다.</returns>
    private static string GetProblemTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "잘못된 웹훅 요청",
            StatusCodes.Status401Unauthorized => "웹훅 인증 실패",
            StatusCodes.Status404NotFound => "접수 결과 없음",
            StatusCodes.Status409Conflict => "웹훅 재전송 감지",
            StatusCodes.Status413PayloadTooLarge => "웹훅 본문 크기 초과",
            StatusCodes.Status415UnsupportedMediaType => "지원하지 않는 미디어 형식",
            _ => "요청 처리 실패"
        };
    }
}
