# 2026-10-27 연습문제 — `Task.WhenEach` 견적 집계

이 문서는 [`README.md`](./README.md)의 설명을 읽고 demo와 self-test를 한 번씩 통과한 뒤 진행하세요. 모든 명령은 이 날짜 폴더(`dailyStudy/exercise/20261027`)에서 실행합니다.

```powershell
dotnet run --project ./src/SupplierQuoteExercise -- --demo
dotnet run --project ./src/SupplierQuoteExercise -- --self-test
```

연습 전 원본을 커밋하거나 별도 branch에 보관하면 언제든 정답 상태로 돌아올 수 있습니다. 각 단계에서는 기존 테스트를 지우거나 기대값을 느슨하게 만들지 말고, 새 동작을 설명하는 self-test를 먼저 추가하세요.

---

## 🟢 Beginner — 값과 흐름을 눈으로 추적하기

### 1. 변수의 정적 형식과 총액 계산

**목표**

- `decimal`, `int`, `var`가 각각 무엇을 표현하는지 구분합니다.
- 돈 계산에서 이진 부동소수점인 `double` 대신 십진수인 `decimal`을 쓰는 이유를 확인합니다.

**수정 위치**

- [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)의 demo 출력 부분

**할 일**

1. 선택된 견적의 `UnitPrice * request.Quantity`를 `totalPrice` 지역 변수에 저장합니다.
2. 공급업체 이름, 단가, 수량, 총액을 한 줄에 출력합니다.
3. `var`를 썼다면 주석으로 compiler가 실제 어떤 형식을 추론하는지 적습니다.

**입력과 기대 결과**

- 현재 demo의 요청 수량은 `25`, 선택된 `BusanBudget` 단가는 `1,050원`이므로 총액은 `26,250원`이어야 합니다.
- 소수 단가로 바꾸더라도 돈 계산에 `double`을 도입하지 않습니다.

**검증**

```powershell
dotnet run --project ./src/SupplierQuoteExercise -- --demo
```

완료 조건: 출력의 총액을 손으로 다시 계산한 값과 일치시키고, 기존 `[DEMO:SUCCESS]`가 유지됩니다.

### 2. 조건문으로 납기 상태 분류

**목표**

- `if` / `else if` / `else`가 처음 참인 branch 하나만 실행한다는 사실을 익힙니다.
- 화면 표시용 분류와 Domain 유효성 검증을 구분합니다.

**수정 위치**

- [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)

**할 일**

선택 견적의 납기일과 요청 희망일을 비교하여 다음 문구를 출력합니다.

| 조건 | 출력 |
| --- | --- |
| 납기일이 희망일보다 빠름 | `조기 납품` |
| 두 날짜가 같음 | `정시 납품` |
| 납기일이 더 늦음 | `납기 지연` |

**검증**

세 날짜 관계를 각각 한 번씩 만들고 demo를 실행합니다. 완료 조건: 세 branch가 모두 예상한 문구를 내며, 날짜를 문자열로 비교하지 않습니다.

### 3. `foreach`로 완료 결과 요약

**목표**

- collection의 각 원소를 `foreach`로 한 번씩 방문합니다.
- 성공과 실패를 `Result` 상태로 분기합니다.

**수정 위치**

- [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)
- 결과 형식은 [`QuoteAttempt.cs`](./src/SupplierQuoteExercise/Domain/QuoteAttempt.cs)를 참고합니다.

**할 일**

완료 순서로 저장된 시도 목록을 반복하여 성공이면 `성공`, 실패이면 오류 code를 출력하세요. 성공 값을 읽기 전에 성공 여부를 확인해야 합니다.

**입력과 기대 결과**

- demo의 세 공급업체는 `성공 → 업무 실패 → 성공` 순서로 표시되어야 합니다.
- 실패 Result에서 성공 값을 억지로 읽지 않습니다.

**검증**

```powershell
dotnet run --project ./src/SupplierQuoteExercise -- --demo
dotnet run --project ./src/SupplierQuoteExercise -- --self-test
```

완료 조건: 모든 시도가 한 번씩만 출력되고 self-test가 계속 통과합니다.

### 4. 잘못된 요청을 Domain 경계에서 거절

**목표**

- nullable, 빈 문자열, 수량 범위를 UI가 아니라 Domain Model에서 지킵니다.
- 예상 가능한 입력 오류는 예외 대신 `Result`로 표현합니다.

**수정 위치**

