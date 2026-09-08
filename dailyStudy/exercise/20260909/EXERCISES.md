# 2026-09-09 실습 문제 — Beginner to Pro

먼저 원본 코드에서 Release 빌드와 `--self-test` 14/14를 확인하세요. 각 문제를 풀 때는 **실패 테스트를 먼저 추가하고**, 최소 구현 뒤 전체 테스트를 다시 실행합니다.

## Beginner — 흐름을 손으로 추적하기

### 문제 1. capacity 1 상태표

Worker를 시작하지 않은 capacity 1 큐에 `JOB-001`, `JOB-002`를 차례로 `WriteAsync`한다고 가정하고 아래 표를 채우세요.

| 시점 | 버퍼 항목 | 첫 쓰기 완료? | 두 번째 쓰기 완료? | 이유 |
| --- | --- | --- | --- | --- |
| 아무것도 쓰기 전 |  |  |  |  |
| JOB-001 쓰기 뒤 |  |  |  |  |
| JOB-002 쓰기 시도 뒤 |  |  |  |  |
| Reader가 한 건 가져간 뒤 |  |  |  |  |

완료 기준: `FullQueueWaitsForReaderAsync`가 `Thread.Sleep` 없이 무엇을 증명하는지 줄 단위로 설명합니다.

### 문제 2. 입력 검증 추가

`ReportJob.Create`에 `CustomerId` 최대 30자 규칙과 `job.customer_too_long` 오류를 추가하세요.

1. 31자 입력이 실패하는 테스트를 먼저 작성합니다.
2. 실패 Result의 코드를 검증합니다.
3. 거부된 작업이 Repository에 기록되지 않음을 확인합니다.

완료 기준: 입력 실패가 Worker까지 들어가지 않고 기존 14개 테스트도 통과합니다.

### 문제 3. 출력 예측

`Program.cs`의 `JOB-004` 형식을 `Pdf`로 바꾸고 실행 전 성공 수, 실패 수, 출력 경로를 적으세요. 확인 뒤 원래 코드로 되돌립니다.

완료 기준: 렌더링 Strategy 선택이 어느 dictionary lookup에서 일어나는지 찾습니다.

## Intermediate — 새로운 Strategy와 저장 규칙

### 문제 4. Markdown Renderer 추가

`ReportFormat.Markdown`과 `MarkdownReportRenderer`를 추가하세요. Worker의 `if`/`switch`는 수정하지 않고 Composition Root의 Renderer 배열만 확장합니다.

필수 테스트:

- Markdown 작업이 `.md` 경로로 성공합니다.
- Markdown Strategy를 등록하지 않으면 `worker.strategy_missing`입니다.
- 기존 CSV/PDF 결과가 변하지 않습니다.

완료 기준: 새 형식을 추가할 때 Worker가 닫혀 있고 Strategy 확장에 열려 있다는 OCP 의도를 설명합니다.

### 문제 5. 중복 JobId Repository 정책

현재 메모리 Repository는 같은 JobId 기록을 두 번 허용합니다. `ConcurrentDictionary<string, ProcessingRecord>`를 사용하는 구현을 새로 만들고 중복 저장을 실패 Result 또는 명시적 예외 중 하나로 처리하세요.

완료 기준: 선택한 실패 방식이 “예상 가능한 중복”인지 “구성/불변식 위반”인지 근거를 적고, 동시에 같은 ID를 저장해도 최종 기록이 하나인 테스트를 작성합니다.

## Advanced — 동시성과 lifecycle

### 문제 6. 전체 Worker 상한과 형식별 상한

먼저 기존 `WorkerCountLimitsConcurrencyAsync`와 `GatedRenderer`를 읽고, Worker 3개·작업 5개로 바꾸면 gate가 닫힌 동안 정확히 세 작업만 시작하는지 확인하세요. 그다음 PDF 외부 엔진은 한 번에 하나만 호출할 수 있다는 새 요구를 구현합니다.

1. `ConcurrencyLimitedRendererDecorator`가 내부 `IReportRenderer`와 `SemaphoreSlim`을 받게 합니다.
2. Worker Pool은 3개를 유지하되 PDF Renderer만 동시성 1로 감쌉니다.
3. `TaskCompletionSource` gate와 `Interlocked`로 CSV는 병렬, PDF는 최대 1임을 실제 시간 지연 없이 검증합니다.
4. 취소·예외에도 permit을 반환하도록 `try/finally`를 사용합니다.

