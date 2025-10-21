`void` (반환값이 없는) 함수를 넘겨주는 것도 C\# 객체를 플러그인으로 등록하는 **자동 함수 호출(Auto Function Calling)** 방식을 그대로 사용하시면 됩니다.

LLM은 해당 함수가 `void`라는 것을 인지하고, 함수를 실행한 뒤 "성공적으로 실행되었습니다"라는 사실을 바탕으로 다음 응답을 생성합니다.

## 1\. AgentTest 클래스 정의

가장 중요한 것은 LLM이 호출할 `testrun` 함수 위에 `[KernelFunction]` 어트리뷰트(Attribute)를 붙여주는 것입니다.

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel; // Description을 위해 추가

public class AgentTest
{
    [KernelFunction]
    [Description("테스트 실행을 위한 에이전트를 작동시킵니다. 시스템 테스트를 시작할 때 호출됩니다.")]
    public void testrun()
    {
        // 이 함수는 반환값이 없습니다 (void).
        Console.WriteLine("\n[C# 함수 실행] AgentTest.testrun()이 성공적으로 호출되었습니다!");
        // (실제 로직: 시스템 상태 변경, 파일 쓰기, API 호출 등...)
    }

    // (참고) 다른 함수가 있어도 SK는 [KernelFunction]이 붙은 것만 인식합니다.
    public void InternalHelperFunction()
    {
        // 이 함수는 LLM에 노출되지 않습니다.
    }
}
```

  * `[KernelFunction]`: 이 함수를 SK 커널(및 LLM)에 노출시킵니다.
  * `[Description(...)]`: **(매우 중요)** LLM에게 이 함수가 *어떤 용도*인지 설명해 줍니다. LLM은 이 설명을 보고 언제 이 함수를 호출할지 판단합니다.

-----

## 2\. Kernel에 플러그인으로 등록 및 호출

이제 `AgentTest` 클래스의 인스턴스를 생성하여 커널에 "플러그인"으로 등록합니다.

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// 1. 커널 빌더 생성
var builder = Kernel.CreateBuilder();
builder.Services.AddOpenAIChatCompletion(
    modelId: "gpt-4-turbo", // 또는 gpt-3.5-turbo
    apiKey: "sk-...");     // 실제 키

// 2. AgentTest 인스턴스 생성
var agentPlugin = new AgentTest();

// 3. 커널에 플러그인으로 등록
// "TestAgent"라는 이름으로 플러그인을 추가합니다.
builder.Plugins.AddFromObject(agentPlugin, "TestAgent");

var kernel = builder.Build();

// 4. 자동 함수 호출 설정
var settings = new OpenAIPromptExecutionSettings
{
    // LLM이 함수를 호출해야 한다고 판단하면 즉시 실행하도록 설정
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

// 5. 사용자 프롬프트 실행
Console.WriteLine("사용자: 테스트 에이전트 좀 실행해줄래?");

string userInput = "테스트 에이전트 좀 실행해줄래?";
var result = await kernel.InvokePromptAsync(userInput, new(settings));

// 6. 최종 결과 출력
Console.WriteLine($"\nLLM 응답: {result.GetValue<string>()}");
```

-----

## 3\. 실행 결과 및 내부 동작

위 코드를 실행하면 다음과 같은 결과가 나옵니다.

```
사용자: 테스트 에이전트 좀 실행해줄래?

[C# 함수 실행] AgentTest.testrun()이 성공적으로 호출되었습니다!

LLM 응답: 네, 테스트 에이전트를 성공적으로 실행했습니다.
```

### 내부 동작 상세

1.  **[LLM 분석]**: LLM이 "테스트 에이전트 좀 실행해줄래?"라는 입력과 `[Description("테스트 실행을 위한 에이전트를 작동시킵니다...")]` 설명을 비교합니다.
2.  **[LLM 판단]**: LLM이 "이 요청은 `TestAgent` 플러그인의 `testrun` 함수를 호출하라는 의미군."이라고 판단합니다.
3.  **[SK 실행]**: (C\#) SK가 `agentPlugin.testrun()` 함수를 실제로 실행합니다.
4.  **[C\# 실행]**: `testrun` 함수 내부의 `Console.WriteLine(...)`이 실행됩니다. 함수가 `void`이므로 반환값 없이 종료됩니다.
5.  **[LLM 보고]**: SK가 LLM에게 " `testrun` 함수를 호출했고, 성공적으로 완료되었어. (반환값은 없음)"이라고 알려줍니다.
6.  **[LLM 응답]**: LLM은 "함수가 성공적으로 실행되었다"는 사실을 바탕으로 "네, 테스트 에이전트를 성공적으로 실행했습니다."라는 최종 응답을 생성합니다.