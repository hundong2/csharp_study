# 2026-09-15 실행 연습 — CQRS와 이벤트 프로젝션

각 과제는 **가설 → 작은 변경 → build → self-test → demo 관찰** 순서로 진행합니다. CQRS와 Event Sourcing은 서로 다른 선택이므로, 기능을 추가할 때 “쓰기 모델과 읽기 모델을 나눠야 하는 이유”와 “이벤트를 원본으로 보존해야 하는 이유”를 각각 설명해 보세요.

## 시작 전 기준선

저장소 루트 `D:\workspace\csharp_study`에서 다음 세 명령이 먼저 성공해야 합니다.

~~~powershell
dotnet build .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release --no-build -- --self-test
dotnet run --project .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release --no-build
~~~

새 메서드나 생성자를 만들면 상단 한글 주석에 목적, 파라미터 의미, 반환값 또는 반환값이 없음을 적으세요. 처음 쓰는 문법과 설계 선택에는 “무엇”뿐 아니라 “왜”도 설명합니다.

---

## Beginner 1 — Command와 Query를 분류하기

목표: 이름이 아니라 **상태를 바꾸는지**로 Command와 Query를 구분합니다.

아래 요구를 표로 분류하고 이유를 한 문장씩 적으세요.

| 요구 | Command 또는 Query | 판단 질문 |
| --- | --- | --- |
| 새 배포 등록 |  | 원본 상태가 바뀌는가? |
| 현재 배포 목록 조회 |  | 관찰만 하는가? |
| 배포 시작 |  | 새 사실이 기록되는가? |
| 성공한 배포 수 조회 |  | 읽기 전용인가? |
| 대시보드 재생성 |  | 원본 이벤트가 바뀌는가, 파생 모델만 바뀌는가? |

그다음 `DeploymentCommandService`와 `DeploymentQueryService`의 public 메서드를 찾아 같은 기준으로 분류하세요. “Query는 절대 비동기일 수 없다” 또는 “Command는 반드시 값을 반환하지 않는다” 같은 규칙을 만들지 마세요. 이 예제에서 중요한 차이는 읽기와 쓰기의 책임입니다.

## Beginner 2 — 불변 이벤트를 시간순으로 읽기

목표: 과거형 이벤트가 “요청”이 아니라 “이미 일어난 사실”임을 이해합니다.

`DeploymentRegistered → DeploymentStarted → DeploymentSucceeded` 순서를 종이에 적고 각 이벤트에서 다음을 찾으세요.

- 공통 배포 ID와 발생 시각
- 그 순간 새로 알게 된 데이터
- 다음 상태를 계산할 때 필요한 데이터
- 나중에 수정하면 과거가 바뀌어 버리는 데이터

`record`의 값 비교와 `sealed` 계층을 설명한 뒤, `DeploymentStarted`를 `StartDeployment`처럼 명령형 이름으로 바꾸면 왜 혼동되는지 적으세요.

## Beginner 3 — 잘못된 상태 전이를 `CommandResult`로 확인하기

목표: 사용자가 예상할 수 있는 거절과 시스템 고장을 구분합니다.

자체 테스트에 다음 사례를 추가하세요.

1. 등록한 배포를 시작하지 않고 성공 처리합니다.
2. 반환된 `CommandResult`가 실패인지 확인합니다.
3. 오류 코드와 설명이 상태 전이 이유를 알려 주는지 확인합니다.
4. Event Store의 stream 길이가 호출 전후로 같은지 확인합니다.

이 실패를 `try/catch`로 검증하지 마세요. Event Store Port는 예상 버전 충돌을 `ExpectedVersionConflictException`으로 알리지만, `DeploymentCommandService`는 호출자가 재시도를 결정할 수 있는 실패 `CommandResult`로 번역합니다. 깨진 stream 순서 같은 저장 계약 위반은 예외로 빠르게 드러냅니다.

## Beginner 4 — LINQ 읽기 모델 정렬 바꾸기

