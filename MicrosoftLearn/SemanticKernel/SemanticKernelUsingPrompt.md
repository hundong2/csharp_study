# question

- 일반 사용자가 prompt를 작성하면 특정 json 데이터를 읽기위한 class에 데이터를 채워 해당 클래스를 기반으로 함수를 호출해야 하는 명령을 수행하고, 해당 수행을 통해 생성 된 class 데이터를 기반으로 다시 llm에서 결과를 도출해야 할 때 semantic kernel은 어떻게 해야하는걸까?  

이해하신 흐름이 정확하며, 이는 Semantic Kernel(SK)의 핵심 기능 중 하나인 \*\*"플러그인(Plugin)"\*\*과 \*\*"오케스트레이션(Orchestration)"\*\*을 활용하는 전형적인 시나리오입니다.

두 가지 주요 접근 방식이 있습니다.

1.  **수동 오케스트레이션**: 프롬프트로 JSON을 추출하고, C\# 코드로 직접 함수를 호출한 뒤, 그 결과를 다시 프롬프트에 넘기는 방식입니다. (사용자께서 질문하신 흐름과 가장 유사합니다.)
2.  **자동 함수 호출 (Function Calling)**: SK에 C\# 함수(네이티브 함수)를 등록해두면, LLM이 스스로 C\# 함수를 호출해야 한다고 판단하고 실행하는 방식입니다. (더 강력하고 권장됩니다.)

-----

## 시나리오 설정: KTX 예매 시스템

이해를 돕기 위해, "부산행 KTX 2장 예매"라는 사용자 프롬프트로 KTX 예매 함수를 호출하는 시나리오를 예로 들겠습니다.

### 준비물: C\# 클래스 및 서비스 (기존 코드)

먼저, 사용자(혹은 LLM)가 채워야 할 \*\*요청 클래스(DTO)\*\*와 비즈니스 로직이 반환할 **결과 클래스**를 정의합니다.

```csharp
// 1. LLM이 채워야 할 데이터 클래스 (JSON으로 변환될 대상)
public class TicketRequest
{
    public string Destination { get; set; }
    public int Quantity { get; set; }
    public string TrainType { get; set; } = "KTX"; // 기본값
}

// 2. 비즈니스 로직(함수)이 반환할 결과 클래스
public class BookingResult
{
    public bool IsSuccess { get; set; }
    public string ConfirmationCode { get; set; }
    public string Message { get; set; }
    public int TotalPrice { get; set; }
}

// 3. 호출 대상이 되는 C# 함수 (비즈니스 로직)
// (SK에서는 이 클래스 자체를 '플러그인'으로 등록합니다)
public class BookingService
{
    [KernelFunction] // SK가 이 함수를 인식하도록 태그 지정
    public BookingResult BookTrainTicket(TicketRequest request)
    {
        Console.WriteLine($"[C# 함수 호출] 목적지: {request.Destination}, 수량: {request.Quantity}");

        // ... (실제 예매 로직 수행) ...
        
        // 결과 클래스를 생성하여 반환
        if (request.Destination == "부산")
        {
            return new BookingResult
            {
                IsSuccess = true,
                ConfirmationCode = "KR-BUSAN-12345",
                Message = "부산행 KTX 2장 예매가 완료되었습니다.",
                TotalPrice = 119600
            };
        }
        else
        {
            return new BookingResult { IsSuccess = false, Message = "해당 목적지는 예매할 수 없습니다." };
        }
    }
}
```

-----

## 방법 1: 수동 오케스트레이션 (JSON 추출 -\> C\# 호출 -\> 결과 포맷팅)

이 방식은 각 단계를 명시적으로 제어합니다.

### 1단계: 프롬프트 1 (사용자 입력 -\> JSON 추출)

먼저 사용자의 자연어 입력을 `TicketRequest` 클래스 형식의 JSON으로 변환하는 프롬프트를 만듭니다.

```csharp
string extractJsonPrompt = @"
사용자의 요청을 분석하여 다음 JSON 형식에 맞게 데이터를 추출하십시오.
요청에 언급되지 않은 값은 기본값을 사용하거나 비워두십시오.
목적지는 '부산', '대구' 등 도시 이름만 추출하십시오.

[JSON 형식]
{
  ""Destination"": ""string"",
  ""Quantity"": int,
  ""TrainType"": ""string""
}

[사용자 요청]
{{$input}}

[JSON 출력]
";
```

### 2단계: 프롬프트 2 (C\# 결과 클래스 -\> 사용자 응답)

C\# 함수가 반환한 `BookingResult` 객체를 받아 사용자에게 친절하게 응답하는 프롬프트를 만듭니다.

```csharp
string formatResponsePrompt = @"
다음 예매 처리 결과를 바탕으로 사용자에게 친절한 응답 메시지를 생성하십시오.
성공 시, 확인 코드와 총액을 반드시 포함하십시오.
실패 시, 실패 사유를 설명하십시오.

[처리 결과]
{{$input}}

[사용자 응답]
";
```

### 3단계: 전체 오케스트레이션 코드 (C\#)

