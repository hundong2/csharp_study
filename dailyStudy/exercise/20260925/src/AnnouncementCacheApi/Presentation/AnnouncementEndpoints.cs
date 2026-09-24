using AnnouncementCacheApi.Application;
using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;
using Microsoft.AspNetCore.OutputCaching;

namespace AnnouncementCacheApi.Presentation;

/// <summary>
/// 공지 유스케이스를 HTTP 경로, 상태 코드, JSON 계약에 연결하는 Presentation Adapter입니다.
/// </summary>
public static class AnnouncementEndpoints
{
    private static readonly TimeSpan CacheCleanupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 공지 목록·상세·생성, 진단, 상태 확인 endpoint를 애플리케이션에 등록합니다.
    /// </summary>
    /// <param name="endpoints">Minimal API 경로를 추가할 endpoint route builder입니다.</param>
    /// <returns>같은 builder에 경로를 추가한 뒤 반환값 없이 끝납니다.</returns>
    // 첫 파라미터 앞 this는 builder.MapAnnouncementEndpoints()처럼 호출할 수 있게 하는 extension method 문법입니다.
    public static void MapAnnouncementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", GetHealth);
        endpoints.MapGet("/diagnostics/repository-reads", GetRepositoryReads);

        // 목록에만 이름 있는 정책을 붙여 상세·쓰기·진단 응답이 실수로 캐시되지 않게 합니다.
        endpoints
            .MapGet("/announcements", GetFeedAsync)
            .CacheOutput(CacheNames.AnnouncementFeedPolicy);

