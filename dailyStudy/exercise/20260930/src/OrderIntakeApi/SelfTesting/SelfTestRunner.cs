using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using OrderIntakeApi.Application;
using OrderIntakeApi.Domain;
using OrderIntakeApi.Infrastructure;
using OrderIntakeApi.Presentation;

namespace OrderIntakeApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 Domain, 취소 전파, 실제 Kestrel 오류 계약을 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 단위 검증과 Development/Production 실제 HTTP 통합 검증을 차례로 실행합니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 첫 실패 설명을 출력하면 1을 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            ValidateDomainAndStrategy();
            await ValidateCatalogCancellationAsync();
            await ValidateDevelopmentHttpContractsAsync();
            await ValidateDevelopmentRequiresDemoOptInAsync();
            await ValidateProductionHidesDemoEndpointAsync();
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
    /// 입력 factory, 불변 복사, 중복 SKU, 배송비 Strategy의 핵심 경계를 확인합니다.
    /// </summary>
    /// <returns>동기 검증만 수행하므로 반환값은 없으며 실패하면 예외를 던집니다.</returns>
    private static void ValidateDomainAndStrategy()
    {
        var normalizedLine = RequestedOrderLine.Create(" book-cs ", 2);
        Assert(normalizedLine.IsSuccess, "유효한 상품 줄은 성공 Result다.");
        Assert(normalizedLine.Value!.Sku == "BOOK-CS", "SKU는 trim 후 대문자로 정규화된다.");

        var invalidQuantity = RequestedOrderLine.Create("BOOK-CS", 0);
        Assert(
            !invalidQuantity.IsSuccess && invalidQuantity.Error!.Code == "order.quantity.out_of_range",
            "0개 수량은 예외가 아니라 예상 가능한 검증 실패다.");

        var invalidSku = RequestedOrderLine.Create("BOOK/CS", 1);
        Assert(
            !invalidSku.IsSuccess && invalidSku.Error!.Code == "order.sku.invalid",
            "허용 목록 밖 SKU 문자는 거절된다.");

        var mutableLines = new List<RequestedOrderLine> { normalizedLine.Value };
        var immutableDraft = OrderDraft.Create("order-1", "customer-1", mutableLines);
        mutableLines.Clear();
        Assert(immutableDraft.Value!.Lines.Count == 1, "OrderDraft는 외부 collection을 복사해 불변성을 지킨다.");

        var duplicateDraft = OrderDraft.Create(
            "order-2",
            "customer-1",
            [normalizedLine.Value, normalizedLine.Value]);
        Assert(
            !duplicateDraft.IsSuccess && duplicateDraft.Error!.Code == "order.sku.duplicate",
            "같은 SKU가 두 줄이면 안정적인 중복 오류다.");

        var tooManyDraft = OrderDraft.Create(
            "order-3",
            "customer-1",
            Enumerable.Repeat(normalizedLine.Value, 21));
        Assert(
            !tooManyDraft.IsSuccess && tooManyDraft.Error!.Code == "order.items.too_many",
            "21개 상품 줄은 중복 검사 전에 공개 크기 한도로 거절된다.");

        var policy = new ThresholdShippingFeePolicy();
        Assert(policy.Calculate(49_999m, 1) == 3_000m, "5만 원 미만은 배송비 3,000원이다.");
        Assert(policy.Calculate(50_000m, 1) == 0m, "5만 원 경계부터 무료 배송이다.");

        AssertThrows<InvalidOperationException>(
            () => OrderLine.Create(normalizedLine.Value, -1m),
            "음수 카탈로그 가격은 Domain 줄 생성 계약 위반이다.");

        var otherLine = RequestedOrderLine.Create("MUG-DOTNET", 2).Value!;
        var wrongPricedLine = OrderLine.Create(otherLine, 18_000m);
        AssertThrows<InvalidOperationException>(
            () => Order.Create(immutableDraft.Value, [wrongPricedLine], shippingFee: 0m),
            "최종 주문 줄은 초안의 SKU와 수량 순서를 바꿀 수 없다.");

        AssertThrows<InvalidOperationException>(
            () => OrderHttpMapper.ToProblemResult(new Error("unknown.code", "알 수 없는 오류")),
            "HTTP 매핑이 없는 Result 코드는 임의의 400으로 숨기지 않는다.");
    }

    /// <summary>
    /// 취소 토큰이 학습용 카탈로그의 실제 비동기 대기까지 전달되는지 확인합니다.
    /// </summary>
    /// <returns>취소 관찰 검증이 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateCatalogCancellationAsync()
    {
        var catalog = new DemoProductCatalog(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var skus = new HashSet<string>(StringComparer.Ordinal) { "BOOK-CS" };

        await AssertThrowsAsync<OperationCanceledException>(
            () => catalog.GetUnitPricesAsync(skus, cancellation.Token),
            "가격 조회 취소는 OperationCanceledException으로 전파된다.");
        Assert(catalog.ObservedCancellationCount == 1, "Infrastructure 대기가 같은 취소 신호를 실제로 관찰한다.");
    }

    /// <summary>
    /// 실제 Development Kestrel에서 201, 예상 4xx, 처리된 503/500, 404 계약과 복구를 확인합니다.
    /// </summary>
    /// <returns>서버 시작부터 안전한 종료까지 HTTP 검증이 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateDevelopmentHttpContractsAsync()
    {
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            environmentName: Environments.Development,
            enableDemoEndpoints: true);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);

            using (var root = await client.GetAsync("/"))
            {
                Assert(root.StatusCode == HttpStatusCode.OK, "서버 준비 endpoint는 HTTP 200이다.");
            }

            var validRequest = CreateRequest("order-http-1");
            using (var created = await client.PostAsJsonAsync("/orders", validRequest))
            {
                Assert(created.StatusCode == HttpStatusCode.Created, "유효한 주문은 HTTP 201이다.");
                Assert(
                    created.Headers.Location?.ToString() == "/orders/ORDER-HTTP-1",
                    "201 응답은 정규화된 주문 조회 Location을 제공한다.");
                var receiptBody = await created.Content.ReadAsStringAsync();
                using var receipt = JsonDocument.Parse(receiptBody);
                var receiptRoot = receipt.RootElement;
                Assert(receiptRoot.GetProperty("subtotal").GetDecimal() == 50_000m, "두 상품의 합계는 50,000원이다.");
                Assert(
                    receiptRoot.GetProperty("shippingFee").GetDecimal() == 0m &&
                    receiptRoot.GetProperty("total").GetDecimal() == 50_000m,
                    "경계 금액은 무료 배송이다.");
            }

            using (var read = await client.GetAsync("/orders/order-http-1"))
            {
                Assert(read.StatusCode == HttpStatusCode.OK, "저장한 주문은 대소문자와 무관하게 HTTP 200 조회된다.");
            }

            using (var duplicate = await client.PostAsJsonAsync("/orders", validRequest))
            {
                await AssertProblemAsync(
                    duplicate,
                    HttpStatusCode.Conflict,
                    "order.duplicate",
                    "/orders");
            }

            var concurrentRequest = CreateRequest("order-http-concurrent");
            var concurrentCalls = new[]
            {
                client.PostAsJsonAsync("/orders", concurrentRequest),
                client.PostAsJsonAsync("/orders", concurrentRequest)
            };
            var concurrentResponses = await Task.WhenAll(concurrentCalls);
            try
            {
                var concurrentStatuses = concurrentResponses
                    .Select(response => response.StatusCode)
                    .OrderBy(status => (int)status)
                    .ToArray();
                Assert(concurrentStatuses[0] == HttpStatusCode.Created, "동시 중복 요청 중 정확히 하나는 201이다.");
                Assert(concurrentStatuses[1] == HttpStatusCode.Conflict, "동시 중복 요청 중 정확히 하나는 409다.");
            }
            finally
            {
                foreach (var response in concurrentResponses)
                {
                    response.Dispose();
                }
            }

            var invalidRequest = new PlaceOrderRequest(
                "bad id",
                "customer-1",
                [new PlaceOrderItemRequest("BOOK-CS", 1)]);
            using (var invalid = await client.PostAsJsonAsync("/orders", invalidRequest))
            {
                await AssertProblemAsync(
                    invalid,
                    HttpStatusCode.BadRequest,
                    "order.id.invalid",
                    "/orders");
            }

            using (var invalidAcceptRequest = CreateJsonRequest(
                       HttpMethod.Post,
                       "/orders",
                       invalidRequest,
                       "text/plain"))
            using (var invalidAccept = await client.SendAsync(invalidAcceptRequest))
            {
                await AssertProblemAsync(
                    invalidAccept,
                    HttpStatusCode.BadRequest,
                    "order.id.invalid",
                    "/orders");
            }

            using (var malformedContent = new StringContent("{", Encoding.UTF8, "application/json"))
            using (var malformed = await client.PostAsync("/orders", malformedContent))
            {
                await AssertProblemAsync(
                    malformed,
                    HttpStatusCode.BadRequest,
                    "http.bad_request",
                    "/orders");
            }

            var nullItemRequest = new PlaceOrderRequest(
                "order-http-null-item",
                "customer-1",
                [null]);
            using (var nullItem = await client.PostAsJsonAsync("/orders", nullItemRequest))
            {
                await AssertProblemAsync(
                    nullItem,
                    HttpStatusCode.BadRequest,
                    "order.item.required",
                    "/orders");
            }

            var missingRequest = new PlaceOrderRequest(
                "order-http-2",
                "customer-1",
                [new PlaceOrderItemRequest("UNKNOWN-ITEM", 1)]);
            using (var missing = await client.PostAsJsonAsync("/orders", missingRequest))
            {
                await AssertProblemAsync(
                    missing,
                    HttpStatusCode.NotFound,
                    "catalog.item.not_found",
                    "/orders");
            }

            using (var missingOrder = await client.GetAsync("/orders/order-does-not-exist"))
            {
                await AssertProblemAsync(
                    missingOrder,
                    HttpStatusCode.NotFound,
                    "order.not_found",
                    "/orders/order-does-not-exist");
            }

            using (var invalidOrderId = await client.GetAsync("/orders/bad%21"))
            {
                await AssertProblemAsync(
                    invalidOrderId,
                    HttpStatusCode.BadRequest,
                    "order.id.invalid",
                    "/orders/bad!");
            }

            using (var invalidDemo = await client.PutAsync("/demo/catalog/broken", content: null))
            {
                await AssertProblemAsync(
                    invalidDemo,
                    HttpStatusCode.BadRequest,
                    "order.demo_behavior.invalid",
                    "/demo/catalog/broken");
            }

            await SetCatalogBehaviorAsync(client, "unavailable");
            using (var unavailable = await client.PostAsJsonAsync(
                       "/orders",
                       CreateRequest("order-http-3")))
            {
                var body = await AssertProblemAsync(
                    unavailable,
                    HttpStatusCode.ServiceUnavailable,
                    "catalog.unavailable",
                    "/orders");
                Assert(unavailable.Headers.RetryAfter?.Delta == TimeSpan.FromSeconds(5), "503은 5초 Retry-After를 제공한다.");
                Assert(!body.Contains("ProductCatalogUnavailableException", StringComparison.Ordinal), "503 본문은 예외 형식을 노출하지 않는다.");
            }

            await SetCatalogBehaviorAsync(client, "bug");
            using (var unexpected = await client.PostAsJsonAsync(
                       "/orders",
                       CreateRequest("order-http-4")))
            {
                var body = await AssertProblemAsync(
                    unexpected,
                    HttpStatusCode.InternalServerError,
                    "server.unexpected",
                    "/orders");
                Assert(!body.Contains("InvalidOperationException", StringComparison.Ordinal), "500 본문은 예외 형식을 노출하지 않는다.");
                Assert(!body.Contains("의도적인 내부 결함", StringComparison.Ordinal), "500 본문은 내부 예외 message를 노출하지 않는다.");
            }

            using (var unexpectedAcceptRequest = CreateJsonRequest(
                       HttpMethod.Post,
                       "/orders",
                       CreateRequest("order-http-4-xml"),
                       "application/xml"))
            using (var unexpectedAccept = await client.SendAsync(unexpectedAcceptRequest))
            {
                await AssertProblemAsync(
                    unexpectedAccept,
                    HttpStatusCode.InternalServerError,
                    "server.unexpected",
                    "/orders");
            }

            await SetCatalogBehaviorAsync(client, "healthy");
            using (var recovered = await client.PostAsJsonAsync(
                       "/orders",
                       CreateRequest("order-http-5")))
            {
                Assert(recovered.StatusCode == HttpStatusCode.Created, "카탈로그 복구 뒤 주문은 다시 HTTP 201이다.");
            }

            using (var routeMissing = await client.GetAsync("/route-that-does-not-exist"))
            {
                await AssertProblemAsync(
                    routeMissing,
                    HttpStatusCode.NotFound,
                    "http.not_found",
                    "/route-that-does-not-exist");
            }

            using (var missingAcceptRequest = new HttpRequestMessage(
                       HttpMethod.Get,
                       "/route-with-xml-accept"))
            {
                missingAcceptRequest.Headers.Accept.ParseAdd("application/xml");
                using var missingAccept = await client.SendAsync(missingAcceptRequest);
                await AssertProblemAsync(
                    missingAccept,
                    HttpStatusCode.NotFound,
                    "http.not_found",
                    "/route-with-xml-accept");
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
    /// Development 환경이어도 명시적 opt-in이 없으면 장애 주입 route가 숨겨지는지 확인합니다.
    /// </summary>
    /// <returns>Development opt-in 404 검증과 서버 종료가 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateDevelopmentRequiresDemoOptInAsync()
    {
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            environmentName: Environments.Development,
            enableDemoEndpoints: false);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);
            using var response = await client.PutAsync("/demo/catalog/bug", content: null);
            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "http.not_found",
                "/demo/catalog/bug");
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
    /// Production에서는 장애 주입 endpoint가 route table에 생기지 않는지 확인합니다.
    /// </summary>
    /// <returns>Production 서버의 404 검증과 종료가 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidateProductionHidesDemoEndpointAsync()
    {
        await using var app = Program.BuildApplication(
            [],
            useEphemeralLoopbackPort: true,
            environmentName: Environments.Production,
            enableDemoEndpoints: true);
        var started = false;

        try
        {
            await app.StartAsync();
            started = true;
            using var client = CreateClient(app.Services);
            using var response = await client.PutAsync("/demo/catalog/bug", content: null);
            await AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                "http.not_found",
                "/demo/catalog/bug");
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
    /// 정상 카탈로그에서 합계가 정확히 무료 배송 경계가 되는 주문 요청을 만듭니다.
    /// </summary>
    /// <param name="orderId">각 테스트에서 충돌하지 않게 지정할 주문 식별자입니다.</param>
    /// <returns>BOOK-CS 한 개와 MUG-DOTNET 한 개가 든 요청을 반환합니다.</returns>
    private static PlaceOrderRequest CreateRequest(string orderId)
    {
        return new PlaceOrderRequest(
            orderId,
            "customer-1",
            [
                new PlaceOrderItemRequest("BOOK-CS", 1),
                new PlaceOrderItemRequest("MUG-DOTNET", 1)
            ]);
    }

    /// <summary>
    /// JSON 본문과 임의 Accept header가 있는 HTTP 요청 메시지를 만듭니다.
    /// </summary>
    /// <typeparam name="T">JSON으로 직렬화할 요청 본문 형식입니다.</typeparam>
    /// <param name="method">POST 같은 HTTP method입니다.</param>
    /// <param name="path">상대 요청 경로입니다.</param>
    /// <param name="body">JSON으로 직렬화할 값입니다.</param>
    /// <param name="accept">서버 응답 형식 선호를 표현할 media type입니다.</param>
    /// <returns>호출자가 using으로 정리할 HttpRequestMessage를 반환합니다.</returns>
    private static HttpRequestMessage CreateJsonRequest<T>(
        HttpMethod method,
        string path,
        T body,
        string accept)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Accept.ParseAdd(accept);
        return request;
    }

    /// <summary>
    /// Development 장애 주입 endpoint로 카탈로그 상태를 바꾸고 성공을 확인합니다.
    /// </summary>
    /// <param name="client">실행 중 Development 서버를 가리키는 HTTP client입니다.</param>
    /// <param name="behavior">healthy, unavailable, bug 중 하나입니다.</param>
    /// <returns>상태 변경 요청 검증이 끝나면 완료되는 Task를 반환합니다.</returns>
    private static async Task SetCatalogBehaviorAsync(HttpClient client, string behavior)
    {
        using var response = await client.PutAsync($"/demo/catalog/{behavior}", content: null);
        Assert(response.StatusCode == HttpStatusCode.OK, $"catalog 상태를 {behavior}(으)로 바꾼다.");
    }

    /// <summary>
    /// 실제 Kestrel 주소를 BaseAddress로 사용하는 짧은 timeout의 HttpClient를 만듭니다.
    /// </summary>
    /// <param name="services">실행 중 서버와 주소 기능을 찾을 DI container입니다.</param>
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
    /// 오류 응답의 상태, media type, 표준 설명, code, traceId, instance 공통 계약을 확인합니다.
    /// </summary>
    /// <param name="response">검사할 실제 HTTP 응답입니다.</param>
    /// <param name="expectedStatus">기대하는 HTTP 상태입니다.</param>
    /// <param name="expectedCode">기대하는 안정 오류 코드입니다.</param>
    /// <param name="expectedInstance">오류가 발생한 기대 요청 경로입니다.</param>
    /// <returns>추가 민감정보 검사를 위해 원문 JSON 문자열을 반환합니다.</returns>
    private static async Task<string> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedInstance)
    {
        Assert(response.StatusCode == expectedStatus, $"오류 상태는 {(int)expectedStatus} {expectedStatus}이다.");
        Assert(
            response.Content.Headers.ContentType?.MediaType == "application/problem+json",
            "오류 media type은 application/problem+json이다.");

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert(root.GetProperty("status").GetInt32() == (int)expectedStatus, "Problem Details status가 HTTP 상태와 같다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()), "Problem Details에 HTTP 의미를 가리키는 type URI가 있다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()), "Problem Details에 비어 있지 않은 title이 있다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()), "Problem Details에 안전한 행동 안내 detail이 있다.");
        Assert(root.GetProperty("code").GetString() == expectedCode, $"Problem Details code는 {expectedCode}이다.");
        Assert(!string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()), "Problem Details에 비어 있지 않은 traceId가 있다.");
        Assert(root.GetProperty("instance").GetString() == expectedInstance, "Problem Details instance가 요청 경로와 같다.");
        return body;
    }

    /// <summary>
    /// 비동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">호출하면 검증 대상 Task를 만드는 함수입니다.</param>
    /// <param name="message">통과 또는 실패 때 보여 줄 학습용 설명입니다.</param>
    /// <returns>예외 확인이 끝나면 완료되는 Task를 반환합니다.</returns>
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
    /// 동기 작업이 기대한 예외 형식으로 끝나는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">반드시 발생해야 하는 예외 형식입니다.</typeparam>
    /// <param name="action">검증할 동기 동작입니다.</param>
    /// <param name="message">통과 또는 실패 때 보여 줄 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
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
}
