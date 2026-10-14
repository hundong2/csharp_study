# 실습 문제 — 배포 진행률 SSE API (Beginner → Pro+)

이 문서는 답을 복사하는 대신 작은 변경을 만들고 검증하는 순서로 구성했습니다. 각 단계에서 먼저 예상 결과를 적고, Release build와 self-test를 다시 실행하세요.

## 공통 준비

저장소 루트 `D:\workspace\csharp_study`의 PowerShell에서 시작합니다.

```powershell
dotnet build .\dailyStudy\exercise\20261015\src\DeploymentSseApi\DeploymentSseApi.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20261015\src\DeploymentSseApi\DeploymentSseApi.csproj -c Release --no-build -- --self-test
```

기준선은 `SELF-TEST PASSED`입니다. 연습 중 오류가 생기면 마지막 수정만 무작정 되돌리기보다 compiler 메시지의 파일·줄·형식부터 읽으세요.

---

## Level 1 — Beginner: HTTP 계약을 말로 번역하기

### 문제

다음 요청 각각에 대해 “허용/거절”, 예상 이유, terminal 여부를 표로 적으세요.

이 표에서는 **stage별 percent 조합만** 판단합니다. `Building`, `Deploying`, `Completed`에는 그 단계로 갈 수 있는 적절한 previous 이벤트가 이미 있다고 가정하세요. 최초 단계·전이 순서 검증은 그 다음에 별도로 확인합니다.

1. `Queued`, `0`
2. `Queued`, `1`
3. `Building`, `1`
4. `Building`, `69`
5. `Building`, `70`
6. `Deploying`, `70`
7. `Deploying`, `99`
8. `Completed`, `100`
9. `Completed`, `99`
10. `Failed`, `0`
11. `Failed`, `99`
12. `Failed`, `100`

추가로 이전 이벤트가 `Building 40`일 때 `Building 40`(message만 변경)과 `Building 39`를 비교하고, 이전 이벤트가 `Deploying 80`일 때도 같은 비교를 하세요. 동일 percent의 상세 message 갱신은 허용하지만 감소는 거절하는 정책입니다.

그 뒤 [`DeploymentProgressTransitionStrategy.cs`](./src/DeploymentSseApi/Application/DeploymentProgressTransitionStrategy.cs)와 비교하세요.

### 완료 기준

- 다섯 stage의 percent 경계를 보지 않고 설명할 수 있습니다.
- `Completed`와 `Failed`가 왜 terminal인지 설명합니다.
- 단순 숫자 범위 검사와 stage-percent 조합 검사가 다름을 말합니다.

### 힌트

전체 percent가 `0..100` 안에 있다는 사실만으로 이벤트가 유효한 것은 아닙니다. `Queued 50`은 전체 범위에는 들지만 업무 규칙에는 맞지 않습니다.

---

## Level 2 — Beginner+: nullable 입력과 Domain 불변식 찾기

### 문제

[`DeploymentContracts.cs`](./src/DeploymentSseApi/Presentation/DeploymentContracts.cs), [`DeploymentProgressDraft.cs`](./src/DeploymentSseApi/Domain/DeploymentProgressDraft.cs), [`DeploymentIdentity.cs`](./src/DeploymentSseApi/Domain/DeploymentIdentity.cs), [`DeploymentProgressEvent.cs`](./src/DeploymentSseApi/Domain/DeploymentProgressEvent.cs)를 읽고 다음 질문에 답하세요.

1. `RespectRequiredConstructorParameters` 때문에 JSON에서 누락하면 framework `400`이 되는 세 positional field는 무엇인가요?
2. field를 명시하면서 JSON `null`을 보낼 수 있어 `string?`로 표현한 두 값은 무엇인가요?
3. 처음 연결에서 없어도 되는 optional header는 무엇이며 Application은 이를 어떤 cursor로 해석하나요?
4. explicit null이나 공백이 어느 계층을 지난 뒤 오류 또는 non-null/정규화 값이 되나요?
5. 클라이언트가 정해서는 안 되는 event ID 후보를 누가 부여하나요?
6. 생성된 event의 property를 나중에 바꾸기 어렵게 만든 문법은 무엇인가요?

그 뒤 빈 deployment ID, 공백 stage, 범위를 벗어난 percent, 너무 긴 message를 다루는 self-test를 하나씩 찾아 주석으로 “왜 경계 테스트인가”를 설명하세요.

### 완료 기준

- body field **누락**은 framework `400`, 명시적인 JSON `null`은 nullable DTO 뒤 Domain 검증이라는 차이를 설명합니다.
- optional `Last-Event-ID` header와 필수 body positional field를 구분합니다.
- `string?`가 “null을 권장한다”가 아니라 “경계의 현실을 표현한다”는 뜻임을 설명합니다.
- null-forgiving `!`가 runtime 검사가 아님을 설명합니다.
- `record`/init-only 값과 Repository의 thread safety를 서로 다른 문제로 구분합니다.

