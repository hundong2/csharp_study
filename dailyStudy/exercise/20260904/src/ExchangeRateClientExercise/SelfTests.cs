using System.Net;
using System.Text;

// 외부 테스트 패키지 없이도 핵심 경계를 반복 검증할 수 있는 작은 학습용 테스트 러너입니다.
static class SelfTests
{
    /// <summary>
    /// HTTP 성공·실패, JSON 오류, 입력 검증, 취소, Strategy 경계의 여섯 테스트를 순서대로 실행합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 각 테스트가 고정 입력과 가짜 Handler를 직접 준비합니다.</remarks>
    /// <returns>모든 테스트가 끝날 때 완료되는 Task를 반환하며, 하나라도 다르면 예외를 던집니다.</returns>
    public static async Task RunAsync()
    {
        // Func<Task>는 매개변수 없이 비동기 작업을 반환하는 메서드를 보관할 수 있는 대리자 형식입니다.
        (string Name, Func<Task> Run)[] tests =
        [
            ("HTTP 200 응답을 Domain으로 매핑하고 환산", SuccessfulResponseMapsAndConvertsAsync),
            ("비정상 HTTP 상태를 Result 실패로 반환", NonSuccessStatusReturnsResultAsync),
            ("손상·중복 JSON을 Result 실패로 반환", InvalidJsonReturnsResultAsync),
            ("잘못된 금액과 미지원 통화를 구분", ValidationAndUnsupportedCurrencyAsync),
            ("취소 신호를 HttpMessageHandler까지 전파", CancellationPropagatesAsync),
            ("Strategy가 동일 통화 경계에서 1을 선택", StrategyIdentityBoundaryAsync)
        ];

        var passed = 0;
        foreach (var test in tests)
        {
            await test.Run();
            passed++;
            Console.WriteLine($"PASS: {test.Name}");
        }

        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
    }

