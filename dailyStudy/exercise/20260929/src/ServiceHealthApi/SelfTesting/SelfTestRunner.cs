using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ServiceHealthApi.Application;
using ServiceHealthApi.Application.Ports;
using ServiceHealthApi.Domain;
using ServiceHealthApi.HealthChecks;
using ServiceHealthApi.Infrastructure;

namespace ServiceHealthApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 Domain, Application, 실제 Kestrel health 계약을 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 단위 검증과 Development/Production 실제 HTTP 통합 검증을 차례로 실행합니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 첫 실패의 설명을 출력하면 1을 반환합니다.</returns>
    // async는 여러 비동기 검증을 await하고 최종 종료 코드를 Task<int>로 돌려준다는 뜻입니다.
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            ValidateDomainAndPolicy();
            // await는 각 비동기 검증이 끝날 때까지 thread를 붙잡지 않고 차례로 기다립니다.
            await ValidateApplicationCancellationAsync();
            await ValidateDevelopmentHttpContractsAsync();
            await ValidateDevelopmentRequiresDemoOptInAsync();
            await ValidateProductionHidesDemoEndpointsAsync();
            // $"...{value}..."는 값이 들어갈 자리에 중괄호를 쓰는 문자열 interpolation입니다.
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
    /// Domain factory의 경계값과 필수/선택 readiness 정책 우선순위를 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateDomainAndPolicy()
    {
        var missingName = DependencyObservation.Create(
            " ",
            DependencyImportance.Required,
            DependencyCondition.Available,
            TimeSpan.Zero,
            "inventory.available");
        // Error!의 !는 바로 앞의 실패 조건으로 null이 아님을 사람이 보증해 nullable 경고만 제거하는 null-forgiving 연산자입니다.
        Assert(
            !missingName.IsSuccess && missingName.Error!.Code == "dependency.name.required",
            "빈 dependency 이름은 예상 가능한 실패 Result다.");

        var negativeDuration = DependencyObservation.Create(
            "inventory",
            DependencyImportance.Required,
            DependencyCondition.Available,
            TimeSpan.FromMilliseconds(-1),
            "inventory.available");
        Assert(
            !negativeDuration.IsSuccess && negativeDuration.Error!.Code == "dependency.duration.negative",
            "음수 probe 시간은 Domain 경계에서 거절된다.");

        // (DependencyImportance)999는 정수를 enum으로 보는 명시적 cast라 잘못된 외부 값을 회귀 검증할 수 있습니다.
        var invalidImportance = DependencyObservation.Create(
            "inventory",
            (DependencyImportance)999,
            DependencyCondition.Available,
            TimeSpan.Zero,
            "inventory.available");
        Assert(
            !invalidImportance.IsSuccess && invalidImportance.Error!.Code == "dependency.importance.invalid",
            "정의되지 않은 importance enum은 fail-open되지 않고 Domain에서 거절된다.");

        var invalidCondition = DependencyObservation.Create(
            "inventory",
            DependencyImportance.Required,
            (DependencyCondition)999,
            TimeSpan.Zero,
            "inventory.invalid");
        Assert(
            !invalidCondition.IsSuccess && invalidCondition.Error!.Code == "dependency.condition.invalid",
            "정의되지 않은 condition enum은 Healthy로 오인되지 않고 Domain에서 거절된다.");

        var unsafeCode = DependencyObservation.Create(
            "inventory",
            DependencyImportance.Required,
            DependencyCondition.Unavailable,
            TimeSpan.Zero,
            "connection string=password");
        Assert(
            !unsafeCode.IsSuccess && unsafeCode.Error!.Code == "dependency.code.invalid",
            "공백과 등호가 든 민감정보 모양의 safeCode는 Domain에서 거절된다.");

        var requiredUp = CreateObservation(
            "inventory",
            DependencyImportance.Required,
            DependencyCondition.Available);
        var requiredDown = CreateObservation(
            "inventory",
            DependencyImportance.Required,
            DependencyCondition.Unavailable);
        var optionalUp = CreateObservation(
            "recommendations",
            DependencyImportance.Optional,
            DependencyCondition.Available);
        var optionalDown = CreateObservation(
            "recommendations",
            DependencyImportance.Optional,
            DependencyCondition.Unavailable);
        var policy = new RequiredOptionalReadinessPolicy();

        // [a, b]는 C# collection expression으로 정책이 요구하는 IReadOnlyList 모양을 간결하게 만듭니다.
        Assert(
            policy.Evaluate([requiredUp, optionalUp]).Level == ReadinessLevel.Healthy,
            "모든 dependency가 정상이면 Healthy다.");
        Assert(
            policy.Evaluate([requiredUp, optionalDown]).Level == ReadinessLevel.Degraded,
            "선택 dependency만 실패하면 Degraded다.");
        Assert(
            policy.Evaluate([requiredDown, optionalUp]).Level == ReadinessLevel.Unhealthy,
            "필수 dependency가 실패하면 Unhealthy다.");
        Assert(
            policy.Evaluate([requiredDown, optionalDown]).Level == ReadinessLevel.Unhealthy,
            "필수 장애는 선택 장애보다 우선한다.");

        // { requiredUp }은 new List 직후 첫 원소를 넣는 collection initializer입니다.
        var mutableObservations = new List<DependencyObservation> { requiredUp };
        var immutableDecision = ReadinessDecision.Create(ReadinessLevel.Healthy, mutableObservations);
        mutableObservations.Clear();
        Assert(immutableDecision.Observations.Count == 1, "ReadinessDecision은 외부 관찰 collection을 복사해 불변 근거를 보관한다.");
        // hasException:은 bool 값의 의미를 호출 위치에 드러내는 named argument입니다.
        Assert(
            HealthResponseWriter.NormalizeSafeCode("startup.ready", hasException: false) == "startup.ready",
            "Health writer는 명시적으로 허용한 상태 코드만 그대로 공개한다.");
        Assert(
            HealthResponseWriter.NormalizeSafeCode("dbpassword123", hasException: false) == "health.check.failed",
            "영숫자만으로 보이는 임의 description도 allowlist 밖이면 숨긴다.");
        Assert(
            HealthResponseWriter.NormalizeSafeCode("startup.ready", hasException: true) == "health.check.failed",
            "예외가 붙은 report entry는 description 모양과 관계없이 일반 코드로 숨긴다.");
    }

    /// <summary>
    /// Application Service가 모든 probe를 한 번 실행하고 같은 취소 신호를 실제 대기까지 전달하는지 확인합니다.
    /// </summary>
    /// <returns>정상 판정과 취소 전파 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    // Task 반환 메서드의 async/await는 취소 예외도 원래 비동기 흐름으로 관찰하게 합니다.
    private static async Task ValidateApplicationCancellationAsync()
    {
        var required = new ConfigurableDependencyProbe(
            "inventory",
            DependencyImportance.Required,
            TimeSpan.FromSeconds(2));
        var optional = new ConfigurableDependencyProbe(
            "recommendations",
            DependencyImportance.Optional,
            TimeSpan.FromSeconds(2));
        var service = new ReadinessApplicationService(
            [required, optional],
            new RequiredOptionalReadinessPolicy());

        var healthy = await service.CheckAsync(CancellationToken.None);
        Assert(healthy.Level == ReadinessLevel.Healthy, "Application Service가 정상 관찰을 Healthy로 합친다.");
        Assert(required.ProbeCount == 1 && optional.ProbeCount == 1, "각 probe는 readiness 실행마다 정확히 한 번 호출된다.");

        required.Configure(DependencyCondition.Available, TimeSpan.FromSeconds(1));
        // using은 검증이 끝나면 CancellationTokenSource의 timer 자원을 자동으로 정리합니다.
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await AssertThrowsAsync<OperationCanceledException>(
            () => service.CheckAsync(cancellationSource.Token),
            "호출자 취소는 실패 Result로 삼키지 않고 OperationCanceledException으로 전파된다.");
        Assert(required.CanceledProbeCount == 1, "느린 Infrastructure probe가 같은 취소 신호를 실제로 관찰한다.");
    }

    /// <summary>
    /// 실제 Development Kestrel에서 boot, 정상, 선택 장애, 필수 장애, timeout, recovery 계약을 확인합니다.
    /// </summary>
    /// <returns>서버 시작부터 안전한 종료까지 모든 HTTP 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateDevelopmentHttpContractsAsync()
    {
        // 긴 warm-up을 주고 테스트가 직접 신호를 바꿔 startup 전후를 race 없이 관찰합니다.
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            startupDelayMilliseconds: 30_000,
            environmentName: Environments.Development,
            enableDemoEndpoints: true);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);

            var probes = app.Services
                .GetRequiredService<IEnumerable<IDependencyProbe>>()
                .OfType<ConfigurableDependencyProbe>()
                .ToArray();
            var callsBeforeLive = probes.Sum(probe => probe.ProbeCount);

            using (var live = await client.GetAsync("/health/live"))
            {
                Assert(live.StatusCode == HttpStatusCode.OK, "warm-up 중에도 liveness는 HTTP 200이다.");
                using var liveJson = await ReadJsonAsync(live);
                Assert(ReadStatus(liveJson) == "Healthy", "liveness 전체 상태는 Healthy다.");
                Assert(liveJson.RootElement.GetProperty("checks").GetArrayLength() == 0, "liveness는 등록 probe를 하나도 실행하지 않는다.");
                AssertHasSafeHealthHeaders(live);
            }

            Assert(probes.Sum(probe => probe.ProbeCount) == callsBeforeLive, "liveness 호출 전후 dependency probe 횟수가 같다.");

            using (var startupBefore = await client.GetAsync("/health/startup"))
            {
                Assert(startupBefore.StatusCode == HttpStatusCode.ServiceUnavailable, "warm-up 전 startup은 HTTP 503이다.");
                using var startupJson = await ReadJsonAsync(startupBefore);
                Assert(ReadStatus(startupJson) == "Unhealthy", "warm-up 전 startup 상태는 Unhealthy다.");
            }

            using (var readyBefore = await client.GetAsync("/health/ready"))
            {
                Assert(readyBefore.StatusCode == HttpStatusCode.ServiceUnavailable, "warm-up 전 readiness는 HTTP 503이다.");
                using var readyJson = await ReadJsonAsync(readyBefore);
                Assert(ReadStatus(readyJson) == "Unhealthy", "startup 검사가 readiness에도 포함된다.");
            }

            app.Services.GetRequiredService<StartupSignal>().MarkReady();

            using (var startupAfter = await client.GetAsync("/health/startup"))
            {
                Assert(startupAfter.StatusCode == HttpStatusCode.OK, "warm-up 완료 후 startup은 HTTP 200이다.");
                using var startupJson = await ReadJsonAsync(startupAfter);
                Assert(ReadStatus(startupJson) == "Healthy", "warm-up 완료 후 startup 상태는 Healthy다.");
            }

            using (var readyHealthy = await client.GetAsync("/health/ready"))
            {
                Assert(readyHealthy.StatusCode == HttpStatusCode.OK, "모든 dependency 정상 시 readiness는 HTTP 200이다.");
                using var readyJson = await ReadJsonAsync(readyHealthy);
                Assert(ReadStatus(readyJson) == "Healthy", "모든 dependency 정상 시 전체 상태는 Healthy다.");
                Assert(readyJson.RootElement.GetProperty("checks").GetArrayLength() == 2, "readiness는 startup과 application 검사를 실행한다.");
                AssertHasSafeHealthHeaders(readyHealthy);
            }

            using (var optionalChange = await client.PutAsync(
                "/demo/dependencies/recommendations/unavailable",
                content: null))
            {
                Assert(optionalChange.StatusCode == HttpStatusCode.OK, "Development demo에서 선택 dependency 장애를 설정할 수 있다.");
            }

            using (var readyDegraded = await client.GetAsync("/health/ready"))
            {
                Assert(readyDegraded.StatusCode == HttpStatusCode.OK, "선택 dependency만 실패하면 트래픽을 받을 수 있어 HTTP 200이다.");
                using var degradedJson = await ReadJsonAsync(readyDegraded);
                Assert(ReadStatus(degradedJson) == "Degraded", "선택 dependency만 실패하면 전체 상태는 Degraded다.");
            }

            using (var requiredChange = await client.PutAsync(
                "/demo/dependencies/inventory/unavailable",
                content: null))
            {
                Assert(requiredChange.StatusCode == HttpStatusCode.OK, "Development demo에서 필수 dependency 장애를 설정할 수 있다.");
            }

            using (var readyUnhealthy = await client.GetAsync("/health/ready"))
            {
                Assert(readyUnhealthy.StatusCode == HttpStatusCode.ServiceUnavailable, "필수 dependency가 실패하면 readiness는 HTTP 503이다.");
                using var unhealthyJson = await ReadJsonAsync(readyUnhealthy);
                Assert(ReadStatus(unhealthyJson) == "Unhealthy", "필수 dependency가 실패하면 전체 상태는 Unhealthy다.");
            }

            using (var liveDuringOutage = await client.GetAsync("/health/live"))
            {
                Assert(liveDuringOutage.StatusCode == HttpStatusCode.OK, "dependency 장애 중에도 liveness는 HTTP 200을 유지한다.");
            }

            await ConfigureDependencyAsync(client, "inventory", "available", 0);
            await ConfigureDependencyAsync(client, "recommendations", "available", 500);
            var recommendations = probes.Single(probe => probe.Name == "recommendations");
            var optionalCanceledBefore = recommendations.CanceledProbeCount;
            var optionalStopwatch = Stopwatch.StartNew();

            using (var optionalTimeout = await client.GetAsync("/health/ready"))
            {
                optionalStopwatch.Stop();
                Assert(optionalTimeout.StatusCode == HttpStatusCode.OK, "느린 선택 probe는 HTTP 200을 유지한다.");
                using var optionalTimeoutJson = await ReadJsonAsync(optionalTimeout);
                Assert(ReadStatus(optionalTimeoutJson) == "Degraded", "선택 probe timeout은 중요도 정책에 따라 Degraded다.");
            }

            Assert(optionalStopwatch.Elapsed < TimeSpan.FromSeconds(1), "500ms 선택 probe가 100ms 개별 timeout으로 빠르게 제한된다.");
            Assert(recommendations.CanceledProbeCount == optionalCanceledBefore + 1, "선택 probe 개별 timeout이 실제 대기를 취소한다.");

            await ConfigureDependencyAsync(client, "recommendations", "available", 0);
            await ConfigureDependencyAsync(client, "inventory", "available", 500);
            var inventory = probes.Single(probe => probe.Name == "inventory");
            var canceledBefore = inventory.CanceledProbeCount;
            var stopwatch = Stopwatch.StartNew();

            using (var readyTimeout = await client.GetAsync("/health/ready"))
            {
                stopwatch.Stop();
                Assert(readyTimeout.StatusCode == HttpStatusCode.ServiceUnavailable, "느린 필수 probe는 개별 timeout 뒤 HTTP 503이다.");
                using var timeoutJson = await ReadJsonAsync(readyTimeout);
                var timeoutBody = timeoutJson.RootElement.GetRawText();
                Assert(ReadStatus(timeoutJson) == "Unhealthy", "probe timeout은 Unhealthy로 fail-closed 처리된다.");
                Assert(!timeoutBody.Contains("Exception", StringComparison.OrdinalIgnoreCase), "health JSON은 exception 형식 이름을 노출하지 않는다.");
                Assert(!timeoutBody.Contains("connection string", StringComparison.OrdinalIgnoreCase), "health JSON은 연결 문자열 같은 민감정보를 노출하지 않는다.");
            }

            Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(1), "500ms 필수 probe가 150ms 개별 timeout으로 빠르게 제한된다.");
            Assert(inventory.CanceledProbeCount == canceledBefore + 1, "필수 probe 개별 timeout이 실제 대기를 취소한다.");

            await ConfigureDependencyAsync(client, "inventory", "available", 0);
            using (var recovered = await client.GetAsync("/health/ready"))
            {
                Assert(recovered.StatusCode == HttpStatusCode.OK, "dependency 복구 후 readiness는 다시 HTTP 200이다.");
                using var recoveredJson = await ReadJsonAsync(recovered);
                Assert(ReadStatus(recoveredJson) == "Healthy", "dependency 복구 후 전체 상태는 Healthy다.");
            }

            using (var invalidDemo = await client.PutAsync(
                "/demo/dependencies/inventory/broken",
                content: null))
            {
                Assert(invalidDemo.StatusCode == HttpStatusCode.BadRequest, "잘못된 demo condition은 HTTP 400이다.");
                Assert(await ReadProblemCodeAsync(invalidDemo) == "dependency.condition.invalid", "400 Problem Details에 안정적인 오류 코드가 있다.");
            }

            using (var invalidDelay = await client.PutAsync(
                "/demo/dependencies/inventory/available?delayMs=abc",
                content: null))
            {
                Assert(invalidDelay.StatusCode == HttpStatusCode.BadRequest, "정수가 아닌 demo delayMs는 HTTP 400이다.");
                Assert(await ReadProblemCodeAsync(invalidDelay) == "dependency.delay.invalid", "binding 실패 대신 직접 만든 안정적인 delay 오류 코드가 있다.");
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
    /// Development 환경 이름만 지정하고 opt-in하지 않으면 demo route가 없는지 확인합니다.
    /// </summary>
    /// <returns>Development Kestrel 시작, 404 검증, 종료가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateDevelopmentRequiresDemoOptInAsync()
    {
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            startupDelayMilliseconds: 0,
            environmentName: Environments.Development,
            enableDemoEndpoints: false);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);
            using var response = await client.PutAsync(
                "/demo/dependencies/inventory/unavailable",
                content: null);
            Assert(response.StatusCode == HttpStatusCode.NotFound, "Development도 명시적 opt-in이 없으면 demo endpoint가 404다.");
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
    /// Production에서는 장애 주입 endpoint 자체가 route table에 없는지 실제 서버로 확인합니다.
    /// </summary>
    /// <returns>Production Kestrel 시작, 404 검증, 종료가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateProductionHidesDemoEndpointsAsync()
    {
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            startupDelayMilliseconds: 0,
            environmentName: Environments.Production,
            enableDemoEndpoints: true);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);
            using var response = await client.PutAsync(
                "/demo/dependencies/inventory/unavailable",
                content: null);
            Assert(response.StatusCode == HttpStatusCode.NotFound, "Production에서는 demo 장애 주입 endpoint가 404다.");
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
    /// 검증용 Domain 관찰 값을 만들고 구성 오류라면 즉시 테스트를 실패시킵니다.
    /// </summary>
    /// <param name="name">관찰 대상 이름입니다.</param>
    /// <param name="importance">필수 또는 선택 중요도입니다.</param>
    /// <param name="condition">가용 또는 불가용 상태입니다.</param>
    /// <returns>검증을 통과한 DependencyObservation을 반환합니다.</returns>
    private static DependencyObservation CreateObservation(
        string name,
        DependencyImportance importance,
        DependencyCondition condition)
    {
        var suffix = condition == DependencyCondition.Available ? "available" : "unavailable";
        var result = DependencyObservation.Create(
            name,
            importance,
            condition,
            TimeSpan.FromMilliseconds(1),
            $"{name}.{suffix}");

        return result.Value ?? throw new InvalidOperationException(result.Error?.Message);
    }

    /// <summary>
    /// 실제 Kestrel 주소를 BaseAddress로 사용하는 짧은 timeout의 HttpClient를 만듭니다.
    /// </summary>
    /// <param name="services">실행 중 서버와 주소 기능을 찾을 DI 컨테이너입니다.</param>
    /// <returns>통합 테스트용 HttpClient를 반환합니다.</returns>
    private static HttpClient CreateClient(IServiceProvider services)
    {
        var server = services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>() ??
            throw new InvalidOperationException("Kestrel 주소 기능을 찾을 수 없습니다.");
        var address = addresses.Addresses.Single();

        return new HttpClient
        {
            BaseAddress = new Uri(address, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// Development endpoint를 호출해 probe 상태와 지연을 바꾸고 성공 여부를 검증합니다.
    /// </summary>
    /// <param name="client">실행 중 Development 서버를 가리키는 HttpClient입니다.</param>
    /// <param name="name">변경할 dependency 이름입니다.</param>
    /// <param name="condition">available 또는 unavailable 문자열입니다.</param>
    /// <param name="delayMs">다음 probe가 기다릴 시간(ms)입니다.</param>
    /// <returns>HTTP 설정 요청과 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ConfigureDependencyAsync(
        HttpClient client,
        string name,
        string condition,
        int delayMs)
    {
        using var response = await client.PutAsync(
            $"/demo/dependencies/{name}/{condition}?delayMs={delayMs}",
            content: null);
        Assert(response.StatusCode == HttpStatusCode.OK, $"{name} probe를 {condition}/{delayMs}ms로 설정한다.");
    }

    /// <summary>
    /// health 응답의 content type과 캐시 방지 헤더를 확인합니다.
    /// </summary>
    /// <param name="response">검사할 health HTTP 응답입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없으며 계약이 다르면 예외를 던집니다.</returns>
    private static void AssertHasSafeHealthHeaders(HttpResponseMessage response)
    {
        Assert(response.Content.Headers.ContentType?.MediaType == "application/json", "health 응답 MIME 형식은 application/json이다.");
        Assert(response.Headers.CacheControl?.NoStore == true, "health 응답은 no-store로 캐시를 금지한다.");
    }

    /// <summary>
    /// HTTP 본문을 문자열로 읽고 JsonDocument로 파싱합니다.
    /// </summary>
    /// <param name="response">JSON 본문이 있는 HTTP 응답입니다.</param>
    /// <returns>호출자가 using으로 정리할 JsonDocument를 반환합니다.</returns>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// health JSON의 필수 status 문자열을 읽습니다.
    /// </summary>
    /// <param name="document">HealthResponseWriter가 만든 JSON 문서입니다.</param>
    /// <returns>Healthy, Degraded, Unhealthy 중 하나인 문자열을 반환합니다.</returns>
    private static string ReadStatus(JsonDocument document)
    {
        return document.RootElement.GetProperty("status").GetString() ??
            throw new InvalidOperationException("health JSON status가 null입니다.");
    }

    /// <summary>
    /// Problem Details의 code 확장 필드를 문자열로 읽습니다.
    /// </summary>
    /// <param name="response">오류 JSON 본문이 든 HTTP 응답입니다.</param>
    /// <returns>호출자가 분기할 안정적인 code 문자열을 반환합니다.</returns>
    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);
        return document.RootElement.GetProperty("code").GetString() ??
            throw new InvalidOperationException("Problem Details code가 null입니다.");
    }

    /// <summary>
    /// 비동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">호출하면 검증 대상 Task를 만드는 함수입니다.</param>
    /// <param name="message">통과 또는 실패 때 보여 줄 학습용 설명입니다.</param>
    /// <returns>예외 확인이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task AssertThrowsAsync<TException>(
        Func<Task> action,
        string message)
        where TException : Exception
    {
        try
        {
            await action();
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
}
