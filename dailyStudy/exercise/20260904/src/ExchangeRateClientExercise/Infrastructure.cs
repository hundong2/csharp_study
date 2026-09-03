using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Gateway Adapter는 HTTP와 JSON이라는 외부 세부사항을 Application의 IExchangeRateGateway Port에 맞춥니다.
sealed class HttpExchangeRateGateway : IExchangeRateGateway
{
    // 외부 응답은 신뢰할 수 없으므로 64 KiB까지만 버퍼링하고 JSON 중첩도 얕게 제한해 과도한 메모리 사용을 막습니다.
    private const long MaxResponseBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        MaxDepth = 8,
        // 중복 JSON 속성을 허용하면 앞의 환율을 뒤 값이 조용히 덮을 수 있으므로 신뢰 경계에서 명시적으로 거부합니다.
        AllowDuplicateProperties = false
    };

    private readonly HttpClient _httpClient;

    /// <summary>
    /// 애플리케이션 수명 동안 재사용할 HttpClient를 외부에서 주입받습니다.
    /// </summary>
    /// <param name="httpClient">BaseAddress와 Handler가 이미 설정된 재사용 가능한 HTTP 클라이언트입니다.</param>
    /// <remarks>생성자는 의존성을 보관할 뿐 반환값이 없습니다. 요청마다 HttpClient를 만들지 않아 연결 고갈을 피합니다.</remarks>
    public HttpExchangeRateGateway(HttpClient httpClient)
    {
        // ?? throw는 주입값이 null이면 나중의 모호한 오류 대신 조립 위치에서 즉시 알려 줍니다.
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 최신 환율 endpoint를 호출하고 HTTP 상태와 JSON을 확인한 뒤 DTO를 Domain 환율 표로 변환합니다.
    /// </summary>
    /// <param name="baseCurrency">API의 base 쿼리에 넣을 검증된 기준 통화입니다.</param>
    /// <param name="cancellationToken">호출자가 HTTP 전송과 JSON 읽기를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>정상 응답이면 ExchangeRateTable, 통신·상태·JSON 문제가 있으면 실패 Result를 비동기로 반환합니다.</returns>
    /// <remarks>async/await는 HTTP 대기 중 스레드를 점유하지 않으며, 같은 취소 토큰을 전송·버퍼링·역직렬화까지 전달합니다.</remarks>
    public async Task<Result<ExchangeRateTable>> GetLatestRatesAsync(
        Currency baseCurrency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseCurrency);

        try
        {
            var encodedBase = Uri.EscapeDataString(baseCurrency.Code);

            // using은 요청과 응답이 가진 네이티브 자원을 메서드가 끝날 때 자동으로 정리합니다.
            // HttpClient 자체는 재사용하지만, 가벼운 요청/응답 객체는 매 호출 뒤 폐기하는 것이 안전합니다.
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"latest?base={encodedBase}");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result<ExchangeRateTable>.Failure(
                    new DomainError(
                        ErrorKind.RemoteService,
                        $"환율 API가 HTTP {(int)response.StatusCode} 상태를 반환했습니다."));
            }

            // 크기 제한 버퍼링은 Content-Length가 없거나 거짓인 응답도 실제로 읽으면서 상한을 지킵니다.
            await response.Content.LoadIntoBufferAsync(
                MaxResponseBytes,
                cancellationToken);

            // await using은 비동기 처리가 끝날 때 응답 스트림의 DisposeAsync를 기다려 안전하게 정리합니다.
            await using var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // DeserializeAsync<T>의 <T>는 JSON을 어떤 DTO 형식으로 만들지 지정하는 제네릭 문법입니다.
            var dto = await JsonSerializer.DeserializeAsync<ExchangeRateResponseDto>(
                jsonStream,
                SerializerOptions,
                cancellationToken);

            return MapToDomain(dto, baseCurrency);
        }
        // 취소는 정상적인 제어 흐름이므로 Result 실패로 숨기지 않고 호출자까지 그대로 전파합니다.
        // catch 뒤 when은 괄호 조건이 참인 예외만 잡는 예외 필터로, 여기서는 호출자가 요청한 취소만 구별합니다.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // 호출자 토큰이 취소되지 않았는데 난 취소 예외는 HttpClient 제한 시간 같은 원격 대기 실패로 취급합니다.
        catch (OperationCanceledException)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(
                    ErrorKind.RemoteService,
                    "환율 API 응답 제한 시간을 초과했습니다."));
        }
        catch (HttpRequestException)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(
                    ErrorKind.RemoteService,
                    "환율 API 연결 또는 응답 읽기에 실패했습니다."));
        }
        catch (JsonException)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(
                    ErrorKind.InvalidPayload,
                    "환율 JSON을 해석하지 못했습니다."));
        }
        catch (NotSupportedException)
        {
            return Result<ExchangeRateTable>.Failure(
                new DomainError(
                    ErrorKind.InvalidPayload,
                    "지원하지 않는 JSON 형식입니다."));
        }
    }

    /// <summary>
    /// 외부 API 모양의 DTO를 검증하면서 내부 Domain 값 객체와 환율 표로 번역합니다.
    /// </summary>
    /// <param name="dto">JSON 역직렬화 결과이며 JSON null이면 null일 수 있습니다.</param>
    /// <param name="requestedBase">요청에 사용한 기준 통화로 응답의 기준 통화와 일치해야 합니다.</param>
    /// <returns>모든 필드가 유효하면 Domain 환율 표, 아니면 InvalidPayload 실패 Result를 반환합니다.</returns>
    private static Result<ExchangeRateTable> MapToDomain(
        ExchangeRateResponseDto? dto,
        Currency requestedBase)
    {
        if (dto is null)
        {
            return InvalidPayload("환율 API 응답 본문이 null입니다.");
        }

        var baseResult = Currency.Create(dto.Base);
        if (!baseResult.IsSuccess)
        {
            return InvalidPayload("응답의 기준 통화가 없거나 형식이 잘못되었습니다.");
        }

        var responseBase = baseResult.Value!;
        if (responseBase != requestedBase)
        {
            return InvalidPayload(
                $"요청 기준 통화 {requestedBase.Code}와 응답 기준 통화 {responseBase.Code}가 다릅니다.");
        }

        if (!DateOnly.TryParseExact(
                dto.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var rateDate))
        {
            return InvalidPayload("응답 날짜는 yyyy-MM-dd 형식이어야 합니다.");
        }

        if (dto.Rates is null)
        {
            return InvalidPayload("응답에 rates 객체가 없습니다.");
        }

        if (dto.Rates.Count > 200)
        {
            return InvalidPayload("응답의 환율 항목이 허용된 200개를 넘었습니다.");
        }

        var mappedRates = new List<KeyValuePair<Currency, decimal>>();
        foreach (var pair in dto.Rates)
        {
            var currencyResult = Currency.Create(pair.Key);
            if (!currencyResult.IsSuccess)
            {
                // 외부 key 원문에는 개행·제어 문자가 섞일 수 있어 로그로 이어지는 오류 메시지에 그대로 넣지 않습니다.
                return InvalidPayload("응답에 형식이 잘못된 목표 통화 코드가 있습니다.");
            }

            mappedRates.Add(
                new KeyValuePair<Currency, decimal>(currencyResult.Value!, pair.Value));
        }

        return ExchangeRateTable.Create(responseBase, rateDate, mappedRates);
    }

    /// <summary>
    /// DTO 검증 오류 메시지를 동일한 종류의 실패 Result로 만드는 중복을 줄입니다.
    /// </summary>
    /// <param name="message">어떤 응답 필드가 잘못되었는지 설명하는 문장입니다.</param>
    /// <returns>InvalidPayload 종류와 입력 메시지를 가진 실패 Result를 반환합니다.</returns>
    private static Result<ExchangeRateTable> InvalidPayload(string message)
    {
        return Result<ExchangeRateTable>.Failure(
            new DomainError(ErrorKind.InvalidPayload, message));
    }
}

