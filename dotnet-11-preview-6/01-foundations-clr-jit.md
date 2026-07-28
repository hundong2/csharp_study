# 1. C# 기초에서 CLR·JIT 내부까지

## C# 최소 문법

```csharp
int count = 3;                       // 값 형식 변수
string name = "CLR";                 // 참조 형식 변수
int doubled = count * 2;             // 식(expression)의 결과를 대입
bool large = doubled >= 6;           // 비교 결과는 bool
string text = $"{name}: {doubled}";  // 보간 문자열
```

- **형식(type)**은 값의 표현, 허용 연산, 메타데이터를 정합니다.
- **변수(variable)**는 값을 가리키는 이름입니다. 지역 변수의 실제 위치는 JIT가 레지스터·스택·최적화 제거 중에서 결정합니다.
- 값 형식이 항상 스택, 참조 형식이 항상 힙이라는 설명은 틀립니다. 필드·배열·boxing·escape·JIT 최적화에 따라 배치가 달라집니다.
- `class` 인스턴스 변수에는 객체 자체가 아니라 관리 참조가 들어갑니다.
- `record`는 값 기반 동등성 같은 코드를 컴파일러가 생성하는 데이터 중심 형식입니다.
- 제네릭 `List<T>`의 `T`는 컴파일 시 형식 안전성을 제공합니다. CLR은 값 형식 인스턴스화에는 보통 별도 기계 코드를 만들고, 여러 참조 형식 인스턴스화는 코드를 공유할 수 있습니다.

## 소스에서 CPU 명령까지

```text
Program.cs / script.csx
  └─ Roslyn: 토큰화 → 구문 트리 → 바인딩 → 형식 검사
       └─ PE 파일: CIL(IL) + 메타데이터 + 리소스
            └─ CLR 로더: AssemblyLoadContext → 형식 로드 → MethodTable/EEType
                 └─ 메서드 첫 실행
                      ├─ JIT: IL → 현재 CPU의 네이티브 코드
                      └─ NativeAOT라면 게시 시 대부분 미리 네이티브 코드 생성
```

### IL과 메타데이터

IL은 x64나 Arm64에 종속되지 않는 스택 기반 중간 명령입니다. `a + b`는 대략 인자를 평가 스택에 올리고 `add`, `ret`를 수행합니다. 메타데이터에는 형식, 메서드 서명, 참조 어셈블리, 사용자 지정 특성이 기록됩니다.

CLR 로더는 모든 형식을 즉시 완성하지 않고 필요할 때 로드합니다. 정적 필드, 가상 메서드 슬롯, 인터페이스 맵, GC가 참조 필드를 찾기 위한 정보가 런타임 형식 구조에 연결됩니다.

### JIT 계층 컴파일

1. 첫 호출에는 빠르게 컴파일하는 **Tier 0** 코드가 사용될 수 있습니다.
2. 호출 카운터가 자주 실행되는 메서드, 즉 hot method를 찾습니다.
3. **Tier 1**은 더 많은 시간을 들여 인라이닝, 상수 접기, 범위 검사 제거, 공통 부분식 제거 등을 수행합니다.
4. **Dynamic PGO**는 실제 호출 형식과 분기 빈도를 관찰해 devirtualization과 코드 배치를 개선합니다.
5. **OSR(On-Stack Replacement)**은 오래 도는 루프가 끝나기를 기다리지 않고 실행 도중 최적화 코드로 옮길 수 있게 합니다.

`[MethodImpl(AggressiveInlining)]`은 명령이 아니라 힌트입니다. 코드 크기, 예외 처리, 재귀, 호출 빈도 같은 JIT 휴리스틱이 최종 결정을 내립니다.

## Preview 6 JIT 변경의 의미

- `Math.BigMul(long, long, out long)`은 x64에서 helper call 대신 단일 `MUL r/m64`로 낮아질 수 있습니다. managed 메서드 호출·레지스터 보존·복귀 비용이 사라집니다.
- `condition ? 42 : 42` 같은 IR의 `SELECT(cond, cns, cns)`는 상수 하나로 접힙니다. 사람이 이런 코드를 쓰지 않아도 앞선 최적화 후 이 모양이 생깁니다.
- prolog를 한 instruction group에 넣어야 했던 제한이 없어져 큰 스택 프레임, 저장 레지스터가 많은 메서드, runtime-async 준비 코드의 codegen 제약이 줄었습니다.
- Arm SVE의 `Vector<T>`는 런타임에 폭이 정해지는 scalable type이므로 값 복사 대신 참조 전달이 ABI와 성능에 맞습니다.

