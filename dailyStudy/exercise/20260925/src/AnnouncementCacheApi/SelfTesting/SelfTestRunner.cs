using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AnnouncementCacheApi.Application;
using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;
using AnnouncementCacheApi.Infrastructure;
using AnnouncementCacheApi.Presentation;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AnnouncementCacheApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 Domain부터 실제 HTTP Output Cache까지 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    // target-typed new는 왼쪽 JsonSerializerOptions 타입이 분명해 생성자 타입 이름 반복을 줄입니다.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 빠른 단위 검증과 실제 Kestrel 통합 검증을 차례로 실행하고 프로세스 종료 코드를 만듭니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 첫 실패가 발생하면 메시지를 출력하고 1을 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        try
        {
            ValidateDomainRules();
            ValidateLanguageParsing();
            await ValidateRepositoryOrderingAsync();
            await ValidateApplicationServiceAsync();
            await ValidateHttpOutputCachingAsync();
            Console.WriteLine("SELF-TEST PASSED: domain, application, HTTP output cache");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Repository가 최신 시각 우선과 같은 시각의 Guid 오름차순을 실제로 지키는지 검증합니다.
    /// </summary>
    /// <returns>결정적인 세 seed의 정렬 결과와 읽기 횟수 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateRepositoryOrderingAsync()
    {
        var older = CreateTestAnnouncement(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "이전 공지",
            new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
        var tiedLater = CreateTestAnnouncement(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "같은 시각의 뒤 Guid",
            new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));
        var tiedEarlier = CreateTestAnnouncement(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "같은 시각의 앞 Guid",
            new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryAnnouncementRepository(
            [older, tiedLater, tiedEarlier],
            TimeSpan.Zero);

        var read = await repository.ListAsync(null, CancellationToken.None);
        Assert(read.Value.Count == 3, "정렬 검증용 공지 세 건을 모두 읽어야 합니다.");
        Assert(
            read.Value[0].Id == tiedEarlier.Id &&
            read.Value[1].Id == tiedLater.Id &&
            read.Value[2].Id == older.Id,
            "최신 시각 우선, 같은 시각은 Guid 오름차순이어야 합니다.");
        Assert(repository.ReadCount == 1, "정렬 검증은 Repository를 정확히 한 번 읽어야 합니다.");
    }

    /// <summary>
    /// 공지 팩터리가 필수값, 카테고리 문자, 불변 값 정규화를 일관되게 적용하는지 확인합니다.
    /// </summary>
    /// <returns>검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateDomainRules()
    {
        var missingTitle = Announcement.Create(
            Guid.NewGuid(),
            "  ",
            "본문",
            "general",
            DateTimeOffset.UtcNow);
        Assert(
            !missingTitle.IsSuccess && missingTitle.Error!.Code == "announcement.title.required",
            "빈 제목은 title.required 실패여야 합니다.");

        var invalidCategory = Announcement.Create(
            Guid.NewGuid(),
            "제목",
            "본문",
            "not valid",
            DateTimeOffset.UtcNow);
        Assert(
            !invalidCategory.IsSuccess &&
            invalidCategory.Error!.Code == "announcement.category.invalid",
            "공백이 든 카테고리는 category.invalid 실패여야 합니다.");

        var valid = Announcement.Create(
            Guid.NewGuid(),
            "  제목  ",
            "  본문  ",
            "  RELEASE  ",
            new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.FromHours(9)));
        Assert(valid.IsSuccess, "올바른 공지는 생성되어야 합니다.");
        Assert(valid.Value!.Title == "제목", "제목 앞뒤 공백을 제거해야 합니다.");
        Assert(valid.Value.Category == "release", "카테고리를 소문자로 정규화해야 합니다.");
        Assert(valid.Value.PublishedAtUtc.Offset == TimeSpan.Zero, "게시 시각을 UTC로 정규화해야 합니다.");
    }

    /// <summary>
    /// Accept-Language 품질값, 지역 태그, 미지원 언어의 기본값 처리를 확인합니다.
    /// </summary>
    /// <returns>검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateLanguageParsing()
    {
        Assert(
            LanguagePreferenceParser.Parse("en-US,en;q=0.9,ko;q=0.8") == "en",
            "가장 높은 품질의 en-US는 en 전략을 선택해야 합니다.");
        Assert(
            LanguagePreferenceParser.Parse("fr;q=1, ko-KR;q=0.7") == "ko",
            "미지원 fr을 건너뛰고 ko-KR을 선택해야 합니다.");
        Assert(
            LanguagePreferenceParser.Parse("fr-FR") == "ko",
            "지원 언어가 없으면 기본 ko를 선택해야 합니다.");
    }

    /// <summary>
    /// Application Service가 사전 검증, Strategy 선택, 고정 시각, 취소 전파를 지키는지 확인합니다.
    /// </summary>
    /// <returns>비동기 조회와 생성 검증이 모두 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateApplicationServiceAsync()
    {
        var seed = CreateTestAnnouncement();
        var repository = new InMemoryAnnouncementRepository([seed], TimeSpan.FromMilliseconds(5));
        // 컬렉션 식은 공통 Port를 구현하는 서로 다른 두 Strategy를 짧게 배열로 묶습니다.
        IAnnouncementLocalizationStrategy[] strategies =
        [
            new KoreanAnnouncementLocalizationStrategy(),
            new EnglishAnnouncementLocalizationStrategy()
        ];
        var fixedNow = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var service = new AnnouncementApplicationService(
            repository,
            strategies,
            new FixedTimeProvider(fixedNow));

        var invalidFilter = await service.GetFeedAsync(
            "bad category",
            "ko",
            CancellationToken.None);
        Assert(!invalidFilter.IsSuccess, "잘못된 필터는 실패 Result여야 합니다.");
        Assert(repository.ReadCount == 0, "필터 검증 실패는 Repository를 읽지 않아야 합니다.");

        var englishFeed = await service.GetFeedAsync("GENERAL", "en", CancellationToken.None);
        Assert(englishFeed.IsSuccess, "정상 목록 조회가 성공해야 합니다.");
        Assert(englishFeed.Value!.Language == "en", "영어 Strategy가 선택되어야 합니다.");
        Assert(
            englishFeed.Value.Items[0].CategoryLabel == "General",
            "영어 Strategy가 카테고리 표시명을 바꿔야 합니다.");

        var invalidCreate = await service.CreateAsync(
            new CreateAnnouncementCommand("", "본문", "general"),
            "ko",
            CancellationToken.None);
        Assert(!invalidCreate.IsSuccess, "빈 제목 생성은 실패 Result여야 합니다.");

        var validCreate = await service.CreateAsync(
            new CreateAnnouncementCommand("새 소식", "본문", "general"),
            "ko",
            CancellationToken.None);
        Assert(validCreate.IsSuccess, "올바른 공지 생성이 성공해야 합니다.");
        Assert(
            validCreate.Value!.PublishedAtUtc == fixedNow,
            "주입한 TimeProvider의 고정 시각을 사용해야 합니다.");

        // using var는 메서드가 끝날 때 CancellationTokenSource를 자동 Dispose해 자원을 정리합니다.
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var canceledTask = service.GetFeedAsync(null, "ko", cancellationSource.Token);
        await AssertThrowsAsync<OperationCanceledException>(
            canceledTask,
            "취소 신호는 Repository까지 전달되어야 합니다.");
    }

    /// <summary>
    /// 실제 HTTP 서버에서 cache hit, 정규화 키, resource locking, generation freshness와 비캐시 endpoint를 검증합니다.
    /// </summary>
    /// <returns>서버 시작부터 모든 HTTP 검증과 안전한 종료가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateHttpOutputCachingAsync()
    {
        var repository = new BlockingSnapshotRepository();
        // lambda는 테스트용 service 교체 절차를 값처럼 전달해 운영 Composition Root를 복제하지 않게 합니다.
        var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            services =>
            {
                services.RemoveAll<IAnnouncementRepository>();
                services.AddSingleton<IAnnouncementRepository>(repository);
            });
        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>() ??
                throw new InvalidOperationException("테스트 서버 주소 기능을 찾을 수 없습니다.");
            // LINQ Single은 주소가 정확히 하나라는 테스트 불변식을 값 추출과 함께 검사합니다.
            // 0개나 2개 이상이면 즉시 실패하므로 잘못된 test host 구성을 숨기지 않습니다.
            var address = addresses.Addresses.Single();

            using var client = new HttpClient
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromSeconds(10)
            };

            var health = await client.GetFromJsonAsync<HealthResponse>("/health", JsonOptions);
            Assert(health?.Status == "ok", "health endpoint가 ok를 반환해야 합니다.");
            Assert(await GetReadCountAsync(client) == 0, "새 호스트의 원본 읽기는 0에서 시작해야 합니다.");

            var firstKoreanRelease = await GetFeedAsync(client, "release", "ko-KR");
            var sameKoreanRelease = await GetFeedAsync(client, "release", "ko-KR");
            Assert(
                firstKoreanRelease.RawBody == sameKoreanRelease.RawBody,
                "같은 cache key의 두 응답 body가 완전히 같아야 합니다.");
            Assert(
                firstKoreanRelease.Feed.OriginReadNumber == 1 &&
                sameKoreanRelease.Feed.OriginReadNumber == 1,
                "두 번째 동일 요청은 첫 원본 읽기 결과를 재사용해야 합니다.");
            Assert(await GetReadCountAsync(client) == 1, "동일 요청 두 번은 원본을 한 번만 읽어야 합니다.");

            var canonicalKoreanRelease = await GetFeedAsync(
                client,
                " RELEASE ",
                "ko;q=1, en;q=0.5");
            Assert(
                canonicalKoreanRelease.RawBody == firstKoreanRelease.RawBody,
                "표현만 다른 category와 언어는 정규화된 같은 cache key를 사용해야 합니다.");
            Assert(
                await GetReadCountAsync(client) == 1,
                "정규화된 동등 요청은 Repository를 다시 읽지 않아야 합니다.");

            // 같은 cold key 요청을 겹쳐 보내면 Output Cache의 resource locking이 한 요청만 원본으로 보냅니다.
            var parallelTasks = new Task<FeedHttpResult>[8];
            for (var index = 0; index < parallelTasks.Length; index++)
            {
                parallelTasks[index] = GetFeedAsync(client, "maintenance", "en-US");
            }

            using var lockingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await repository.WaitUntilLockingReadEnteredAsync(lockingTimeout.Token);
            }
            // finally는 gate 도착 확인이 실패하거나 취소되어도 대기 요청을 반드시 풀어 교착과 자원 누수를 막습니다.
            finally
            {
                repository.ReleaseLockingRead();
            }

            var parallelResults = await Task.WhenAll(parallelTasks);
            foreach (var result in parallelResults)
            {
                Assert(
                    result.RawBody == parallelResults[0].RawBody,
                    "병렬 동일 key 요청은 동일한 캐시 body를 받아야 합니다.");
                Assert(
                    result.Feed.OriginReadNumber == 2,
                    "병렬 cold key는 두 번째 원본 읽기 하나로 합쳐져야 합니다.");
            }

            Assert(await GetReadCountAsync(client) == 2, "resource locking은 병렬 원본 읽기를 하나로 줄여야 합니다.");

            var englishRelease = await GetFeedAsync(client, "release", "en-US");
            Assert(englishRelease.Feed.Language == "en", "다른 언어 헤더는 영어 변형을 만들어야 합니다.");
            Assert(await GetReadCountAsync(client) == 3, "Accept-Language가 다르면 별도 캐시 항목이어야 합니다.");

            var koreanMaintenance = await GetFeedAsync(client, "maintenance", "ko-KR");
            Assert(koreanMaintenance.Feed.Language == "ko", "한국어 maintenance 변형이 만들어져야 합니다.");
            Assert(await GetReadCountAsync(client) == 4, "category가 다르면 별도 캐시 항목이어야 합니다.");

            var announcementId = firstKoreanRelease.Feed.Items[0].Id;
            await GetAnnouncementAsync(client, announcementId, "ko-KR");
            await GetAnnouncementAsync(client, announcementId, "ko-KR");
            Assert(await GetReadCountAsync(client) == 6, "상세 endpoint는 두 요청을 모두 원본에서 읽어야 합니다.");

            var missingProblem = await GetMissingAnnouncementProblemAsync(client);
            Assert(
                missingProblem.Code == "announcement.not_found",
                "없는 상세는 안정적인 not-found 코드를 반환해야 합니다.");
            Assert(await GetReadCountAsync(client) == 7, "없는 상세 조회도 원본 읽기로 기록되어야 합니다.");

            var invalidPost = await PostAnnouncementAsync(
                client,
                new CreateAnnouncementRequest("잘못된 공지", "본문", "bad category"),
                "ko-KR");
            Assert(invalidPost.StatusCode == HttpStatusCode.BadRequest, "잘못된 POST는 400이어야 합니다.");
            Assert(
                invalidPost.Problem?.Code == "announcement.category.invalid",
                "잘못된 POST는 category.invalid 코드를 반환해야 합니다.");

            var stillCached = await GetFeedAsync(client, "release", "ko-KR");
            Assert(
                stillCached.RawBody == firstKoreanRelease.RawBody,
                "검증 실패 POST는 목록 캐시를 제거하면 안 됩니다.");
            Assert(await GetReadCountAsync(client) == 7, "실패 POST 뒤 동일 목록은 cache hit여야 합니다.");

            var validPost = await PostAnnouncementAsync(
                client,
                new CreateAnnouncementRequest("세대 전환 확인", "새 release 공지입니다.", "release"),
                "ko-KR");
            Assert(validPost.StatusCode == HttpStatusCode.Created, "올바른 POST는 201이어야 합니다.");
            Assert(validPost.Item is not null, "201 응답에는 생성된 공지가 있어야 합니다.");
            var createdItem = validPost.Item ??
                throw new InvalidOperationException("201 성공 공지가 누락되었습니다.");
            Assert(
                validPost.Location?.OriginalString == $"/announcements/{createdItem.Id:D}",
                "201 Location은 생성된 공지 상세 URL이어야 합니다.");
            Assert(await GetReadCountAsync(client) == 7, "쓰기는 원본 읽기 횟수를 늘리지 않아야 합니다.");

            var refreshedKorean = await GetFeedAsync(client, "release", "ko-KR");
            Assert(
                ContainsTitle(refreshedKorean.Feed, "세대 전환 확인"),
                "성공 POST 뒤 한국어 목록은 새 공지를 포함해야 합니다.");
            Assert(await GetReadCountAsync(client) == 8, "세대 전환 뒤 한국어 변형을 다시 읽어야 합니다.");

            var refreshedEnglish = await GetFeedAsync(client, "release", "en-US");
            Assert(
                ContainsTitle(refreshedEnglish.Feed, "세대 전환 확인"),
                "generation은 영어 변형도 새 key로 전환해야 합니다.");
            Assert(await GetReadCountAsync(client) == 9, "영어 변형도 세대 전환 뒤 다시 읽어야 합니다.");

            const string raceTitle = "진행 중 GET 세대 보호 확인";
            var oldGenerationGet = GetFeedAsync(client, "race", "ko-KR");
            using var raceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            CreateHttpResult racePost;
            try
            {
                await repository.WaitUntilRaceSnapshotCapturedAsync(raceTimeout.Token);
                racePost = await PostAnnouncementAsync(
                    client,
                    new CreateAnnouncementRequest(raceTitle, "세대 key 경합 검증 본문", "race"),
                    "ko-KR");
            }
            finally
            {
                // gate 대기나 POST가 예외로 끝나도 old GET을 풀어 test server가 안전하게 종료되게 합니다.
                repository.ReleaseRaceSnapshot();
            }

            Assert(racePost.StatusCode == HttpStatusCode.Created, "경합 검증용 POST는 201이어야 합니다.");
            var staleCandidate = await oldGenerationGet;
            Assert(
                !ContainsTitle(staleCandidate.Feed, raceTitle),
                "쓰기 전에 잡은 진행 중 GET snapshot은 새 공지를 포함하지 않아야 합니다.");

            var readsBeforeGenerationRefresh = await GetReadCountAsync(client);
            var generationProtected = await GetFeedAsync(client, "race", "ko-KR");
            Assert(
                ContainsTitle(generationProtected.Feed, raceTitle),
                "POST 완료 뒤 GET은 뒤늦게 저장된 이전 세대 응답을 재사용하면 안 됩니다.");
            Assert(
                await GetReadCountAsync(client) == readsBeforeGenerationRefresh + 1,
                "새 세대 GET은 Repository에서 최신 snapshot을 정확히 한 번 읽어야 합니다.");
        }
        finally
        {
            // finally는 중간 단언이 실패해도 포트와 서버 자원이 반드시 정리되게 합니다.
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// 지정한 category와 원문 Accept-Language로 목록을 요청해 역직렬화 값과 정확한 body를 함께 보존합니다.
    /// </summary>
    /// <param name="client">임시 테스트 서버를 가리키는 HttpClient입니다.</param>
    /// <param name="category">캐시 query 변형에 사용할 카테고리입니다.</param>
    /// <param name="acceptLanguage">캐시 header 변형과 Strategy 선택에 사용할 원문 헤더입니다.</param>
    /// <returns>성공 목록과 body 문자열을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<FeedHttpResult> GetFeedAsync(
        HttpClient client,
        string category,
        string acceptLanguage)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/announcements?category={Uri.EscapeDataString(category)}");
        request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

        using var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert(response.StatusCode == HttpStatusCode.OK, $"목록 상태가 200이 아닙니다: {rawBody}");
        var feed = Deserialize<AnnouncementFeedResponse>(rawBody, "목록 JSON을 읽을 수 없습니다.");
        return new FeedHttpResult(feed, rawBody);
    }

    /// <summary>
    /// 공지 상세를 요청해 200 응답을 역직렬화합니다.
    /// </summary>
    /// <param name="client">테스트 서버 HttpClient입니다.</param>
    /// <param name="id">조회할 공지 식별자입니다.</param>
    /// <param name="acceptLanguage">표시 언어를 고를 헤더입니다.</param>
    /// <returns>상세 공지를 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<AnnouncementItemResponse> GetAnnouncementAsync(
        HttpClient client,
        Guid id,
        string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/announcements/{id:D}");
        request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        using var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert(response.StatusCode == HttpStatusCode.OK, $"상세 상태가 200이 아닙니다: {rawBody}");
        return Deserialize<AnnouncementItemResponse>(rawBody, "상세 JSON을 읽을 수 없습니다.");
    }

    /// <summary>
    /// 존재하지 않는 고정 식별자를 조회해 404 문제 계약을 확인할 값으로 읽습니다.
    /// </summary>
    /// <param name="client">테스트 서버 HttpClient입니다.</param>
    /// <returns>404 body의 문제 응답을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<ApiProblemResponse> GetMissingAnnouncementProblemAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            "/announcements/ffffffff-ffff-ffff-ffff-ffffffffffff");
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert(response.StatusCode == HttpStatusCode.NotFound, "없는 공지는 404여야 합니다.");
        return Deserialize<ApiProblemResponse>(rawBody, "404 문제 JSON을 읽을 수 없습니다.");
    }

    /// <summary>
    /// POST 요청에 언어 헤더를 더해 보내고 성공/실패 계약을 하나의 관찰값으로 읽습니다.
    /// </summary>
    /// <param name="client">테스트 서버 HttpClient입니다.</param>
    /// <param name="body">JSON으로 직렬화할 생성 요청입니다.</param>
    /// <param name="acceptLanguage">생성 응답 표시 언어입니다.</param>
    /// <returns>상태, Location, 선택적인 성공 공지와 문제 응답을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<CreateHttpResult> PostAnnouncementAsync(
        HttpClient client,
        CreateAnnouncementRequest body,
        string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/announcements")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

        using var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();
        var location = response.Headers.Location;

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var item = Deserialize<AnnouncementItemResponse>(rawBody, "201 공지 JSON을 읽을 수 없습니다.");
            return new CreateHttpResult(response.StatusCode, location, item, null);
        }

        var problem = Deserialize<ApiProblemResponse>(rawBody, "실패 문제 JSON을 읽을 수 없습니다.");
        return new CreateHttpResult(response.StatusCode, location, null, problem);
    }

    /// <summary>
    /// 캐시되지 않는 진단 endpoint에서 현재 원본 읽기 횟수를 가져옵니다.
    /// </summary>
    /// <param name="client">테스트 서버 HttpClient입니다.</param>
    /// <returns>Repository의 현재 누적 읽기 횟수를 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<long> GetReadCountAsync(HttpClient client)
    {
        var diagnostics = await client.GetFromJsonAsync<RepositoryDiagnosticsResponse>(
            "/diagnostics/repository-reads",
            JsonOptions);
        return diagnostics?.ReadCount ??
            throw new InvalidOperationException("진단 JSON을 읽을 수 없습니다.");
    }

    /// <summary>
    /// 목록에 기대하는 제목의 공지가 하나라도 있는지 반복문으로 검사합니다.
    /// </summary>
    /// <param name="feed">확인할 목록 응답입니다.</param>
    /// <param name="title">정확히 일치해야 할 제목입니다.</param>
    /// <returns>제목을 찾으면 true, 끝까지 없으면 false를 반환합니다.</returns>
    private static bool ContainsTitle(AnnouncementFeedResponse feed, string title)
    {
        foreach (var item in feed.Items)
        {
            if (string.Equals(item.Title, title, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 테스트용으로 검증된 general 공지 한 건을 만듭니다.
    /// </summary>
    /// <returns>고정 식별자와 시각을 가진 Announcement를 반환합니다.</returns>
    private static Announcement CreateTestAnnouncement()
    {
        return CreateTestAnnouncement(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "테스트 공지",
            new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// 지정한 식별자·제목·시각으로 유효한 general 테스트 공지를 만듭니다.
    /// </summary>
    /// <param name="id">정렬과 식별 검증에 사용할 고정 Guid입니다.</param>
    /// <param name="title">실패 위치를 읽기 쉽게 구분할 공지 제목입니다.</param>
    /// <param name="publishedAtUtc">정렬 순서를 결정할 UTC 게시 시각입니다.</param>
    /// <returns>Domain factory 검증을 통과한 Announcement를 반환합니다.</returns>
    private static Announcement CreateTestAnnouncement(
        Guid id,
        string title,
        DateTimeOffset publishedAtUtc)
    {
        var result = Announcement.Create(
            id,
            title,
            "테스트 본문",
            "general",
            publishedAtUtc);
        Assert(result.IsSuccess, "테스트 seed 자체가 유효해야 합니다.");
        return result.Value!;
    }

    /// <summary>
    /// JSON 문자열을 지정한 참조 형식으로 바꾸고 null 또는 문법 오류를 읽기 쉬운 테스트 실패로 바꿉니다.
    /// </summary>
    /// <typeparam name="T">역직렬화할 참조 형식입니다.</typeparam>
    /// <param name="json">HTTP 응답에서 읽은 JSON 문자열입니다.</param>
    /// <param name="failureMessage">값을 만들지 못했을 때 표시할 설명입니다.</param>
    /// <returns>역직렬화에 성공한 T 인스턴스를 반환합니다.</returns>
    // where T : class는 JSON 결과가 null일 수 있는 참조 형식만 T로 받겠다는 generic constraint입니다.
    private static T Deserialize<T>(string json, string failureMessage)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ??
            throw new InvalidOperationException(failureMessage);
    }

    /// <summary>
    /// 이미 시작된 비동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 Exception 파생 형식입니다.</typeparam>
    /// <param name="task">실패해야 하는 비동기 작업입니다.</param>
    /// <param name="failureMessage">예외가 없거나 다른 형식일 때 표시할 설명입니다.</param>
    /// <returns>기대한 예외를 확인하면 정상 완료되는 Task를 반환합니다.</returns>
    // where TException : Exception은 예외 계층 타입만 검사 대상으로 받겠다는 generic constraint입니다.
    private static async Task AssertThrowsAsync<TException>(Task task, string failureMessage)
        where TException : Exception
    {
        try
        {
            await task;
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(failureMessage);
    }

    /// <summary>
    /// 조건이 거짓이면 현재 자체 테스트를 즉시 실패시킵니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 검증 조건입니다.</param>
    /// <param name="message">실패 원인을 설명할 메시지입니다.</param>
    /// <returns>조건을 검사할 뿐 값을 만들지 않으므로 반환값은 없습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 목록 값과 직렬화된 정확한 body를 함께 비교하기 위한 내부 불변 관찰값입니다.
    /// </summary>
    /// <param name="Feed">역직렬화된 목록 계약입니다.</param>
    /// <param name="RawBody">캐시가 재사용해야 하는 원문 JSON입니다.</param>
    private sealed record FeedHttpResult(AnnouncementFeedResponse Feed, string RawBody);

    /// <summary>
    /// 생성 HTTP 호출의 성공 또는 실패 정보를 함께 보존하는 내부 불변 관찰값입니다.
    /// </summary>
    /// <param name="StatusCode">실제 HTTP 상태 코드입니다.</param>
    /// <param name="Location">201일 때 생성된 상세 URL이며 실패 시 보통 null입니다.</param>
    /// <param name="Item">201 성공 body이며 실패일 때 null입니다.</param>
    /// <param name="Problem">실패 body이며 성공일 때 null입니다.</param>
    private sealed record CreateHttpResult(
        HttpStatusCode StatusCode,
        Uri? Location,
        AnnouncementItemResponse? Item,
        ApiProblemResponse? Problem);

    /// <summary>
    /// maintenance는 원본 진입 전에, race는 쓰기 전 snapshot 뒤에 멈춰 두 동시성 순서를 재현하는 test double입니다.
    /// </summary>
    private sealed class BlockingSnapshotRepository : IAnnouncementRepository
    {
        private readonly InMemoryAnnouncementRepository _inner = new();
        // RunContinuationsAsynchronously는 gate를 연 스레드에서 후속 코드를 즉시 길게 실행하지 않게 합니다.
        private readonly TaskCompletionSource<bool> _lockingReadEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseLockingRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _raceSnapshotCaptured =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseRaceSnapshot =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 내부 실제 Repository가 수행한 목록·상세 읽기 횟수를 그대로 제공합니다.
        /// </summary>
        public long ReadCount => _inner.ReadCount;

        /// <summary>
        /// maintenance는 실제 읽기 전 resource-lock gate에서, race는 snapshot 생성 뒤 generation-race gate에서 멈춥니다.
        /// </summary>
        /// <param name="normalizedCategory">null 또는 정규화된 category이며 maintenance/race에 각각 다른 gate를 사용합니다.</param>
        /// <param name="cancellationToken">대기 중 테스트 취소를 전달할 신호입니다.</param>
        /// <returns>해당 gate가 열리면 실제 또는 쓰기 전에 잡아 둔 Repository snapshot을 반환합니다.</returns>
        public async Task<RepositoryRead<IReadOnlyList<Announcement>>> ListAsync(
            string? normalizedCategory,
            CancellationToken cancellationToken)
        {
            if (string.Equals(normalizedCategory, "maintenance", StringComparison.Ordinal))
            {
                _lockingReadEntered.TrySetResult(true);
                await _releaseLockingRead.Task.WaitAsync(cancellationToken);
            }

            var snapshot = await _inner.ListAsync(normalizedCategory, cancellationToken);
            if (string.Equals(normalizedCategory, "race", StringComparison.Ordinal))
            {
                _raceSnapshotCaptured.TrySetResult(true);
                await _releaseRaceSnapshot.Task.WaitAsync(cancellationToken);
            }

            return snapshot;
        }

        /// <summary>
        /// resource-locking 시나리오의 첫 원본 호출이 test gate에 도착할 때까지 기다립니다.
        /// </summary>
        /// <param name="cancellationToken">미도착 시 제한 시간에 테스트를 실패시킬 신호입니다.</param>
        /// <returns>첫 maintenance 원본 호출이 도착하면 완료되는 Task를 반환합니다.</returns>
        public Task WaitUntilLockingReadEnteredAsync(CancellationToken cancellationToken)
        {
            return _lockingReadEntered.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 멈춰 둔 maintenance 원본 호출을 풀어 병렬 요청이 한 결과를 공유하는지 확인하게 합니다.
        /// </summary>
        /// <returns>gate 상태만 바꾸므로 반환값은 없습니다.</returns>
        public void ReleaseLockingRead()
        {
            _releaseLockingRead.TrySetResult(true);
        }

        /// <summary>
        /// 상세 조회는 경합 대상이 아니므로 실제 in-memory Adapter에 그대로 위임합니다.
        /// </summary>
        /// <param name="id">찾을 공지 식별자입니다.</param>
        /// <param name="cancellationToken">실제 조회 지연을 중단할 신호입니다.</param>
        /// <returns>내부 Repository의 읽기 순번과 상세 결과를 담은 Task를 반환합니다.</returns>
        public Task<RepositoryRead<Announcement?>> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return _inner.FindByIdAsync(id, cancellationToken);
        }

        /// <summary>
        /// 생성 요청의 저장은 실제 thread-safe in-memory Adapter에 그대로 위임합니다.
        /// </summary>
        /// <param name="announcement">Domain 검증을 마친 불변 공지입니다.</param>
        /// <param name="cancellationToken">저장 전에 요청 중단을 확인할 신호입니다.</param>
        /// <returns>내부 Repository의 저장 성공 또는 중복 실패 Result를 담은 Task를 반환합니다.</returns>
        public Task<Result<Announcement>> AddAsync(
            Announcement announcement,
            CancellationToken cancellationToken)
        {
            return _inner.AddAsync(announcement, cancellationToken);
        }

        /// <summary>
        /// race 목록이 쓰기 전 snapshot을 확보할 때까지 결정적으로 기다립니다.
        /// </summary>
        /// <param name="cancellationToken">테스트가 교착되면 제한 시간에 실패시킬 신호입니다.</param>
        /// <returns>snapshot 확보 신호가 오면 완료되는 Task를 반환합니다.</returns>
        public Task WaitUntilRaceSnapshotCapturedAsync(CancellationToken cancellationToken)
        {
            return _raceSnapshotCaptured.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 멈춰 둔 race 목록이 이전 세대 응답을 endpoint로 반환하도록 gate를 엽니다.
        /// </summary>
        /// <returns>gate 상태만 바꾸므로 반환값은 없습니다.</returns>
        public void ReleaseRaceSnapshot()
        {
            _releaseRaceSnapshot.TrySetResult(true);
        }
    }

    /// <summary>
    /// 테스트가 원하는 UTC 시각을 항상 돌려주는 교체 가능한 TimeProvider입니다.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        /// <summary>
        /// 모든 GetUtcNow 호출에 반환할 고정 UTC 시각을 보관합니다.
        /// </summary>
        /// <param name="utcNow">테스트 시나리오의 결정적인 현재 시각입니다.</param>
        /// <returns>생성자는 값을 반환하지 않고 테스트가 사용할 고정 UTC 시각을 보관합니다.</returns>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        /// <summary>
        /// 시스템 시계 대신 생성자에서 받은 고정 시각을 제공합니다.
        /// </summary>
        /// <returns>항상 같은 UTC DateTimeOffset을 반환합니다.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