---

## Level 3 — Basic: `Task<List<T>>`와 `IAsyncEnumerable<T>` 비교하기

### 문제

다음 두 API의 소비 시점과 메모리 특성을 글로 비교하세요.

```csharp
// 모든 이벤트를 모은 뒤 한 번 반환하므로 live stream을 자연스럽게 표현하지 못합니다.
Task<List<DeploymentProgressEvent>> GetAllAsync(CancellationToken cancellationToken);

// 실제 Port는 미존재(null)를 먼저 판단한 뒤, wrapper 안의 Events로 비동기 열거를 노출합니다.
ValueTask<DeploymentProgressStream?> OpenStreamAsync(
    string deploymentId,
    long afterEventId,
    CancellationToken cancellationToken);

// wrapper의 Events는 소비자가 다음 항목을 요청할 때 준비된 값을 하나씩 전달합니다.
public sealed record DeploymentProgressStream(
    IAsyncEnumerable<DeploymentProgressEvent> Events);
```

[`DeploymentProgressApplicationService.cs`](./src/DeploymentSseApi/Application/DeploymentProgressApplicationService.cs), [`InMemoryDeploymentProgressRepository.cs`](./src/DeploymentSseApi/Infrastructure/InMemoryDeploymentProgressRepository.cs), [`DeploymentEndpoints.cs`](./src/DeploymentSseApi/Presentation/DeploymentEndpoints.cs)에서 다음 문법을 각각 한 곳씩 찾으세요.

- `async IAsyncEnumerable<T>`
- `yield return`
- `await foreach`
- `[EnumeratorCancellation]`
- `WithCancellation` 또는 취소 token이 전달되는 동일한 역할의 코드

### 완료 기준

- 호출, 열거 시작, 다음 값 요청의 시점이 다름을 설명합니다.
- `yield return`과 일반 `return`의 차이를 설명합니다.
- 비동기 스트림이 자동으로 새 thread를 만드는 기능이 아님을 설명합니다.

---

## Level 4 — Intermediate: `Last-Event-ID` replay 표 만들기

### 문제

저장 이벤트 ID가 `[1, 2, 3, 4, 5]`일 때 각 header 입력의 결과를 먼저 예상하세요.

| 입력 | 예상 replay |
| --- | --- |
| header 없음 | ? |
| `Last-Event-ID: 0` | ? |
| `Last-Event-ID: 2` | ? |
| `Last-Event-ID: 5` | ? |
| `Last-Event-ID: 8` | ? 또는 오류 정책? |
| `Last-Event-ID: -1` | ? |
| `Last-Event-ID: abc` | ? |

그 뒤 [`DeploymentEndpoints.cs`](./src/DeploymentSseApi/Presentation/DeploymentEndpoints.cs)의 header 추출, [`DeploymentProgressApplicationService.cs`](./src/DeploymentSseApi/Application/DeploymentProgressApplicationService.cs)의 parser, [`InMemoryDeploymentProgressRepository.cs`](./src/DeploymentSseApi/Infrastructure/InMemoryDeploymentProgressRepository.cs)의 replay/live filter를 찾아 실제 계약을 적으세요.

현재 마지막 ID가 5인데 cursor가 8인 경우도 형식상 허용됩니다. replay는 비고 live ID 6–8도 걸러지며 9부터 전달됩니다. 다만 9 전에 terminal이 저장되면 terminal ID도 8 이하일 수 있으므로 아무 이벤트 없이 stream이 끝날 수 있습니다.

### 확장 과제

header가 여러 값으로 들어온 경우를 self-test 또는 HTTP 검증에 추가하세요. 여러 값을 임의로 첫 값만 고르는 것과 명시적으로 거절하는 것의 trade-off를 한 문단으로 적습니다.

### 완료 기준

- replay 조건을 `eventId > lastEventId`로 정확히 씁니다.
- “마지막 처리 ID”와 “다음 원하는 ID”를 혼동하지 않습니다.
- HTTP 문자열 파싱 책임과 Repository filtering 책임을 구분합니다.

---

## Level 5 — Intermediate+: LINQ로 replay하되 상태 변경은 숨기지 않기

### 문제

replay snapshot에서 기준 ID보다 큰 이벤트만 고르고 ID 순서로 정렬하는 코드가 있다면 찾아 설명하세요. 없다면 다음 의사 코드를 실제 형식에 맞게 구현하는 연습을 합니다.

