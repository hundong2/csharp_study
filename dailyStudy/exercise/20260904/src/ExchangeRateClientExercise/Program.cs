// 파일 맨 위의 using은 System.Text 안의 Encoding을 전체 이름 없이 쓰게 해 줍니다.
using System.Text;

// Program은 구현 객체를 만들고 서로 연결하는 Composition Root입니다.
// 의존성 조립을 한곳에 모으면 Domain/Application은 구체 Adapter를 몰라도 되어 교체와 테스트가 쉬워집니다.
// static class는 객체를 만들지 않고 프로그램 전체에 하나뿐인 진입점과 조립 함수만 모을 때 사용합니다.
static class Program
{
    /// <summary>
    /// 명령행 옵션을 확인하여 자체 테스트 또는 deterministic 환율 환산 데모를 실행합니다.
    /// </summary>
    /// <param name="args">--self-test를 포함할 수 있는 명령행 인수 배열입니다.</param>
    /// <returns>정상 완료 시 0, 자체 테스트 실패 시 1을 담아 완료되는 Task를 반환합니다.</returns>
    /// <remarks>async는 이 메서드가 await로 비동기 작업을 기다리며, Task&lt;int&gt;가 미래의 종료 코드를 나타냄을 뜻합니다.</remarks>
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // LINQ의 Contains는 배열을 직접 반복하는 코드 없이 특정 옵션이 들어 있는지 의도를 드러냅니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await RunSelfTestsAsync();
        }

        // HttpClient는 연결 풀을 재사용해야 하므로 요청마다 만들지 않고 앱 수명 동안 한 인스턴스를 공유합니다.
        // DemoHttpMessageHandler가 실제 네트워크를 고정 응답으로 바꿔 기본 실행은 인터넷이 필요 없습니다.
        // var는 오른쪽 생성식으로 형식이 명확할 때 이름 반복을 줄이며, 컴파일 뒤 형식은 고정됩니다.
        var demoHandler = new DemoHttpMessageHandler();
        // using var는 Main이 끝날 때 HttpClient와 소유한 Handler를 자동으로 Dispose합니다.
        // 뒤의 { BaseAddress = ... }는 만든 직후 속성을 채우는 객체 초기화(object initializer) 문법입니다.
        using var httpClient = new HttpClient(demoHandler)
        {
            BaseAddress = new Uri("https://rates.example.test/v1/")
        };

        // 인터페이스 형식의 변수에 구현을 주입하는 수동 DI입니다. 실제 서비스에서는 DI Container가 이 조립을 맡을 수도 있습니다.
        IExchangeRateGateway gateway = new HttpExchangeRateGateway(httpClient);
        IRateSelectionPolicy policy = new ExactCurrencyRateSelectionPolicy();
        IConversionAudit audit = new ConsoleConversionAudit();
        var service = new ConvertCurrencyService(gateway, policy, audit);

        // await는 데모 Task가 끝날 때까지 스레드를 붙잡지 않고 기다린 뒤 다음 줄을 실행합니다.
        await RunDemoAsync(service, CancellationToken.None);
        // $"..."는 중괄호 안의 값을 문자열에 넣는 문자열 보간 문법입니다.
        Console.WriteLine($"HTTP 요청 {demoHandler.RequestCount}건");
        return 0;
    }

    /// <summary>
    /// 자체 테스트를 실행하고 예외를 프로세스 종료 코드와 읽기 쉬운 메시지로 바꿉니다.
    /// </summary>
    /// <remarks>매개변수는 없으며 SelfTests가 준비한 고정 입력만 사용합니다.</remarks>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1을 담아 완료되는 Task를 반환합니다.</returns>
    private static async Task<int> RunSelfTestsAsync()
    {
        try
        {
            await SelfTests.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 성공, 동일 통화, 미지원 통화, 잘못된 금액 사례를 차례로 실행해 전체 흐름을 보여 줍니다.
    /// </summary>
    /// <param name="service">Composition Root가 조립한 실제 Application Service입니다.</param>
    /// <param name="cancellationToken">모든 데모 요청과 HTTP Adapter에 전달할 취소 신호입니다.</param>
    /// <returns>모든 데모 환산이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RunDemoAsync(
        ConvertCurrencyService service,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("=== deterministic 외부 환율 API 환산 데모 ===");

        // (decimal, string, string)은 이름이 붙은 여러 값을 한 항목으로 묶는 튜플입니다.
        // [ ... ]는 여러 항목으로 배열을 만드는 C# 컬렉션 식(collection expression)입니다.
        (decimal Amount, string Source, string Target)[] examples =
        [
            // m은 금융 계산에 적합한 decimal 상수임을 뜻하고, 숫자의 _는 큰 수의 자릿수를 읽기 쉽게 나눕니다.
            (100m, "USD", "KRW"),
            (25_000m, "KRW", "JPY"),
            (50m, "EUR", "EUR"),
            (10m, "USD", "XYZ"),
            (-5m, "USD", "KRW")
        ];

        foreach (var example in examples)
        {
            await RunDemoRequestAsync(
                service,
                example.Amount,
                example.Source,
                example.Target,
                cancellationToken);
        }
    }

    /// <summary>
    /// 문자열·숫자 입력을 Domain 요청으로 검증한 뒤 서비스로 환산하고 한 줄 결과를 출력합니다.
    /// </summary>
    /// <param name="service">환율 조회와 환산을 수행할 Application Service입니다.</param>
    /// <param name="amount">환산할 숫자 금액입니다.</param>
    /// <param name="sourceCode">원본 통화 코드 문자열입니다.</param>
    /// <param name="targetCode">목표 통화 코드 문자열입니다.</param>
    /// <param name="cancellationToken">환율 조회를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>한 건의 검증·환산·출력이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RunDemoRequestAsync(
        ConvertCurrencyService service,
        decimal amount,
        string sourceCode,
        string targetCode,
        CancellationToken cancellationToken)
    {
        var requestResult = CreateRequest(amount, sourceCode, targetCode);
        if (!requestResult.IsSuccess)
        {
            // !는 위 성공 검사 덕분에 Problem이 null이 아님을 컴파일러에 알립니다. 실제 null 검사를 대신하지는 않습니다.
            Console.WriteLine(
                $"[INPUT:FAILURE] {amount} {sourceCode}->{targetCode}: " +
                requestResult.Problem!.Message);
            return;
        }

        var result = await service.ConvertAsync(requestResult.Value!, cancellationToken);
        if (!result.IsSuccess)
        {
            Console.WriteLine(
                $"[RESULT:FAILURE] {amount} {sourceCode}->{targetCode}: " +
                result.Problem!.Message);
            return;
        }

        var receipt = result.Value!;
        Console.WriteLine(
            $"[RESULT:SUCCESS] {receipt.Source.Amount:0.####} {receipt.Source.Currency.Code} = " +
            $"{receipt.Converted.Amount:0.####} {receipt.Converted.Currency.Code}");
    }

    /// <summary>
    /// 바깥 문자열과 숫자를 Currency와 Money 값 객체로 단계적으로 검증해 ConversionRequest를 만듭니다.
    /// </summary>
    /// <param name="amount">0보다 커야 하는 원본 금액입니다.</param>
    /// <param name="sourceCode">영문 세 글자여야 하는 원본 통화 코드입니다.</param>
    /// <param name="targetCode">영문 세 글자여야 하는 목표 통화 코드입니다.</param>
    /// <returns>모든 입력이 유효하면 요청 성공 Result, 아니면 첫 번째 검증 실패 Result를 반환합니다.</returns>
    internal static Result<ConversionRequest> CreateRequest(
        decimal amount,
        string sourceCode,
        string targetCode)
    {
        var sourceCurrencyResult = Currency.Create(sourceCode);
        if (!sourceCurrencyResult.IsSuccess)
        {
            return Result<ConversionRequest>.Failure(sourceCurrencyResult.Problem!);
        }

        var targetCurrencyResult = Currency.Create(targetCode);
        if (!targetCurrencyResult.IsSuccess)
        {
            return Result<ConversionRequest>.Failure(targetCurrencyResult.Problem!);
        }

        var moneyResult = Money.Create(amount, sourceCurrencyResult.Value!);
        if (!moneyResult.IsSuccess)
        {
            return Result<ConversionRequest>.Failure(moneyResult.Problem!);
        }

        return Result<ConversionRequest>.Success(
            new ConversionRequest(moneyResult.Value!, targetCurrencyResult.Value!));
    }
}