```csharp
using System.Text.Json;
using Microsoft.SemanticKernel;

var kernel = Kernel.CreateBuilder()
                   .AddOpenAIChatCompletion(...) // 모델 설정
                   .Build();

// 프롬프트 함수로 등록
var extractFunction = kernel.CreateFunctionFromPrompt(extractJsonPrompt);
var formatFunction = kernel.CreateFunctionFromPrompt(formatResponsePrompt);

// 비즈니스 로직 인스턴스 생성
var bookingService = new BookingService();

// --- 오케스트레이션 시작 ---
string userInput = "다음 달 부산으로 가는 KTX 기차표 2장 예매해줘.";

// 1. 프롬프트 1 실행 (JSON 추출)
var jsonResult = await kernel.InvokeAsync(
    extractFunction, 
    new() { { "input", userInput } }
);

string jsonOutput = jsonResult.GetValue<string>();
Console.WriteLine($"[SK 1단계 - JSON 추출] \n{jsonOutput}");

// 2. C# 클래스로 변환 (Deserialization)
var ticketRequest = JsonSerializer.Deserialize<TicketRequest>(jsonOutput);

// 3. C# 함수 직접 호출
BookingResult bookingResult = bookingService.BookTrainTicket(ticketRequest);
Console.WriteLine($"[C# 함수 결과] 성공: {bookingResult.IsSuccess}, 코드: {bookingResult.ConfirmationCode}");

// 4. 결과 클래스를 다시 문자열(JSON)로 변환 (프롬프트 입력을 위해)
string bookingResultString = JsonSerializer.Serialize(bookingResult);

// 5. 프롬프트 2 실행 (최종 응답 생성)
var finalResponse = await kernel.InvokeAsync(
    formatFunction,
    new() { { "input", bookingResultString } }
);

Console.WriteLine($"[SK 2단계 - 최종 응답]\n{finalResponse.GetValue<string>()}");
```

-----

## 방법 2: 자동 함수 호출 (Auto Function Calling) (권장)

이 방식은 **LLM이 C\# 함수를 직접 인식하고 호출**하도록 합니다. 코드가 훨씬 간결해집니다.

### 1단계: 플러그인 등록

`BookingService` 인스턴스를 SK 커널에 "플러그인"으로 등록합니다.

```csharp
var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(...);
// BookingService 인스턴스를 'BookingPlugin'이라는 이름으로 커널에 등록
builder.Plugins.AddFromObject(new BookingService(), "BookingPlugin");
var kernel = builder.Build();
```

### 2단계: 자동 호출 설정 및 실행

`InvokeAsync`를 호출할 때 `OpenAIPromptExecutionSettings`를 통해 함수 호출을 허용(Auto)하도록 설정합니다.

```csharp
using Microsoft.SemanticKernel.Connectors.OpenAI;

string userInput = "다음 달 부산으로 가는 KTX 기차표 2장 예매해줘.";

// 'ToolCallBehavior.AutoInvokeKernelFunctions' 설정이 핵심입니다.
var settings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

// 프롬프트와 설정을 함께 전달
var result = await kernel.InvokePromptAsync(userInput, new(settings));

// 결과 출력
Console.WriteLine(result.GetValue<string>());
```

### 자동 호출의 내부 동작

위 코드를 실행하면 SK 내부에서는 다음과 같은 일이 자동으로 발생합니다.

1.  **[LLM]**: SK가 LLM에게 사용자 프롬프트(`"부산행 KTX..."`)와 함께 "너는 `BookingPlugin`에 있는 `BookTrainTicket`이라는 C\# 함수를 사용할 수 있어. 이 함수는 `TicketRequest` 객체를 인자로 받아."라고 알려줍니다.
2.  **[LLM]**: LLM이 프롬프트를 분석하고, "이 요청을 처리하려면 `BookTrainTicket` 함수를 호출해야겠다. 인자는 `Destination='부산'`, `Quantity=2`로 채워야지."라고 판단합니다.
3.  **[SK]**: SK가 LLM의 "호출 요청"을 받아, C\#의 `BookingService.BookTrainTicket(request)` 함수를 **실제로 실행**합니다.
4.  **[C\#]**: C\# 함수가 실행되고 `BookingResult` 객체(성공, 확인 코드 등)를 SK에 반환합니다.
5.  **[LLM]**: SK가 이 C\# 반환값(`BookingResult`)을 다시 LLM에게 "함수 실행 결과는 이거야."라고 전달합니다.
6.  **[LLM]**: LLM이 C\# 함수의 실행 결과를 보고 최종 사용자 응답("예매가 완료되었습니다. 확인 코드는...")을 생성하여 반환합니다.

-----

## 결론 및 권장

  * \*\*방법 1 (수동)\*\*은 데이터의 변환(JSON)과 호출 시점을 명확하게 제어해야 할 때 유용합니다.
  * \*\*방법 2 (자동 함수 호출)\*\*는 코드가 훨씬 간결하며, LLM이 문맥을 파악하여 알아서 C\# 함수를 호출하므로 더 유연하고 강력합니다. **대부분의 경우 이 방법을 권장합니다.**