목표: Query가 쓰기 Aggregate 대신 읽기 목적에 맞는 `DashboardRow`를 사용한다는 점을 확인합니다.

최종 표시 순서를 소유한 `DashboardView` 생성자의 LINQ를 다음 순서로 바꾸는 작은 실험을 하세요. `ReadModelState`의 내부 정렬을 바꾸는 것만으로는 `DashboardView`가 다시 정렬하므로 화면 결과가 달라지지 않습니다.

1. 상태 이름 오름차순
2. 배포 환경 이름 ordinal 오름차순
3. 같은 환경에서는 배포 ID 오름차순
4. 마지막에 `ToArray`로 현재 snapshot 고정

원래 정렬과 결과를 비교하고, `InMemoryDeploymentReadModel` 내부 `Dictionary`의 우연한 열거 순서에 의존하면 테스트와 UI가 왜 흔들릴 수 있는지 적으세요.

---

## Intermediate 1 — Projection 지연을 직접 관찰하기

목표: Command 성공과 Query 반영 완료가 같은 사건이 아님을 이해합니다.

1. 배포를 등록하고 시작합니다.
2. `RunOnceAsync`를 호출하기 전에 Query합니다.
3. 결과가 비어 있거나 이전 상태인 것을 확인합니다.
4. Projection을 따라잡게 한 뒤 다시 Query합니다.
5. 두 결과와 checkpoint를 표로 기록합니다.

Command가 Read Model까지 직접 고치게 만들지 마세요. 그렇게 하면 Event Store append와 Read Model 갱신 사이에 실패했을 때 어느 쪽이 진실인지 모르는 dual-write 문제가 생깁니다.

## Intermediate 2 — 중복 delivery를 멱등 처리하기

목표: 같은 `EventEnvelope`가 다시 전달되어도 대시보드가 두 번 변하지 않게 합니다.

Projection에 같은 envelope를 연속으로 두 번 전달하고 다음을 확인하세요.

- 처리된 마지막 global position은 한 번만 전진합니다.
- 현재 상태가 두 번 전이되지 않습니다.
- 마지막 stream version과 global position이 그대로입니다.
- 서로 다른 payload인데 같은 position인 입력은 조용히 무시하지 않고 계약 위반으로 실패합니다.

단순히 event type만 보고 중복을 제거하지 마세요. 서로 다른 두 번의 실제 배포 시작이 같은 type을 가질 수 있습니다. 안정적인 identity 또는 global position이 필요합니다.

## Intermediate 3 — expected version 충돌 재현하기

목표: optimistic concurrency가 lost update를 막는 경계를 확인합니다.

1. 등록 이벤트까지 있는 같은 stream을 두 요청이 version 1에서 읽었다고 가정합니다.
2. 첫 요청이 `DeploymentStarted`를 expected version 1로 Event Store에 append합니다.
3. 두 번째 요청도 별도로 판단한 `DeploymentStarted`를 같은 expected version 1로 append합니다.
4. Event Store의 두 번째 append가 `ExpectedVersionConflictException`으로 실패하고 부분 쓰기가 없는지 확인합니다.
5. `DeploymentCommandService` 경계를 통하면 같은 충돌이 `deployment.concurrency_conflict` 실패 `CommandResult`로 번역되는지도 별도로 확인합니다.

재시도할 때는 이전 Aggregate를 그대로 재사용하지 말고 최신 stream을 다시 읽어 `Rehydrate`한 뒤 Command를 다시 판단해야 합니다. 이미 성립하지 않는 상태 전이는 새 Result 실패가 될 수 있습니다.

## Intermediate 4 — 취소 경계 검증하기

목표: append 전에 취소된 작업은 아무것도 쓰지 않고, commit 이후 늦은 취소는 성공 결과를 불확실하게 만들지 않는 정책을 이해합니다.

최소 테스트:

- 이미 취소된 토큰으로 Command 호출 → `OperationCanceledException`, event 0개
- load 뒤 append 직전 취소 → event 수 변화 없음
- append 완료 뒤 토큰 취소 → 이미 commit한 사실을 실패 Result로 되돌리지 않음
- Projection 시작 전 취소 → checkpoint와 Read Model 변화 없음

실제 DB에서는 transaction commit 응답이 유실될 수 있습니다. 그 경우 Command ID, idempotency key, 상태 조회 또는 reconciliation으로 결과 불확실성을 해소해야 합니다.

---

## Advanced 1 — replay로 읽기 모델 재구축하기

목표: Event Store를 원본으로 두면 파생 Read Model을 다시 만들 수 있음을 증명합니다.

1. 여러 deployment stream에 이벤트를 기록합니다.
2. 첫 Read Model과 Projection을 끝까지 따라잡습니다.
3. 비어 있는 새 Read Model과 새 Projection을 만듭니다.
4. global position 0부터 모든 이벤트를 replay합니다.
5. 두 대시보드 snapshot이 값과 순서까지 같은지 비교합니다.

원래 Read Model의 객체를 복사하지 마세요. 새 Projection이 Event Store만으로 같은 결과를 만드는 것이 과제의 핵심입니다.

## Advanced 2 — Projection gap을 fail-fast 하기

목표: position 10 다음에 12가 왔을 때 11을 영원히 건너뛰지 않도록 합니다.

`IDeploymentReadModel.TryApplyAsync` 구현에 연속 position 계약을 검증하는 테스트를 추가하세요. 순수 `DeploymentDashboardProjection`은 한 stream 행의 상태 전이만 계산하고 전역 checkpoint를 소유하지 않습니다.

- 첫 position이 1이 아닌 경우 실패
- checkpoint 다음 position이 정확히 `checkpoint + 1`이 아닌 경우 실패
- 실패 시 checkpoint와 Read Model을 바꾸지 않음
- 같은 envelope의 재전달만 멱등 성공

운영 consumer가 partition을 여러 개 사용한다면 “전역 연속 숫자” 대신 partition별 offset과 ordering key가 필요합니다. 예제 계약을 메시지 브로커에 그대로 복사하지 말고 저장 기술의 보장을 문서화하세요.

## Advanced 3 — 이벤트 schema 진화와 upcaster

목표: 오래 저장한 이벤트를 새 코드도 읽을 수 있게 합니다.

`DeploymentRegisteredV1`에는 배포 환경만 있고, V2에는 산출물 버전이 추가됐다고 가정합니다.

1. 저장 envelope에 event type과 schema version을 명시합니다.
2. V1 payload를 현재 domain event로 바꾸는 upcaster를 만듭니다.
3. 과거 V1과 현재 V2가 섞인 replay 테스트를 작성합니다.
4. 알 수 없는 type/version은 skip하지 말고 quarantine 또는 명시적 실패 정책을 선택합니다.

이미 저장된 과거 JSON을 제자리 수정하지 마세요. 감사·재생 가능성을 지키려면 변환 책임과 적용 시점을 별도로 둡니다.

## Advanced 4 — Snapshot 최적화

목표: 이벤트가 수십만 개인 긴 stream도 빠르게 복원하면서 원본 이벤트를 유지합니다.

`IDeploymentSnapshotStore` Port를 설계하고 다음 규칙을 적으세요.

- snapshot에는 Aggregate 상태와 해당 stream version을 함께 저장
- snapshot 이후 이벤트만 읽어 이어서 적용
- snapshot checksum/schema version 검증 실패 시 전체 replay로 fallback
- snapshot 저장 실패가 원본 append 성공을 되돌리지 않음
- snapshot은 최적화이며 진실의 원본이 아님

성능 측정 없이 snapshot을 무조건 추가하지 말고, stream 길이·복원 시간·메모리를 먼저 관찰하세요.

---

## Pro 1 — 내구성 있는 Projection checkpoint