- [`QuoteRequest.cs`](./src/SupplierQuoteExercise/Domain/QuoteRequest.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

SKU가 trim 후 3자 미만이면 실패하는 규칙과 테스트를 추가하세요. 원본 SKU가 `null`, 공백, 2자, 3자일 때의 경계를 모두 검증합니다.

**기대 결과**

- `" A "`는 실패합니다.
- `" AB "`는 trim 후 `AB`이므로 실패합니다.
- `" ABC "`는 성공하고 저장된 값은 `ABC`입니다.

**검증**

```powershell
dotnet run --project ./src/SupplierQuoteExercise -- --self-test
```

완료 조건: 새 경계 테스트와 기존 테스트가 모두 통과하며 `Program`이나 Gateway에 같은 검증을 복제하지 않습니다.

---

## 🟡 Intermediate — 비동기 완료 순서와 부분 실패

### 5. 입력 순서와 완료 순서 비교

**목표**

- `Task.WhenEach`가 전달받은 순서가 아니라 **완료된 순서**로 Task를 내보낸다는 사실을 관찰합니다.
- `Task.WhenAll`의 결과 배열 순서와 혼동하지 않습니다.

**수정 위치**

- [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)
- [`ScriptedSupplierQuoteGateway.cs`](./src/SupplierQuoteExercise/Infrastructure/ScriptedSupplierQuoteGateway.cs)

**할 일**

Gateway 등록 순서를 `slow`, `fast`, `medium`으로 두고 지연은 각각 `150ms`, `20ms`, `80ms`로 설정합니다. 등록 순서와 callback 완료 순서를 각각 출력하세요.

**기대 결과**

```text
등록: slow -> fast -> medium
완료: fast -> medium -> slow
```

**검증**

demo를 5번 반복 실행해도 순서가 같아야 합니다. 완료 조건: 출력을 맞추기 위해 Gateway 목록을 완료 예상 순서로 미리 정렬하지 않습니다.

### 6. `Task.WhenAll`과 진행 표시 비교

**목표**

- “전체 완료를 기다린 뒤 한꺼번에 처리”와 “하나가 끝날 때마다 처리”의 UX 차이를 이해합니다.

**수정 위치**

- 새 실험 method를 [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)에 추가합니다.

**할 일**

같은 Gateway Task 배열을 대상으로 다음 두 방식을 별도 method로 구현합니다.

1. `await Task.WhenAll(tasks)` 뒤 결과 출력
2. `await foreach (Task<QuoteAttempt> completed in Task.WhenEach(tasks))`에서 결과 출력

모든 method 위에 역할, parameter, 반환값을 설명하는 한글 주석을 작성합니다.

**기대 결과**

- `WhenAll` 방식은 가장 느린 Task가 끝난 뒤에야 첫 결과를 출력합니다.
- `WhenEach` 방식은 빠른 결과부터 출력합니다.
- 두 방식 모두 최종 시도 수와 성공/실패 수는 같습니다.

**검증**

stopwatch 출력은 참고 자료로만 쓰고, 테스트 성공을 몇 ms 이하 같은 느슨한 환경 의존 기준에 맡기지 않습니다.

### 7. 한 공급업체의 업무 실패를 보존

**목표**

- 재고 없음처럼 예상 가능한 외부 응답은 `Result.Failure`로 모델링합니다.
- 부분 실패가 다른 성공 결과를 지우지 않게 합니다.

**수정 위치**

- [`ScriptedSupplierQuoteGateway.cs`](./src/SupplierQuoteExercise/Infrastructure/ScriptedSupplierQuoteGateway.cs)
- [`CollectSupplierQuotesService.cs`](./src/SupplierQuoteExercise/Application/CollectSupplierQuotesService.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

네 Gateway 중 두 곳이 `supplier.out_of_stock` 실패를 반환하게 만들고 다음을 테스트하세요.

- 완료 시도 4개
- 성공 2개, 실패 2개
- 성공 중에서 선택된 견적 존재
- Repository 저장 1회

**검증**

```powershell
dotnet run --project ./src/SupplierQuoteExercise -- --self-test
```

완료 조건: 첫 실패에서 loop를 `break`하거나 전체 결과를 실패로 바꾸지 않습니다.

### 8. 모든 공급업체가 실패한 경우

**목표**

- “집계 자체는 정상 완료했지만 선택 가능한 견적은 없음”을 표현합니다.
- nullable 값에 접근하기 전에 pattern matching으로 존재를 확인합니다.

**수정 위치**

- [`QuoteRoundReport.cs`](./src/SupplierQuoteExercise/Domain/QuoteRoundReport.cs)
- [`CollectSupplierQuotesService.cs`](./src/SupplierQuoteExercise/Application/CollectSupplierQuotesService.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

모든 Gateway가 업무 실패를 반환하는 test를 작성하세요. 선택 견적은 `null`, 실패 상세는 모두 보존되어야 하며 report는 Repository에 한 번 저장되어야 합니다.

**기대 결과**

- `SelectedQuote is null`
- 성공 `0`, 실패는 Gateway 수와 같음
- 예외가 발생하지 않음

**검증**

완료 조건: `!` null-forgiving operator로 경고를 숨기지 않고 테스트가 통과합니다.

---

## 🔵 Advanced — 정책 교체, 예외, 취소

### 9. 최저가 정책을 최단 납기 정책으로 교체

**목표**

- Strategy를 바꾸어도 Application Service가 수정되지 않는 OCP를 경험합니다.
- 선택 규칙과 비동기 수집 규칙을 분리합니다.

**수정 위치**

- [`IQuoteSelectionPolicy.cs`](./src/SupplierQuoteExercise/Application/Ports/IQuoteSelectionPolicy.cs)
- 새 파일 `Infrastructure/EarliestDeliveryPolicy.cs`
- [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

납기일 오름차순, 같은 날이면 총액 오름차순, 그래도 같으면 공급업체 ID의 ordinal 순서로 선택하는 Strategy를 구현하세요.

**입력과 기대 결과**

- A: 총액 30,000원 / 3일 후
- B: 총액 32,000원 / 1일 후
- C: 총액 29,000원 / 4일 후
- 새 정책은 B를 선택해야 합니다.

**검증**

완료 조건: Composition Root의 주입 대상만 바꾸며 `CollectSupplierQuotesService`에는 `if (policyName == ...)` 같은 분기를 추가하지 않습니다.

### 10. 예상 밖 예외를 실패 Result로 숨기지 않기

**목표**

- 업무 실패와 programmer/인프라 결함의 경계를 구분합니다.
- `Task.WhenEach`가 내보낸 완료 Task를 다시 `await`할 때 예외가 관찰됨을 확인합니다.

**수정 위치**

- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

`InvalidOperationException("broken protocol")`을 던지는 fake Gateway를 추가합니다. Service 호출이 같은 예외를 전파하고 Repository 저장은 0회인지 검증하세요.

**검증**

완료 조건: broad `catch (Exception)`으로 `quote.unavailable` Result를 만들지 않습니다. 테스트는 예외 형식과 메시지, 미저장을 모두 확인합니다.

### 11. 취소는 취소로 전파하기

**목표**

- `CancellationToken`이 호출자의 제어 신호임을 이해합니다.
- 취소된 집계를 불완전한 성공 report로 저장하지 않습니다.

**수정 위치**

- [`CollectSupplierQuotesService.cs`](./src/SupplierQuoteExercise/Application/CollectSupplierQuotesService.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

첫 성공이 callback에 도착한 직후 token을 취소하는 테스트를 작성합니다. 아직 끝나지 않은 Gateway가 취소를 관찰하도록 fake도 준비하세요.

**기대 결과**

- 호출자는 `OperationCanceledException`을 받습니다.
- Repository 저장 횟수는 0입니다.
- 취소를 `Result.Failure`나 빈 report로 바꾸지 않습니다.

**검증**

완료 조건: 테스트가 timeout 없이 결정적으로 끝나고, 실제 `CancellationToken`을 예외 및 Gateway에 연결합니다.

### 12. 동시 시작 계약을 결정적으로 검증

**목표**

- `Task.WhenEach` 열거 전에 Gateway Task를 모두 만들어야 fan-out이 시작된다는 점을 테스트합니다.
- 시간 측정 대신 명시적 신호를 사용해 flaky test를 피합니다.

**수정 위치**

- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

각 호출이 “시작됨”을 기록한 뒤 `TaskCompletionSource` 신호를 기다리는 fake Gateway 3개를 만드세요.

1. Service 호출을 시작합니다.
2. 결과를 하나도 완료시키지 않은 상태에서 세 Gateway의 호출 횟수가 모두 1인지 검사합니다.
3. B, C, A 순으로 완료시킵니다.
4. callback과 report의 완료 순서가 B, C, A인지 검사합니다.

**검증**

완료 조건: `Task.Delay(100)` 같은 추측성 대기 없이 동시 시작과 완료 순서를 증명합니다.

---

## 🟣 Pro — 운영 제약을 설계에 반영하기

### 13. bounded concurrency 추가

**목표**

- 공급업체가 수천 개일 때 모든 요청을 동시에 시작하면 socket, thread pool, 상대 시스템을 압박할 수 있음을 이해합니다.
- 완료 순서 소비는 유지하면서 최대 동시 호출 수를 제한합니다.

**수정 위치**

- 새 Port 또는 Infrastructure 구성 요소
- [`CollectSupplierQuotesService.cs`](./src/SupplierQuoteExercise/Application/CollectSupplierQuotesService.cs)
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

최대 동시 호출 수를 `2`로 제한하세요. `SemaphoreSlim`을 사용해도 되지만 acquire와 release는 `try/finally`로 짝을 맞춰야 합니다. 대안으로 두 개씩 batch를 만들 수 있지만, batch 경계가 빠른 다음 요청을 불필요하게 기다리게 하는 차이를 설명하세요.

**입력과 기대 결과**

- 5개 Gateway를 모두 성공시키며 각 fake가 현재 호출 수와 관찰된 최대값을 기록합니다.
- 완료 시도는 5개이고 관찰된 최대 동시 호출 수는 2를 넘지 않아야 합니다. fake가 두 호출을 의도적으로 겹치게 만든 테스트에서는 최대값이 정확히 2여야 합니다.
- 시작 순서와 완료 순서를 같은 것으로 가정하지 않습니다.

**검증**

```powershell
dotnet build ./SupplierQuoteExercise.slnx -c Release
dotnet run --project ./src/SupplierQuoteExercise -c Release -- --self-test
```

완료 조건: 정상·예외·취소 경로 모두 permit을 반환하며, 테스트가 동시 호출 수와 결과 보존을 함께 확인합니다.

### 14. 결과를 진행 stream으로 노출하는 설계

**목표**

- callback과 `IAsyncEnumerable<QuoteAttempt>`의 trade-off를 설명합니다.
- API 사용자가 완료 결과를 직접 `await foreach`로 소비할 수 있는 형태를 설계합니다.

**수정 위치**

- 새 Application Service method
- [`SelfTestRunner.cs`](./src/SupplierQuoteExercise/SelfTesting/SelfTestRunner.cs)

**할 일**

완료된 시도를 하나씩 `yield return`하는 async stream method의 signature와 구현을 추가하세요. `CancellationToken`을 async iterator에 연결하고, report 저장 책임을 어디에 둘지 결정해 문서화하세요.

**주의할 설계 질문**

- 소비자가 두 개만 읽고 중단하면 Repository에 무엇을 저장해야 하나요?
- iterator 본문은 첫 열거 때 시작되므로 “호출 시 시작”과 “열거 시 시작” 중 어떤 계약인가요?
- 같은 stream을 두 번 열거하면 Gateway도 두 번 호출되나요?

**검증**

완료 조건: happy path뿐 아니라 조기 중단과 취소 테스트가 있고, 부분 report 저장 정책이 코드와 주석에서 일치합니다.

---

## ✅ 최종 복습 체크리스트

- [ ] `var`가 `dynamic`이 아니라 compile-time 형식 추론임을 설명할 수 있다.
- [ ] 입력 순서와 Task 완료 순서를 예측하고 실제 출력으로 확인했다.
- [ ] Gateway Task를 모두 먼저 시작해야 하는 이유를 설명할 수 있다.
- [ ] 업무 실패, 예외, 취소를 서로 다른 경로로 처리했다.
- [ ] 일부 업무 실패에도 성공한 견적을 잃지 않았고, 성공이 없을 때 nullable을 안전하게 처리했다.
- [ ] Strategy 교체 때 Application Service를 수정하지 않았다.
- [ ] Repository 저장 시점과 취소 시 미저장 계약을 테스트했다.
- [ ] 시간에 의존하는 flaky test 대신 제어 가능한 fake와 신호를 사용했다.
- [ ] 무제한 fan-out의 운영 위험과 bounded concurrency 대안을 설명할 수 있다.
- [ ] 모든 새 method에 역할·parameter·반환값과 설계 이유를 설명하는 한글 주석을 달았다.
