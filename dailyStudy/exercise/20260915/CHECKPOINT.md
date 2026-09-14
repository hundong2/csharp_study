# 2026-09-15 이해도 점검 — CQRS와 이벤트 프로젝션

먼저 답을 펼치지 말고 각 질문을 한두 문장으로 설명하세요. 막히면 “다시 볼 파일”의 코드와 구조도를 직접 따라갑니다.

## 1. CQRS는 무엇을 분리하나요?

<details>
<summary>답 보기</summary>

시스템 상태를 바꾸는 Command 책임과 상태를 관찰하는 Query 책임을 분리합니다. 같은 프로세스와 같은 데이터베이스를 써도 책임과 모델을 분리하면 CQRS일 수 있으며, 반드시 두 서비스나 두 DB가 필요한 것은 아닙니다.

다시 볼 파일: [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs), [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs), [`README.md`](./README.md)

</details>

## 2. CQRS를 사용하면 Event Sourcing도 반드시 사용해야 하나요?

<details>
<summary>답 보기</summary>

아닙니다. CQRS는 읽기와 쓰기 책임의 분리이고, Event Sourcing은 현재 row 대신 과거 이벤트 연속을 원본 상태로 삼는 저장 방식입니다. 오늘 예제는 두 개념의 경계를 비교하기 위해 함께 사용합니다.

다시 볼 파일: [`README.md`](./README.md)

</details>

## 3. `DeploymentStarted`는 왜 과거형 이름인가요?

<details>
<summary>답 보기</summary>

이벤트는 “시작해 달라”는 요청이 아니라 “시작되었다”는 이미 일어난 사실입니다. 과거형 이름은 Command와 Event를 구분하고, append된 뒤 과거가 바뀌지 않는다는 의도를 드러냅니다.

다시 볼 파일: [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs)

</details>

## 4. 불변 `record`가 이벤트에 잘 맞는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

이벤트의 속성을 생성 뒤 다시 할당하기 어렵게 하고, 같은 구성 값을 가진 이벤트를 값으로 비교할 수 있기 때문입니다. 다만 record 안에 변경 가능한 컬렉션을 넣으면 깊은 불변성이 자동으로 생기지는 않으므로 별도 복사 정책이 필요합니다.

다시 볼 파일: [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs)

</details>

## 5. `Rehydrate`는 무엇을 하나요?

<details>
<summary>답 보기</summary>

저장된 이벤트를 처음부터 순서대로 내부 `ApplyHistoryEvent`에 적용해 Aggregate의 현재 상태와 stream version을 다시 계산합니다. 이벤트를 새로 발생시키거나 Event Store에 다시 쓰는 작업이 아닙니다.

다시 볼 파일: [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs)

</details>

## 6. 판단 메서드와 `ApplyHistoryEvent`를 나누는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

`Register`·`Start`·`Succeed`·`Fail`은 현재 상태에서 Command가 허용되는지 판단하고 새 이벤트를 만들며, `ApplyHistoryEvent`는 이미 확정된 이벤트로 상태를 계산합니다. 분리하면 live command와 replay가 같은 상태 계산 규칙을 공유하면서, replay 중 새 이벤트를 다시 만들지 않습니다.

다시 볼 파일: [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs)

</details>

## 7. expected stream version은 어떤 문제를 막나요?

<details>
<summary>답 보기</summary>

두 요청이 같은 과거 상태를 읽고 서로의 변경을 덮어쓰는 lost update를 막습니다. append 시 실제 version이 읽었던 expected version과 다르면 충돌로 거절하고, 호출자는 최신 stream을 다시 읽어 Command를 재판단해야 합니다.

