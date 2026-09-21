using System.Security.Claims;
using DocumentAccessApi.Application;
using DocumentAccessApi.Domain;
using DocumentAccessApi.Security;
using Microsoft.AspNetCore.Authorization;

namespace DocumentAccessApi.Presentation;

/// <summary>
/// HTTP 입력을 Application 호출로 바꾸고 결과를 HTTP 응답으로 변환하는 얇은 Endpoint Adapter입니다.
/// </summary>
public static class DocumentEndpoints
{
    /// <summary>
    /// 프로세스가 요청을 받을 수 있는지 확인할 공개 상태 응답을 만듭니다.
    /// </summary>
    /// <returns>상태 문자열을 담은 HTTP 200 응답을 반환합니다.</returns>
    public static IResult GetHealth()
    {
        return Results.Ok(new { status = "ok" });
    }

    /// <summary>
    /// JSON 입력과 인증된 사용자를 Application command로 바꾸고 생성 결과를 HTTP 응답으로 변환합니다.
    /// </summary>
    /// <param name="request">JSON 본문에서 역직렬화한 제목과 본문입니다.</param>
    /// <param name="user">Authentication Handler가 만든 현재 사용자와 claim 모음입니다.</param>
    /// <param name="application">문서 생성 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료 같은 중단 요청을 전달하는 토큰입니다.</param>
    /// <returns>성공하면 201과 문서 DTO, 입력이 잘못되면 400 validation 응답을 반환합니다.</returns>
    public static async Task<IResult> CreateAsync(
        CreateDocumentRequest request,
        ClaimsPrincipal user,
        DocumentApplicationService application,
        CancellationToken cancellationToken)
    {
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await application.CreateAsync(
            ownerId,
            new CreateDocumentCommand(request.Title, request.Body),
            cancellationToken);

        if (!result.IsSuccess)
        {
            // 느낌표(!)는 실패 분기라 Error가 null이 아님을 컴파일러에 알려 줍니다. Result가 이 불변식을 한곳에서 보장합니다.
            var error = result.Error!;
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [error.Code] = [error.Message],
            });
        }

        return Results.Created(
            $"/documents/{result.Value.Id}",
            DocumentResponse.From(result.Value));
    }

    /// <summary>
    /// 문서를 조회한 뒤 실제 문서를 resource로 사용해 현재 사용자의 읽기 권한을 판단합니다.
    /// </summary>
    /// <param name="id">URL 경로에서 받은 문서 식별자입니다.</param>
    /// <param name="user">Authentication Handler가 만든 현재 사용자와 claim 모음입니다.</param>
    /// <param name="application">문서를 조회할 Application Service입니다.</param>
    /// <param name="authorization">requirement에 맞는 handler를 찾아 실행하는 Authorization Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료 같은 중단 요청을 전달하는 토큰입니다.</param>
    /// <returns>허용되면 200과 문서 DTO, 문서가 없거나 권한이 없으면 같은 모양의 404를 반환합니다.</returns>
    public static async Task<IResult> FindAsync(
        Guid id,
        ClaimsPrincipal user,
        DocumentApplicationService application,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var document = await application.FindAsync(id, cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        var decision = await authorization.AuthorizeAsync(
            user,
            document,
            DocumentReadRequirement.Instance);

        if (!decision.Succeeded)
        {
            // 다른 사용자가 문서 존재 여부를 열거하지 못하게 403 대신 404로 감춥니다. 제품의 위협 모델에 맞춰 일관되게 적용해야 합니다.
            return Results.NotFound();
        }

        return Results.Ok(DocumentResponse.From(document));
    }
}

/// <summary>
/// Minimal API가 JSON 요청 본문을 역직렬화할 입력 DTO입니다.
/// </summary>
/// <param name="Title">새 문서 제목이며 Domain에서 길이와 공백을 검사합니다.</param>
/// <param name="Body">새 문서 본문이며 Domain에서 길이와 공백을 검사합니다.</param>
public sealed record CreateDocumentRequest(string? Title, string? Body);

/// <summary>
/// Domain 객체에서 API 응답에 공개할 필드만 골라낸 출력 DTO입니다. 내부 권한 비교용 OwnerId는 공개하지 않습니다.
/// </summary>
/// <param name="Id">문서 식별자입니다.</param>
/// <param name="Title">문서 제목입니다.</param>
/// <param name="Body">인가를 통과한 사용자에게만 반환할 본문입니다.</param>
/// <param name="CreatedAtUtc">문서 생성 UTC 시각입니다.</param>
public sealed record DocumentResponse(
    Guid Id,
    string Title,
    string Body,
    DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// Domain 문서를 HTTP 응답 DTO로 복사하되 권한 판단용 OwnerId는 제외합니다.
    /// </summary>
    /// <param name="document">인가가 끝난 원본 Domain 문서입니다.</param>
    /// <returns>API에 공개하도록 선택한 필드만 가진 응답 객체를 반환합니다.</returns>
    public static DocumentResponse From(ProjectDocument document)
    {
        return new DocumentResponse(
            document.Id,
            document.Title,
            document.Body,
            document.CreatedAtUtc);
    }
}
