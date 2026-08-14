# 0. 처음 시작하기: C#과 .NET의 최소 지도

이 문서는 “코드 한 줄도 처음”인 학습자가 뒤의 기사를 읽기 위한 출발점입니다. 먼저 [00_BeginnerSyntax.csx](./00_BeginnerSyntax.csx)를 열어 번호 주석과 그 아래 코드 한 줄을 짝으로 읽으세요.

## 0.1 자주 나오는 용어

| 용어 | 쉬운 뜻 | 이 과정에서의 예 |
|---|---|---|
| C# | 사람이 작성하는 프로그래밍 언어 | `int count = 3;` |
| .NET SDK | 빌드·실행·테스트 도구 묶음 | `dotnet --info`, `dotnet build` |
| .NET Runtime | 이미 빌드된 앱을 실행하는 구성요소 | CoreCLR, 기본 라이브러리 |
| CLR | IL 로드, 형식 검사, JIT, GC, 예외 등을 관리하는 실행 엔진 | CoreCLR 또는 Mono |
| IL | CPU 종류와 독립적인 중간 명령 | 어셈블리의 메서드 본문 |
| JIT | 실행 중 IL을 현재 CPU의 기계어로 바꾸는 컴파일러 | Tier 0, Tier 1 |
| GC | 더 이상 참조되지 않는 관리 객체 메모리를 회수 | 세대 0/1/2, LOH |
| API | 프로그램끼리 약속한 호출 규칙 | MCP 도구, HTTP JSON API |
| JSON | 이름과 값을 텍스트로 표현하는 형식 | `{"status":"working"}` |
| 프로세스 | 실행 중인 프로그램의 격리 단위 | 에이전트와 별도 스크립트 실행기 |
| 스레드 | 프로세스 안에서 명령을 수행하는 흐름 | CLR 스레드 풀 작업자 |
| async/await | 기다리는 동안 스레드를 붙잡지 않고 나중에 이어서 실행하는 문법 | MCP 상태 폴링 |
| null | “값이 없음”을 나타내는 참조 값 | 아직 결과가 없는 workflow |
| record | 값 중심 데이터를 간단히 선언하는 C# 형식 | `record Approval(...)` |
| lambda | 이름 없이 전달하는 작은 함수 | `s => s == "completed"` |
| nullable | 값이 없을 가능성을 형식에 표시 | `string?`, `int?` |
| metadata | 코드/데이터를 설명하는 구조화 정보 | .NET 형식 정보, episode 번호 |
| tenant | 한 시스템을 공유하지만 데이터·권한을 격리한 고객/조직 단위 | tenant별 skill filter/cache |
| context | 현재 판단에 제공되는 코드·지침·대화 정보 | branch 또는 skill 지침 |
| token | 모델이 텍스트를 처리하는 작은 단위 | 불필요한 skill 본문 비용 |
| serialization | 객체를 JSON 같은 저장·전송 형식으로 바꾸는 과정 | MCP 응답 전송 |
| deterministic | 같은 초기 상태와 입력에서 같은 결과가 나오는 성질 | Dev Proxy 평가 |
| idempotent | 같은 요청을 여러 번 적용해도 한 번과 결과가 같은 성질 | activity 재시도 |
| embedding | 텍스트/이미지의 의미를 숫자 벡터로 표현 | frame semantic search |
| NativeAOT | 게시할 때 미리 네이티브 기계어를 만드는 .NET 배포 방식 | 모바일 시작 성능 |
| trimming | 사용되지 않는다고 분석된 코드를 게시물에서 제거 | 앱 크기 축소 |

## 0.2 한 줄을 읽는 순서

```csharp
int retryCount = 3;
```

1. `int`: 값의 형식은 32비트 정수입니다.
2. `retryCount`: 값을 다시 찾기 위한 변수 이름입니다.
3. `=`: 오른쪽 값을 계산해 왼쪽 저장 위치에 대입합니다.
4. `3`: 정수 리터럴입니다.
5. `;`: 한 문장이 끝났음을 나타냅니다.

지역 변수는 보통 메서드의 스택 프레임 또는 JIT가 선택한 CPU 레지스터에 놓입니다. “C# 변수는 항상 스택”은 정확하지 않습니다. 캡처된 변수는 컴파일러가 만든 객체의 필드가 되어 관리 힙으로 이동할 수 있고, JIT 최적화 뒤에는 실제 메모리 위치가 없을 수도 있습니다.