// DTO(Data Transfer Object)는 외부 JSON 필드 모양만 표현하고 업무 규칙은 Domain에 맡깁니다.
sealed class ExchangeRateResponseDto
{
    // JsonPropertyName은 C# 속성 이름과 실제 JSON 키를 명시적으로 연결하는 특성(attribute)입니다.
    // init은 역직렬화로 객체를 만들 때만 값을 넣고 그 뒤에는 바꾸지 못하게 하는 전용 setter입니다.
    [JsonPropertyName("base")]
    public string? Base { get; init; }

    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("rates")]
    public Dictionary<string, decimal>? Rates { get; init; }
}

// 이 Handler는 실제 인터넷 대신 고정 JSON을 반환하는 deterministic Adapter입니다.
// 같은 입력은 늘 같은 결과를 내므로 학습·데모·테스트가 네트워크 상태나 실시간 환율에 흔들리지 않습니다.
sealed class DemoHttpMessageHandler : HttpMessageHandler
{
    private int _requestCount;

    // Volatile.Read는 여러 비동기 요청이 동시에 실행되어도 다른 스레드가 최신 횟수를 안전하게 읽게 합니다.
    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>
    /// 요청의 base 쿼리를 읽어 미리 정한 환율 JSON을 담은 HTTP 200 응답을 만듭니다.
    /// </summary>
    /// <param name="request">HttpClient가 보낸 method, URL, header를 가진 요청입니다.</param>
    /// <param name="cancellationToken">호출자가 응답 생성을 중단할 때 확인할 취소 신호입니다.</param>
    /// <returns>네트워크 없이 즉시 완성되는 HttpResponseMessage Task를 반환합니다.</returns>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Interlocked.Increment는 동시 요청끼리 값을 덮어쓰지 않도록 증가를 하나의 원자적 연산으로 수행합니다.
        Interlocked.Increment(ref _requestCount);
        cancellationToken.ThrowIfCancellationRequested();