중요한 관점은 “C# 한 줄이 언제나 특정 어셈블리 한 줄”이 아니라는 것입니다. Roslyn lowering, IL, JIT IR 변환, CPU ISA와 런타임 프로필이 모두 결과를 바꿉니다.

## GC 내부

관리 힙 할당은 흔히 스레드별 allocation context의 포인터를 증가시키므로 매우 빠릅니다. 공간이 부족하면 GC가 다음을 수행합니다.

1. 레지스터, 스택, 정적 필드, GC handle 등 **root**에서 살아 있는 객체를 찾습니다.
2. 세대별 정책에 따라 Gen 0/1/2 또는 LOH를 수집합니다.
3. 이동 가능한 객체를 압축하고 참조를 갱신합니다.
4. JIT가 만든 **GC info**는 각 safe point에서 어느 위치가 관리 참조인지 알려 줍니다.
5. 오래된 객체가 새 객체를 가리킬 때 **write barrier**가 카드 테이블을 표시해 전체 힙 재탐색을 줄입니다.

Preview 6의 x86 구조체 복사 GC hole, return hijacking 중 반환값 liveness, TLS 정렬 수정은 이 계약이 틀리면 단순 성능 저하가 아니라 메모리 손상·잘못된 수집·충돌이 될 수 있음을 보여 줍니다.

## async/await 내부

전통적 async lowering에서 컴파일러는 지역 변수와 진행 위치를 가진 상태 머신을 만듭니다.

```text
호출 → 동기 구간 실행
  ├─ await 대상 완료: 같은 흐름으로 계속
  └─ 미완료: 상태 저장 → continuation 등록 → 호출자에게 Task 반환
                                      └─ 완료 신호 → 상태 머신 MoveNext 재개
```

- `Task`는 비동기 작업의 완료·결과·예외를 나타냅니다. 스레드 그 자체가 아닙니다.
- I/O await는 대기 동안 스레드를 붙잡지 않을 수 있습니다.
- `SynchronizationContext`는 continuation을 어느 실행 환경으로 보낼지 관여합니다.
- `ExecutionContext`는 `AsyncLocal<T>` 같은 ambient state, 보안·문화권 관련 흐름을 캡처합니다. 둘은 다릅니다.
- `ConfigureAwait(false)`는 특정 `SynchronizationContext`로 돌아가려는 요구를 피하지만, 일반적으로 `ExecutionContext` 흐름을 끄는 API는 아닙니다.

Preview 6 runtime-async는 동기 Task 반환 메서드의 전용 async 버전을 JIT하고, tail call을 runtime-async call/await로 바꾸며, suspension point tail merge와 continuation cache를 사용합니다. 복구할 ambient state가 없으면 `ExecutionContext` 캡처·복원도 건너뜁니다. 의미는 유지하면서 할당과 간접 호출을 줄이는 런타임 최적화입니다.

## NativeAOT와 JIT의 차이

| 항목 | JIT | NativeAOT |
|---|---|---|
| 컴파일 시점 | 실행 중 | publish 시 |
| 현재 실행 프로필 사용 | Dynamic PGO 가능 | 제한적, 빌드 정보 중심 |
| 시작·메모리 | JIT 준비 비용 존재 | 빠른 시작·작은 런타임 가능 |
| 동적 코드/리플렉션 | 유연 | trim/AOT 분석 제약 |
| 배포 | Runtime 필요 가능 | 자체 포함 네이티브 실행 파일 가능 |

Preview 6 NativeAOT 인터페이스 호출은 공유 dispatch helper를 거쳐 warm-up 후 올바른 구현으로 패치될 수 있습니다. 호출 지점마다 큰 fat-pointer 시퀀스를 두는 것보다 코드 크기와 반복 호출 처리량에 유리합니다.

## 직접 확인

```powershell
dotnet script ./01_CSharpClrPipeline.csx
dotnet script ./03_AsyncJitSimd.csx
```

어셈블리를 직접 보고 싶다면 일반 프로젝트로 옮긴 뒤 `ildasm`, ILSpy, `COMPlus_JitDisasm`, BenchmarkDotNet의 DisassemblyDiagnoser 같은 도구를 사용합니다. 디버그/릴리스, Tiered Compilation, CPU가 결과를 바꾸므로 한 번의 출력만 일반화하지 않습니다.

> 🔗 [Runtime Preview 6 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md)

## 공식 기초 링크

- [CLR 개요](https://learn.microsoft.com/dotnet/standard/clr)
- [Managed execution process](https://learn.microsoft.com/dotnet/standard/managed-execution-process)
- [GC 기초](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals)
- [Native AOT 배포](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

> 이전: [C# 최소 문법](./00-csharp-primer.md) · 다음: [Libraries와 Runtime](./02-libraries-runtime.md)