        endpoints.MapGet("/announcements/{id:guid}", GetByIdAsync);
        endpoints.MapPost("/announcements", CreateAsync);
    }

    /// <summary>
    /// 카테고리와 Accept-Language를 Application Service에 전달해 캐시 가능한 목록 응답을 만듭니다.
    /// </summary>
    /// <param name="category">query string의 선택적인 카테고리 필터입니다.</param>
    /// <param name="request">Accept-Language 헤더를 읽을 현재 HTTP 요청입니다.</param>
    /// <param name="service">목록 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결이 끊기면 조회를 중단할 신호입니다.</param>
    /// <returns>성공 시 200 목록, 검증 실패 시 400 문제 응답을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<IResult> GetFeedAsync(
        string? category,
        HttpRequest request,
        AnnouncementApplicationService service,
        CancellationToken cancellationToken)
    {
        var language = LanguagePreferenceParser.Parse(request.Headers.AcceptLanguage);
        var result = await service.GetFeedAsync(category, language, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemResult(result.Error!);
        }

        var feed = result.Value!;
        var items = new List<AnnouncementItemResponse>(feed.Items.Count);
        foreach (var announcement in feed.Items)
        {
            items.Add(AnnouncementItemResponse.FromApplication(announcement));
        }

        return Results.Ok(
            new AnnouncementFeedResponse(feed.Language, feed.OriginReadNumber, items));
    }

    /// <summary>
    /// 식별자 상세를 매번 원본 Repository에서 읽어 최신 단건 응답을 만듭니다.
    /// </summary>
    /// <param name="id">route의 공지 Guid입니다.</param>
    /// <param name="request">표시 언어를 선택할 Accept-Language 헤더가 있는 요청입니다.</param>
    /// <param name="service">상세 조회 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="cancellationToken">원본 읽기를 중단할 수 있는 요청 취소 신호입니다.</param>
    /// <returns>찾으면 200 공지, 없으면 404, 잘못된 입력이면 400을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<IResult> GetByIdAsync(
        Guid id,
        HttpRequest request,
        AnnouncementApplicationService service,
        CancellationToken cancellationToken)
    {
        var language = LanguagePreferenceParser.Parse(request.Headers.AcceptLanguage);
        var result = await service.GetByIdAsync(id, language, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemResult(result.Error!);
        }

        return Results.Ok(AnnouncementItemResponse.FromApplication(result.Value!));
    }

    /// <summary>
    /// 새 공지를 검증·저장하고 성공한 경우 세대를 올린 뒤 이전 목록 cache tag 정리를 시도합니다.
    /// </summary>
    /// <param name="requestBody">JSON body에서 바인딩된 아직 검증하지 않은 생성 요청입니다.</param>
    /// <param name="request">생성 응답 언어를 결정할 HTTP 요청입니다.</param>
    /// <param name="service">생성 유스케이스를 수행할 Application Service입니다.</param>
    /// <param name="outputCacheStore">태그에 속한 이전 세대 캐시 항목을 정리할 ASP.NET Core 저장소입니다.</param>
    /// <param name="cacheGeneration">저장 뒤 새 GET을 새 cache key 세대로 보내는 process 단위 보호 장치입니다.</param>
    /// <param name="applicationLifetime">client 연결과 무관한 cache 정리 수명 신호를 제공하는 server 수명입니다.</param>
    /// <param name="loggerFactory">tag 정리 실패를 숨기지 않고 운영 로그에 남길 logger를 만드는 factory입니다.</param>
    /// <param name="cancellationToken">검증과 저장이 끝나기 전까지 요청 취소를 전달하는 신호입니다.</param>
    /// <returns>성공 시 Location이 있는 201, 검증 실패 시 400을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<IResult> CreateAsync(
        CreateAnnouncementRequest requestBody,
        HttpRequest request,
        AnnouncementApplicationService service,
        IOutputCacheStore outputCacheStore,
        AnnouncementFeedCacheGeneration cacheGeneration,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var command = new CreateAnnouncementCommand(
            requestBody.Title,
            requestBody.Body,
            requestBody.Category);
        var language = LanguagePreferenceParser.Parse(request.Headers.AcceptLanguage);
        var result = await service.CreateAsync(command, language, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemResult(result.Error!);
        }

        // 저장 성공 직후 세대를 먼저 올리면, 진행 중이던 옛 GET이 나중에 응답을 저장해도
        // 새 GET은 새 세대 key만 사용하므로 그 오래된 cache entry를 다시 읽지 않습니다.
        cacheGeneration.Advance();

        // 저장이 끝난 뒤의 정리 작업을 RequestAborted에 묶으면 client 연결 종료가 정리만 취소할 수 있습니다.
        // 그래서 server 종료 신호와 짧은 내부 timeout을 사용하고, 새 세대가 정확성을 지키는 동안 tag는 메모리를 정리합니다.
        // using var는 메서드가 끝날 때 CancellationTokenSource.Dispose를 자동 호출해 timer 자원을 정리합니다.
        using var cleanupCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            applicationLifetime.ApplicationStopping);
        cleanupCancellation.CancelAfter(CacheCleanupTimeout);

        try
        {
            await outputCacheStore.EvictByTagAsync(
                CacheTags.AnnouncementFeed,
                cleanupCancellation.Token);
        }
        // when은 지정한 조건까지 참인 예외만 이 catch에서 처리하는 exception filter입니다.
        catch (OperationCanceledException) when (cleanupCancellation.IsCancellationRequested)
        {
            var logger = loggerFactory.CreateLogger("AnnouncementFeedCacheCleanup");
            logger.LogWarning("공지 저장 뒤 Output Cache tag 정리가 제한 시간 초과 또는 서버 종료로 취소되었습니다.");
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("AnnouncementFeedCacheCleanup");
            logger.LogError(exception, "공지 저장 뒤 Output Cache tag 정리에 실패했습니다.");
        }

        var response = AnnouncementItemResponse.FromApplication(result.Value!);
        return Results.Created($"/announcements/{response.Id:D}", response);
    }

    /// <summary>
    /// 캐시 효과를 숫자로 확인할 수 있도록 현재 원본 읽기 횟수를 반환합니다.
    /// </summary>
    /// <param name="repository">동일한 singleton Repository Port입니다.</param>
    /// <returns>캐시하지 않는 200 진단 응답을 반환합니다.</returns>
    private static IResult GetRepositoryReads(IAnnouncementRepository repository)
    {
        return Results.Ok(new RepositoryDiagnosticsResponse(repository.ReadCount));
    }

    /// <summary>
    /// 서버 프로세스가 HTTP 요청을 받을 수 있는지 확인하는 가벼운 상태 응답을 만듭니다.
    /// </summary>
    /// <returns><c>{ "status": "ok" }</c> 모양의 캐시하지 않는 200 응답을 반환합니다.</returns>
    private static IResult GetHealth()
    {
        return Results.Ok(new HealthResponse("ok"));
    }

    /// <summary>
    /// Domain 오류 코드를 적절한 HTTP 상태와 공통 문제 JSON으로 변환합니다.
    /// </summary>
    /// <param name="error">Application Service가 반환한 예상 가능한 오류입니다.</param>
    /// <returns>not-found는 404, 중복은 409, 나머지 검증 오류는 400 IResult를 반환합니다.</returns>
    private static IResult ToProblemResult(DomainError error)
    {
        var problem = new ApiProblemResponse(error.Code, error.Message);
        return error.Code switch
        {
            "announcement.not_found" => Results.NotFound(problem),
            "announcement.id.duplicate" => Results.Conflict(problem),
            _ => Results.BadRequest(problem)
        };
    }
}
