# 0. 처음 시작하기

Issue 27은 웹·AI·보안·DB·JIT를 한꺼번에 다룹니다. 먼저 [00_BeginnerSyntax.csx](./00_BeginnerSyntax.csx)를 열어 `// 01` 설명과 바로 아래 코드를 한 쌍으로 읽으세요.

## 핵심 용어 사전

| 용어 | 쉬운 뜻 | 과정의 예 |
|---|---|---|
| SDK / Runtime | 앱을 만드는 도구 / 앱을 실행하는 구성요소 | `dotnet build` / CoreCLR |
| IL / JIT | CPU 독립 중간 명령 / 실행 중 기계어 변환기 | Roslyn → IL → RyuJIT |
| GC | 사용하지 않는 관리 객체 메모리 회수기 | Channel에 들어간 `byte[]` |
| protocol | 프로그램 사이 메시지와 순서의 약속 | MCP `tools/call` |
| session | 여러 요청 사이의 연결 문맥 | 과거 `Mcp-Session-Id` |
| stateless | 요청 처리에 숨은 이전 연결 상태가 필요 없음 | self-contained HTTP POST |
| round trip | 요청이 갔다가 응답이 돌아오는 한 왕복 | MRTR의 사용자 입력 왕복 |
| opaque handle | 내부 의미를 추측하지 않고 그대로 돌려주는 식별자 | `requestState`, task ID |
| Agent Skill | 지침·리소스·선택적 스크립트 묶음 | `SKILL.md` |
| assertion | 실제 결과가 기대와 같은지 검사 | `actual == expected` |
| mutation test | 코드를 일부러 틀리게 해 테스트가 실패하는지 확인 | 비교 연산 변경 |
| binlog | MSBuild가 수행한 모든 build event의 이진 기록 | target/task/property/error |
| TFM | 라이브러리가 대상으로 삼는 .NET 계약 이름 | `net10.0`, `netstandard2.0` |
| OIDC | 신뢰 관계로 짧은 identity token을 교환하는 표준 | NuGet Trusted Publishing |
| transaction | 여러 데이터 변경을 모두 성공/모두 실패시키는 경계 | 주문·재고·감사 로그 |
| sargable | index가 바로 찾을 수 있는 검색 조건 | 날짜 열 자체의 범위 비교 |
| execution plan | DB가 query를 실행하기로 선택한 방법 | scan, seek, sort, join |
| ESR | compound index의 Equality→Sort→Range 순서 | customer/status/createdAt |
| tenant | 시스템을 공유하되 데이터·권한을 격리한 고객 | schema-per-tenant |
| backpressure | 생산자가 소비자보다 빠를 때 유입을 제한하는 제어 | bounded Channel |
| PCM | 압축되지 않은 디지털 오디오 sample | 16kHz, 16-bit, mono |
| record | 값 중심 데이터 형식을 간단히 선언하는 C# 문법 | `record Result(...)` |
| shallow copy | 바깥 객체만 새로 만들고 내부 참조는 공유하는 복사 | `sample with { }` |
| boxing | 값 형식을 `object` 같은 참조 형식 상자로 감싸는 할당 | 과거 generic Enum.Equals |
| SIMD | 한 CPU 명령으로 여러 숫자를 처리 | Vector128 Min/Max |

## C# 문장을 읽는 최소 규칙

```csharp
record Request(string Method, string? State);
var requests = new List<Request>();
requests.Add(new("tools/call", null));
bool needsInput = requests.Any(r => r.State is null);
```

- `record`: 생성자 매개변수와 같은 이름의 속성을 만드는 참조 형식입니다.
- `string?`: 문자열이거나 `null`일 수 있습니다. `#nullable enable` 문맥에서 compiler가 검사합니다.
- `var`: runtime 동적 형식이 아니라 오른쪽에서 compile-time 형식을 추론합니다.
- `<Request>`: 목록 원소 형식을 지정하는 generic 문법입니다.
- `new(...)`: 문맥에서 형식을 알 수 있어 `new Request(...)`를 줄인 target-typed new입니다.
- `r => ...`: 값을 받아 식을 계산하는 lambda입니다.
- `is null`: overloaded `==`에 영향받지 않는 null pattern입니다.

## 메모리와 비동기의 최소 지도

```text
메서드 호출 → IL 실행 → 첫 호출 때 JIT 기계어 생성
지역 값       → register 또는 stack slot (최적화에 따라 달라짐)
List/record   → 보통 managed heap, GC가 참조 추적
await         → compiler가 state machine 생성, 완료 뒤 continuation 실행
Channel       → producer/consumer 사이 queue와 backpressure
```

`async`는 새 thread를 자동 생성한다는 뜻이 아닙니다. 완료되지 않은 `Task`를 `await`하면 현재 실행 흐름을 반환하고, 완료 뒤 이어질 코드를 상태 머신에 보존합니다. CPU 작업을 무작정 `Task.Run`으로 감싸는 것과 I/O 비동기는 다릅니다.

## 실습

```powershell
dotnet script .\00_BeginnerSyntax.csx
```

정상 출력에는 `visible = mcp,testing`, `terminal = completed,failed`, `기초 완료`가 포함됩니다.

변형 실험:

1. 상태에 `cancelled`를 추가하고 terminal filter에도 포함합니다.
2. skill의 `Enabled`를 `false`로 바꾸고 보이는 목록을 예상합니다.
3. `await Task.Delay(10)`을 지워도 결과는 같지만 비동기 경계가 사라지는 이유를 설명합니다.

## 다음 단계

- 다음: [MCP C# SDK v2와 원격 Agent Skills](./01-mcp-v2-agent-skills.md)
- 더 깊은 C# 기초: [C# 최소 문법](../dotnet-11-preview-6/00-csharp-primer.md)
- 공식 학습: [C# 둘러보기](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/)
