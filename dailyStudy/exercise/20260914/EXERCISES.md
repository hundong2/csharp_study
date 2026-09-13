# 2026-09-14 실행 연습 — Generic Host 재고 감시 Worker

각 과제는 **가설 → 작은 변경 → build → self-test → demo 관찰** 순서로 진행합니다. 한 번에 여러 과제를 섞지 말고, 실패 메시지가 어떤 계약을 알려 주는지 먼저 읽으세요.

## 시작 전 기준선

저장소 루트 `D:\workspace\csharp_study`에서 다음 두 명령이 먼저 성공해야 합니다.

~~~powershell
dotnet build .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build -- --self-test
~~~

연습 중 새 메서드를 만들면 상단 한글 주석에 목적, 파라미터 의미, 반환값을 적으세요. 처음 사용하는 문법에는 “무엇이며 왜 여기서 썼는지”를 설명하고, 단순히 코드를 소리 내 읽는 주석은 피합니다.

---

## Beginner 1 — Options override를 눈으로 확인하기

목표: 코드를 바꾸지 않고 `appsettings.json`보다 환경 변수가 나중에 적용된다는 것을 확인합니다.

1. 아래 명령으로 간격과 최대 회차를 유효 범위 안에서 임시로 덮어씁니다.
2. 로그의 회차 진행과 종료 시점이 override를 반영하는지 봅니다.
3. `finally`에서 환경 변수를 원래 존재 여부와 값으로 반드시 복원합니다.

~~~powershell
$hadInventoryInterval = Test-Path Env:InventoryWatcher__IntervalMilliseconds
$previousInventoryInterval = $env:InventoryWatcher__IntervalMilliseconds
$hadInventoryMaxCycles = Test-Path Env:InventoryWatcher__MaxCycles
$previousInventoryMaxCycles = $env:InventoryWatcher__MaxCycles
try {
    $env:InventoryWatcher__IntervalMilliseconds = "25"
    $env:InventoryWatcher__MaxCycles = "1"
    dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
}
finally {
    if ($hadInventoryInterval) {
        $env:InventoryWatcher__IntervalMilliseconds = $previousInventoryInterval
    }
    else {
        Remove-Item Env:InventoryWatcher__IntervalMilliseconds -ErrorAction SilentlyContinue
    }

    if ($hadInventoryMaxCycles) {
        $env:InventoryWatcher__MaxCycles = $previousInventoryMaxCycles
    }
    else {
        Remove-Item Env:InventoryWatcher__MaxCycles -ErrorAction SilentlyContinue
    }
}
~~~

통과 기준:

- Host가 유효한 override로 시작합니다.
- 설정한 회차를 마치고 스스로 정상 종료합니다.
- 실행 뒤 두 환경 변수가 현재 PowerShell 세션의 원래 존재 여부와 값으로 복원됩니다.

## Beginner 2 — Domain 경계값 테스트하기

목표: `InventoryItem.Create`와 `DefaultStockThresholdStrategy`의 경계를 표로 만들고 코드로 검증합니다.

`SelfTests.cs`에 다음 사례를 추가하세요.

| 입력 | 기대 |
| --- | --- |
| SKU `" cab-100 "` | 성공 후 `"CAB-100"`으로 정규화 |
| 상품 이름이 공백 | 실패 Result |
| 수량 `-1` | 실패 Result |
| 수량 `0` | `Critical` |
| 양수 수량(예: 5)과 재주문 기준이 같음 | `Low` |
| 수량이 재주문 기준보다 1 큼 | `Healthy` |

예상 가능한 사용자 입력 실패는 `try/catch`가 아니라 `IsFailure`와 `Error`로 검증하세요. 실패 Result에서 `Value`를 읽는 개발 실수는 별도의 `InvalidOperationException` 사례로 구분합니다.

검증:

~~~powershell
dotnet build .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build -- --self-test
~~~

## Beginner 3 — LINQ 파이프라인을 말로 풀기

목표: `Select → Where → Select → OrderByDescending → ThenBy → ToArray`가 만드는 결과를 이해합니다. 첫 `Select`는 `(Item, Level)` tuple을 만들고 두 번째 `Select`는 실제 `StockAlert`로 바꿉니다.