        var baseCurrency = ReadQueryValue(request.RequestUri, "base");

        // switch 식은 입력값에 따라 결과 하나를 고르는 문법이며, _는 앞 조건에 없는 모든 값을 뜻합니다.
        Dictionary<string, decimal>? rates = baseCurrency switch
        {
            // ["KRW"]는 Dictionary의 key를 지정하면서 값을 넣는 인덱서 초기화 문법입니다.
            "USD" => new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["KRW"] = 1390.25m,
                ["JPY"] = 147.80m,
                ["EUR"] = 0.92m
            },
            "KRW" => new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["USD"] = 0.00072m,
                ["JPY"] = 0.1063m,
                ["EUR"] = 0.00066m
            },
            "EUR" => new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["USD"] = 1.0870m,
                ["KRW"] = 1511.14m,
                ["JPY"] = 160.65m
            },
            _ => null
        };

        if (rates is null || baseCurrency is null)
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":\"unsupported base currency\"}",
                        Encoding.UTF8,
                        "application/json")
                });
        }

        var dto = new ExchangeRateResponseDto
        {
            Base = baseCurrency,
            Date = "2026-09-04",
            Rates = rates
        };
        var json = JsonSerializer.Serialize(dto);

        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// URL 쿼리 문자열에서 지정한 key의 값을 찾아 URL 인코딩을 해제합니다.
    /// </summary>
    /// <param name="uri">쿼리를 포함할 수 있는 요청 URI이며 없을 수도 있습니다.</param>
    /// <param name="key">찾을 쿼리 매개변수 이름입니다.</param>
    /// <returns>찾은 문자열 값, URI나 key가 없으면 null을 반환합니다.</returns>
    private static string? ReadQueryValue(Uri? uri, string key)
    {
        if (uri is null)
        {
            return null;
        }

        var query = uri.Query.TrimStart('?');
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}

// Console Adapter는 감사 Port를 사람이 바로 볼 수 있는 출력으로 구현합니다.
sealed class ConsoleConversionAudit : IConversionAudit
{
    /// <summary>
    /// 성공한 환산의 핵심 값과 기준 날짜를 한 줄로 콘솔에 기록합니다.
    /// </summary>
    /// <param name="receipt">출력할 원본·환산 금액과 적용 환율입니다.</param>
    /// <returns>콘솔에 기록만 하므로 반환값은 없습니다.</returns>
    public void RecordSuccess(ConversionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Console.WriteLine(
            $"[AUDIT:SUCCESS] {receipt.Source.Currency.Code}->{receipt.Converted.Currency.Code}, " +
            $"rate={receipt.AppliedRate}, date={receipt.RateDate:yyyy-MM-dd}");
    }

    /// <summary>
    /// 실패한 환산 방향과 오류 종류·설명을 한 줄로 콘솔에 기록합니다.
    /// </summary>
    /// <param name="request">실패한 원본 통화와 목표 통화를 가진 요청입니다.</param>
    /// <param name="problem">실패 분류와 사람이 읽을 설명입니다.</param>
    /// <returns>콘솔에 기록만 하므로 반환값은 없습니다.</returns>
    public void RecordFailure(ConversionRequest request, DomainError problem)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(problem);
        Console.WriteLine(
            $"[AUDIT:FAILURE] {request.Source.Currency.Code}->{request.Target.Code}, " +
            $"kind={problem.Kind}, message={problem.Message}");
    }
}
