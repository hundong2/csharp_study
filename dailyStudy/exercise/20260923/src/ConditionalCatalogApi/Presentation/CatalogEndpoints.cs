using ConditionalCatalogApi.Application;
using ConditionalCatalogApi.Domain;

namespace ConditionalCatalogApi.Presentation;

/// <summary>
/// HTTP 요청·헤더·상태 코드를 Application의 명령과 결과로 변환하는 얇은 Presentation Adapter입니다.
/// ETag 해석과 HTTP 응답만 담당하고 가격 규칙이나 저장 기술은 직접 알지 않습니다.
/// </summary>
public static class CatalogEndpoints
{
    private const int PreconditionRequiredStatusCode = 428;

    /// <summary>
    /// 상품 조회·수정과 간단한 상태 확인 endpoint를 WebApplication에 연결합니다.
    /// </summary>
    /// <param name="app">DI와 middleware 구성이 끝난 ASP.NET Core 애플리케이션입니다.</param>
    /// <returns>같은 WebApplication을 반환하여 추가 route 구성을 이어 갈 수 있게 합니다.</returns>
    // parameter 앞 this는 정적 메서드를 app.MapCatalogEndpoints()처럼 호출하게 하는 extension method 문법입니다.
    // route 조립 코드를 Presentation에 모으면서 Composition Root는 선언적으로 읽히게 합니다.
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // lambda는 이름 없는 짧은 함수를 값처럼 넘기는 문법입니다. 고정 응답 하나뿐인 health route를 간결하게 표현합니다.
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/catalog/{id:guid}", GetAsync);

