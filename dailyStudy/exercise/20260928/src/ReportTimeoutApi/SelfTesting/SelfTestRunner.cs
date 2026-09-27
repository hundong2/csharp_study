using System.Net;
using System.Text.Json;
using ReportTimeoutApi.Application;
using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Domain;
using ReportTimeoutApi.Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ReportTimeoutApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 Domain, Application, 실제 Kestrel timeout 계약을 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 빠른 단위 검증과 실제 HTTP 통합 검증을 차례로 실행하고 프로세스 종료 코드를 만듭니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 첫 실패가 발생하면 설명을 출력하고 1을 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            ValidateDomainRules();
            await ValidateApplicationServiceAsync();
            await ValidateHttpTimeoutPipelineAsync();
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
    /// ReportRequest 팩터리가 정규화, 기본 형식, 허용 문자, 지연 범위를 지키는지 검증합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateDomainRules()
    {
        var valid = ReportRequest.Create("  cust-100  ", null, 25);
        Assert(valid.IsSuccess, "올바른 요청은 성공한다.");
        Assert(valid.Value!.CustomerId == "CUST-100", "고객 번호의 공백 제거와 대문자 정규화가 적용된다.");
        Assert(valid.Value.Format == "csv", "format을 생략하면 csv가 기본값이다.");

        var invalidCharacter = ReportRequest.Create("CUST_100", "csv", 25);
        Assert(
            !invalidCharacter.IsSuccess &&
            invalidCharacter.Error!.Code == "report.customer.invalid",
            "밑줄이 든 고객 번호는 안정적인 invalid 코드로 거절된다.");

        var unsupportedFormat = ReportRequest.Create("CUST-100", "xml", 25);
        Assert(
            !unsupportedFormat.IsSuccess &&
            unsupportedFormat.Error!.Code == "report.format.unsupported",
            "지원하지 않는 형식은 Repository 호출 전 거절된다.");

        var excessiveDelay = ReportRequest.Create("CUST-100", "json", 2_001);
        Assert(
            !excessiveDelay.IsSuccess &&
            excessiveDelay.Error!.Code == "report.delay.out_of_range",
            "교육용 지연 시간은 2,000ms를 넘을 수 없다.");
    }

    /// <summary>
    /// Application Service가 사전 검증, Strategy 선택, 미존재 Result, 취소 전파를 올바르게 조정하는지 확인합니다.
    /// </summary>
    /// <returns>비동기 Repository와 취소 검증이 모두 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateApplicationServiceAsync()
    {
        var repository = new InMemoryReportRepository();
        // 컬렉션 식 [ ... ]은 공통 interface를 구현한 두 Strategy를 배열로 간결하게 묶습니다.
        IReportFormatterStrategy[] formatters =
        [
            new CsvReportFormatterStrategy(),
            new JsonReportFormatterStrategy()
        ];

        // () =>는 나중에 실행할 생성 코드를 파라미터 없는 lambda(Action) 값으로 전달합니다.
        AssertThrows<InvalidOperationException>(
            () => new ReportApplicationService(
                repository,
                [new CsvReportFormatterStrategy()]),
            "제품 허용 형식의 Strategy가 빠지면 Repository I/O 전에 DI 구성 오류가 드러난다.");
        var service = new ReportApplicationService(repository, formatters);

        var invalid = await service.GenerateAsync("x", "csv", 0, CancellationToken.None);
        Assert(!invalid.IsSuccess, "잘못된 입력은 실패 Result를 반환한다.");
        Assert(repository.StartedReads == 0, "입력 검증 실패는 Repository를 읽지 않는다.");

        var json = await service.GenerateAsync("cust-100", "JSON", 0, CancellationToken.None);
        Assert(json.IsSuccess, "존재하는 고객의 JSON 보고서는 성공한다.");
        Assert(json.Value!.ContentType.StartsWith("application/json", StringComparison.Ordinal), "JSON Strategy의 MIME 형식이 적용된다.");
        Assert(json.Value.LineCount == 2, "CUST-100 보고서에는 seed 두 줄이 포함된다.");
        Assert(json.Value.Content.Contains("\"customerId\": \"CUST-100\"", StringComparison.Ordinal), "JSON 본문에는 정규화된 고객 번호가 있다.");

        var missing = await service.GenerateAsync("CUST-999", "csv", 0, CancellationToken.None);
        Assert(
            !missing.IsSuccess && missing.Error!.Code == "report.customer.not_found",
            "없는 고객은 예외가 아니라 not_found 실패 Result다.");

        // using은 검증 뒤 CancellationTokenSource가 가진 자원을 자동 Dispose합니다.
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var canceledTask = service.GenerateAsync("CUST-100", "csv", 500, cancellationSource.Token);
        await AssertThrowsAsync<OperationCanceledException>(
            canceledTask,
            "취소는 일반 실패 Result로 바뀌지 않고 OperationCanceledException으로 전파된다.");
        Assert(repository.CanceledReads == 1, "Repository가 취소 신호를 실제로 관찰한다.");
    }

    /// <summary>
    /// 실제 Kestrel에서 성공 파일, 400, 404, 504 Problem Details와 health timeout 제외 계약을 확인합니다.
    /// </summary>
    /// <returns>서버 시작, HTTP 검증, 안전한 종료가 모두 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateHttpTimeoutPipelineAsync()
    {
        // await using은 시작이나 검증이 실패해도 WebApplication이 가진 server 자원을 비동기로 정리합니다.
        // useEphemeralLoopbackPort: true는 파라미터 이름을 함께 적는 named argument라 true의 의미가 바로 보입니다.
        await using var app = Program.BuildApplication([], useEphemeralLoopbackPort: true);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            var address = GetServerAddress(app.Services);
            using var client = new HttpClient
            {
                BaseAddress = new Uri(address, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var success = await client.GetAsync("/reports/CUST-100?format=csv&simulateMs=10");
            var successBody = await success.Content.ReadAsStringAsync();
            Assert(success.StatusCode == HttpStatusCode.OK, "짧은 보고서 요청은 HTTP 200이다.");
            Assert(success.Content.Headers.ContentType?.MediaType == "text/csv", "성공 응답의 MIME 형식은 text/csv다.");
            Assert(success.Content.Headers.ContentDisposition?.DispositionType == "attachment", "성공 응답은 브라우저 다운로드용 attachment다.");
            Assert(success.Content.Headers.ContentDisposition?.FileNameStar?.Contains("cust-100", StringComparison.Ordinal) == true, "성공 응답은 고객 번호가 든 다운로드 파일 이름을 제공한다.");
            Assert(successBody.Contains("INV-1001", StringComparison.Ordinal), "CSV 본문에는 첫 청구 번호가 있다.");

            using var invalid = await client.GetAsync("/reports/AB?format=csv&simulateMs=0");
            Assert(invalid.StatusCode == HttpStatusCode.BadRequest, "잘못된 고객 번호는 HTTP 400이다.");
            Assert(await ReadProblemCodeAsync(invalid) == "report.customer.length", "400 Problem Details에는 안정적인 오류 코드가 있다.");

            using var invalidDelay = await client.GetAsync("/reports/CUST-100?format=csv&simulateMs=oops");
            Assert(invalidDelay.StatusCode == HttpStatusCode.BadRequest, "정수가 아닌 simulateMs는 HTTP 400이다.");
            Assert(await ReadProblemCodeAsync(invalidDelay) == "report.delay.invalid", "query binding 실패도 빈 본문 대신 안정적인 Problem Details code를 준다.");

            using var missing = await client.GetAsync("/reports/CUST-999?format=json&simulateMs=0");
            Assert(missing.StatusCode == HttpStatusCode.NotFound, "없는 고객은 HTTP 404다.");
            Assert(await ReadProblemCodeAsync(missing) == "report.customer.not_found", "404 Problem Details에는 not_found 코드가 있다.");

            // (InMemoryReportRepository)는 DI가 interface로 돌려준 객체를 테스트용 구체 형식으로 보는 명시적 cast입니다.
            var repository = (InMemoryReportRepository)app.Services.GetRequiredService<IReportRepository>();
            var canceledBefore = repository.CanceledReads;
            using var timedOut = await client.GetAsync("/reports/CUST-100?format=json&simulateMs=600");
            Assert(timedOut.StatusCode == HttpStatusCode.GatewayTimeout, "150ms를 넘긴 보고서 요청은 HTTP 504다.");
            Assert(timedOut.Content.Headers.ContentType?.MediaType == "application/problem+json", "504 응답은 Problem Details MIME 형식이다.");
            Assert(await ReadProblemCodeAsync(timedOut) == "request.timeout", "504 본문은 request.timeout 코드를 제공한다.");
            Assert(repository.CanceledReads == canceledBefore + 1, "timeout 신호가 Repository 대기를 중단시킨다.");

            using var health = await client.GetAsync("/health");
            Assert(health.StatusCode == HttpStatusCode.OK, "timeout을 끈 health endpoint는 HTTP 200이다.");
        }
        // finally는 검증 중 예외가 나도 실행되어, 시작된 테스트 서버가 항상 종료되게 합니다.
        finally
        {
            if (started)
            {
                await app.StopAsync();
            }
        }
    }

    /// <summary>
    /// 실행 중인 Kestrel이 실제로 선택한 단 하나의 주소를 서비스 기능에서 읽습니다.
    /// </summary>
    /// <param name="services">IServer를 찾을 애플리케이션 DI 컨테이너입니다.</param>
    /// <returns>HttpClient BaseAddress로 사용할 절대 주소 문자열을 반환합니다.</returns>
    private static string GetServerAddress(IServiceProvider services)
    {
        var server = services.GetRequiredService<IServer>();
        // ?? throw는 왼쪽 기능이 null일 때만 오른쪽 예외를 던지는 null 병합 throw 식입니다.
        var addresses = server.Features.Get<IServerAddressesFeature>() ??
            throw new InvalidOperationException("Kestrel 주소 기능을 찾을 수 없습니다.");

        // LINQ Single은 주소가 정확히 하나라는 테스트 불변식도 함께 검사합니다.
        return addresses.Addresses.Single();
    }

    /// <summary>
    /// Problem Details JSON의 code 확장 필드를 반드시 문자열로 읽습니다.
    /// </summary>
    /// <param name="response">본문을 아직 읽지 않은 HTTP 오류 응답입니다.</param>
    /// <returns>JSON의 code 문자열을 읽어 완료되는 Task를 반환합니다.</returns>
    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("code", out var codeElement))
        {
            throw new InvalidOperationException($"Problem Details에 code가 없습니다. 본문: {body}");
        }

        // 같은 ?? throw로 JSON 값이 실제 문자열이라는 계약도 마지막에 확인합니다.
        return codeElement.GetString() ??
            throw new InvalidOperationException("Problem Details code가 null입니다.");
    }

    /// <summary>
    /// 비동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="task">예외 발생을 기다릴 비동기 작업입니다.</param>
    /// <param name="message">통과 또는 실패 때 보여 줄 학습용 설명입니다.</param>
    /// <returns>예외 확인이 끝날 때 완료되는 Task를 반환합니다.</returns>
    // <TException>은 기대할 예외 타입을 호출할 때 정하는 generic이며 where 절은 Exception 파생형만 허용합니다.
    private static async Task AssertThrowsAsync<TException>(Task task, string message)
        where TException : Exception
    {
        try
        {
            await task;
        }
        catch (TException)
        {
            Assert(true, message);
            return;
        }

        throw new InvalidOperationException($"검증 실패: {message} (예외가 발생하지 않음)");
    }

    /// <summary>
    /// 동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외 발생을 확인할 동기 코드 묶음입니다.</param>
    /// <param name="message">통과 또는 실패 때 보여 줄 학습용 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없으며 기대한 예외가 없으면 예외를 던집니다.</returns>
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
    /// 조건이 참인지 검사하고 통과 개수를 누적합니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 검증 조건입니다.</param>
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
