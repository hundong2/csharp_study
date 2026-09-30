using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using WorkshopContractApi.Application;
using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Domain;
using WorkshopContractApi.Infrastructure;
using WorkshopContractApi.Presentation;

namespace WorkshopContractApi.SelfTesting;

/// <summary>
/// 외부 테스트 framework 없이 Domain, Repository 동시성, Application 흐름, 실제 OpenAPI/HTTP 계약을 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 순수 규칙부터 실제 Kestrel black-box 계약까지 모든 회귀 검증을 차례로 실행합니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 첫 실패를 출력하면 1을 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            ValidateDomainAndStrategy();
            await ValidateRepositoryConcurrencyAsync();
            await ValidateApplicationServiceAsync();
            await ValidateOpenApiAndHttpContractsAsync();
            Console.WriteLine($"SELF-TEST PASSED: {_passedAssertions} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception}");
            return 1;
        }
    }

    /// <summary>
    /// nullable 입력 정규화, 범위 검증, enum 해석, 가격 Strategy의 경계를 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateDomainAndStrategy()
    {
        var valid = RegistrationDraft.Create(
            " reg-unit-1 ",
            " csharp-101 ",
            " student-1 ",
            2,
            "premium");
        Assert(valid.IsSuccess, "유효한 외부 입력은 Domain 초안이 된다.");
        // !는 바로 앞 성공 assertion으로 값이 null이 아님을 확인했다고 nullable 분석기에 알립니다.
        Assert(valid.Value!.RegistrationId == "REG-UNIT-1", "예약 ID는 trim 후 대문자로 정규화된다.");
        Assert(valid.Value.SessionId == "CSHARP-101", "회차 ID도 같은 규칙으로 정규화된다.");
        Assert(valid.Value.Tier == AttendeeTier.Premium, "대소문자가 다른 premium 문자열도 enum으로 해석된다.");

        var missingId = RegistrationDraft.Create(null, "CSHARP-101", "STUDENT-1", 1, "Standard");
        Assert(
            !missingId.IsSuccess && missingId.Error!.Code == "registration.id.invalid",
            "누락한 예약 ID는 예외가 아니라 필드별 Result 오류다.");

        var invalidCharacter = RegistrationDraft.Create("REG/1", "CSHARP-101", "STUDENT-1", 1, "Standard");
        Assert(
            !invalidCharacter.IsSuccess && invalidCharacter.Error!.Field == "registrationId",
            "식별자 허용 문자 위반은 정확한 JSON 필드를 가리킨다.");

        var invalidSeat = RegistrationDraft.Create("REG-2", "CSHARP-101", "STUDENT-1", 0, "Standard");
        Assert(
            !invalidSeat.IsSuccess && invalidSeat.Error!.Code == "registration.seat.out_of_range",
            "0번 좌석은 공개 입력 범위에서 거절된다.");

        var invalidTier = RegistrationDraft.Create("REG-3", "CSHARP-101", "STUDENT-1", 1, "Vip");
        Assert(
            !invalidTier.IsSuccess && invalidTier.Error!.Code == "registration.tier.invalid",
            "문서에 없는 Vip 등급은 안정 오류로 거절된다.");

        var numericTier = RegistrationDraft.Create("REG-4", "CSHARP-101", "STUDENT-1", 1, "1");
        Assert(
            !numericTier.IsSuccess && numericTier.Error!.Code == "registration.tier.invalid",
            "enum 내부 숫자값 1은 공개 등급 이름이 아니므로 거절된다.");

        var policy = new StandardTicketPricePolicy();
        var session = new WorkshopSession("CSHARP-101", "C#", 3, 50_000);
        Assert(policy.CalculatePriceWon(session, AttendeeTier.Standard) == 50_000, "Standard는 정가를 낸다.");
        Assert(policy.CalculatePriceWon(session, AttendeeTier.Premium) == 40_000, "Premium은 20% 할인된다.");
        var largePriceSession = new WorkshopSession("LARGE-PRICE", "큰 가격", 1, int.MaxValue);
        Assert(policy.CalculatePriceWon(largePriceSession, AttendeeTier.Premium) == 1_717_986_917,
            "Premium 계산은 최종값이 int 범위면 중간 곱셈 overflow 없이 처리된다.");

        var invalidSession = new WorkshopSession("BROKEN", "잘못된 가격", 1, -1);
        AssertThrows<InvalidOperationException>(
            () => policy.CalculatePriceWon(invalidSession, AttendeeTier.Standard),
            "음수 기본 가격은 조용히 공개 응답이 되지 않고 계약 위반 예외가 된다.");
    }

    /// <summary>
    /// 같은 회차·좌석을 동시에 저장해도 정확히 한 요청만 성공하는지 확인합니다.
    /// </summary>
    /// <returns>두 비동기 저장의 결과를 모두 확인하면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateRepositoryConcurrencyAsync()
    {
        var repository = new InMemoryRegistrationRepository();
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var first = new WorkshopRegistration(
            "REG-CONCURRENT-1", "CSHARP-101", "C#", "STUDENT-1", 1,
            AttendeeTier.Standard, 50_000, now);
        // with는 원본 record를 바꾸지 않고 지정한 속성만 다른 새 복사본을 만듭니다.
        var second = first with
        {
            RegistrationId = "REG-CONCURRENT-2",
            AttendeeId = "STUDENT-2"
        };

        // using var는 scope가 끝날 때 두 동기화 객체의 Dispose를 자동 호출합니다.
        using var readyGate = new CountdownEvent(2);
        using var startGate = new ManualResetEventSlim(false);
        // Task.Run은 서로 다른 thread-pool 작업으로 두 호출을 예약합니다. readyGate는 둘 다 준비될 때까지 기다리고,
        // startGate는 같은 순간에 출발시켜 겹친 실행을 시도합니다. scheduler의 모든 interleaving을 증명하는 테스트는 아닙니다.
        var firstTask = Task.Run(async () =>
        {
            readyGate.Signal();
            startGate.Wait();
            return await repository.TryAddAsync(first, CancellationToken.None);
        });
        var secondTask = Task.Run(async () =>
        {
            readyGate.Signal();
            startGate.Wait();
            return await repository.TryAddAsync(second, CancellationToken.None);
        });

        Assert(readyGate.Wait(TimeSpan.FromSeconds(5)), "두 저장 작업이 제한 시간 안에 경쟁 시작점에 도착한다.");
        startGate.Set();
        // Task.WhenAll은 두 작업이 모두 끝난 뒤 각 결과를 배열로 돌려줍니다.
        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Assert(outcomes.Count(outcome => outcome == RegistrationSaveOutcome.Added) == 1,
            "같은 좌석의 동시 저장 중 하나만 추가된다.");
        Assert(outcomes.Count(outcome => outcome == RegistrationSaveOutcome.SeatAlreadyTaken) == 1,
            "경쟁에서 진 요청은 예외가 아니라 좌석 선점 결과를 받는다.");
    }

    /// <summary>
    /// Application Service가 검증, Port 조회, Strategy, 원자 저장, 조회를 올바른 순서로 조율하는지 확인합니다.
    /// </summary>
    /// <returns>성공과 404·409 경계 검증이 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateApplicationServiceAsync()
    {
        var fixedNow = new DateTimeOffset(2026, 10, 1, 1, 2, 3, TimeSpan.Zero);
        var service = CreateService(fixedNow);
        var command = CreateCommand("REG-SERVICE-1", seatNumber: 1);

        var created = await service.RegisterAsync(command, CancellationToken.None);
        Assert(created.IsSuccess, "Application Service는 유효한 예약을 확정한다.");
        Assert(created.Value!.PriceWon == 40_000, "Application Service가 Premium 가격 Strategy를 적용한다.");
        Assert(created.Value.ReservedAtUtc == fixedNow, "교체 가능한 TimeProvider로 예약 시각이 결정적이다.");

        var found = await service.FindAsync(" reg-service-1 ", CancellationToken.None);
        Assert(found.IsSuccess && found.Value == created.Value, "조회도 같은 ID 정규화 규칙을 사용한다.");

        var duplicateId = await service.RegisterAsync(
            CreateCommand("REG-SERVICE-1", seatNumber: 2),
            CancellationToken.None);
        Assert(
            !duplicateId.IsSuccess && duplicateId.Error!.Code == "registration.id.duplicate",
            "같은 예약 ID 재사용은 409에 대응할 충돌 Result다.");

        var seatTaken = await service.RegisterAsync(
            CreateCommand("REG-SERVICE-2", seatNumber: 1),
            CancellationToken.None);
        Assert(
            !seatTaken.IsSuccess && seatTaken.Error!.Code == "registration.seat.taken",
            "다른 예약 ID라도 같은 회차 좌석은 충돌한다.");

        var missingSession = await service.RegisterAsync(
            CreateCommand("REG-SERVICE-3", seatNumber: 1) with { SessionId = "MISSING-SESSION" },
            CancellationToken.None);
        Assert(
            !missingSession.IsSuccess && missingSession.Error!.Code == "workshop.session.not_found",
            "카탈로그에 없는 회차는 404에 대응할 Result다.");

        var beyondCapacity = await service.RegisterAsync(
            CreateCommand("REG-SERVICE-4", seatNumber: 4),
            CancellationToken.None);
        Assert(
            !beyondCapacity.IsSuccess && beyondCapacity.Error!.Code == "workshop.seat.out_of_range",
            "공통 최대 200을 통과해도 실제 회차 정원을 넘으면 거절된다.");
    }

    /// <summary>
    /// 실제 Production Kestrel에서 OpenAPI 3.1 문서와 201·200·400·404·409·415 응답 계약이 일치하는지 확인합니다.
    /// </summary>
    /// <returns>서버 시작, 문서와 HTTP 검증, 안전한 종료가 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateOpenApiAndHttpContractsAsync()
    {
        // []는 인수가 없는 string 배열을 만드는 collection expression입니다.
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            environmentName: Environments.Production);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);

            using (var documentResponse = await client.GetAsync("/openapi/v1.json"))
            {
                Assert(documentResponse.StatusCode == HttpStatusCode.OK, "OpenAPI JSON endpoint는 HTTP 200이다.");
                var json = await documentResponse.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                AssertOpenApiDocument(document.RootElement);
            }

            var validRequest = CreateRequest("REG-HTTP-1", seatNumber: 1);
            using (var created = await client.PostAsJsonAsync("/registrations", validRequest))
            {
                Assert(created.StatusCode == HttpStatusCode.Created, "문서의 201 응답이 실제 유효 요청에서 발생한다.");
                Assert(created.Headers.Location?.ToString() == "/registrations/REG-HTTP-1",
                    "201 Location은 정규화된 예약 조회 URL이다.");
                var body = await created.Content.ReadFromJsonAsync<RegistrationResponse>();
                Assert(body?.PriceWon == 40_000, "201 JSON은 문서화한 RegistrationResponse 가격을 담는다.");
            }

            var whitespaceRequest = new CreateRegistrationRequest(
                " reg-http-space ",
                " csharp-101 ",
                " student-space ",
                2,
                " premium ");
            using (var normalized = await client.PostAsJsonAsync("/registrations", whitespaceRequest))
            {
                Assert(normalized.StatusCode == HttpStatusCode.Created,
                    "schema가 허용한 앞뒤 공백 입력은 runtime에서도 201이다.");
                Assert(normalized.Headers.Location?.ToString() == "/registrations/REG-HTTP-SPACE",
                    "공백 입력의 Location에는 trim·대문자화한 ID가 들어간다.");
                var body = await normalized.Content.ReadFromJsonAsync<RegistrationResponse>();
                Assert(body is
                {
                    RegistrationId: "REG-HTTP-SPACE",
                    SessionId: "CSHARP-101",
                    AttendeeId: "STUDENT-SPACE",
                    AttendeeTier: "Premium"
                }, "공백을 허용한 schema와 Domain 정규화 결과가 일치한다.");
            }

            using (var read = await client.GetAsync("/registrations/reg-http-1"))
            {
                Assert(read.StatusCode == HttpStatusCode.OK, "생성한 예약은 문서의 200 계약으로 조회된다.");
            }

            using (var invalid = await client.PostAsJsonAsync(
                       "/registrations",
                       CreateRequest("bad/id", seatNumber: 1)))
            {
                await AssertProblemAsync(
                    invalid,
                    HttpStatusCode.BadRequest,
                    "registration.id.invalid",
                    "/registrations",
                    "registrationId",
                    "registrationId은(는) 3~40자의 영문, 숫자, 하이픈만 사용할 수 있습니다.");
            }

            using (var missing = await client.PostAsJsonAsync(
                       "/registrations",
                       CreateRequest("REG-HTTP-2", seatNumber: 1) with { SessionId = "UNKNOWN-SESSION" }))
            {
                await AssertProblemAsync(
                    missing,
                    HttpStatusCode.NotFound,
                    "workshop.session.not_found",
                    "/registrations",
                    "sessionId");
            }

            using (var conflict = await client.PostAsJsonAsync(
                       "/registrations",
                       CreateRequest("REG-HTTP-3", seatNumber: 1)))
            {
                await AssertProblemAsync(
                    conflict,
                    HttpStatusCode.Conflict,
                    "registration.seat.taken",
                    "/registrations",
                    "seatNumber");
            }

            using (var duplicate = await client.PostAsJsonAsync(
                       "/registrations",
                       CreateRequest("REG-HTTP-1", seatNumber: 2)))
            {
                await AssertProblemAsync(
                    duplicate,
                    HttpStatusCode.Conflict,
                    "registration.id.duplicate",
                    "/registrations",
                    "registrationId");
            }

            using (var absent = await client.GetAsync("/registrations/REG-NOT-FOUND"))
            {
                await AssertProblemAsync(
                    absent,
                    HttpStatusCode.NotFound,
                    "registration.not_found",
                    "/registrations/REG-NOT-FOUND",
                    "registrationId");
            }

            using (var invalidRouteId = await client.GetAsync("/registrations/x"))
            {
                await AssertProblemAsync(
                    invalidRouteId,
                    HttpStatusCode.BadRequest,
                    "registration.id.invalid",
                    "/registrations/x",
                    "registrationId");
            }

            using (var malformedJson = await client.PostAsync(
                       "/registrations",
                       new StringContent("{", Encoding.UTF8, "application/json")))
            {
                await AssertProblemAsync(
                    malformedJson,
                    HttpStatusCode.BadRequest,
                    "http.bad_request",
                    "/registrations",
                    "request",
                    "요청 JSON 형식과 값의 자료형을 확인하세요.");
            }

            using (var nullJson = await client.PostAsync(
                       "/registrations",
                       new StringContent("null", Encoding.UTF8, "application/json")))
            {
                await AssertProblemAsync(
                    nullJson,
                    HttpStatusCode.BadRequest,
                    "http.bad_request",
                    "/registrations",
                    "request");
            }

            // """는 따옴표 escape를 줄이는 raw string literal이며 테스트 JSON을 원문처럼 읽게 합니다.
            const string quotedSeatJson =
                """{"registrationId":"REG-HTTP-4","sessionId":"CSHARP-101","attendeeId":"STUDENT-HTTP","seatNumber":"2","attendeeTier":"Premium"}""";
            using (var quotedSeat = await client.PostAsync(
                       "/registrations",
                       new StringContent(quotedSeatJson, Encoding.UTF8, "application/json")))
            {
                await AssertProblemAsync(
                    quotedSeat,
                    HttpStatusCode.BadRequest,
                    "http.bad_request",
                    "/registrations",
                    "request");
            }

            using (var wrongMediaType = await client.PostAsync(
                       "/registrations",
                       new StringContent("{}", Encoding.UTF8, "text/plain")))
            {
                await AssertProblemAsync(
                    wrongMediaType,
                    HttpStatusCode.UnsupportedMediaType,
                    "http.unsupported_media_type",
                    "/registrations",
                    "request");
            }
        }
        finally
        {
            if (started)
            {
                await app.StopAsync();
            }
        }
    }

    /// <summary>
    /// OpenAPI 버전, transformer 정보, path, operationId, 응답·header·요청·오류 schema를 확인합니다.
    /// </summary>
    /// <param name="root">파싱한 OpenAPI JSON의 최상위 요소입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없으며 어긋나면 예외를 던집니다.</returns>
    private static void AssertOpenApiDocument(JsonElement root)
    {
        Assert(root.GetProperty("openapi").GetString() == "3.1.1",
            ".NET 10 기본 문서는 OpenAPI 3.1.1이다.");
        var info = root.GetProperty("info");
        Assert(info.GetProperty("title").GetString() == "워크숍 등록 계약 API",
            "Document Transformer가 API 제목을 넣는다.");
        Assert(info.GetProperty("version").GetString() == "v1",
            "Info의 v1은 문서 이름이며 URL 기반 API versioning을 자동 제공하지 않는다.");

        var paths = root.GetProperty("paths");
        Assert(paths.EnumerateObject().Count() == 2, "상태 확인 endpoint를 제외한 업무 path 두 개만 문서화된다.");
        var collectionPath = paths.GetProperty("/registrations");
        var createOperation = collectionPath.GetProperty("post");
        Assert(createOperation.GetProperty("operationId").GetString() == "CreateWorkshopRegistration",
            "WithName이 안정적인 POST operationId가 된다.");
        Assert(createOperation.GetProperty("summary").GetString() == "워크숍 좌석 예약 생성",
            "WithSummary가 사람이 읽는 작업 요약이 된다.");
        // out _는 성공 여부만 필요하고 찾은 값은 쓰지 않을 때 결과 값을 버리는 discard 문법입니다.
        Assert(createOperation.TryGetProperty("requestBody", out _), "POST 문서에는 JSON requestBody schema가 있다.");

        var createResponses = createOperation.GetProperty("responses");
        Assert(createResponses.TryGetProperty("201", out var createdResponse), "POST 문서가 201 성공을 선언한다.");
        Assert(createResponses.TryGetProperty("400", out _), "POST 문서가 400 검증 오류를 선언한다.");
        Assert(createResponses.TryGetProperty("404", out _), "POST 문서가 404 회차 미존재를 선언한다.");
        Assert(createResponses.TryGetProperty("409", out _), "POST 문서가 409 좌석 충돌을 선언한다.");
        Assert(createResponses.TryGetProperty("415", out _), "POST 문서가 415 media type 오류를 선언한다.");

        var locationHeader = createdResponse.GetProperty("headers").GetProperty("Location");
        Assert(locationHeader.GetProperty("required").GetBoolean(), "201 문서의 Location header는 필수다.");
        Assert(locationHeader.GetProperty("schema").GetProperty("format").GetString() == "uri-reference",
            "Location은 상대 URI도 허용하는 uri-reference 형식이다.");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var requestSchema = schemas.GetProperty("CreateRegistrationRequest");
        // Select는 JSON 배열에서 각 문자열만 꺼내고, ToHashSet은 순서와 무관하게 필수 필드를 비교하게 합니다.
        var requiredRequestFields = requestSchema.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        // []는 C# 12+ collection expression으로 비교할 문자열 집합을 짧게 만듭니다.
        Assert(requiredRequestFields.SetEquals(
                ["registrationId", "sessionId", "attendeeId", "seatNumber", "attendeeTier"]),
            "POST 요청 schema의 다섯 필드는 모두 required다.");
        Assert(requestSchema.GetProperty("properties").GetProperty("seatNumber").GetProperty("type").GetString() ==
            "integer", "엄격한 JSON 숫자 설정과 OpenAPI seatNumber 형식이 integer로 일치한다.");
        Assert(requestSchema.GetProperty("properties").GetProperty("registrationId").GetProperty("pattern")
            .GetString() == @"^\s*[A-Za-z0-9-]{3,40}\s*$",
            "요청 ID schema는 Domain처럼 앞뒤 공백 제거 후 3~40자를 허용한다.");

        var problemSchema = schemas.GetProperty("ApiProblemResponse");
        var problemProperties = problemSchema.GetProperty("properties");
        Assert(problemProperties.TryGetProperty("code", out _), "오류 schema에 안정적인 code가 명시된다.");
        Assert(problemProperties.TryGetProperty("field", out _), "오류 schema에 관련 field가 명시된다.");
        Assert(problemProperties.TryGetProperty("traceId", out _), "오류 schema에 진단용 traceId가 명시된다.");
        var problemReference = createResponses.GetProperty("400")
            .GetProperty("content")
            .GetProperty("application/problem+json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert(problemReference == "#/components/schemas/ApiProblemResponse",
            "400 metadata는 runtime 확장 모양을 설명하는 전용 오류 schema를 참조한다.");

        var itemPath = paths.GetProperty("/registrations/{registrationId}");
        var getOperation = itemPath.GetProperty("get");
        Assert(getOperation.GetProperty("operationId").GetString() == "GetWorkshopRegistration",
            "GET도 명시한 operationId를 가진다.");
        // Single은 조건에 맞는 항목이 정확히 하나가 아니면 실패해 중복·누락 parameter drift도 잡습니다.
        var registrationIdParameter = getOperation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "registrationId");
        Assert(registrationIdParameter.GetProperty("schema").GetProperty("pattern").GetString() ==
            @"^\s*[A-Za-z0-9-]{3,40}\s*$",
            "GET registrationId path parameter도 Domain 정규화 규칙을 문서화한다.");
        var getResponses = getOperation.GetProperty("responses");
        Assert(getResponses.TryGetProperty("200", out _), "GET 문서가 200 조회 성공을 선언한다.");
        Assert(getResponses.TryGetProperty("400", out _), "GET 문서가 잘못된 route ID의 400을 선언한다.");
        Assert(getResponses.TryGetProperty("404", out _), "GET 문서가 예약 미존재 404를 선언한다.");
    }

    /// <summary>
    /// HTTP 오류의 상태, Problem Details 핵심 필드, 안정 code가 예상 계약과 같은지 확인합니다.
    /// </summary>
    /// <param name="response">검사할 실제 HTTP 응답입니다.</param>
    /// <param name="expectedStatus">기대하는 400, 404, 409, 415 상태입니다.</param>
    /// <param name="expectedCode">클라이언트가 분기할 기대 안정 코드입니다.</param>
    /// <param name="expectedInstance">오류가 발생한 기대 요청 경로입니다.</param>
    /// <param name="expectedField">관련 입력 필드 또는 요청 전체를 뜻하는 request입니다.</param>
    /// <param name="expectedDetail">문구 보존까지 확인할 때 사용할 선택적 안전 설명입니다.</param>
    /// <returns>JSON 본문 검증이 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedInstance,
        string expectedField,
        string? expectedDetail = null)
    {
        Assert(response.StatusCode == expectedStatus, $"실제 오류 상태는 {(int)expectedStatus} {expectedStatus}이다.");
        Assert(
            response.Content.Headers.ContentType?.MediaType == "application/problem+json",
            "Problem Details 응답 media type은 application/problem+json이다.");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert(root.GetProperty("status").GetInt32() == (int)expectedStatus,
            "Problem Details status가 HTTP 상태와 같다.");
        Assert(root.GetProperty("code").GetString() == expectedCode,
            $"Problem Details code는 {expectedCode}이다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()),
            "Problem Details에 사람이 읽는 title이 있다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()),
            "Problem Details에 안전한 detail이 있다.");
        Assert(root.GetProperty("instance").GetString() == expectedInstance,
            $"Problem Details instance는 {expectedInstance}이다.");
        Assert(root.GetProperty("field").GetString() == expectedField,
            $"Problem Details field는 {expectedField}이다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()),
            "Problem Details에 서버 로그와 연결할 traceId가 있다.");
        if (expectedDetail is not null)
        {
            Assert(root.GetProperty("detail").GetString() == expectedDetail,
                "Problem Details가 계층에 맞는 안전한 구체 detail을 보존한다.");
        }
    }

    /// <summary>
    /// 단위 검증마다 독립된 Adapter와 고정 시계를 가진 Application Service를 만듭니다.
    /// </summary>
    /// <param name="utcNow">예약 결과에 기록할 결정적인 UTC 시각입니다.</param>
    /// <returns>실제 Domain/Application 흐름을 실행할 WorkshopRegistrationService를 반환합니다.</returns>
    private static WorkshopRegistrationService CreateService(DateTimeOffset utcNow)
    {
        return new WorkshopRegistrationService(
            new DemoWorkshopCatalog(),
            new InMemoryRegistrationRepository(),
            new StandardTicketPricePolicy(),
            new FixedTimeProvider(utcNow));
    }

    /// <summary>
    /// Premium CSHARP-101 예약을 만드는 단위/Application 테스트 명령을 만듭니다.
    /// </summary>
    /// <param name="registrationId">테스트끼리 충돌하지 않을 예약 ID입니다.</param>
    /// <param name="seatNumber">검증하려는 좌석 번호입니다.</param>
    /// <returns>Application Service에 전달할 RegisterWorkshopCommand를 반환합니다.</returns>
    private static RegisterWorkshopCommand CreateCommand(string registrationId, int seatNumber)
    {
        return new RegisterWorkshopCommand(
            registrationId,
            "CSHARP-101",
            "STUDENT-1",
            seatNumber,
            "Premium");
    }

    /// <summary>
    /// Premium CSHARP-101 예약을 만드는 HTTP 테스트 DTO를 만듭니다.
    /// </summary>
    /// <param name="registrationId">HTTP 시나리오마다 구분할 예약 ID입니다.</param>
    /// <param name="seatNumber">요청 JSON에 넣을 좌석 번호입니다.</param>
    /// <returns>JSON 직렬화할 CreateRegistrationRequest를 반환합니다.</returns>
    private static CreateRegistrationRequest CreateRequest(string registrationId, int seatNumber)
    {
        return new CreateRegistrationRequest(
            registrationId,
            "CSHARP-101",
            "STUDENT-HTTP",
            seatNumber,
            "Premium");
    }

    /// <summary>
    /// 실제 Kestrel 주소를 BaseAddress로 사용하는 짧은 timeout의 HttpClient를 만듭니다.
    /// </summary>
    /// <param name="services">실행 중 서버와 주소 기능을 찾을 DI container입니다.</param>
    /// <returns>통합 검증용 HttpClient를 반환합니다.</returns>
    private static HttpClient CreateClient(IServiceProvider services)
    {
        var server = services.GetRequiredService<IServer>();
        // ?? throw는 왼쪽 기능이 없을 때 즉시 이해 가능한 예외를 던지는 null 병합 표현입니다.
        var addresses = server.Features.Get<IServerAddressesFeature>() ??
            throw new InvalidOperationException("Kestrel 주소 기능을 찾을 수 없습니다.");
        // Single은 테스트 server가 정확히 한 주소를 가져야 한다는 전제까지 검증합니다.
        var address = addresses.Addresses.Single();

        return new HttpClient
        {
            BaseAddress = new Uri(address, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// 동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">호출할 동기 작업입니다.</param>
    /// <param name="message">성공과 실패 때 보여 줄 학습용 계약 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    // where는 TException에 Exception 계층만 올 수 있게 하는 generic 제약입니다.
    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            Assert(true, message);
            return;
        }

        throw new InvalidOperationException($"검증 실패: {message} (예외가 발생하지 않음)");
    }

    /// <summary>
    /// 조건이 참인지 확인하고 통과한 assertion 수를 누적합니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 조건입니다.</param>
    /// <param name="message">성공 시에도 출력할 이해 가능한 계약 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없으며 false이면 예외를 던집니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }

        _passedAssertions++;
        Console.WriteLine($"  [PASS] {message}");
    }

    /// <summary>
    /// 테스트가 실제 시계와 실행 시각에 의존하지 않도록 고정 UTC 시각을 제공하는 TimeProvider입니다.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        /// <summary>
        /// 이후 모든 GetUtcNow 호출에서 돌려줄 고정 시각을 저장합니다.
        /// </summary>
        /// <param name="utcNow">테스트가 기대할 UTC 시각입니다.</param>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <summary>
        /// 생성자에서 받은 고정 UTC 시각을 반환합니다.
        /// </summary>
        /// <returns>실제 시간이 지나도 바뀌지 않는 DateTimeOffset을 반환합니다.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