    /// <summary>
    /// HTTP 200 JSON이 값 객체와 환율 표를 거쳐 정확한 환산 영수증으로 바뀌는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 USD 2와 KRW 환율 1400의 고정 응답을 사용합니다.</remarks>
    /// <returns>환산과 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task SuccessfulResponseMapsAndConvertsAsync()
    {
        // const는 실행 중 바뀌지 않는 컴파일 시간 상수이고, 문자열 안의 \"는 JSON의 큰따옴표를 문자로 넣습니다.
        const string json =
            "{\"base\":\"USD\",\"date\":\"2026-09-04\",\"rates\":{\"KRW\":1400.00,\"JPY\":150.00}}";
        // (request, _) => ...는 HTTP 요청을 검사한 뒤 응답 Task를 돌려주는 람다이며, _는 사용하지 않는 취소 입력을 버립니다.
        using var httpClient = CreateClient(
            (request, _) =>
            {
                AssertEqual(HttpMethod.Get, request.Method, "Gateway는 조회용 GET을 사용해야 합니다.");
                AssertEqual(
                    "/v1/latest",
                    request.RequestUri?.AbsolutePath,
                    "Gateway는 latest endpoint를 호출해야 합니다.");
                Assert(
                    request.RequestUri?.Query.Contains("base=USD", StringComparison.Ordinal) == true,
                    "Gateway는 검증된 기준 통화를 base query로 보내야 합니다.");
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, json));
            });
        var service = CreateService(httpClient);
        var request = RequireRequest(2m, "USD", "KRW");

        var result = await service.ConvertAsync(request, CancellationToken.None);

        // ?.는 Problem이 null이면 Message 접근을 건너뛰어 null을 안전하게 문자열 보간에 전달합니다.
        Assert(result.IsSuccess, $"정상 응답은 성공해야 합니다: {result.Problem?.Message}");
        var receipt = result.Value!;
        AssertEqual(1400.00m, receipt.AppliedRate, "응답 환율을 그대로 적용해야 합니다.");
        AssertEqual(2800.00m, receipt.Converted.Amount, "2 USD 환산값이 정확해야 합니다.");
        AssertEqual(new DateOnly(2026, 9, 4), receipt.RateDate, "응답 날짜를 보존해야 합니다.");
    }

    /// <summary>
    /// HTTP 503을 예외로 터뜨리지 않고 RemoteService 종류의 실패 Result로 바꾸는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 본문 없는 ServiceUnavailable 응답을 사용합니다.</remarks>
    /// <returns>Gateway 호출과 실패 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task NonSuccessStatusReturnsResultAsync()
    {
        using var httpClient = CreateClient(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var gateway = new HttpExchangeRateGateway(httpClient);

        var result = await gateway.GetLatestRatesAsync(
            RequireCurrency("USD"),
            CancellationToken.None);

        Assert(!result.IsSuccess, "HTTP 503은 실패 Result여야 합니다.");
        AssertEqual(
            ErrorKind.RemoteService,
            result.Problem!.Kind,
            "HTTP 상태 실패의 종류가 RemoteService여야 합니다.");
        Assert(
            result.Problem.Message.Contains("503", StringComparison.Ordinal),
            "오류 설명에 실제 상태 코드가 있어야 합니다.");
    }

    /// <summary>
    /// 문법이 깨졌거나 중복 속성이 있는 JSON을 InvalidPayload 실패로 바꾸는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 닫히지 않은 JSON과 rates 안에 KRW가 두 번 있는 JSON을 사용합니다.</remarks>
    /// <returns>두 Gateway 호출과 JSON 오류 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task InvalidJsonReturnsResultAsync()
    {
        using var httpClient = CreateClient(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"base\":")));
        var gateway = new HttpExchangeRateGateway(httpClient);

        var result = await gateway.GetLatestRatesAsync(
            RequireCurrency("USD"),
            CancellationToken.None);

        Assert(!result.IsSuccess, "손상된 JSON은 실패 Result여야 합니다.");
        AssertEqual(
            ErrorKind.InvalidPayload,
            result.Problem!.Kind,
            "JSON 해석 실패의 종류가 InvalidPayload여야 합니다.");

        const string duplicatePropertyJson =
            "{\"base\":\"USD\",\"date\":\"2026-09-04\",\"rates\":{\"KRW\":1400,\"KRW\":1}}";
        using var duplicateClient = CreateClient(
            (_, _) => Task.FromResult(
                JsonResponse(HttpStatusCode.OK, duplicatePropertyJson)));
        var duplicateGateway = new HttpExchangeRateGateway(duplicateClient);

        var duplicateResult = await duplicateGateway.GetLatestRatesAsync(
            RequireCurrency("USD"),
            CancellationToken.None);

        Assert(!duplicateResult.IsSuccess, "중복 환율 속성은 마지막 값으로 덮지 말고 실패해야 합니다.");
        AssertEqual(
            ErrorKind.InvalidPayload,
            duplicateResult.Problem!.Kind,
            "중복 JSON 속성도 InvalidPayload로 분류해야 합니다.");
    }

    /// <summary>
    /// 0 이하 금액은 Domain 검증 실패이고 표에 없는 목표 통화는 Strategy 실패인지 함께 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 -1 USD와 환율 표에 없는 XYZ 통화를 사용합니다.</remarks>
    /// <returns>입력 검증과 미지원 통화 환산 단언이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task ValidationAndUnsupportedCurrencyAsync()
    {
        var invalidAmount = Program.CreateRequest(-1m, "USD", "KRW");
        Assert(!invalidAmount.IsSuccess, "음수 금액은 요청 생성 단계에서 실패해야 합니다.");
        AssertEqual(
            ErrorKind.Validation,
            invalidAmount.Problem!.Kind,
            "음수 금액은 Validation 실패여야 합니다.");

        const string json =
            "{\"base\":\"USD\",\"date\":\"2026-09-04\",\"rates\":{\"KRW\":1400.00}}";
        using var httpClient = CreateClient(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var service = CreateService(httpClient);

        var unsupportedResult = await service.ConvertAsync(
            RequireRequest(10m, "USD", "XYZ"),
            CancellationToken.None);

        Assert(!unsupportedResult.IsSuccess, "표에 없는 XYZ는 실패해야 합니다.");
        AssertEqual(
            ErrorKind.UnsupportedCurrency,
            unsupportedResult.Problem!.Kind,
            "미지원 통화는 UnsupportedCurrency 실패여야 합니다.");
    }

    /// <summary>
    /// 서비스에 보낸 취소 토큰이 대기 중인 가짜 HTTP Handler까지 도달하고 같은 취소 예외로 돌아오는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 Handler 진입 직후 취소하는 동기화 신호를 사용합니다.</remarks>
    /// <returns>OperationCanceledException 전파를 확인한 뒤 완료되는 Task를 반환합니다.</returns>
    private static async Task CancellationPropagatesAsync()
    {
        // TaskCompletionSource는 Handler가 실제로 호출된 시점을 테스트 메서드에 알려 주는 비동기 신호입니다.
        var handlerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // _는 이 테스트에서 사용하지 않는 요청 매개변수를 버린다는 뜻이고 async 람다는 취소될 때까지 비동기로 기다립니다.
        using var httpClient = CreateClient(async (_, token) =>
        {
            handlerEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        var service = CreateService(httpClient);
        using var cancellation = new CancellationTokenSource();

        var conversionTask = service.ConvertAsync(
            RequireRequest(10m, "USD", "KRW"),
            cancellation.Token);
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        var canceled = false;
        try
        {
            await conversionTask;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            canceled = true;
        }

        Assert(canceled, "취소가 Handler에서 서비스 호출자까지 숨겨지지 않고 전파되어야 합니다.");
    }

    /// <summary>
    /// 목표 통화가 기준 통화와 같을 때 표 조회 여부와 관계없이 Strategy가 정확히 1을 반환하는지 검증합니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 USD 기준 표에는 KRW 환율만 넣어 동일 통화 경계를 분리해 확인합니다.</remarks>
    /// <returns>동기 Strategy 단언을 마친 완료 Task를 반환합니다.</returns>
    private static Task StrategyIdentityBoundaryAsync()
    {
        var usd = RequireCurrency("USD");
        var krw = RequireCurrency("KRW");
        KeyValuePair<Currency, decimal>[] rates =
        [
            new KeyValuePair<Currency, decimal>(krw, 1400m)
        ];
        var tableResult = ExchangeRateTable.Create(
            usd,
            new DateOnly(2026, 9, 4),
            rates);
        Assert(tableResult.IsSuccess, "테스트용 환율 표가 만들어져야 합니다.");

        var policy = new ExactCurrencyRateSelectionPolicy();
        var result = policy.SelectRate(tableResult.Value!, usd);

        Assert(result.IsSuccess, "동일 통화 환율 선택은 성공해야 합니다.");
        AssertEqual(1m, result.Value, "동일 통화 적용 환율은 정확히 1이어야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 응답 생성 함수를 주입한 가짜 Handler와 테스트용 BaseAddress를 가진 HttpClient를 만듭니다.
    /// </summary>
    /// <param name="responder">요청과 취소 토큰을 받아 원하는 HTTP 응답 Task를 만드는 함수입니다.</param>
    /// <returns>인터넷 대신 responder를 호출하도록 설정된 HttpClient를 반환합니다.</returns>
    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("https://tests.example.test/v1/")
        };
    }

    /// <summary>
    /// 테스트용 HttpClient에 실제 Gateway, Strategy, 무출력 Audit을 연결한 서비스를 만듭니다.
    /// </summary>
    /// <param name="httpClient">가짜 Handler가 연결된 테스트 전용 HTTP 클라이언트입니다.</param>
    /// <returns>외부 출력 없이 환산 유스케이스를 실행할 ConvertCurrencyService를 반환합니다.</returns>
    private static ConvertCurrencyService CreateService(HttpClient httpClient)
    {
        return new ConvertCurrencyService(
            new HttpExchangeRateGateway(httpClient),
            new ExactCurrencyRateSelectionPolicy(),
            new SilentConversionAudit());
    }

    /// <summary>
    /// 상태 코드와 문자열 본문을 application/json 응답으로 만드는 테스트 준비 중복을 줄입니다.
    /// </summary>
    /// <param name="statusCode">가짜 API가 반환할 HTTP 상태 코드입니다.</param>
    /// <param name="json">응답 본문에 넣을 정상 또는 비정상 JSON 문자열입니다.</param>
    /// <returns>UTF-8 JSON Content를 가진 새 HttpResponseMessage를 반환합니다.</returns>
    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// 테스트 입력을 ConversionRequest로 만들고 준비 단계가 실패하면 즉시 테스트를 중단합니다.
    /// </summary>
    /// <param name="amount">테스트에서 사용할 양수 원본 금액입니다.</param>
    /// <param name="sourceCode">테스트 원본 통화 코드입니다.</param>
    /// <param name="targetCode">테스트 목표 통화 코드입니다.</param>
    /// <returns>검증이 끝난 ConversionRequest를 반환합니다.</returns>
    private static ConversionRequest RequireRequest(
        decimal amount,
        string sourceCode,
        string targetCode)
    {
        var result = Program.CreateRequest(amount, sourceCode, targetCode);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"테스트 요청 준비에 실패했습니다: {result.Problem!.Message}");
        }

        return result.Value!;
    }

    /// <summary>
    /// 테스트 문자열을 Currency로 만들고 준비 단계가 실패하면 즉시 테스트를 중단합니다.
    /// </summary>
    /// <param name="code">영문 세 글자의 테스트 통화 코드입니다.</param>
    /// <returns>검증이 끝난 Currency 값 객체를 반환합니다.</returns>
    private static Currency RequireCurrency(string code)
    {
        var result = Currency.Create(code);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"테스트 통화 준비에 실패했습니다: {result.Problem!.Message}");
        }

        return result.Value!;
    }

    /// <summary>
    /// 조건이 거짓이면 설명을 담은 예외로 현재 자체 테스트를 실패시킵니다.
    /// </summary>
    /// <param name="condition">테스트가 통과하려면 반드시 참이어야 하는 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 무엇이 달랐는지 알려 줄 설명입니다.</param>
    /// <returns>조건이 참이면 아무 값도 반환하지 않고 다음 검증으로 진행합니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 제네릭 형식의 예상값과 실제값을 해당 형식의 표준 같음 규칙으로 비교합니다.
    /// </summary>
    /// <param name="expected">코드가 만들어야 하는 예상값입니다.</param>
    /// <param name="actual">코드가 실제로 만든 값입니다.</param>
    /// <param name="message">두 값이 다를 때 보여 줄 검증 설명입니다.</param>
    /// <returns>두 값이 같으면 아무 값도 반환하지 않고, 다르면 예외를 던집니다.</returns>
    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} 예상={expected}, 실제={actual}");
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        /// <summary>
        /// 테스트마다 다른 상태·본문·지연을 만들 응답 함수를 Handler에 주입합니다.
        /// </summary>
        /// <param name="responder">HTTP 요청과 취소 토큰을 받아 가짜 응답을 만드는 함수입니다.</param>
        /// <remarks>생성자는 함수를 보관하므로 반환값이 없습니다.</remarks>
        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        /// <summary>
        /// HttpClient의 실제 네트워크 전송을 주입된 응답 함수 호출로 대체합니다.
        /// </summary>
        /// <param name="request">Gateway가 만든 HTTP 요청입니다.</param>
        /// <param name="cancellationToken">Gateway에서 그대로 전달된 취소 신호입니다.</param>
        /// <returns>테스트가 정한 HttpResponseMessage를 담아 완료되는 Task를 반환합니다.</returns>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _responder(request, cancellationToken);
        }
    }

    // 무출력 Test Double은 콘솔이라는 부수 효과 없이 서비스 결과만 검증하게 합니다.
    private sealed class SilentConversionAudit : IConversionAudit
    {
        /// <summary>
        /// 성공 기록 요청을 의도적으로 무시하여 테스트 출력을 조용하게 유지합니다.
        /// </summary>
        /// <param name="receipt">서비스가 만든 성공 결과이며 이 Test Double에서는 사용하지 않습니다.</param>
        /// <returns>아무 작업도 하지 않으므로 반환값은 없습니다.</returns>
        public void RecordSuccess(ConversionReceipt receipt)
        {
        }

        /// <summary>
        /// 실패 기록 요청을 의도적으로 무시하여 반환 Result 검증에만 집중합니다.
        /// </summary>
        /// <param name="request">실패한 환산 요청이며 이 Test Double에서는 사용하지 않습니다.</param>
        /// <param name="problem">실패 정보이며 이 Test Double에서는 사용하지 않습니다.</param>
        /// <returns>아무 작업도 하지 않으므로 반환값은 없습니다.</returns>
        public void RecordFailure(ConversionRequest request, DomainError problem)
        {
        }
    }
}
