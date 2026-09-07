# 2026-09-08 실습 문제 — Beginner to Pro

먼저 기본 코드를 실행해 `--self-test`가 16/16인지 확인하세요. 각 단계가 끝날 때마다 Release 빌드와 자체 테스트를 다시 실행합니다.

## Beginner — 상태를 손으로 추적하기

### 문제 1. 주문 입력 바꾸기

`Program.cs`의 주문 금액을 `0m`으로 바꾸고 실행 전에 다음을 적으세요.

- 예상 Result의 성공/실패
- 예상 오류 코드
- 예상 주문/Outbox/외부 전달 건수

실행 결과와 비교한 뒤 원래 양수 금액으로 되돌립니다.

완료 기준: `Order.Create`에서 어느 `if`가 실행되는지 줄 단위로 설명할 수 있습니다.

### 문제 2. 첫 실패 뒤 메시지 표 만들기

첫 Dispatcher 실행 직후의 `OutboxMessage`를 다음 표로 손으로 채우세요.

| 필드 | 예상 값 | 그렇게 되는 코드 |
| --- | --- | --- |
| `MessageId` |  |  |
| `PublishedAtUtc` |  |  |
| `AttemptCount` |  |  |

완료 기준: “발행 실패인데 왜 메시지를 삭제하지 않는가?”에 두 문장으로 답할 수 있습니다.

## Intermediate — 유효성 규칙과 테스트 추가

### 문제 3. 주문 ID 길이 규칙

`Order.Create`에 주문 ID가 40자를 넘으면 `order.id_too_long` Result를 반환하는 규칙을 추가하세요.

1. 먼저 `SelfTests`에 41자 ID가 저장되지 않는 실패 테스트를 작성합니다.
2. 테스트가 실패하는 것을 확인합니다.
3. 최소 구현을 추가해 통과시킵니다.

완료 기준: 주문과 Outbox가 모두 0건이고 기존 16개 테스트도 계속 통과합니다.

### 문제 4. 통화 코드 값 객체

`USD`, `KRW`, `EUR`만 허용하는 불변 `Currency` record를 만들고 주문에 포함하세요. 지원하지 않는 값은 예외가 아니라 입력 Result 실패로 표현합니다.

완료 기준: 정상 KRW와 실패 JPY 테스트가 있고 JSON payload에도 통화가 포함됩니다.

## Advanced — 재시도 정책 분리

### 문제 5. Retry Strategy

`IRetryPolicy`를 만들고 `AttemptCount`에 따라 다음 시도 가능 여부를 결정하게 하세요.

- 최대 3회까지만 발행
- `AttemptCount >= 3`인 메시지는 이번 실행에서 건너뜀
- 정책은 `OutboxDispatcher` 생성자로 주입

실제 `Task.Delay`를 넣지 말고, “지금 시도 가능한가?”만 결정하는 순수 Strategy로 시작합니다.

완료 기준: 시작 전 `AttemptCount`가 2면 한 번 더 시도하고, 3과 4면 시도하지 않는 경계 테스트가 있으며 Dispatcher 코드를 바꾸지 않고 다른 정책 구현을 주입할 수 있습니다.

### 문제 6. Dead Letter 상태

`PublishedAtUtc?`만으로 부족한 이유를 설명하고 `OutboxStatus` enum(`Pending`, `Published`, `DeadLetter`)을 도입하세요. 최대 실패 뒤 Dead Letter로 보내는 테스트를 먼저 작성합니다.

완료 기준: 완료 메시지와 격리 메시지가 둘 다 Pending 조회에서 빠지며, 격리 이유를 보존합니다.

## Pro — 분산 시스템의 중복과 경쟁 다루기

### 문제 7. 멱등 소비자 Inbox

가짜 소비자에 `HashSet<Guid>` 또는 별도 `IInboxRepository`를 두어 같은 `MessageId`를 두 번 받아도 주문 확인 메일 효과는 한 번만 기록되게 구현하세요.

다음 순서의 테스트가 필수입니다.

1. 서로 다른 ID 두 개는 각각 처리됩니다.
2. 같은 ID 두 번은 두 번째를 건너뜁니다.
3. 처리 효과와 Inbox ID 저장도 실제 DB에서는 같은 소비자 트랜잭션이어야 함을 주석으로 설명합니다.

완료 기준: `MarkFailureSimulationCanDuplicateAsync`처럼 생산자가 두 번 전달해도 소비자 효과가 한 번임을 증명합니다.

### 문제 8. 다중 Dispatcher claim 설계

현재 `GetPendingAsync` 뒤 두 Dispatcher가 동시에 같은 메시지를 읽을 수 있습니다. 아래 필드를 가진 claim/lease 설계를 의사 코드와 Mermaid sequence diagram으로 작성하세요.

- `ClaimedBy`
- `ClaimedUntilUtc`
- 행 버전 또는 조건부 UPDATE
- lease 만료 뒤 복구 규칙

가능하면 메모리 구현에 `TryClaimBatchAsync(workerId, nowUtc, lease, limit)`을 추가하고 두 Task를 `TaskCompletionSource` 장벽으로 동시에 시작하는 테스트를 작성하세요. `Thread.Sleep`으로 타이밍을 맞추지 않습니다.

완료 기준: 두 작업자가 같은 ID를 동시에 획득하지 않으며, 만료된 lease는 다시 획득됩니다.

### 문제 9. EF Core 운영 매핑 설계

코드를 바로 작성하기 전에 다음을 문서로 설계하세요.

- `Orders`와 `OutboxMessages` 테이블 및 고유 키
- 하나의 `DbContext.SaveChangesAsync` 또는 명시적 트랜잭션 경계
- Pending 조회 인덱스
- batch 크기와 정렬 기준
- 재시도 횟수·다음 시도 시각·Dead Letter 필드
- payload 버전·보존 기간·개인정보 정책
- Pending 개수와 가장 오래된 메시지 나이 경보

완료 기준: exactly-once라고 쓰지 않고 at-least-once와 소비자 멱등성을 명시합니다.

## 매 단계 공통 검증 명령

```powershell
dotnet build dailyStudy/exercise/20260908/src/TransactionalOutboxExercise/TransactionalOutboxExercise.csproj -c Release
dotnet run --project dailyStudy/exercise/20260908/src/TransactionalOutboxExercise/TransactionalOutboxExercise.csproj -c Release --no-build -- --self-test
```
