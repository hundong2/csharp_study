# question

- semantic kernel을 활용해서 기존 singleton인 class의 함수를 호출하여 return 된 데이터 형식을 미리 지정한 prompt로 분석하여 해당 return된 값으로 singleton내 함수를 호출하여 결과를 prompt와 조합하여 만들어주는 예제코드  

Semantic Kernel(SK)을 사용해 기존 Singleton 클래스의 함수 반환값을 분석하고, 그 분석 결과를 다시 Singleton의 다른 함수로 전달하여 최종 결과를 프롬프트와 조합하는 예제 코드를 작성해 드립니다.

이 시나리오는 **기존 비즈니스 로직(Singleton)은 그대로 유지**하면서 **AI(Semantic Kernel)를 중간에 연결**하여 로직을 지능적으로 확장하는 일반적인 통합 패턴입니다.

-----

## 1\. 예제 Singleton 클래스 (기존 코드)

먼저, 우리가 연동할 기존 코드로 사용할 간단한 Singleton 클래스를 정의합니다. 이 클래스에는 두 가지 메서드가 있습니다.

1.  `GetRawData()`: 분석할 원본 데이터를 반환합니다. (첫 번째 호출)
2.  `ProcessAnalyzedData(string processedInfo)`: AI가 분석한 정보를 받아 최종 비즈니스 로직을 처리합니다. (두 번째 호출)

<!-- end list -->

```csharp
// 기존에 존재하던 비즈니스 로직 클래스라고 가정합니다.
public sealed class BusinessLogicSingleton
{
    // 표준적인 Thread-safe Singleton 패턴
    private static readonly BusinessLogicSingleton instance = new BusinessLogicSingleton();
    static BusinessLogicSingleton() { }
    private BusinessLogicSingleton()
    {
        // 초기화 로직 (예: DB 연결 등)
        Console.WriteLine("[Singleton] Singleton 인스턴스가 생성되었습니다.");
    }
    public static BusinessLogicSingleton Instance => instance;

    /// <summary>
    * 1. 분석할 원본 데이터를 반환하는 함수
    /// </summary>
    public string GetRawData()
    {
        Console.WriteLine("[Singleton] GetRawData() 호출됨.");
        // 실제로는 DB 조회, 캐시 읽기, 복잡한 계산 등의 결과일 수 있습니다.
        return "The user's request is to check the inventory for product SKU: 'XYZ-987-PDQ'.";
    }

    /// <summary>
    /// 2. AI가 분석/추출한 데이터를 받아 처리하는 함수
    /// </summary>
    /// <param name="productSku">AI가 추출한 SKU 정보</param>
    /// <returns>비즈니스 로직 처리 결과</returns>
    public string ProcessAnalyzedData(string productSku)
    {
        Console.WriteLine($"[Singleton] ProcessAnalyzedData() 호출됨 (SKU: {productSku}).");
        
        // SKU를 기반으로 재고 시스템을 조회한다고 가정합니다.
        if (productSku == "XYZ-987-PDQ")
        {
            return "SKU: XYZ-987-PDQ / Status: In Stock / Quantity: 150 units";
        }
        else
        {
            return $"SKU: {productSku} / Status: Not Found";
        }
    }
}
```

-----

## 2\. Semantic Kernel 프롬프트 정의

이 시나리오에서는 두 개의 프롬프트가 필요합니다.

1.  **분석 프롬프트**: Singleton의 `GetRawData()`가 반환한 텍스트에서 **SKU**를 추출합니다.
2.  **조합 프롬프트**: Singleton의 `ProcessAnalyzedData()`가 반환한 **처리 결과**를 받아 사용자 친화적인 메시지로 포맷팅합니다.

<!-- end list -->

```csharp
// 1. 분석 프롬프트 (데이터 추출용)
string extractSkuPrompt = @"
Given the following input text, extract the product SKU.
An SKU is typically in the format 'XXX-NNN-XXX'.
If no SKU is found, return 'N/A'.

Input: {{$input}}

SKU:
";

// 2. 조합(포맷팅) 프롬프트 (최종 결과 생성용)
string formatResultPrompt = @"
Format the following inventory status information into a polite, 
customer-facing response.

Status Data: {{$input}}

Customer Response:
";
```

-----

## 3\. SK와 Singleton 연동 메인 로직 (C\#)

이제 Semantic Kernel을 설정하고 위에서 정의한 1, 2번 프롬프트와 Singleton 클래스를 순서대로 호출(Orchestration)합니다.

(※ `.NET 8` 및 최신 `Microsoft.SemanticKernel` 패키지 기준입니다.)