목표: 프로세스 재시작 뒤에도 마지막 처리 지점부터 안전하게 이어 갑니다.

운영용 `IDeploymentReadModel` Adapter가 row 갱신과 checkpoint 전진을 **같은 transaction**에 넣도록 설계하세요.

테스트할 장애 지점:

- row 갱신 전 crash
- row 갱신 후 checkpoint 전 crash
- transaction commit 후 응답 유실
- 같은 envelope redelivery
- poison event 때문에 반복 실패

row와 checkpoint를 따로 commit하면 중복 또는 유실이 생길 수 있습니다. atomic transaction이 불가능한 저장소라면 멱등 event ID와 재처리 규칙을 명시합니다.

## Pro 2 — Read-your-writes 요구 설계하기

목표: eventual consistency를 숨기지 않고 제품 요구에 맞는 UX/API 계약을 고릅니다.

아래 중 하나를 선택하고 장단점을 적으세요.

- Command 응답에 새 stream version을 포함하고, Query가 최소 version까지 기다림
- Command 응답에 상태 snapshot을 포함해 UI가 임시 표시
- Projection 완료 notification 뒤 화면 갱신
- stale 표시와 수동 refresh 허용

무제한 polling은 피하고 timeout, 취소, 부하, 실패 메시지를 함께 설계하세요. 모든 화면에 강한 일관성이 필요하다면 CQRS의 별도 Read Model이 오히려 복잡성만 늘릴 수 있습니다.

## Pro 3 — 다중 projector와 순서 보장

목표: 여러 consumer가 병렬 처리해도 같은 deployment의 이벤트 순서가 뒤집히지 않게 합니다.

설계에 다음을 포함하세요.

- partition key로 deployment ID 사용
- partition 안에서는 stream version 순서 유지
- consumer group과 lease/fencing
- retry와 dead-letter/quarantine
- lag, 처리량, 실패 횟수, 마지막 checkpoint metric
- replay 작업과 live consumer의 전환 절차

“at least once”는 같은 이벤트가 다시 올 수 있다는 뜻입니다. “같은 Aggregate 내 순서”와 “전체 시스템의 단일 전역 순서”는 비용과 보장이 다르므로 구분하세요.

## Pro 4 — CQRS를 쓰지 않을 조건 설명하기

목표: 패턴을 적용하는 능력뿐 아니라 제거하는 판단력을 기릅니다.

다음 조건의 작은 CRUD 시스템을 가정하고 한 문단의 의사결정 기록을 쓰세요.

- 읽기와 쓰기의 데이터 모양이 거의 같음
- 트래픽과 확장 요구가 낮음
- 감사용 전체 이력이 필요하지 않음
- 한 transaction의 즉시 일관성이 중요함
- 운영 인력이 적음

단일 모델·일반 관계형 테이블이 더 단순한 이유와, 어떤 요구가 생기면 CQRS 또는 이벤트 소싱을 다시 검토할지 적으세요.

---

## 모든 과제의 최종 검증

~~~powershell
dotnet build .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release --no-build -- --self-test
dotnet run --project .\dailyStudy\exercise\20260915\src\DeploymentDashboardExercise\DeploymentDashboardExercise.csproj -c Release --no-build
~~~

- [ ] 새 C# 메서드와 생성자마다 목적·파라미터·반환값 한글 설명이 있다.
- [ ] 처음 등장하는 문법과 설계 선택의 이유를 주석으로 설명했다.
- [ ] Command 성공 직후 Query가 잠시 stale일 수 있음을 테스트했다.
- [ ] 중복 envelope, position gap, stale expected version을 서로 다른 계약으로 검증했다.
- [ ] 취소, 예상 가능한 Result 실패, 인프라·계약 예외를 구분했다.
- [ ] replay한 새 Read Model이 기존 결과와 같다.
- [ ] `bin/`, `obj/`를 stage하거나 commit하지 않았다.
- [ ] Release build 경고 0·오류 0, self-test와 demo 성공을 확인했다.