다시 볼 파일: [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs), [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 8. stream version과 global position은 어떻게 다른가요?

<details>
<summary>답 보기</summary>

stream version은 한 deployment 안에서의 이벤트 순서이고, global position은 모든 deployment 이벤트를 하나의 구독 순서로 읽기 위한 위치입니다. Aggregate 동시성에는 stream version을, Projection checkpoint에는 global position을 사용합니다.

다시 볼 파일: [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs), [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 9. Command handler가 Read Model까지 바로 쓰면 왜 위험한가요?

<details>
<summary>답 보기</summary>

Event Store append와 Read Model 갱신이라는 두 쓰기 중 하나만 성공하는 dual-write 문제가 생깁니다. 오늘은 Command가 원본 이벤트만 append하고, Projection이 별도로 Read Model을 갱신해 실패 시 checkpoint부터 재처리할 수 있게 합니다.

다시 볼 파일: [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs), [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 10. Command 성공 직후 Query가 이전 값을 돌려줄 수 있는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

이벤트 append와 Projection 처리가 서로 다른 단계이기 때문입니다. Projection이 아직 새 global position까지 따라잡지 않았다면 Read Model은 의도적으로 뒤처져 있으며, 이를 eventual consistency라고 합니다.

다시 볼 파일: [`Program.cs`](./src/DeploymentDashboardExercise/Program.cs), [`README.md`](./README.md)

</details>

## 11. Projection은 왜 멱등해야 하나요?

<details>
<summary>답 보기</summary>

consumer가 처리 후 checkpoint 저장 전에 멈추거나 응답을 잃으면 같은 envelope가 다시 전달될 수 있기 때문입니다. 같은 identity를 재처리해도 횟수와 상태가 두 번 변하지 않아야 안전하게 retry할 수 있습니다.

다시 볼 파일: [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs), [`SelfTests.cs`](./src/DeploymentDashboardExercise/SelfTests.cs)

</details>

## 12. position gap을 조용히 건너뛰면 어떤 문제가 생기나요?

<details>
<summary>답 보기</summary>

빠진 이벤트가 나중에 도착해도 checkpoint보다 오래된 것으로 취급되어 영원히 적용되지 않을 수 있습니다. 예제가 연속 position을 계약으로 검증하는 이유는 손상이나 잘못된 consumer 순서를 빨리 드러내기 위해서입니다.

다시 볼 파일: [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 13. Query Service는 왜 Aggregate 대신 `DashboardRow`를 읽나요?

<details>
<summary>답 보기</summary>

대시보드가 필요한 모양으로 Projection해 둔 읽기 전용 모델이고, Query 시에는 그 행들을 결정적으로 정렬하므로 매번 모든 이벤트를 replay하지 않아도 됩니다. Aggregate는 상태 전이 불변식을 보호하는 쓰기 모델이고, DashboardRow는 화면 조회에 맞춘 읽기 모델입니다.

다시 볼 파일: [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 14. 어떤 실패를 Result 패턴으로 표현하나요?

<details>
<summary>답 보기</summary>

빈 ID나 허용되지 않는 상태 전이처럼 호출자가 이해하고 고칠 수 있는 예상 가능한 실패입니다. Event Store는 stale expected version을 `ExpectedVersionConflictException`으로 알리지만 `DeploymentCommandService`는 이를 `deployment.concurrency_conflict` 실패 `CommandResult`로 번역합니다. 깨진 이벤트 순서와 Port 계약 위반은 예외로 빠르게 드러냅니다.

다시 볼 파일: [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs), [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs)

</details>

## 15. 취소를 실패 Result로 바꾸지 않는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

취소는 잘못된 업무 입력이 아니라 호출자가 요청한 협력적 중단입니다. 같은 `CancellationToken`을 Port까지 전달하고 `OperationCanceledException`을 보존해야 상위 계층이 취소와 장애를 구분할 수 있습니다.

다시 볼 파일: [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs), [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs)

</details>

## 16. replay와 일반 catch-up은 무엇이 다른가요?

<details>
<summary>답 보기</summary>

일반 catch-up은 기존 checkpoint 이후의 새 이벤트만 적용합니다. replay는 비어 있는 새 Read Model과 checkpoint에서 과거 전체를 다시 적용해 모델을 재구축하거나 새 Projection 로직을 검증합니다.

다시 볼 파일: [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs), [`SelfTests.cs`](./src/DeploymentDashboardExercise/SelfTests.cs)

</details>

## 17. in-memory Event Store가 운영 저장소가 아닌 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

프로세스 종료 시 기록을 잃고, 여러 replica가 같은 lock과 데이터를 공유하지 않으며, 내구성 transaction·백업·복구·보안·감사를 제공하지 않기 때문입니다. 예제는 계약과 흐름을 결정적으로 배우기 위한 Adapter입니다.

다시 볼 파일: [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs), [`README.md`](./README.md)

</details>

## 18. 운영 Event Sourcing에 추가로 필요한 것은 무엇인가요?

<details>
<summary>답 보기</summary>

event schema/version과 upcaster, 내구성 있는 append transaction, snapshot 정책, Projection checkpoint transaction, retry·quarantine, 개인정보·보존 정책, 모니터링, 백업·복구, command idempotency 등이 필요합니다.

다시 볼 파일: [`README.md`](./README.md), [`EXERCISES.md`](./EXERCISES.md)

</details>

---

## 코드로 돌아가는 지도

| 막힌 주제 | 다시 볼 곳 |
| --- | --- |
| nullable, 불변 event record, Aggregate 상태 전이, replay | [`Domain.cs`](./src/DeploymentDashboardExercise/Domain.cs) |
| Event envelope, Event Store Port, Command Service | [`Application.cs`](./src/DeploymentDashboardExercise/Application.cs) |
| 메모리 Event Store, Query, Projection, checkpoint, Read Model Port·Adapter | [`Infrastructure.cs`](./src/DeploymentDashboardExercise/Infrastructure.cs) |
| Composition Root와 projection 전후 데모 | [`Program.cs`](./src/DeploymentDashboardExercise/Program.cs) |
| 회귀 계약과 가짜 입력 | [`SelfTests.cs`](./src/DeploymentDashboardExercise/SelfTests.cs) |
| 운영 한계와 구조도 | [`README.md`](./README.md) |

## 최종 한 문장

빈칸을 보지 않고 말해 보세요.

> “CQRS는 ______와 ______의 책임을 나누고, Event Sourcing은 ______를 원본으로 저장한다. Command가 append한 뒤 ______가 Read Model을 갱신하므로 Query는 잠시 ______일 수 있으며, consumer는 같은 envelope의 ______를 안전하게 처리해야 한다.”

정답 키워드: `Command`, `Query`, `이벤트`, `Projection`, `stale`, `재전달`.