        // [HttpMethods.Patch]는 collection expression으로 문자열 목록을 만드는 C# 문법입니다.
        // 전체 표현을 교체하는 PUT이 아니라 custom 수정 명령을 적용하므로 PATCH route를 명시합니다.
        app.MapMethods("/catalog/{id:guid}", [HttpMethods.Patch], PatchAsync);
        return app;
    }

    /// <summary>
    /// 상품을 조회하고 If-None-Match가 현재 ETag와 맞으면 본문 없는 304를 반환합니다.
    /// </summary>
    /// <param name="id">route에서 읽은 상품 식별자입니다.</param>
    /// <param name="request">If-None-Match 헤더를 읽을 현재 HTTP 요청입니다.</param>
    /// <param name="response">ETag 응답 헤더를 기록할 현재 HTTP 응답입니다.</param>
    /// <param name="application">상품 조회 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="etagCodec">ETag 문법과 weak 비교를 담당할 Strategy입니다.</param>
    /// <param name="cancellationToken">연결 종료를 Application과 Repository까지 전달하는 토큰입니다.</param>
    /// <returns>200·304·400·404 중 알맞은 HTTP 결과를 담은 Task를 반환합니다.</returns>
    private static async Task<IResult> GetAsync(
        Guid id,
        HttpRequest request,
        HttpResponse response,
        CatalogApplicationService application,
        IEntityTagCodec etagCodec,
        CancellationToken cancellationToken)
    {
        var item = await application.FindAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFoundProblem(id);
        }

        var evaluation = etagCodec.Evaluate(
            request.Headers["If-None-Match"],
            item.Version,
            useStrongComparison: false);

        if (evaluation == EntityTagEvaluation.Invalid)
        {
            return InvalidHeaderProblem(
                "If-None-Match",
                "유효한 ETag, ETag 목록 또는 *를 사용하세요.");
        }

        response.Headers.ETag = etagCodec.Format(item.Version);
        if (evaluation == EntityTagEvaluation.Match)
        {
            // 304는 client가 가진 표현을 재사용하라는 뜻이므로 JSON 본문을 보내지 않습니다.
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(CatalogItemResponse.From(item));
    }

    /// <summary>
    /// If-Match를 strong 방식으로 검사하고 원래 조건이 허용하는 최신 버전에 custom patch를 적용합니다.
    /// </summary>
    /// <param name="id">route에서 읽은 수정 대상 상품 식별자입니다.</param>
    /// <param name="body">JSON에서 bind된 이름과 가격이며 JSON null도 검증하기 위해 nullable입니다.</param>
    /// <param name="request">필수 If-Match 헤더를 읽을 현재 HTTP 요청입니다.</param>
    /// <param name="response">최신 ETag를 기록할 현재 HTTP 응답입니다.</param>
    /// <param name="application">조회와 원자적 수정 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="etagCodec">ETag 문법과 strong 비교를 담당할 Strategy입니다.</param>
    /// <param name="cancellationToken">연결 종료를 저장 경계까지 전달하는 토큰입니다.</param>
    /// <returns>200·400·404·412·428·503 중 알맞은 HTTP 결과를 담은 Task를 반환합니다.</returns>
    // ?는 ReviseCatalogRequest 값 자체가 JSON null일 수 있음을 표시해 endpoint 안에서 안전하게 거절하도록 돕습니다.
    private static async Task<IResult> PatchAsync(
        Guid id,
        ReviseCatalogRequest? body,
        HttpRequest request,
        HttpResponse response,
        CatalogApplicationService application,
        IEntityTagCodec etagCodec,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "요청 본문이 필요합니다.",
                detail: "name과 price를 가진 JSON 객체를 보내세요.");
        }

        var current = await application.FindAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFoundProblem(id);
        }

        // StringValues는 같은 이름으로 여러 번 온 header line도 보존합니다. 원본을 잡아 두어 CAS 충돌 뒤에도 같은 조건을 재평가합니다.
        var ifMatch = request.Headers["If-Match"];
        var evaluation = etagCodec.Evaluate(
            ifMatch,
            current.Version,
            useStrongComparison: true);

        if (evaluation == EntityTagEvaluation.Missing)
        {
            response.Headers.ETag = etagCodec.Format(current.Version);
            return Results.Problem(
                statusCode: PreconditionRequiredStatusCode,
                title: "If-Match 헤더가 필요합니다.",
                detail: "먼저 GET으로 최신 ETag를 읽은 뒤 수정 요청에 그대로 보내세요.");
        }

        if (evaluation == EntityTagEvaluation.Invalid)
        {
            return InvalidHeaderProblem(
                "If-Match",
                "유효한 strong ETag, ETag 목록 또는 *를 사용하세요.");
        }

        if (evaluation == EntityTagEvaluation.NoMatch)
        {
            response.Headers.ETag = etagCodec.Format(current.Version);
            return PreconditionFailedProblem();
        }

        // 이 lambda는 Application에 HTTP 문자열을 넘기지 않고 “이 버전이 원래 If-Match에 맞는가?”만 알려 줍니다.
        var result = await application.UpdateAsync(
            id,
            version => etagCodec.Evaluate(ifMatch, version, useStrongComparison: true)
                == EntityTagEvaluation.Match,
            new UpdateCatalogCommand(body.Name, body.Price),
            cancellationToken);

        if (result.Status == UpdateCatalogStatus.NotFound)
        {
            return NotFoundProblem(id);
        }

        if (result.Status == UpdateCatalogStatus.ValidationFailed)
        {
            // ?.와 ??는 오류가 비정상적으로 없더라도 null을 역참조하지 않고 안전한 기본 메시지를 선택합니다.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["catalogItem"] = [result.Error?.Message ?? "상품 입력이 유효하지 않습니다."],
            });
        }

        if (result.Item is null)
        {
            throw new InvalidOperationException("갱신·충돌·재시도 한도 초과 결과에는 최신 상품이 필요합니다.");
        }

        if (result.Status == UpdateCatalogStatus.VersionConflict)
        {
            response.Headers.ETag = etagCodec.Format(result.Item.Version);
            return PreconditionFailedProblem();
        }

        if (result.Status == UpdateCatalogStatus.ContentionLimitExceeded)
        {
            response.Headers.ETag = etagCodec.Format(result.Item.Version);
            response.Headers["Retry-After"] = "1";
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "동시에 수정 중이라 안전하게 저장하지 못했습니다.",
                detail: "1초 뒤 최신 상품과 ETag를 다시 읽고 수정 의도를 재적용하세요.");
        }

        if (result.Status != UpdateCatalogStatus.Updated)
        {
            throw new InvalidOperationException("알 수 없는 Application 수정 결과입니다.");
        }

        response.Headers.ETag = etagCodec.Format(result.Item.Version);
        return Results.Ok(CatalogItemResponse.From(result.Item));
    }

    /// <summary>
    /// 존재하지 않는 상품을 일관된 Problem Details 404 응답으로 바꿉니다.
    /// </summary>
    /// <param name="id">찾지 못한 상품 식별자입니다.</param>
    /// <returns>상품 ID가 detail에 포함된 404 IResult를 반환합니다.</returns>
    private static IResult NotFoundProblem(Guid id)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "상품을 찾을 수 없습니다.",
            detail: $"상품 {id}가 존재하지 않습니다.");
    }

    /// <summary>
    /// HTTP 조건 헤더 문법 오류를 일관된 Problem Details 400 응답으로 바꿉니다.
    /// </summary>
    /// <param name="headerName">잘못된 헤더의 이름입니다.</param>
    /// <param name="guidance">올바른 값을 만들 수 있게 알려 줄 짧은 지침입니다.</param>
    /// <returns>헤더 이름과 수정 지침을 담은 400 IResult를 반환합니다.</returns>
    private static IResult InvalidHeaderProblem(string headerName, string guidance)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"{headerName} 헤더 형식이 잘못되었습니다.",
            detail: guidance);
    }

    /// <summary>
    /// 오래된 ETag 때문에 수정을 적용하지 않았음을 Problem Details 412로 나타냅니다.
    /// </summary>
    /// <returns>최신 상태를 다시 조회하라는 412 IResult를 반환합니다.</returns>
    private static IResult PreconditionFailedProblem()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status412PreconditionFailed,
            title: "ETag 전제 조건이 맞지 않습니다.",
            detail: "다른 요청이 먼저 수정했을 수 있습니다. 최신 상품과 ETag를 다시 읽어 변경을 재적용하세요.");
    }
}