## 0.3 꼭 알아야 할 문법

```csharp
string state = "working";                 // 문자열 변수
bool finished = state == "completed";    // 비교 결과는 true/false
if (finished) Console.WriteLine("완료");  // 조건이 참일 때만 호출

foreach (string item in new[] { "a", "b" })
{
    Console.WriteLine(item);               // 배열의 각 원소를 한 번씩 출력
}

static int Add(int left, int right)        // 입력 형식과 반환 형식을 선언
{
    return left + right;                   // 계산 결과를 호출자에게 반환
}
```

- `new`: 객체나 배열을 만듭니다. 참조 형식 객체는 대개 관리 힙에 할당됩니다.
- `.`: 객체/형식의 멤버에 접근합니다.
- `()` : 메서드를 호출하거나 매개변수를 선언합니다.
- `{}`: 함께 실행되는 블록입니다.
- `=>`: 간단한 함수 본문 또는 람다를 표현합니다.
- `?`: `string?`에서는 null 가능, `x?.Name`에서는 null 안전 접근, `a ? b : c`에서는 조건 연산자입니다.
- `!`: 논리 부정이며, `value!` 형태에서는 컴파일러의 null 경고를 억제할 뿐 런타임 검사를 추가하지 않습니다.

## 0.4 컬렉션과 LINQ

`List<T>`는 순서가 있고 크기가 변하는 목록입니다. `<T>`는 원소 형식을 지정하는 제네릭 문법입니다. `Dictionary<TKey,TValue>`는 키로 값을 찾습니다. LINQ의 `Where`, `Select`, `OrderBy`는 컬렉션을 선언적으로 변환합니다.

```csharp
var states = new List<string> { "working", "completed", "failed" };
var terminal = states.Where(s => s is "completed" or "failed").ToList();
```

`var`는 동적 형식이 아닙니다. 컴파일러가 오른쪽 식으로 `List<string>`을 추론하며, 이후 형식은 고정됩니다. 많은 LINQ 연산은 열거할 때 실행되는 지연 실행입니다. `ToList()`가 실제 열거와 새 목록 할당을 일으킵니다.

## 0.5 비동기의 핵심

```csharp
await Task.Delay(100);
```

`await`는 현재 스레드를 100ms 동안 재우는 명령이 아닙니다. 컴파일러가 메서드를 상태 머신으로 바꾸고, 완료되지 않은 `Task`를 만나면 계속 실행할 위치와 지역 상태를 보관한 뒤 호출자에게 제어를 돌려줍니다. 타이머가 완료되면 continuation이 스케줄되어 이후 코드가 실행됩니다. 기다림이 길다고 작업 자체가 내구성 있게 저장되는 것은 아닙니다. 프로세스가 종료되어도 이어져야 하는 작업에는 뒤에서 배울 Durable Functions 같은 영속 워크플로가 필요합니다.

## 0.6 오류를 읽는 법

1. 가장 먼저 나온 컴파일 오류의 파일명과 줄 번호를 읽습니다.
2. `;`, `}`, 따옴표, 변수 이름, 형식을 확인합니다.
3. 런타임 예외라면 예외 형식·메시지·스택 추적의 첫 사용자 코드 줄을 찾습니다.
4. 입력을 줄여 재현하고, 기대값과 실제값을 함께 출력합니다.

## 0.7 실습 방법

```powershell
dotnet script .\00_BeginnerSyntax.csx
```

정상 출력에는 `평균 = 85.0`, `terminal = completed,failed`, `기초 실습 완료`가 포함됩니다. 그다음 아래를 한 번씩 바꾸세요.

1. 점수 `70`을 `100`으로 바꾸고 평균을 예상합니다.
2. `retryCount < 3`을 `retryCount < 5`로 바꾸고 반복 횟수를 셉니다.
3. 상태 목록에 `cancelled`를 추가하고 종료 상태 조건에도 포함합니다.

## 다음 단계

- 이전: 없음
- 다음: [.NET 11, CLR/JIT, MAUI, 서비스 업데이트](./01-dotnet11-runtime-servicing.md)
- 문법을 더 깊게: [.NET 11 자료의 C# 최소 문법](../dotnet-11-preview-6/00-csharp-primer.md)
- 공식 학습: [C# 둘러보기](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/)