1. 순서가 섞인 재고 다섯 건을 반환하는 테스트 Repository를 만듭니다.
2. `Critical` 두 건, `Low` 두 건, `Healthy` 한 건이 되게 수량을 정합니다.
3. `InventoryWatchCycle.RunAsync`를 한 번 호출합니다.
4. Alert Sink가 받은 값이 Critical 우선이고, 같은 단계에서는 SKU의 ordinal 순서인지 검증합니다.
5. Healthy 항목은 `InspectedCount`에는 포함되지만 `Alerts`에는 없는지 확인합니다.

Repository가 처음부터 정렬된 데이터를 주지 않게 해야 테스트가 실제 정렬 계약을 검증합니다.

검증:

~~~powershell
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release -- --self-test
~~~

---

## Intermediate 1 — 교체 가능한 임계값 Strategy

목표: Application Service를 수정하지 않고 “안전 여유 수량” 정책을 추가합니다.

`BufferedStockThresholdStrategy`를 새로 구현하세요.

- 생성자에서 0 이상 `InventoryItem.MaximumQuantity` 이하의 `buffer`를 받습니다.
- 수량이 0이면 `Critical`입니다.
- 수량이 `ReorderPoint + buffer` 이하이면 `Low`입니다. 덧셈 전에 두 값을 `long`으로 승격해 overflow를 막습니다.
- 나머지는 `Healthy`입니다.
- 음수 buffer는 사용자 입력이 아니라 잘못된 구성 계약이므로 생성자 예외로 막습니다.

그다음 `AddInventoryWatcher`의 Strategy 등록만 새 구현으로 바꿉니다. `InventoryWatchCycle`에는 형식 검사나 새 `if`를 추가하지 마세요.

최소 테스트:

- buffer 0은 기존 Strategy와 같은 결과
- buffer 2에서 새 Low 경계
- 음수 buffer 구성 실패
- `InventoryItem.MaximumQuantity + 1` buffer 구성 실패
- 같은 가짜 Repository를 두 Strategy에 주입했을 때 Alert 목록만 달라짐

검증:

~~~powershell
dotnet build .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build -- --self-test
~~~

## Intermediate 2 — Options를 시작 시 실패시키기

목표: `ValidateDataAnnotations`와 `ValidateOnStart`가 첫 tick 전 구성 오류를 잡는지 확인합니다.

먼저 코드 변경 없이 잘못된 간격을 주입합니다.

~~~powershell
$hadInventoryInterval = Test-Path Env:InventoryWatcher__IntervalMilliseconds
$previousInventoryInterval = $env:InventoryWatcher__IntervalMilliseconds
try {
    $env:InventoryWatcher__IntervalMilliseconds = "0"
    dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
}
finally {
    if ($hadInventoryInterval) {
        $env:InventoryWatcher__IntervalMilliseconds = $previousInventoryInterval
    }
    else {
        Remove-Item Env:InventoryWatcher__IntervalMilliseconds -ErrorAction SilentlyContinue
    }
}
~~~

기대 결과는 성공 실행이 아니라 시작 단계의 options validation 실패와 non-zero exit code입니다. 그다음 자체 테스트에 다음 경계를 추가하세요.

- `IntervalMilliseconds`: 24 실패, 25 성공, 60000 성공, 60001 실패
- `MaxCycles`: 0 실패, 1 성공, 100 성공, 101 실패

검증을 첫 회차 메서드 안으로 옮기지 마세요. 잘못된 배포를 시작 전에 거부하는 것이 과제의 핵심입니다.

## Intermediate 3 — 취소를 원래 의미로 보존하기

목표: 취소가 Result나 일반 장애로 바뀌지 않고 아래 계층까지 같은 토큰으로 전달되는지 검증합니다.

1. `TaskCompletionSource`로 조회 시작 신호와 계속 진행 신호를 가진 가짜 Repository를 만듭니다.
2. `RunAsync`를 시작하고 조회 시작 신호를 기다립니다.
3. 호출에 사용한 `CancellationTokenSource`를 취소합니다.
4. Repository가 그 토큰으로 `OperationCanceledException`을 내게 합니다.
5. Alert Sink는 호출되지 않았고, 잡힌 예외의 `CancellationToken`이 원래 토큰과 같은지 검증합니다.

`Thread.Sleep`과 임의의 `Task.Delay`로 타이밍을 맞추지 마세요. 완료 신호에는 테스트가 영원히 걸리지 않도록 제한 시간만 둡니다.

검증:

~~~powershell
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release -- --self-test
~~~

