프롬프트를 실행한 결과(LLM의 응답)를 C\# 클래스 객체로 바로 받아오는 방법은 Semantic Kernel의 매우 강력한 기능 중 하나입니다.

가장 간단하고 현대적인 방법은 **`InvokePromptAsync<T>()`** 제네릭 메서드를 사용하는 것입니다.

이 방법을 사용하면 LLM이 응답으로 생성한 JSON을 SK가 자동으로 C\# 클래스로 역직렬화(Deserialize)하여 반환해 줍니다.

-----

## 1\. 데이터를 채울 C\# 클래스 정의

먼저 LLM의 응답을 채워 넣을 대상 C\# 클래스(DTO)를 정의합니다.

```csharp
// 프롬프트의 결과로 채워질 대상 클래스
public class TicketRequest
{
    [Description("예매할 목적지 (예: 부산, 대구)")]
    public string Destination { get; set; }

    [Description("예매할 티켓 수량")]
    public int Quantity { get; set; }

    [Description("선호하는 좌석 등급 (예: First, Economy)")]
    public string SeatPreference { get; set; } = "Economy"; // 기본값
}
```

  * `[Description]` 어트리뷰트는 필수 사항은 아니지만, LLM이 각 속성(Property)의 의미를 더 잘 파악하여 정확한 데이터를 채워 넣는 데 큰 도움이 됩니다.

-----

## 2\. 프롬프트 작성 (JSON 요구)

LLM이 정의한 `TicketRequest` 클래스와 일치하는 JSON 형식으로 응답하도록 프롬프트를 작성해야 합니다.

**핵심:** SK의 `InvokePromptAsync<T>()`를 사용하면, SK가 내부적으로 "이 클래스에 맞는 JSON으로 응답해줘"라는 지시를 LLM에 전달합니다. (과거에는 개발자가 프롬프트에 JSON 예시를 직접 넣어야 했지만, 최신 SK와 OpenAI 모델(JSON 모드 지원)을 사용하면 이 과정이 자동화됩니다.)

따라서 프롬프트는 **JSON 형식을 명시하기보다, "정보를 추출하라"는 본질적인 작업**에 집중하면 됩니다.

```csharp
// LLM에게 정보 추출을 지시하는 프롬프트
string extractionPrompt = @"
사용자의 요청에서 다음 정보를 추출하십시오:
1. 목적지
2. 수량
3. 좌석 등급 (언급이 없으면 기본값 사용)

사용자 요청: {{$input}}
";
```

-----

## 3\. `InvokePromptAsync<T>()`를 사용하여 클래스로 받기

이제 Kernel을 설정하고 `InvokePromptAsync<T>()`를 호출할 때, 제네릭 타입으로 `TicketRequest`를 지정합니다.

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI; // (필요시)
using System.Text.Json;
using System.ComponentModel;

// 1. 커널 설정 (OpenAI 모델이 JSON 모드를 잘 지원합니다)
var builder = Kernel.CreateBuilder();
builder.Services.AddOpenAIChatCompletion(
    modelId: "gpt-4-turbo", // 또는 gpt-3.5-turbo (JSON 모드 지원 모델)
    apiKey: "sk-..."); 
var kernel = builder.Build();

// 2. 위에서 정의한 프롬프트
string extractionPrompt = @"
사용자의 요청에서 다음 정보를 추출하십시오:
1. 목적지
2. 수량
3. 좌석 등급 (언급이 없으면 기본값 사용)

사용자 요청: {{$input}}
";

// 3. 사용자 입력
string userInput = "부산 가는 기차표 3장, 1등석으로 부탁해.";

// 4. (핵심) InvokePromptAsync<T>() 호출
// 제네릭 타입으로 <TicketRequest>를 지정합니다.
Console.WriteLine("[SK] 사용자 요청을 TicketRequest 클래스로 변환 요청 중...");

TicketRequest ticketRequest = await kernel.InvokePromptAsync<TicketRequest>(
    extractionPrompt,
    new() { { "input", userInput } }
);

// 5. 결과 확인
Console.WriteLine("\n[SK] 클래스 변환 완료!");
Console.WriteLine($"목적지: {ticketRequest.Destination}");
Console.WriteLine($"수량: {ticketRequest.Quantity}");
Console.WriteLine($"좌석: {ticketRequest.SeatPreference}");
```

-----

## 4\. 실행 결과

```
[SK] 사용자 요청을 TicketRequest 클래스로 변환 요청 중...

[SK] 클래스 변환 완료!
목적지: 부산
수량: 3
좌석: First
```

-----

## 내부 동작 원리 (How it works)

1.  SK가 `InvokePromptAsync<TicketRequest>` 호출을 받습니다.
2.  SK는 `TicketRequest` 클래스의 구조(속성 이름, 타입, `[Description]`)를 분석합니다.
3.  SK가 LLM(예: OpenAI)에게 사용자의 프롬프트(`extractionPrompt`)와 함께 "이 프롬프트의 결과를 `TicketRequest` 클래스 구조에 맞는 JSON 형식으로 응답해"라고 요청합니다. (이때 모델의 "JSON 모드"가 활성화됩니다.)
4.  LLM은 요청을 분석하여 다음과 같은 **JSON 문자열**을 생성합니다.
    ```json
    {
      "Destination": "부산",
      "Quantity": 3,
      "SeatPreference": "First"
    }
    ```
5.  SK가 이 JSON 문자열을 수신합니다.
6.  SK는 `System.Text.Json` (또는 설정된 직렬화기)을 사용하여 이 JSON 문자열을 `TicketRequest` C\# 객체로 **자동 역직렬화**합니다.
7.  개발자는 `TicketRequest` 타입의 객체를 깔끔하게 전달받습니다.

이 방식은 프롬프트의 결과를 받아 C\# 코드로 직접 함수를 호출하거나 비즈니스 로직에 전달해야 할 때 가장 효율적이고 강력한 방법입니다.