```csharp
// 이미 확보한 불변 snapshot을 조회하는 작업이므로 LINQ가 의도를 잘 드러냅니다.
var replay = snapshot
    .Where(progressEvent => progressEvent.EventId > afterEventId)
    .OrderBy(progressEvent => progressEvent.EventId)
    .ToArray();
```

다음 작업을 같은 LINQ chain에 억지로 넣지 말아야 하는 이유도 적으세요.

- 다음 ID 발급
- history append
- subscriber fan-out
- terminal channel completion

### 완료 기준

- 조회와 상태 변경을 구분합니다.
- snapshot이 필요한 이유를 동시성 관점에서 설명합니다.
- LINQ가 thread safety를 제공하지 않는다는 점을 설명합니다.

---

## Level 6 — Advanced: 취소와 subscriber 정리 증명하기

### 문제

SSE 연결 하나를 열고 취소했을 때 다음 신호가 어떻게 흐르는지 코드의 줄을 따라 적으세요.

```text
HTTP 연결 종료
→ RequestAborted CancellationToken
→ Presentation async iterator
→ Application stream
→ Repository/Channel read
→ iterator finally
→ subscriber 제거
```

그 뒤 다음 self-test를 추가하거나 기존 테스트의 근거를 찾으세요.

1. 구독을 시작해 live 대기 상태로 만듭니다.
2. cancellation source를 취소합니다.
3. 열거가 제한 시간 안에 끝나는지 확인합니다.
4. 이후 append가 취소된 subscriber에 쓰려 하지 않는지 확인합니다.

### 완료 기준

- `[EnumeratorCancellation]`의 목적을 자신의 말로 설명합니다.
- `OperationCanceledException`을 업무 `Result`로 바꾸지 않습니다.
- `finally`가 정상 종료뿐 아니라 취소/예외 정리에도 필요한 이유를 설명합니다.

### 주의

시간 의존 테스트에서 무한 대기하지 마세요. 짧은 명시적 timeout을 두고 실패 시 어느 단계가 끝나지 않았는지 보여 주세요.

---

## Level 7 — Advanced+: bounded Channel backpressure 실험

### 문제

[`InMemoryDeploymentProgressRepository.cs`](./src/DeploymentSseApi/Infrastructure/InMemoryDeploymentProgressRepository.cs)와 [`Program.cs`](./src/DeploymentSseApi/Program.cs)에서 다음을 찾습니다.

- channel capacity
- `BoundedChannelFullMode`
- single/multiple reader·writer 옵션
- `WriteAsync`, `TryWrite`, `ReadAllAsync` 중 사용한 API

현재 구현은 capacity `8`, `FullMode.Wait` channel을 만들지만 lock 안에서는 기다리지 않도록 `TryWrite`를 호출합니다. channel이 가득 찼을 때 producer, 종료되는 느린 subscriber, 계속 받는 다른 subscriber가 각각 어떻게 되는지 표로 적으세요. 생성자 기본 capacity `16`과 Composition Root 설정 `8`도 구분하세요.

### 실험

테스트 전용으로 아주 작은 capacity를 주입할 수 있게 만드는 방안을 설계하세요. 소비하지 않는 느린 subscriber와 즉시 읽는 빠른 subscriber를 동시에 만들고 capacity보다 많은 이벤트를 append합니다.

다음 현재 정책을 검증하세요.

- capacity까지는 `TryWrite`가 성공합니다.
- 다음 write가 실패하면 느린 subscriber를 제거하고 channel을 오류로 완료합니다.
- 빠른 subscriber와 producer는 계속 진행합니다.
- 끊긴 client는 마지막 처리 ID로 재연결해 history에서 누락 구간을 복구할 수 있습니다.

### 완료 기준

- bounded queue가 backpressure를 “없애는” 것이 아니라 동작을 명시하게 만든다고 설명합니다.
- capacity와 full mode를 성능 튜닝 숫자가 아닌 외부 전달 계약으로 취급합니다.
- 테스트를 위해 Production 값을 전역 상수로 마구 바꾸지 않고 configuration/constructor 주입을 고려합니다.

---

## Level 8 — Pro: replay/live cutover race 재현하기

### 문제

다음 잘못된 순서를 설명하고 어느 순간 event `N+1`이 사라지는지 표시하세요.

```text
1. history snapshot 복사 (마지막 ID N)
2. producer가 event N+1 append
3. subscriber channel 등록
4. snapshot replay 후 channel read
```

이제 현재 Repository Adapter가 snapshot/cutover와 subscriber 등록을 어떤 동기화 경계에서 처리하는지 찾으세요. 동시에 여러 번 실행하는 테스트를 만들어 event ID가 누락·역전·중복되지 않는지 검증합니다.

### 테스트 아이디어