완료 기준: Worker 수가 전체 상한, Decorator가 특정 의존성 상한, capacity가 버퍼 상한이라는 세 차이를 테스트로 증명합니다.

### 문제 7. 여러 Producer의 올바른 완료 소유권

Producer 3개가 각자 4개 작업을 쓰게 만드세요. 각 Producer 안에서는 `TryComplete`를 호출하지 않습니다. 상위 coordinator가 `await Task.WhenAll(producers)` 뒤 한 번만 완료합니다.

필수 테스트:

- 12개가 모두 처리됩니다.
- Producer 하나가 중간에 실패하면 `TryComplete(exception)`으로 소비자에게 실패가 전달됩니다.
- 너무 일찍 완료하면 다른 Producer가 `ChannelClosedException`을 받는 반례를 별도 테스트 또는 주석으로 설명합니다.

완료 기준: “큐를 만든 수명 관리자만 완료한다”는 규칙을 코드에서 찾을 수 있습니다.

### 문제 8. Drop 정책 비교와 관측성

별도 Adapter에서 `Wait`, `DropWrite`, `DropOldest`를 선택할 수 있게 하되 기본값은 계속 `Wait`로 유지하세요. drop callback 또는 명시적 계수기로 버린 수를 기록합니다.

완료 기준: 동일한 capacity 2와 입력 5개에 대해 각 정책의 보존 결과를 표로 만들고, 청구서 생성에는 왜 drop이 부적합한지 설명합니다.

## Pro — 운영 환경으로 확장하기

### 문제 9. graceful shutdown 상태 머신

다음 상태를 가진 queue lifecycle을 설계하고 Mermaid state diagram을 작성하세요.

```text
Accepting -> Completing -> Draining -> Stopped
                         \-> ForcedCancellation
```

호스트 종료 시 새 요청 수락 중단, Producer 완료 대기, `TryComplete`, drain 제한 시간, 최종 강제 취소를 순서대로 구현합니다. `CancellationTokenSource.CreateLinkedTokenSource`를 사용할 때 어느 token이 “새 제출 중단”, “graceful drain”, “강제 중단”인지 이름으로 구분하세요.

완료 기준: 강제 취소 때 버퍼/진행 중 작업이 어떻게 되는지 문서와 metric에 명시합니다.

### 문제 10. durable queue 경계 설계

프로세스 재시작 뒤에도 작업을 보존해야 한다고 가정합니다. 코드를 바로 바꾸기 전에 다음 항목을 포함한 설계 문서를 작성하세요.

- DB queue 또는 broker 선택 근거와 delivery 보장
- JobId unique constraint와 idempotent Renderer/consumer
- dequeue/외부 파일 생성/완료 기록 사이 장애 구간
- retry 상한, exponential backoff, jitter, poison job과 dead-letter 상태
- lease/visibility timeout과 Worker crash 복구
- payload/schema version, 개인정보 최소화, 보존 기간
- queue depth뿐 아니라 oldest job age와 처리 지연 metric
- 제출 데이터와 queue 메시지를 함께 저장해야 할 때 Transactional Outbox를 적용하는 위치

완료 기준: exactly-once라고 단정하지 않고 중복 가능 구간과 멱등성 책임을 명시합니다.

## 매 단계 공통 검증 명령

```powershell
dotnet build dailyStudy/exercise/20260909/src/BoundedChannelExercise/BoundedChannelExercise.csproj -c Release
dotnet run --project dailyStudy/exercise/20260909/src/BoundedChannelExercise/BoundedChannelExercise.csproj -c Release --no-build
dotnet run --project dailyStudy/exercise/20260909/src/BoundedChannelExercise/BoundedChannelExercise.csproj -c Release --no-build -- --self-test
```

마지막에는 새로 작성한 모든 메서드 상단에 목적, parameter 의미, 반환값을 설명했는지 확인하고, 첫 `ValueTask`, `await foreach`, nullable, record, pattern 문법에는 “무엇이며 왜 썼는지” 한글 주석을 남깁니다.
