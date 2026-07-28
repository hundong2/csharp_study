# 7. 연습 문제와 해설

먼저 답을 가리고 직접 설명하거나 CSX를 수정해 봅니다.

## A. 기초

1. SDK와 Runtime은 무엇이 다른가?
2. `int`가 항상 stack에 있고 `class`가 항상 heap에 있다는 설명은 왜 부정확한가?
3. `Task`와 thread는 같은가?
4. `.csx`, 일반 `.csproj` 프로젝트, .NET 11 file-based app은 어떻게 다른가?
5. `CancellationToken`을 받기만 하고 하위 I/O에 전달하지 않으면 어떤 일이 생기는가?

## B. CLR·JIT

6. C# → Roslyn → IL/metadata → CLR loader → JIT 순서를 자신의 말로 설명한다.
7. Tier 0과 Tier 1을 나누는 이유는 무엇인가?
8. Dynamic PGO와 OSR은 각각 어떤 정보를/시점을 이용하는가?
9. GC가 safe point에서 JIT의 GC info를 필요로 하는 이유는 무엇인가?
10. `condition ? 42 : 42` 최적화가 손으로 쓴 코드보다 compiler/JIT pipeline에서 더 의미 있는 이유는?
11. NativeAOT interface dispatch helper가 call-site binary size를 줄이는 원리를 설명한다.
12. `ExecutionContext`와 `SynchronizationContext`를 비교한다.

## C. Preview 6 기능

13. `ReadOnlySequenceStream`이 `ReadOnlySequence<byte>.ToArray()`보다 유리한 상황은?
14. 동기 DataAnnotations로 원격 DB를 조회하면 왜 문제가 될 수 있는가?
15. C# union과 class hierarchy의 차이를 API 폐쇄성·JSON·pattern 관점에서 비교한다.
16. extension indexer가 instance indexer보다 우선하는가?
17. `StartSuspended`와 PID lookup에서 다뤄야 할 OS resource는?
18. `Vector.Zip/Unzip`은 어떤 데이터 layout 문제를 해결하는가?
19. `dotnet test`의 2단계 Ctrl+C가 필요한 이유는?
20. file-based app의 `#:include .dll`과 CSX의 `#r`을 혼동하면 안 되는 이유는?

## D. Web·Data·Mobile·Container

21. CORS와 CSRF 보호는 왜 같은 것이 아닌가?
22. `[ShortCircuit]`을 인증 endpoint에 붙이면 어떤 위험이 있는가?
23. SignalR client cancellation이 이미 commit된 DB 작업을 자동 rollback하는가?
24. union의 두 case가 같은 JSON shape면 무엇이 필요한가?
25. EF의 `IQueryable<T>` lambda와 `IEnumerable<T>` lambda의 실행 주체는?
26. `IsConstrained(false)` 관계에서 orphan 무결성은 누가 책임지는가?
27. Android가 MediaPicker 중 process를 죽일 수 있다는 사실이 Task 기반 설계에 어떤 영향을 주는가?
28. NativeAOT SDK image 감소와 최종 runtime image 감소가 같은 수치가 아닌 이유는?

## 실습 변형

1. `01_CSharpClrPipeline.csx`의 hot loop 횟수를 바꾸고 시간을 재되, 첫 실행/JIT warm-up과 steady state를 분리한다.
2. `03_AsyncJitSimd.csx`에서 배열 길이를 vector width의 배수가 아니게 바꾸고 tail loop가 필요한 이유를 확인한다.
3. `05_AspNetCoreConcepts.csx`에 same-origin POST, cross-site GET case를 추가한다.
4. `06_DataMobileContainers.csx`에 customer 한 명당 주문 여러 개를 허용하도록 FullJoin 실습을 확장한다.
5. `07_CapstonePipeline.csx`의 timeout을 10ms로 바꾸고 취소 예외를 endpoint 결과 case로 변환한다.

---

## 해설

1. SDK는 build/compiler/template/CLI를 포함하고 Runtime은 이미 빌드된 앱 실행에 필요한 CLR/BCL 중심입니다.
2. 값/참조 형식은 의미 분류입니다. field, boxing, array, escape와 JIT 최적화가 실제 위치를 정합니다.
3. `Task`는 완료를 나타내는 object입니다. I/O 대기에는 전용 thread가 없을 수 있습니다.
4. CSX는 script host, csproj는 명시적 build graph, file-based app은 SDK가 한 `.cs`를 project처럼 처리하는 기능입니다.
5. 호출자는 취소됐다고 생각하지만 실제 하위 작업은 계속되어 resource를 소모합니다.
6. [기초 장](./01-foundations-clr-jit.md#소스에서-cpu-명령까지)의 pipeline을 참고합니다.
7. 빠른 시작과 hot code의 최대 성능을 동시에 얻기 위해서입니다.
8. PGO는 실제 형식/분기 profile을, OSR은 실행 중인 긴 loop를 최적화 code로 옮기는 시점을 다룹니다.
9. GC는 register/stack의 어느 값이 managed reference인지 정확히 알아야 살아 있는 object를 놓치지 않습니다.
10. 앞선 상수 전파와 branch 단순화가 이 IR 모양을 자주 만들 수 있기 때문입니다.
11. 각 call site에 큰 lookup sequence를 복제하지 않고 공유 helper와 patchable dispatch cell을 사용합니다.
12. ExecutionContext는 ambient logical state 흐름, SynchronizationContext는 continuation 실행 위치 정책입니다.
13. Pipelines처럼 multi-segment buffer가 크고 기존 Stream API에 전달해야 할 때 contiguous copy를 피합니다.
14. thread blocking, thread-pool starvation, deadlock 위험이 있으므로 async validation이 필요합니다.
15. union은 compiler가 제한 case와 pattern을 이해하고 Preview 6 JSON/OpenAPI에 통합됩니다. hierarchy는 더 일반적이지만 외부 파생·boilerplate가 있습니다.
16. 아닙니다. 적용 가능한 instance member가 우선합니다.
17. native process handle의 수명, 권한, PID 재사용, Windows 전용 resume 계약을 다룹니다.
18. RGBA/audio/행렬처럼 lane 사이 interleave, de-interleave, rearrange가 필요한 layout입니다.
19. 첫 입력은 graceful cancellation, 두 번째는 멈추지 않는 child test process의 강제 종료를 제공합니다.
20. 전자는 Preview SDK file-based directive, 후자는 script host reference directive로 parser/build model이 다릅니다.
21. CORS는 browser의 cross-origin response 읽기, CSRF는 자동 credential을 이용한 state-changing request 위조를 다룹니다.
22. routing 뒤 auth middleware를 건너뛰어 보호가 사라질 수 있습니다.
23. 아닙니다. 취소는 협력적 실행 중단이며 transaction 보상/rollback은 별도입니다.
24. `[JsonUnion]` classifier 같은 명시적 분류가 필요합니다.
25. IQueryable은 provider가 expression tree를 번역하고, IEnumerable은 .NET code가 delegate를 실행합니다.
26. DB constraint가 없으므로 application/운영 reconciliation이 책임집니다.
27. in-memory continuation과 Task가 사라지므로 durable result recovery와 중복 소비 방지가 필요합니다.
28. SDK는 builder stage이고 최종 image는 별도 base와 publish artifact를 사용하기 때문입니다.

> 이전: [공식 발표 링크 커버리지](./06-link-coverage.md) · 처음으로: [학습 가이드 README](./README.md)