/// <summary>
/// PATCH custom JSON 문서를 받는 Presentation DTO입니다. nullable 이름은 JSON 누락을 Domain 검증으로 전달합니다.
/// positional record의 괄호는 속성과 생성자 parameter를 함께 선언하며, 생성자는 반환값 없이 DTO를 초기화합니다.
/// </summary>
/// <param name="Name">새 상품 이름이며 누락되거나 null일 수 있습니다.</param>
/// <param name="Price">새 원화 가격입니다.</param>
public sealed record ReviseCatalogRequest(string? Name, decimal Price);

/// <summary>
/// 내부 Domain 객체에서 client에 필요한 값만 고른 HTTP 응답 DTO입니다.
/// positional record의 괄호는 속성과 생성자 parameter를 함께 선언하며, 생성자는 반환값 없이 DTO를 초기화합니다.
/// </summary>
/// <param name="Id">상품 식별자입니다.</param>
/// <param name="Name">검증된 상품 이름입니다.</param>
/// <param name="Price">검증된 원화 가격입니다.</param>
/// <param name="Version">학습 확인용 버전이며 실제 동시성 계약은 ETag 헤더입니다.</param>
public sealed record CatalogItemResponse(Guid Id, string Name, decimal Price, long Version)
{
    /// <summary>
    /// Domain 상품을 외부 응답 전용 DTO로 복사합니다.
    /// </summary>
    /// <param name="item">응답으로 변환할 검증 완료 상품입니다.</param>
    /// <returns>HTTP serialization에 사용할 CatalogItemResponse를 반환합니다.</returns>
    public static CatalogItemResponse From(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new CatalogItemResponse(item.Id, item.Name, item.Price, item.Version);
    }
}
