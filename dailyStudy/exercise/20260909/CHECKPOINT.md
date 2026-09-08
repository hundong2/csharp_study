# 2026-09-09 이해도 체크포인트

코드를 보지 않고 먼저 답하세요. 답을 적은 뒤 접힌 해설을 열어 비교합니다.

## 질문

1. bounded Channel이 해결하는 문제와 새로 만드는 대기는 무엇인가요?
2. capacity 2와 Worker 2는 각각 무엇을 제한하나요?
3. `BoundedChannelFullMode.Wait`와 `DropWrite`의 차이는 무엇인가요?
4. 빈 Channel에서 `ReadAllAsync`가 즉시 끝나는 경우와 기다리는 경우를 구분하세요.
5. `TryComplete()` 직후 버퍼의 작업은 어떻게 되나요?
6. graceful shutdown과 `CancellationToken` 강제 취소의 처리 보장은 어떻게 다른가요?
7. 여러 Producer가 각자 `TryComplete()`를 호출하면 어떤 경쟁이 생기나요?
8. `ValueTask`를 일반 호출부에서는 한 번만 await하라는 이유는 무엇인가요?
9. PDF template 차단은 Result인데 Worker 수 0은 예외인 이유는 무엇인가요?
10. 여러 Worker에서 enqueue 순서와 완료 순서가 달라질 수 있는 이유는 무엇인가요?
11. Channel 한 항목을 한 Reader가 가져간다는 사실이 분산 시스템 exactly-once를 뜻하지 않는 이유는 무엇인가요?
12. process-local Channel을 영속 queue로 바꿀 때 필요한 운영 요소를 네 가지 말하세요.

## 손으로 실행 추적

capacity 1, Worker 1이며 처음에는 Worker가 멈춰 있다고 가정합니다. 아래 빈칸을 채우세요.

| 시점 | 버퍼 | Producer 상태 | Worker 상태 | 완료 여부 |
| --- | --- | --- | --- | --- |
| `JOB-A` 쓰기 뒤 |  |  |  |  |
| `JOB-B` 쓰기 시도 뒤 |  |  |  |  |
| Worker가 `JOB-A`를 꺼낸 직후 |  |  |  |  |
| Producer가 `TryComplete`한 직후 |  |  |  |  |
| Worker가 `JOB-B` 처리 완료 뒤 |  |  |  |  |

<details>
<summary>정답과 해설 보기</summary>

## 정답

1. Channel 내부에서 아직 읽히지 않은 버퍼 항목이 무한히 쌓이는 것을 제한합니다. 대신 버퍼가 가득 차면 생산자의 `WriteAsync`가 공간이 날 때까지 대기합니다. 호출자가 `SubmitAsync`를 await하지 않고 Task를 무제한 만들면 대기 요청은 Channel 밖에 쌓일 수 있으므로 upstream 동시성 제한도 필요합니다.
2. capacity는 아직 Reader가 가져가지 않은 버퍼 항목 수를, Worker 수는 동시에 처리 중일 수 있는 항목 수를 제한합니다. 초당 요청 수를 제한하는 rate limit과도 다릅니다.
3. `Wait`는 공간이 생길 때까지 생산자를 늦추며 항목을 보존합니다. `DropWrite`는 가득 차면 새 항목을 버리므로 유실 허용과 drop 관측 정책이 필요합니다.
4. Writer가 완료되고 버퍼도 비었으면 끝납니다. 버퍼만 비고 Writer가 열려 있으면 새 항목을 비동기로 기다립니다.
5. 새 쓰기는 거부하지만 Reader는 이미 버퍼에 있던 작업을 끝까지 읽을 수 있습니다. 마지막 항목 뒤 비동기 열거가 종료됩니다.
6. graceful shutdown은 새 생산을 멈추고 완료 신호 뒤 남은 항목을 drain합니다. 강제 취소는 Writer/Reader/Renderer 대기를 즉시 중단할 수 있어 버퍼와 진행 중 작업이 처리되지 않을 수 있습니다.
7. 먼저 끝난 Producer가 큐를 닫아 아직 쓰는 다른 Producer가 `ChannelClosedException`을 받을 수 있습니다. 상위 coordinator가 모든 Producer 종료 뒤 한 번 완료해야 합니다.
8. `ValueTask`는 Task 객체가 아닐 수 있고 내부 소스가 한 번의 소비만 지원할 수 있습니다. 반복 대기나 여러 곳 전달이 필요하면 한 번 `AsTask()`로 바꿔 그 Task를 사용합니다.
9. template 차단은 특정 업무 입력에서 예상하고 기록한 뒤 계속할 수 있는 실패입니다. Worker 수 0은 유스케이스가 실행될 수 없는 개발·배포 구성 오류라 시작을 실패시키는 예외가 적합합니다.
10. 두 Worker가 서로 다른 작업을 동시에 처리하며 Renderer 지연도 다를 수 있기 때문입니다. Repository의 정렬은 표시 순서만 바꾸며 실제 완료 순서를 보장하지 않습니다.
11. 외부 파일 생성 성공 뒤 처리 기록 저장 전에 process가 죽을 수 있습니다. 재시도하면 외부 효과가 중복될 수 있고 메모리 Channel 자체는 재시작 뒤 복구되지도 않으므로 멱등 키와 영속 상태가 필요합니다.
12. 예: JobId unique/idempotency, retry 상한과 backoff/jitter, poison job dead-letter, lease/visibility timeout, schema version, 개인정보·보존 정책, queue depth/oldest age metric, graceful shutdown, Transactional Outbox입니다.

## 실행 추적 정답

| 시점 | 버퍼 | Producer 상태 | Worker 상태 | 완료 여부 |
| --- | --- | --- | --- | --- |
| `JOB-A` 쓰기 뒤 | `JOB-A` | 첫 쓰기 완료 | 아직 멈춤 | 열림 |
| `JOB-B` 쓰기 시도 뒤 | `JOB-A` | 두 번째 쓰기 대기 | 아직 멈춤 | 열림 |
| Worker가 `JOB-A`를 꺼낸 직후 | `JOB-B`가 들어갈 공간 확보 | 두 번째 쓰기 완료 가능 | `JOB-A` 처리 | 열림 |
| Producer가 `TryComplete`한 직후 | `JOB-B` | 종료 | `JOB-A` 처리 중 | 완료 신호, drain 중 |
| Worker가 `JOB-B` 처리 완료 뒤 | 비어 있음 | 종료 | 비동기 열거 종료 | 완전히 종료 |

</details>

## 최종 자기 설명

- [ ] `WriteAsync`가 대기하는 시점을 그림으로 설명한다.
- [ ] capacity, Worker 수, rate limit을 혼동하지 않는다.
- [ ] Complete와 cancellation의 차이를 말한다.
- [ ] Result, 예외, 취소를 각각 한 사례로 설명한다.
- [ ] Port/Adapter, Strategy, Repository, DI, Composition Root를 코드 파일과 연결한다.
- [ ] process-local queue의 유실·중복 경계를 말하고 durable 대안을 제시한다.