## Intermediate 4 — 빈 경고는 Sink를 호출하지 않기

목표: “확인한 재고 없음”과 “확인했지만 모두 정상”을 보고서로 구분합니다.

다음 두 테스트를 추가합니다.

- 빈 Repository: `InspectedCount == 0`, 경고 0, Sink 호출 0
- Healthy 두 건: `InspectedCount == 2`, 경고 0, Sink 호출 0

그 뒤 제품 요구가 “빈 회차도 heartbeat event를 보내야 한다”로 바뀌었다고 가정하고, 이 책임을 Alert Sink에 억지로 넣지 않고 별도 `ICycleObserver` Port로 분리할 설계를 한 문단으로 적으세요.

---

## Advanced 1 — 회차별 scope 수명 증명하기

목표: singleton Worker가 scoped 서비스를 붙잡지 않고 매 회차 새 객체를 만들고 정리하는지 결정적으로 검증합니다.

테스트 전용 `ScopedProbeRepository : IInventoryRepository, IAsyncDisposable`을 만듭니다.

- 생성될 때 고유 instance ID를 기록합니다.
- `GetAllAsync`가 자신의 ID를 기록합니다.
- `DisposeAsync`가 정확히 한 번 호출됐는지 기록합니다.

실제 `ServiceCollection`으로 `InventoryWatcherWorker`를 조립하고 `RunOnceAsync`를 두 번 직접 호출하세요.

통과 기준:

- 두 회차의 Repository instance ID가 다릅니다.
- 각 instance는 자기 회차가 끝날 때 한 번씩 Dispose됩니다.
- Worker는 두 번째 회차 뒤에도 사용할 수 있습니다.
- scoped `IInventoryWatchCycle`을 Worker 생성자에 직접 주입하지 않습니다.

검증:

~~~powershell
dotnet build .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build -- --self-test
~~~

## Advanced 2 — 실제 시간을 제거한 Worker 반복 테스트

목표: `PeriodicTimer`를 기다리지 않고 `ExecuteAsync`의 반복·종료 계약을 검증합니다.

`ITickSource`의 가짜 구현이 준비한 `true, true, false`를 순서대로 반환하게 하고 `MaxCycles`는 false까지 읽도록 3 이상으로 설정합니다. Worker의 protected 메서드를 직접 노출하기보다 Generic Host 또는 `StartAsync`/`StopAsync` 수명을 통해 실행하세요.

확인할 것:

- true tick마다 정확히 한 scope가 만들어집니다.
- 이전 `RunOnceAsync`가 끝나기 전 다음 회차가 시작되지 않습니다.
- false를 받으면 반복이 끝납니다.
- Host 종료 토큰이 취소되면 새 scope가 생기지 않습니다.
- 임의 시간 대기가 없어 테스트가 빠르고 반복 실행해도 같습니다.

> 실제 `PeriodicTimer`의 interval은 회차 사이 최소 휴식 시간이 아니라 고정 tick 주기입니다. 회차 실행 중 여러 tick이 오면 하나로 병합될 수 있으므로, 완료 뒤 반드시 쉬어야 하는 요구와 구분해 설명하세요.

## Advanced 3 — Repository 계약 위반을 숨기지 않기

목표: Adapter 버그와 사용자가 고칠 수 있는 실패를 구분합니다.

다음 잘못된 Repository를 하나씩 주입하고 `InvalidOperationException`을 검증하세요.

- 목록 자체가 null
- 목록 안에 null 항목
- 대소문자 정규화 뒤 같은 SKU가 두 번 등장

이 오류를 `Result<WatchCycleReport>` 실패로 바꾸지 마세요. Port의 non-null·unique SKU 계약을 어긴 프로그래밍 오류이므로 배포 전에 빨리 드러나야 합니다.

## Advanced 4 — retry Decorator와 멱등 Sink

목표: Application Service를 바꾸지 않고 일시적인 전송 실패만 제한적으로 재시도합니다.

1. `ILowStockAlertSink`를 감싸는 `RetryingLowStockAlertSink` Decorator를 만듭니다.
2. 취소와 구성·계약 예외는 재시도하지 않습니다.
3. 명시적으로 정한 transient 예외만 최대 횟수와 backoff로 재시도합니다.
4. 지연에는 호출자의 토큰을 전달합니다.
5. 테스트에는 실제 시간 대신 주입 가능한 delay 전략을 사용합니다.