- barrier 또는 `TaskCompletionSource`로 “구독 준비”와 “append” 시점을 의도적으로 가깝게 만듭니다.
- 단순 `Task.Delay` 확률에만 의존하지 않습니다.
- 받은 ID를 배열로 모아 기대 연속 범위와 비교합니다.
- 실패 시 전체 ID 목록을 출력해 어느 경계가 깨졌는지 알 수 있게 합니다.

### 완료 기준

- 왜 중복보다 누락이 복구하기 더 어려운지 설명합니다.
- lock 안에서 blocking/await를 오래 하면 생길 수 있는 문제를 설명합니다.
- 동기화 구현보다 먼저 지켜야 할 invariant를 문장으로 씁니다.

---

## Level 9 — Pro: terminal 완료와 상태 충돌

### 문제

다음 두 scenario를 검증하세요.

#### 이미 구독 중인 client

1. `Queued 0`을 저장합니다.
2. client가 live 구독합니다.
3. `Building 20`, `Deploying 70`을 순서대로 저장해 client가 두 live 이벤트를 받는지 확인합니다.
4. `Completed 100`을 저장합니다.
5. client가 terminal 이벤트를 정확히 한 번 받고 열거를 끝내는지 확인합니다.

#### terminal 뒤 늦게 온 client

1. `Queued 0`, `Building 20`, `Deploying 70`, `Completed 100`을 먼저 저장합니다.
2. `Last-Event-ID: 1`로 구독합니다.
3. ID 2, 3, 4를 replay하고 live 대기 없이 끝나는지 확인합니다.
4. terminal 뒤 새 POST가 거절되는지 확인합니다.

### 완료 기준

- “writer complete → terminal write”가 아니라 “terminal 저장/fan-out → complete” 순서임을 설명합니다.
- channel completion을 HTTP 500과 혼동하지 않습니다.
- 브라우저 `EventSource`가 EOF 뒤 자동 재연결하므로 `isTerminal: true`에서 `close()`해야 함을 설명합니다.
- terminal 뒤 append 실패가 예외인지 Result인지 설계 기준을 설명합니다.

---

## Level 10 — Pro+: 다중 인스턴스 운영 설계

### 문제

현재 메모리 Adapter를 서버 A/B 두 대에서 실행한다고 가정합니다. POST가 A에, SSE GET이 B에 도착할 때 무엇이 깨지는지 그리세요. 그런 다음 다음 구성요소를 가진 운영 설계 메모를 작성합니다.

- durable event store
- deployment stream별 순서를 보장하는 ID/offset 정책
- live pub/sub 또는 broker
- replay retention과 오래된 `Last-Event-ID` 처리
- 인증·deployment별 인가
- subscriber quota와 연결 제한
- proxy buffering/idle timeout/heartbeat
- channel 포화, replay 크기, lag, 연결 수 metric
- graceful shutdown 때 reconnect 유도

### 선택지 비교

| 선택 | 장점 | 반드시 해결할 문제 |
| --- | --- | --- |
| DB append log + polling/tail | 단순한 원본 저장 | 효율적인 wake-up, DB 부하 |
| DB + Redis pub/sub | replay 원본과 빠른 live | pub/sub 유실 시 DB gap 보충 |
| Kafka/stream broker | offset·retention·분산 소비 | 운영 복잡도, stream key 설계 |
| managed message service | 운영 부담 감소 | 비용, 순서/retention 제약 |

### Pro+ 완료 기준

- 저장 성공과 live publish 사이 실패를 어떻게 복구할지 설명합니다.
- event ID가 전역인지 deployment별인지 명시합니다.
- reconnect가 exactly-once를 자동 보장하지 않음을 설명합니다.
- client가 ID 기반 idempotent 처리를 해야 하는 이유를 포함합니다.
- 현재 Port를 유지할 부분과 바꿔야 할 부분을 구분합니다.

---

## 최종 제출 체크

- [ ] Release build가 warning 없이 성공한다.
- [ ] `SELF-TEST PASSED`를 확인한다.
- [ ] `HTTP VERIFY PASSED`를 확인한다.
- [ ] 수동 SSE에서 replay와 live 이벤트를 모두 관찰했다.
- [ ] `Last-Event-ID` 재연결이 마지막 ID **다음**부터 이어짐을 확인했다.
- [ ] 취소, channel 포화, terminal 완료 테스트를 설명할 수 있다.
- [ ] 모든 새 C# 메서드 위에 목적·파라미터·반환값의 쉬운 한글 설명이 있다.
- [ ] 처음 등장한 async/SSE/Channel 문법과 설계 선택의 Why를 주석으로 설명했다.
- [ ] `bin/`, `obj/` 또는 테스트 로그를 커밋하지 않았다.