```csharp
using Microsoft.SemanticKernel;
using System.Text;

// --- 1. Semantic Kernel 설정 ---
var builder = Kernel.CreateBuilder();

// (필수) OpenAI 또는 Azure OpenAI 모델을 추가합니다.
// .NET User Secrets, appsettings.json 또는 환경 변수 등에 키가 설정되어 있어야 합니다.
builder.Services.AddOpenAIChatCompletion(
    modelId: "gpt-4-turbo",       // 또는 "gpt-3.5-turbo"
    apiKey: "sk-..."); // 실제 키로 대체

// Kernel 빌드
var kernel = builder.Build();

// --- 2. 프롬프트 함수로 등록 ---
var extractFunction = kernel.CreateFunctionFromPrompt(extractSkuPrompt);
var formatFunction = kernel.CreateFunctionFromPrompt(formatResultPrompt);

Console.WriteLine("--- Semantic Kernel과 Singleton 연동 시작 ---");

// --- 3. [STEP 1] Singleton 인스턴스 가져오기 ---
var singleton = BusinessLogicSingleton.Instance;

// --- 4. [STEP 2] Singleton의 첫 번째 함수 호출 (데이터 가져오기) ---
string rawData = singleton.GetRawData();
Console.WriteLine($"\n[Main] 1. 원본 데이터:\n{rawData}\n");

// --- 5. [STEP 3] SK 프롬프트 1 (분석) 호출 ---
// 원본 데이터를 프롬프트에 $input으로 전달하여 분석(SKU 추출)을 요청
var analysisResult = await kernel.InvokeAsync(
    extractFunction,
    new() { { "input", rawData } }
);

string extractedSku = analysisResult.GetValue<string>()!.Trim();
Console.WriteLine($"[Main] 2. SK 분석 결과 (추출된 SKU):\n{extractedSku}\n");

// --- 6. [STEP 4] Singleton의 두 번째 함수 호출 (추출된 값으로 로직 처리) ---
// SK가 추출한 SKU 값을 Singleton의 다른 함수로 전달
string businessResult = singleton.ProcessAnalyzedData(extractedSku);
Console.WriteLine($"[Main] 3. Singleton 처리 결과:\n{businessResult}\n");

// --- 7. [STEP 5] SK 프롬프트 2 (조합) 호출 ---
// Singleton의 최종 처리 결과를 프롬프트에 $input으로 전달하여 포맷팅
var finalResult = await kernel.InvokeAsync(
    formatFunction,
    new() { { "input", businessResult } }
);

string finalResponse = finalResult.GetValue<string>()!.Trim();
Console.WriteLine($"[Main] 4. 최종 조합 결과 (사용자 응답):\n{finalResponse}");

Console.WriteLine("\n--- 연동 종료 ---");
```

-----

## 4\. 실행 결과 예시

위 코드를 실행하면 다음과 같은 순서로 로그가 출력됩니다.

```
--- Semantic Kernel과 Singleton 연동 시작 ---
[Singleton] Singleton 인스턴스가 생성되었습니다.

[Main] 1. 원본 데이터:
[Singleton] GetRawData() 호출됨.
The user's request is to check the inventory for product SKU: 'XYZ-987-PDQ'.

[Main] 2. SK 분석 결과 (추출된 SKU):
XYZ-987-PDQ

[Main] 3. Singleton 처리 결과:
[Singleton] ProcessAnalyzedData() 호출됨 (SKU: XYZ-987-PDQ).
SKU: XYZ-987-PDQ / Status: In Stock / Quantity: 150 units

[Main] 4. 최종 조합 결과 (사용자 응답):
Hello! We've checked the inventory for product SKU: XYZ-987-PDQ.
It is currently In Stock with 150 units available.

--- 연동 종료 ---
```

## 5\. 핵심 요약

이 예제의 핵심은 다음과 같습니다.

1.  **관심사 분리 (SoC)**: 비즈니스 로직(Singleton)과 AI의 자연어 처리 로직(SK Prompts)이 명확하게 분리되었습니다. Singleton은 SKU가 무엇인지, 어떻게 추출하는지 알 필요가 없습니다.
2.  **데이터 흐름**: `Singleton.Get()` $\rightarrow$ `SK (분석/추출)` $\rightarrow$ `Singleton.Process()` $\rightarrow$ `SK (조합/포맷팅)`
3.  **유연성**: 만약 나중에 원본 데이터(`GetRawData`)의 형식이 "SKU: [번호]"에서 "Product ID: [번호]"로 변경되더라도, `extractSkuPrompt` 프롬프트만 수정하면 Singleton 코드를 전혀 변경하지 않고도 시스템이 정상 작동하게 할 수 있습니다.