중요: 첫 전송이 성공했지만 응답만 유실되면 같은 알림이 다시 전송될 수 있습니다. stable alert ID를 Sink에 전달하고 수신자가 멱등 처리하는 테스트 없이는 “안전한 retry”가 완성되지 않습니다.

---

## Pro 1 — 상태 전이 기반 중복 알림 방지

목표: 매 tick마다 같은 부족 알림을 반복하지 않고 상태가 바뀔 때만 알립니다.

지속 가능한 `IStockAlertLedger` Port를 설계합니다.

- 키: warehouse 또는 tenant 범위 + SKU
- 값: 마지막 `StockLevel`, 관찰 버전, 마지막 alert ID
- Healthy → Low/Critical은 새 경고
- Low → Critical은 긴급도 상승 경고
- Low → Low와 Critical → Critical은 중복 억제
- Low/Critical → Healthy는 복구 event 여부를 제품 정책으로 결정

최소 테스트:

- 같은 snapshot 두 번 실행
- Low에서 Critical로 악화
- Healthy로 복구 뒤 다시 Low
- 프로세스 재시작을 흉내 낸 새 service instance
- 같은 SKU를 동시에 처리하는 두 회차

단순 singleton `Dictionary`는 재시작과 여러 replica에서 상태를 잃으므로 Pro 과제의 답이 아닙니다.

## Pro 2 — 다중 replica lease와 fencing

목표: 여러 Worker가 같은 저장소를 감시할 때 “한 번만 실행”이라는 모호한 표현을 실제 보장으로 바꿉니다.

1. `IInventoryWatchLease` Port에 owner ID, 만료, 단조 증가 fencing token을 모델링합니다.
2. lease를 얻은 회차만 Repository 읽기와 Alert Outbox 쓰기를 수행합니다.
3. 오래 멈춘 owner가 lease 만료 뒤 돌아와도 더 작은 fencing token의 쓰기는 저장소가 거부합니다.
4. lease 갱신 실패와 Host 취소를 구분합니다.
5. 두 Worker가 동시에 시작하는 통합 테스트를 작성합니다.

검증 문서에는 저장소가 어떤 조건부 쓰기 또는 transaction을 제공해야 하는지 명시하세요. 프로세스 내부 `lock`만으로는 다중 replica를 막지 못합니다.

## Pro 3 — durable scheduling과 missed run 정책

목표: `PeriodicTimer`가 보장하지 않는 재시작 이후 스케줄을 설계합니다.

요구를 먼저 하나 선택합니다.

- 최신 상태만 한 번 확인: 놓친 tick을 합쳐 즉시 한 회차 실행
- 모든 예정 시각 처리: durable job store의 미실행 항목을 순서대로 실행
- 오래된 실행 폐기: 허용 지연보다 늦으면 skip하고 metric만 기록

선택 뒤 scheduler Adapter를 `ITickSource`와 분리해 모델링하고 다음을 테스트합니다.

- 앱이 여러 주기 동안 중단됐다가 재시작
- 같은 예정 시각 job이 두 번 전달
- 실행 중 crash 뒤 redelivery
- 종료 요청 중 job claim
- 시간대와 DST 경계

“exactly once”라고 이름만 붙이지 말고, job ID의 uniqueness, claim transaction, alert idempotency, 완료 기록이 각각 무엇을 보장하는지 적으세요.

---

## 모든 과제의 최종 검증

~~~powershell
dotnet build .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build -- --self-test
dotnet run --project .\dailyStudy\exercise\20260914\src\InventoryWatcherExercise\InventoryWatcherExercise.csproj -c Release --no-build
~~~

- [ ] 새 C# 메서드마다 목적·파라미터·반환값 한글 설명이 있다.
- [ ] 새 문법과 설계 선택의 “왜”를 주석으로 설명했다.
- [ ] 실제 `PeriodicTimer` 계약 테스트도 시간 경과나 실제 tick을 기다리지 않고 취소·Dispose 신호로 끝낸다.
- [ ] expected Result, 예외, 취소를 서로 다른 assertion으로 검증한다.
- [ ] singleton → scoped 직접 의존을 만들지 않았다.
- [ ] 구조화 로그에 비밀이나 개인정보를 넣지 않았다.
- [ ] Release build 경고 0·오류 0, self-test와 demo 성공을 확인했다.
