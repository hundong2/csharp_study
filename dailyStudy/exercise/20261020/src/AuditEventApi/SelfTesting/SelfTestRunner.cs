using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuditEventApi.Application;
using AuditEventApi.Application.Ports;
using AuditEventApi.Domain;
using AuditEventApi.Infrastructure;
using AuditEventApi.Presentation;
using AuditEventApi.Serialization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AuditEventApi.SelfTesting;

/// <summary>
/// 별도 테스트 framework 없이 source-generated JSON, Domain, Strategy, Application, Repository 경계를 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passed;

    /// <summary>
    /// 모든 자체 테스트를 순서대로 실행하고 사람이 읽을 수 있는 PASS/FAIL 요약을 출력합니다.
    /// </summary>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1인 프로세스 종료 코드를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passed = 0;
        try
        {
            VerifySourceGeneratedStrictJson();
            VerifyUserSignedInDomain();
            VerifyDocumentAndPermissionDomain();
            VerifyDomainLengthBoundaries();
            VerifyResultGuards();
            await VerifyApplicationAndDuplicateRejectionAsync();
            await VerifyAtomicParallelDuplicateAsync();
            await VerifyCancellationAsync();
            VerifyCompositionGuards();
            await VerifyUnexpectedExceptionHttpRedactionAsync();
            Console.WriteLine($"[SELF-TEST PASS] {_passed} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[SELF-TEST FAIL] {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>
    /// 생성된 다형성 metadata가 세 DTO를 고르고 .NET 10 엄격 옵션이 모호한 JSON을 거절하는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifySourceGeneratedStrictJson()
    {
        // var는 오른쪽 값에서 지역 변수 형식을 추론합니다. 실제 형식은 JsonSerializerOptions로 고정되며 dynamic이 아닙니다.
        var options = CreateStrictJsonOptions();
        Assert(
            AuditJsonContext.Default.AuditEventRequest.Type == typeof(AuditEventRequest),
            "source generator가 기반 요청의 JsonTypeInfo를 만든다");

        // 따옴표 세 개로 감싼 raw string literal은 JSON의 큰따옴표를 \"로 반복해서 피하지 않고 원래 모양대로 쓰게 합니다.
        const string validJson = """
            {
              "type": "document-changed",
              "eventId": "EVT-JSON-1",
              "occurredAtUtc": "2026-10-20T01:00:00Z",
              "actorId": "EDITOR-1",
              "documentId": "DOC-1",
              "changeSummary": "제목을 수정함"
            }
            """;
        var request = JsonSerializer.Deserialize<AuditEventRequest>(validJson, options);
        Assert(request is DocumentChangedRequest, "type 판별자가 올바른 하위 record를 선택한다");
        // property pattern은 형식 확인과 함께 필요한 속성 값까지 한 번에 검사해 잘못된 하위 DTO를 놓치지 않습니다.
        Assert(
            request is DocumentChangedRequest { DocumentId: "DOC-1", ChangeSummary: "제목을 수정함" },
            "source-generated metadata로 종류별 필드를 읽는다");

        const string typeLastJson = """
            {
              "eventId": "EVT-JSON-TYPE-LAST",
              "occurredAtUtc": "2026-10-20T01:00:00Z",
              "actorId": "USER-1",
              "ipAddress": "192.0.2.40",
              "succeeded": true,
              "type": "user-signed-in"
            }
            """;
        var typeLastRequest = JsonSerializer.Deserialize<AuditEventRequest>(typeLastJson, options);
        Assert(typeLastRequest is UserSignedInRequest, "type 판별자가 뒤에 있어도 본문 상한 안에서는 올바른 하위 record를 선택한다");

        const string missingDiscriminatorJson = """
            {
              "eventId": "EVT-JSON-MISSING-TYPE",
              "occurredAtUtc": "2026-10-20T01:00:00Z",
              "actorId": "EDITOR-1",
              "documentId": "DOC-1",
              "changeSummary": "제목을 수정함"
            }
            """;

        AssertJsonRejected(
            validJson.Replace("document-changed", "server-internal-type", StringComparison.Ordinal),
            options,
            "허용 목록 밖 discriminator를 거절한다");
        AssertJsonRejected(
            missingDiscriminatorJson,
            options,
            "type discriminator 누락을 거절한다");
        AssertJsonRejected(
            validJson.Replace("\"document-changed\"", "null", StringComparison.Ordinal),
            options,
            "null type discriminator를 거절한다");
        AssertJsonRejected(
            validJson.Replace("\"document-changed\"", "42", StringComparison.Ordinal),
            options,
            "문자열이 아닌 type discriminator를 거절한다");
        AssertJsonRejected(
            validJson.Replace("\"documentId\"", "\"unknownDocumentId\"", StringComparison.Ordinal),
            options,
            "알 수 없는 JSON property를 거절한다");
        AssertJsonRejected(
            validJson.Replace(
                "\"actorId\": \"EDITOR-1\"",
                "\"actorId\": \"EDITOR-1\", \"actorId\": \"ATTACKER\"",
                StringComparison.Ordinal),
            options,
            "같은 JSON property의 중복을 거절한다");
        AssertJsonRejected(
            validJson.Replace("\"eventId\"", "\"EventId\"", StringComparison.Ordinal),
            options,
            "property 이름의 잘못된 대소문자를 거절한다");
        AssertJsonRejected(
            validJson.Replace("\"actorId\": \"EDITOR-1\",", string.Empty, StringComparison.Ordinal),
            options,
            "required property 누락을 거절한다");
    }

    /// <summary>
    /// 로그인 Domain 팩터리가 공통 값과 IP·성공 여부를 정규화하고 잘못된 입력을 막는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifyUserSignedInDomain()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
        var valid = AuditEventDraft.CreateUserSignedIn(
            " evt-login.1 ",
            occurredAt,
            " user-7 ",
            " 2001:db8::1 ",
            false);

        Assert(valid.IsSuccess, "올바른 로그인 사건을 Domain 초안으로 만든다");
        Assert(valid.Value?.EventId == "EVT-LOGIN.1", "eventId를 대문자로 정규화한다");
        Assert(valid.Value?.ActorId == "user-7", "actorId의 양끝 공백을 제거한다");
        Assert(valid.Value?.Details["ipAddress"] == "2001:db8::1", "IP 주소를 표준 문자열로 보관한다");
        Assert(valid.Value?.Details["succeeded"] == "false", "실패한 로그인 결과를 명시적으로 보관한다");
        Assert(valid.Value?.Summary.Contains("실패", StringComparison.Ordinal) == true, "안전한 로그인 요약을 만든다");

        var invalidIp = AuditEventDraft.CreateUserSignedIn(
            "EVT-LOGIN-2",
            occurredAt,
            "USER-7",
            "not-an-ip",
            true);
        Assert(invalidIp.Error?.Code == "audit.invalid_ip_address", "잘못된 IP를 안정적인 오류로 거절한다");

        var missingOutcome = AuditEventDraft.CreateUserSignedIn(
            "EVT-LOGIN-3",
            occurredAt,
            "USER-7",
            "127.0.0.1",
            null);
        Assert(missingOutcome.Error?.Field == "succeeded", "누락된 로그인 결과의 field를 알려 준다");

        var nonUtc = AuditEventDraft.CreateUserSignedIn(
            "EVT-LOGIN-4",
            occurredAt.ToOffset(TimeSpan.FromHours(9)),
            "USER-7",
            "127.0.0.1",
            true);
        Assert(nonUtc.Error?.Code == "audit.invalid_occurred_at", "UTC가 아닌 발생 시각을 거절한다");

        var invalidId = AuditEventDraft.CreateUserSignedIn(
            "EVT/LOGIN/5",
            occurredAt,
            "USER-7",
            "127.0.0.1",
            true);
        Assert(invalidId.Error?.Field == "eventId", "허용하지 않은 식별자 문자를 거절한다");
    }

    /// <summary>
    /// 문서·권한 Strategy가 각자 필요한 값만 검증하고 종류별 불변 세부 정보를 만드는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifyDocumentAndPermissionDomain()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 20, 1, 5, 0, TimeSpan.Zero);
        var document = new DocumentChangedAuditEventMapper().Map(
            new IngestAuditEventCommand(
                AuditEventDraft.DocumentChangedKind,
                "EVT-DOC-1",
                occurredAt,
                "EDITOR-1",
                DocumentId: " DOC-42 ",
                ChangeSummary: " 제목과 공개 범위를 수정함 "));
        Assert(document.IsSuccess, "문서 변경 Mapper가 Domain 팩터리에 연결된다");
        Assert(document.Value?.Details["documentId"] == "DOC-42", "문서 식별자를 정규화한다");
        Assert(document.Value?.Summary.Contains("제목과 공개 범위", StringComparison.Ordinal) == true, "한글 변경 요약을 보존한다");

        var shortSummary = AuditEventDraft.CreateDocumentChanged(
            "EVT-DOC-2",
            occurredAt,
            "EDITOR-1",
            "DOC-42",
            "한");
        Assert(shortSummary.Error?.Code == "audit.invalid_change_summary", "너무 짧은 변경 요약을 거절한다");

        var permission = new PermissionChangedAuditEventMapper().Map(
            new IngestAuditEventCommand(
                AuditEventDraft.PermissionChangedKind,
                "EVT-PERM-1",
                occurredAt,
                "ADMIN-1",
                TargetUserId: " USER-9 ",
                Permission: " Document.Read ",
                Action: " GRANT "));
        Assert(permission.IsSuccess, "권한 변경 Mapper가 grant 입력을 처리한다");
        Assert(permission.Value?.Details["permission"] == "document.read", "권한 이름을 소문자로 정규화한다");
        Assert(permission.Value?.Details["action"] == "grant", "action을 안정적인 문자열로 정규화한다");

        var invalidAction = AuditEventDraft.CreatePermissionChanged(
            "EVT-PERM-2",
            occurredAt,
            "ADMIN-1",
            "USER-9",
            "document.read",
            "approve");
        Assert(invalidAction.Error?.Field == "action", "허용 목록 밖 권한 동작을 거절한다");
    }

    /// <summary>
    /// Domain 문자열 규칙의 최소·최대 경계와 공백 입력을 검사해 길이 비교의 off-by-one 회귀를 막습니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifyDomainLengthBoundaries()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 20, 1, 10, 0, TimeSpan.Zero);
        var maximumCommon = AuditEventDraft.CreateUserSignedIn(
            new string('E', 80),
            occurredAt,
            new string('A', 80),
            "192.0.2.30",
            true);
        Assert(maximumCommon.IsSuccess, "eventId와 actorId의 정확한 최대 길이 80자를 허용한다");

        var tooLongEventId = AuditEventDraft.CreateUserSignedIn(
            new string('E', 81),
            occurredAt,
            "ACTOR-1",
            "192.0.2.31",
            true);
        Assert(tooLongEventId.Error?.Code == "audit.invalid_event_id", "81자 eventId를 거절한다");

        var blankActor = AuditEventDraft.CreateUserSignedIn(
            "EVT-BLANK-ACTOR",
            occurredAt,
            "   ",
            "192.0.2.32",
            true);
        Assert(blankActor.Error?.Code == "audit.invalid_actor_id", "공백뿐인 actorId를 거절한다");

        var tooLongActor = AuditEventDraft.CreateUserSignedIn(
            "EVT-LONG-ACTOR",
            occurredAt,
            new string('A', 81),
            "192.0.2.33",
            true);
        Assert(tooLongActor.Error?.Field == "actorId", "81자 actorId를 거절한다");

        var controlCharacterActor = AuditEventDraft.CreateUserSignedIn(
            "EVT-CONTROL-ACTOR",
            occurredAt,
            "ACTOR-1\nFORGED-LINE",
            "192.0.2.34",
            true);
        Assert(controlCharacterActor.Error?.Code == "audit.invalid_actor_id", "줄바꿈이 든 actorId를 거절한다");

        var maximumDocument = AuditEventDraft.CreateDocumentChanged(
            "EVT-DOC-BOUNDARY",
            occurredAt,
            "EDITOR-1",
            new string('D', 120),
            new string('요', 200));
        Assert(maximumDocument.IsSuccess, "documentId 120자와 changeSummary 200자 경계를 허용한다");

        var tooLongDocumentId = AuditEventDraft.CreateDocumentChanged(
            "EVT-DOC-ID-LONG",
            occurredAt,
            "EDITOR-1",
            new string('D', 121),
            "정상 요약");
        Assert(tooLongDocumentId.Error?.Code == "audit.invalid_document_id", "121자 documentId를 거절한다");

        var tooLongSummary = AuditEventDraft.CreateDocumentChanged(
            "EVT-DOC-SUMMARY-LONG",
            occurredAt,
            "EDITOR-1",
            "DOC-1",
            new string('요', 201));
        Assert(tooLongSummary.Error?.Code == "audit.invalid_change_summary", "201자 changeSummary를 거절한다");

        var controlCharacterSummary = AuditEventDraft.CreateDocumentChanged(
            "EVT-DOC-CONTROL",
            occurredAt,
            "EDITOR-1",
            "DOC-1",
            "정상처럼 보임\t숨은 열");
        Assert(
            controlCharacterSummary.Error?.Code == "audit.invalid_change_summary",
            "탭 제어문자가 든 changeSummary를 거절한다");

        var maximumPermission = AuditEventDraft.CreatePermissionChanged(
            "EVT-PERM-BOUNDARY",
            occurredAt,
            "ADMIN-1",
            new string('T', 80),
            new string('P', 80),
            "revoke");
        Assert(maximumPermission.IsSuccess, "targetUserId와 permission의 정확한 최대 길이 80자를 허용한다");

        var tooLongTarget = AuditEventDraft.CreatePermissionChanged(
            "EVT-PERM-TARGET-LONG",
            occurredAt,
            "ADMIN-1",
            new string('T', 81),
            "document.read",
            "grant");
        Assert(tooLongTarget.Error?.Code == "audit.invalid_target_user_id", "81자 targetUserId를 거절한다");

        var tooLongPermission = AuditEventDraft.CreatePermissionChanged(
            "EVT-PERM-NAME-LONG",
            occurredAt,
            "ADMIN-1",
            "USER-1",
            new string('P', 81),
            "grant");
        Assert(tooLongPermission.Error?.Code == "audit.invalid_permission", "81자 permission을 거절한다");
    }

    /// <summary>
    /// Result factory가 성공 값과 실패 오류 중 정확히 하나만 가진 상태를 강제하는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifyResultGuards()
    {
        var success = Result<string>.Success("saved");
        Assert(success is { IsSuccess: true, Value: "saved", Error: null }, "Result 성공은 값만 가진다");

        var expectedError = new Error("audit.test_failure", "테스트 실패입니다.", "test");
        var failure = Result<string>.Failure(expectedError);
        Assert(
            !failure.IsSuccess && failure.Value is null && ReferenceEquals(failure.Error, expectedError),
            "Result 실패는 오류만 가진다");

        // null!은 실제 null을 넘기되 이 줄에서 의도한 guard 검증이라는 사실을 nullable compiler에 알려 주는 테스트 전용 표기입니다.
        AssertThrows<ArgumentNullException>(
            () => _ = Result<string>.Success(null!),
            "Result.Success는 null 성공 값을 거절한다");
        AssertThrows<ArgumentNullException>(
            () => _ = Result<string>.Failure(null!),
            "Result.Failure는 null 오류를 거절한다");
    }

    /// <summary>
    /// Application Service가 Strategy 선택, 고정 시각 부여, 저장, 조회, 순차 중복을 올바르게 조정하는지 확인합니다.
    /// </summary>
    /// <returns>모든 비동기 저장·조회 검증의 완료를 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyApplicationAndDuplicateRejectionAsync()
    {
        var context = CreateContext();
        var command = ValidSignInCommand(" evt-app-1 ");
        var first = await context.Service.IngestAsync(command, CancellationToken.None);

        Assert(first.IsSuccess, "Application Service가 유효한 command를 저장한다");
        Assert(first.Value?.EventId == "EVT-APP-1", "Application 결과에도 정규화한 eventId가 나타난다");
        Assert(first.Value?.ReceivedAtUtc == context.Clock.GetUtcNow(), "TimeProvider의 고정 수신 시각을 사용한다");
        Assert(context.Repository.Count == 1, "첫 성공 뒤 Repository에 한 건만 있다");

        // with 식은 원본 record를 바꾸지 않고 지정한 속성만 교체한 복사본을 만들어 중복 변형 입력을 간결하게 준비합니다.
        var duplicate = await context.Service.IngestAsync(
            command with { EventId = "EvT-App-1" },
            CancellationToken.None);
        Assert(duplicate.Error?.Code == "audit.duplicate_event_id", "대소문자가 다른 같은 ID를 중복으로 판정한다");
        Assert(context.Repository.Count == 1, "중복 요청은 저장 건수를 늘리지 않는다");

        var found = await context.Service.FindByEventIdAsync(" evt-app-1 ", CancellationToken.None);
        Assert(found?.EventId == "EVT-APP-1", "조회도 저장과 같은 방식으로 ID를 정규화한다");

        var unsupported = await context.Service.IngestAsync(
            command with { Kind = "server-secret-event", EventId = "EVT-APP-2" },
            CancellationToken.None);
        Assert(unsupported.Error?.Code == "audit.unsupported_kind", "등록되지 않은 kind를 Application 경계에서 거절한다");
        Assert(context.Repository.Count == 1, "지원하지 않는 kind는 Repository를 변경하지 않는다");
    }

    /// <summary>
    /// 같은 eventId의 병렬 요청이 몰려도 Repository 원자 연산으로 정확히 하나만 저장되는지 확인합니다.
    /// </summary>
    /// <returns>모든 병렬 요청 완료를 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyAtomicParallelDuplicateAsync()
    {
        const int attemptCount = 20;
        const int contenderCount = 24;

        for (var attempt = 1; attempt <= attemptCount; attempt++)
        {
            var context = CreateContext();
            // TaskCompletionSource는 모든 contender를 먼저 대기시킨 뒤 한 신호로 함께 출발시켜 직렬 실행의 우연한 통과를 줄입니다.
            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // Select는 지연 실행되므로 ToArray로 즉시 materialize해야 24개 작업이 모두 gate 앞에 만들어집니다.
            var tasks = Enumerable.Range(1, contenderCount)
                .Select(
                    async number =>
                    {
                        await startGate.Task;
                        return await context.Service.IngestAsync(
                            ValidSignInCommand("EVT-RACE-1") with { ActorId = $"USER-{number}" },
                            CancellationToken.None);
                    })
                .ToArray();

            startGate.SetResult();
            var results = await Task.WhenAll(tasks);
            Assert(
                results.Count(result => result.IsSuccess) == 1,
                $"병렬 중복 반복 {attempt}/{attemptCount}: {contenderCount}개 중 정확히 하나만 성공한다");
            Assert(
                results.Count(result => result.Error?.Code == "audit.duplicate_event_id") == contenderCount - 1,
                $"병렬 중복 반복 {attempt}/{attemptCount}: 나머지는 모두 명시적인 중복 결과다");
            Assert(
                context.Repository.Count == 1,
                $"병렬 중복 반복 {attempt}/{attemptCount}: Repository에는 한 이벤트만 남는다");
        }
    }

    /// <summary>
    /// 이미 취소된 신호가 업무 실패 Result로 바뀌지 않고 Repository 전에 중단되는지 확인합니다.
    /// </summary>
    /// <returns>취소 예외 관찰을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyCancellationAsync()
    {
        var context = CreateContext();
        // using var는 method가 끝날 때 CancellationTokenSource.Dispose를 자동 호출해 timer·handle 자원 정리를 빠뜨리지 않습니다.
        using var source = new CancellationTokenSource();
        source.Cancel();

        var canceled = false;
        try
        {
            await context.Service.IngestAsync(ValidSignInCommand("EVT-CANCEL-1"), source.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Assert(canceled, "취소를 Result로 숨기지 않고 OperationCanceledException으로 전파한다");
        Assert(context.Repository.Count == 0, "취소된 요청은 저장되지 않는다");
    }

    /// <summary>
    /// Mapper 누락·중복처럼 배포 전에 고쳐야 할 DI 구성 오류가 조용히 진행되지 않는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없습니다.</returns>
    private static void VerifyCompositionGuards()
    {
        var repository = new InMemoryAuditEventRepository();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero));

        var expectedKinds = new HashSet<string>(
            AuditEventMappers.SupportedKinds,
            StringComparer.Ordinal);
        var defaultMappers = AuditEventMappers.CreateDefault();
        var defaultMapperKinds = defaultMappers
            .Select(mapper => mapper.SupportedKind)
            .ToHashSet(StringComparer.Ordinal);
        Assert(expectedKinds.Count == 3, "공식 이벤트 종류 목록에는 정확히 세 종류만 있다");
        Assert(defaultMappers.Count == 3, "기본 DI 구성에는 정확히 세 Mapper만 있다");
        Assert(defaultMapperKinds.SetEquals(expectedKinds), "기본 Mapper 종류가 공식 종류 목록과 정확히 일치한다");

        // reflection은 attribute로 선언한 JSON 허용 목록을 읽어 DI Strategy 목록과 서로 어긋나지 않는지 검사할 때만 사용합니다.
        var jsonDerivedTypes = typeof(AuditEventRequest)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToArray();
        var jsonDiscriminators = jsonDerivedTypes
            .Select(attribute => attribute.TypeDiscriminator as string)
            .Where(discriminator => discriminator is not null)
            .Select(discriminator => discriminator!)
            .ToHashSet(StringComparer.Ordinal);
        Assert(jsonDerivedTypes.Length == 3, "JSON 다형성 허용 목록에는 정확히 세 하위 형식만 있다");
        Assert(jsonDiscriminators.SetEquals(expectedKinds), "JSON type 허용 목록과 Mapper 공식 종류가 정확히 일치한다");

        // []는 목표 parameter 형식에 맞는 빈 collection을 만드는 collection expression입니다.
        AssertThrows<InvalidOperationException>(
            () => _ = new IngestAuditEventService(repository, [], clock),
            "Mapper가 하나도 없으면 구성 오류를 드러낸다");
        AssertThrows<InvalidOperationException>(
            () => _ = new IngestAuditEventService(
                repository,
                [new UserSignedInAuditEventMapper(), new DocumentChangedAuditEventMapper()],
                clock),
            "공식 세 종류 중 Mapper 하나만 빠져도 구성 오류를 드러낸다");
        AssertThrows<InvalidOperationException>(
            () => _ = new IngestAuditEventService(
                repository,
                [new UserSignedInAuditEventMapper(), new UserSignedInAuditEventMapper()],
                clock),
            "같은 kind Mapper가 중복 등록되면 구성 오류를 드러낸다");
        AssertThrows<InvalidOperationException>(
            () => _ = new IngestAuditEventService(
                repository,
                defaultMappers.Append(new UnexpectedAuditEventMapper()),
                clock),
            "공식 목록 밖 kind Mapper가 등록되면 구성 오류를 드러낸다");
    }

    /// <summary>
    /// Production 예외 middleware가 예상 밖 예외의 메시지·형식·source 경로를 HTTP 본문에서 지우는지 확인합니다.
    /// </summary>
    /// <returns>test-only endpoint 요청과 서버 종료가 끝났음을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyUnexpectedExceptionHttpRedactionAsync()
    {
        const string internalDetail = "SELF-TEST-SECRET-INTERNAL-DETAIL";
        // 이 WebApplication 인스턴스에만 fault route를 붙입니다. 정상 실행의 production route 표에는 추가되지 않습니다.
        await using var app = Program.BuildApplication(
            [
                "--environment", "Production",
                "--urls", "http://127.0.0.1:0",
                "--Logging:LogLevel:Default", "None"
            ]);
        app.MapGet(
            "/__self-test/fault",
            (Func<IResult>)(() => throw new InvalidOperationException(internalDetail)));

        var started = false;
        try
        {
            await app.StartAsync();
            started = true;

            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features
                .Get<IServerAddressesFeature>()?
                .Addresses
                .SingleOrDefault(value => value.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("자체 테스트 Kestrel의 loopback 주소를 찾지 못했습니다.");

            using var client = new HttpClient
            {
                BaseAddress = new Uri(address, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var response = await client.GetAsync("/__self-test/fault");
            var body = await response.Content.ReadAsStringAsync();

            Assert(response.StatusCode == HttpStatusCode.InternalServerError, "예상 밖 endpoint 예외는 HTTP 500이다");
            Assert(
                response.Content.Headers.ContentType?.MediaType == "application/problem+json",
                "예상 밖 endpoint 예외는 Problem Details다");
            Assert(!body.Contains(internalDetail, StringComparison.Ordinal), "500 본문은 내부 예외 메시지를 노출하지 않는다");
            Assert(!body.Contains("InvalidOperationException", StringComparison.Ordinal), "500 본문은 내부 예외 형식을 노출하지 않는다");
            Assert(!body.Contains("SelfTestRunner.cs", StringComparison.Ordinal), "500 본문은 source 파일 경로를 노출하지 않는다");

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            Assert(root.GetProperty("status").GetInt32() == 500, "500 Problem Details status가 HTTP 상태와 같다");
            Assert(root.GetProperty("instance").GetString() == "/__self-test/fault", "500 Problem Details instance가 요청 경로와 같다");
            Assert(root.GetProperty("code").GetString() == "http.internal_error", "500 Problem Details는 안정적인 공개 code를 가진다");
            Assert(root.GetProperty("field").GetString() == "http", "500 Problem Details field는 http다");
            Assert(
                !string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()),
                "500 Problem Details에 추적 가능한 traceId가 있다");
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
    /// 자체 테스트와 HTTP 설정이 같은 .NET 10 엄격 JSON 규칙과 generated resolver를 사용하게 옵션을 만듭니다.
    /// </summary>
    /// <returns>camelCase, 엄격 입력, type 위치 자유, AuditJsonContext 우선순위를 가진 새 옵션을 반환합니다.</returns>
    private static JsonSerializerOptions CreateStrictJsonOptions()
    {
        // copy constructor는 읽기 전용 singleton인 Strict를 바꾸지 않고 같은 정책의 편집 가능한 옵션을 만듭니다.
        var options = new JsonSerializerOptions(JsonSerializerOptions.Strict)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowOutOfOrderMetadataProperties = true
        };
        options.TypeInfoResolverChain.Insert(0, AuditJsonContext.Default);
        return options;
    }

    /// <summary>
    /// 주어진 JSON 역직렬화가 잘못된 JSON 또는 추상 형식 생성 불가 오류로 거절되는지 확인합니다.
    /// </summary>
    /// <param name="json">검증할 의도적으로 잘못된 JSON 문자열입니다.</param>
    /// <param name="options">source-generated metadata와 엄격 정책을 가진 serializer 옵션입니다.</param>
    /// <param name="message">성공 또는 실패 때 출력할 계약 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void AssertJsonRejected(
        string json,
        JsonSerializerOptions options,
        string message)
    {
        try
        {
            _ = JsonSerializer.Deserialize<AuditEventRequest>(json, options);
        }
        catch (JsonException)
        {
            Assert(true, message);
            return;
        }
        catch (NotSupportedException)
        {
            // discriminator가 없으면 추상 기반 형식을 직접 만들 수 없다는 NotSupportedException으로도 안전하게 거절됩니다.
            Assert(true, message);
            return;
        }

        throw new InvalidOperationException($"검증 실패: {message}");
    }

    /// <summary>
    /// 각 Application 테스트가 독립된 Repository와 고정 수신 시각을 사용하도록 문맥을 새로 만듭니다.
    /// </summary>
    /// <returns>서비스, 관찰 가능한 메모리 Repository, 고정 TimeProvider를 묶어 반환합니다.</returns>
    private static TestContext CreateContext()
    {
        var repository = new InMemoryAuditEventRepository();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero));
        var service = new IngestAuditEventService(
            repository,
            AuditEventMappers.CreateDefault(),
            clock);
        return new TestContext(service, repository, clock);
    }

    /// <summary>
    /// 여러 테스트에서 재사용할 유효한 로그인 command를 만듭니다.
    /// </summary>
    /// <param name="eventId">테스트가 중복 여부를 조절할 생산자 이벤트 식별자입니다.</param>
    /// <returns>고정 UTC 시각과 유효한 IPv4 주소를 가진 command를 반환합니다.</returns>
    private static IngestAuditEventCommand ValidSignInCommand(string eventId)
    {
        return new IngestAuditEventCommand(
            AuditEventDraft.UserSignedInKind,
            eventId,
            new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero),
            "USER-1",
            IpAddress: "203.0.113.10",
            Succeeded: true);
    }

    /// <summary>
    /// 조건이 거짓이면 즉시 테스트를 실패시키고, 참이면 PASS 수를 증가시킵니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검증 조건입니다.</param>
    /// <param name="message">실패 원인과 성공 항목을 설명하는 문장입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }

        _passed++;
        Console.WriteLine($"  [PASS] {message}");
    }

    /// <summary>
    /// 작업이 지정한 예외 형식을 던지는지 확인하고, 예외가 없거나 다른 형식이면 테스트를 실패시킵니다.
    /// </summary>
    /// <typeparam name="TException">반드시 관찰해야 할 예외 형식입니다.</typeparam>
    /// <param name="action">예외를 던져야 하는 동기 작업입니다.</param>
    /// <param name="message">성공 또는 실패 때 출력할 계약 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void AssertThrows<TException>(Action action, string message)
        // where generic constraint는 이 helper가 Exception 계열 형식만 받게 해 잘못된 사용을 compile 단계에서 막습니다.
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            _passed++;
            Console.WriteLine($"  [PASS] {message}");
            return;
        }

        throw new InvalidOperationException($"검증 실패: {message}");
    }

    /// <summary>
    /// 공식 목록 밖 Mapper를 DI에 넣었을 때 시작 단계 구성 검증이 거절하는지 확인하기 위한 테스트 대역입니다.
    /// </summary>
    private sealed class UnexpectedAuditEventMapper : IAuditEventMapper
    {
        /// <summary>공개 허용 목록에 없는 의도적인 테스트 종류를 가져옵니다.</summary>
        public string SupportedKind => "server-restarted";

        /// <summary>
        /// 이 Mapper는 서비스 생성 중 거절되어야 하므로 실제 mapping이 호출되면 테스트 자체의 구성 오류를 드러냅니다.
        /// </summary>
        /// <param name="command">호출되어서는 안 되는 테스트 command입니다.</param>
        /// <returns>정상 테스트에서는 이 지점에 도달하지 않습니다.</returns>
        public Result<AuditEventDraft> Map(IngestAuditEventCommand command)
        {
            throw new UnreachableException("목록 밖 Mapper는 Map 호출 전에 거절되어야 합니다.");
        }
    }

    /// <summary>
    /// 각 테스트에 함께 필요한 서비스, Repository, 시계를 한 값으로 묶습니다.
    /// </summary>
    /// <param name="Service">검증 대상 Application Service입니다.</param>
    /// <param name="Repository">저장 건수를 관찰할 메모리 Adapter입니다.</param>
    /// <param name="Clock">수신 시각을 재현 가능하게 고정한 시계입니다.</param>
    private sealed record TestContext(
        IngestAuditEventService Service,
        InMemoryAuditEventRepository Repository,
        FixedTimeProvider Clock);

    /// <summary>
    /// 실행 시각과 무관하게 같은 수신 시각을 돌려주는 테스트 전용 TimeProvider입니다.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        /// <summary>
        /// 모든 GetUtcNow 호출에서 반환할 고정 UTC 시각을 저장합니다.
        /// </summary>
        /// <param name="utcNow">UTC 오프셋을 가진 재현 가능한 테스트 시각입니다.</param>
        /// <returns>생성자이므로 값을 반환하지 않고 고정 시각을 보관합니다.</returns>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            if (utcNow.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("고정 시각은 UTC여야 합니다.", nameof(utcNow));
            }

            _utcNow = utcNow;
        }

        /// <summary>
        /// 테스트가 지정한 동일한 UTC 시각을 반환합니다.
        /// </summary>
        /// <returns>생성 때 받은 고정 UTC 시각을 반환합니다.</returns>
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
