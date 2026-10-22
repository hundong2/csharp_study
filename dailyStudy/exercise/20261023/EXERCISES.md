# 2026-10-23 Beginner to Pro 연습문제

## 시작 전 규칙

각 과제는 다음 순서로 진행합니다.

1. 코드를 바꾸기 전에 예상 결과를 적습니다.
2. 한 번에 하나의 계약만 바꿉니다.
3. 새 메서드에는 “무엇을 하는지, 파라미터 의미, 반환값” 한글 주석을 답니다.
4. 새 문법을 처음 쓰는 줄에는 문법과 사용 이유를 설명합니다.
5. 아래 공통 검증을 실행합니다.
6. 실패하면 오류 문구만 고치지 말고 어떤 불변식이 깨졌는지 설명합니다.

```powershell
cd dailyStudy/exercise/20261023
dotnet build .\OrderPaymentReconciliation.slnx -c Release
dotnet run --project .\src\OrderPaymentReconciliation\OrderPaymentReconciliation.csproj `
  -c Release --no-build -- --self-test
dotnet run --project .\src\OrderPaymentReconciliation\OrderPaymentReconciliation.csproj `
  -c Release --no-build -- --demo
dotnet format .\OrderPaymentReconciliation.slnx --verify-no-changes --no-restore
```

현재 기준은 Release build 경고 0개·오류 0개, `[SELF-TEST] 33/33 checks passed`, demo exit code 0입니다. 검사를 추가하면 분모도 함께 늘어나는 것이 정상입니다.

---

## Beginner 1 — 대사 결과를 코드 없이 예측하기

다음 입력의 결과를 먼저 종이에 적습니다.

### 주문

| OrderId | 기대 금액 |
| --- | ---: |
| `A` | 100 KRW |
| `B` | 200 KRW |
| `C` | 3 USD |

### 결제

| PaymentId | OrderId | 실제 금액 |
| --- | --- | ---: |
| `P1` | `A` | 100 KRW |
| `P2` | `C` | 3 EUR |
| `P3` | `D` | 400 KRW |

다음 표를 채우세요.

| OrderId | 상태 | `ExpectedAmount` null? | `ActualAmount` null? | `Difference` |
| --- | --- | --- | --- | --- |
| `A` |  |  |  |  |
| `B` |  |  |  |  |
| `C` |  |  |  |  |
| `D` |  |  |  |  |

### 실행 확인

[`SelfTestRunner.cs`](./src/OrderPaymentReconciliation/SelfTesting/SelfTestRunner.cs)에 별도 `TestBeginnerPredictionAsync` 메서드를 추가하고 `RunAsync`에서 호출합니다. 기존 `TestFiveStatusesAsync`는 다섯 상태와 5개 행을 함께 검증하므로 **입력만 위 표로 바꾸면 기존 assertion과 맞지 않습니다.** 새 메서드에서는 성공 여부를 먼저 확인한 다음, 총 4개 행과 `Matched`·`MissingPayment`·`CurrencyMismatch`·`OrphanPayment` 각 1개, `AmountMismatch` 0개를 검사하세요. 마지막으로 표의 null과 `Difference` 예측도 assertion으로 옮깁니다.

### 완료 기준

- 주문 `B`가 `LeftJoin`에서 사라지지 않는 이유를 말할 수 있습니다.
- 결제 `P3`가 별도 anti-join 없이는 나타나지 않는 이유를 말할 수 있습니다.
- 서로 다른 통화의 `Difference`를 숫자로 만들지 않습니다.
- 기존 33개 검사를 그대로 통과시키면서 새 검사가 분모에 추가됩니다.

---

## Beginner 2 — `Money.Create` 경계값을 표 기반으로 검사하기

다음 입력을 배열 또는 collection expression으로 만들고 `foreach`로 반복 검사하세요.

| amount | currency | 예상 |
| ---: | --- | --- |
| `0` | `KRW` | 성공 |
| `0.01` | `usd` | 성공, `USD` |
| `-0.01` | `KRW` | `money.amount_negative` |
| `1.001` | `KRW` | `money.amount_scale` |
| `10` | `null` | `money.currency_required` |
| `10` | `EURO` | `money.currency_invalid` |
| `10` | `K1W` | `money.currency_invalid` |

### 구현 힌트

- 입력과 기대 오류 코드를 담는 작은 `record`를 self-test 파일에 추가할 수 있습니다.
- 성공 행과 실패 행을 같은 `if`로 억지로 처리하지 말고 기대 결과를 명확하게 표현하세요.
- `double`로 바꾸지 마세요. 왜 `decimal`인지 README에서 다시 확인합니다.

### 완료 기준

- 모든 행이 자동 검사됩니다.
- 오류 메시지 전체 문자열이 아니라 안정적인 `Error.Code`를 비교합니다.
- 빌드 경고 0개를 유지합니다.

---

## Beginner 3 — join key selector를 추적하기

[`ReconcileOrdersService.cs`](./src/OrderPaymentReconciliation/Application/ReconcileOrdersService.cs)의 `LeftJoin`에는 key selector가 두 개 있습니다.

```csharp
order => order.OrderId
payment => payment.OrderId
```

두 번째 selector를 실험 목적으로 `payment => payment.PaymentId`로 바꾸기 전에 demo의 다섯 행이 어떻게 변할지 적으세요. 실행해 예측을 확인하고 즉시 원래 코드로 되돌립니다.

### 생각할 질문

- compiler는 두 selector가 모두 `string`을 돌려준다는 것만 알 수 있습니다. 의미가 잘못된 key를 왜 compile-time에 잡지 못할까요?
- `OrderId`와 `PaymentId`를 모두 원시 `string`으로 둔 설계의 약점은 무엇일까요?
- `readonly record struct OrderId(string Value)` 같은 strongly typed ID가 어떤 실수를 막을 수 있을까요?

### 완료 기준

- 문법적으로 맞는 코드와 업무 의미가 맞는 코드의 차이를 설명합니다.
- 실험 뒤 self-test가 다시 전부 통과합니다.

---

## Beginner+ 4 — 결제 없는 주문을 한 건 더 추가하기

[`Program.CreateDemoOrders`](./src/OrderPaymentReconciliation/Program.cs)에 `ORD-104`, `7_500 KRW` 주문을 추가하고 결제는 추가하지 않습니다.

### 실행 전 예측

- 전체 행 수는 몇 개가 될까요?
- `MissingPayment` 수와 `ReviewCount`는 각각 몇 개가 될까요?
- 출력 정렬에서 `ORD-104`는 어디에 놓일까요?

### 구현

1. demo 주문을 추가합니다.
2. demo summary의 기대값을 README가 아니라 코드 출력으로 확인합니다.
3. 같은 계약을 self-test에도 추가합니다.
4. self-test 분모가 증가했는지 확인합니다.

### 완료 기준

- 새 주문이 `PaymentId == null`인 행으로 남습니다.
- inner join으로 바꾸지 않고 `LeftJoin`의 보존성을 설명합니다.

---

## Intermediate 5 — 고아 결제 두 건을 탐지하기

주문에 없는 `ORD-Y`를 가리키는 `PAY-Y`를 하나 더 추가합니다.

### 확인할 것

- `LeftJoin`이 아니라 어떤 LINQ 단계가 새 행을 만들었나요?
- `HashSet<string>`을 만든 이유는 무엇인가요?
- 결과 순서가 입력 순서가 아니라 `OrderId`, `PaymentId` 순서인 이유는 무엇인가요?

### 추가 검사

`OrphanPayment`가 두 건인지, 두 행 모두 `ExpectedAmount is null`인지 자동 검사합니다.

### 완료 기준

- 오른쪽에만 있는 데이터를 찾는 anti-join을 말로 설명합니다.
- 순서가 다른 입력 두 개를 넣어도 최종 보고서 순서가 같은지 확인합니다.

---

## Intermediate 6 — 식별자 대소문자 정책 비교하기

주문 ID는 `ORD-CASE`, 결제의 OrderId는 `ord-case`로 만듭니다. 현재 `StringComparer.Ordinal`에서는 서로 다른 ID입니다.

1. 현재 결과를 예측하고 검사합니다.
2. `LeftJoin`, `HashSet`, `GroupBy`, 정렬뿐 아니라 `ReconciliationLine.ValidatePair`의 OrderId 비교도 `OrdinalIgnoreCase`로 바꾼 별도 branch를 실험합니다.
3. 일부 comparer만 바꿨을 때 생기는 모순을 확인합니다.
4. 원래 정책으로 되돌립니다.

### 생각할 질문

- 식별자 비교 정책은 왜 join 한 줄에만 있는 선택이 아닌가요?
- 데이터베이스 collation이 대소문자를 무시한다면 메모리 검사와 어떤 불일치가 생길 수 있나요?
- 사용자 표시 이름과 시스템 식별자는 같은 comparer를 써야 할까요?

### 완료 기준

- 비교 규칙을 모든 경계에서 일관되게 적용해야 함을 설명합니다.
- join만 바꾸면 상태별 factory가 대소문자가 다른 OrderId를 거절한다는 점을 검사합니다.
- 실험 뒤 `StringComparer.Ordinal` 계약과 33개 이상 검사를 복구합니다.

---

## Intermediate+ 7 — 허용 오차 Strategy 추가하기

카드사 반올림 차이 1원까지는 일치로 보는 `WithinTolerancePolicy`를 만듭니다.

### 요구사항

- `IReconciliationPolicy`를 구현합니다.
- 생성자에서 `decimal tolerance`를 받습니다.
- tolerance가 음수면 `ArgumentOutOfRangeException`을 던집니다. 이는 사용자 주문 데이터가 아니라 잘못된 객체 구성입니다.
- 통화가 다르거나 결제가 없거나 고아인 규칙은 기존과 같습니다.
- 같은 통화이고 `Math.Abs(difference) <= tolerance`이면 `Matched`입니다.
- `Matched` 행은 `ReconciliationLine.ForPolicyMatch`로 만들고 실제 `Difference`를 그대로 보존합니다.
- `Program`의 Composition Root에서만 Strategy 선택을 바꿉니다.

### 자동 검사

| expected | actual | tolerance | 결과 |
| ---: | ---: | ---: | --- |
| 100 | 100 | 1 | `Matched` |
| 100 | 101 | 1 | `Matched` |
| 100 | 99 | 1 | `Matched` |
| 100 | 102 | 1 | `AmountMismatch` |

### 완료 기준

- 기존 `ReconcileOrdersService`를 수정하지 않습니다.
- 기존 Domain factory를 수정하지 않고 Strategy 추가·교체만으로 규칙이 달라집니다.
- 새 class의 모든 메서드와 생성자에 한글 계약 주석을 답니다.

---

## Advanced 8 — 부분 결제 계약을 먼저 설계하기

현재 코드는 주문 한 건의 결제 두 건을 `data.multiple_payments_for_order`로 거절합니다. 부분 결제를 허용하려면 단순히 이 검사를 지우면 안 됩니다.

### 먼저 결정할 질문

- 두 결제의 통화가 다르면 합계를 낼 수 있나요?
- 승인·취소·환불 결제를 어떻게 구분하나요?
- 같은 `PaymentId` 재수신과 실제 두 번 결제를 어떻게 구분하나요?
- 결제 합계의 기준 시각은 언제인가요?
- 고아 결제는 개별 행인가요, OrderId별 group인가요?

### 구현 과제

1. `PaymentStatus` enum에 최소 `Completed`, `Canceled`, `Refunded`를 정의하고 `Payment`가 상태를 보존하게 합니다. 기존 fixture는 모두 `Completed`로 명시합니다.
2. `IReconciliationPolicy`가 `IReadOnlyList<Payment>`를 받는 새 계약을 설계합니다.
3. `GroupJoin` 또는 `GroupBy`로 주문별 payment group을 전달합니다.
4. 같은 통화의 `Completed` 결제만 합산하고, 취소·환불을 제외한 이유를 판정 근거에 남깁니다.
5. 위 질문에서 정한 규칙마다 self-test를 추가합니다.

### 완료 기준

- 단순히 중복 검사를 삭제하지 않습니다.
- aggregate의 불변식과 오류 코드를 문서로 남깁니다.
- 기존 결제 한 건 시나리오도 계속 통과합니다.

---

## Advanced 9 — CSV Repository Adapter 만들기

메모리 Adapter 대신 파일에서 읽는 `CsvOrderRepository`를 추가합니다. 외부 package 없이 `File.ReadLinesAsync` 또는 `StreamReader`를 사용합니다.

### 최소 계약

```text
orderId,customerId,amount,currency,placedAtUtc
ORD-CSV-1,CUS-1,1000.00,KRW,2026-10-23T00:00:00Z
```

- header를 검사합니다.
- 열 개수가 다르면 줄 번호를 포함한 예상 오류로 바꿉니다.
- `decimal.TryParse`에 `CultureInfo.InvariantCulture`를 명시합니다.
- `DateTimeOffset.TryParse` 결과는 UTC로 정규화합니다.
- 취소 신호를 읽기 반복에 전달하거나 매 줄 확인합니다.
- file path를 Domain Model에 넣지 않습니다.

### 설계 질문

현재 `IOrderRepository.ListAsync`는 `Result`를 반환하지 않습니다. CSV parsing 실패를 어떻게 전달할지 다음 중 하나를 선택하고 이유를 적으세요.

1. Port를 `Task<Result<IReadOnlyList<Order>>>`로 바꾼다.
2. Infrastructure 예외를 Application 경계에서 안정적인 오류로 변환한다.
3. 시작 시 CSV를 검증·적재하고 Repository에는 유효한 snapshot만 넣는다.

### 완료 기준

- 선택한 오류 경계가 코드와 주석에 일관됩니다.
- 정상 CSV, 빈 값, 잘못된 금액, 잘못된 날짜, 취소를 자동 검사합니다.
- Composition Root만 Adapter 선택을 압니다.

---

## Pro 10 — 큰 데이터에서 query 경계 다시 설계하기

현재 `ListAsync`는 모든 데이터를 메모리에 올립니다. 주문 1천만 건이라면 적합하지 않습니다.

다음 중 한 접근을 선택해 Port를 다시 설계하세요.

- 날짜 범위와 tenant를 받는 page query
- DB가 반환하는 projection 전용 query
- `IAsyncEnumerable<T>` 기반 streaming
- batch key 집합을 사용하는 chunked lookup

### 작성할 산출물

1. 새 Port method signature
2. 메모리 사용 상한
3. 정렬 안정성
4. 재시작 기준점
5. DB index 후보
6. 취소와 timeout 처리
7. 기존 `LeftJoin` 중 어느 부분을 DB query로 내릴지
8. 전체 `ImmutableArray` 보고서 대신 page별 결과를 저장하는 sink 또는 `IAsyncEnumerable<ReconciliationLine>` 출력 계약

작은 in-memory Adapter와 결과 수집용 test sink도 새 Port를 구현해 self-test가 실행되게 만드세요. 메모리 상한에는 입력 page뿐 아니라 아직 저장되지 않은 출력 행도 포함합니다.

### 완료 기준

- `IEnumerable`과 `IQueryable`을 무심코 섞지 않습니다.
- 입력과 출력 어느 쪽도 전체 데이터를 한 번에 materialize하지 않는다는 근거가 있습니다.
- 같은 입력 page를 재시도해도 결과가 결정적입니다.

---

## Pro+ 11 — 운영 가능한 대사 job으로 확장하기

코드를 먼저 작성하지 말고 다음 운영 계약을 README의 별도 절에 추가합니다.

- 같은 대사 기준 시각으로 재실행했을 때 결과가 같은가?
- 성공한 report를 어떤 key로 저장하는가?
- 중간 실패 뒤 어디부터 재시작하는가?
- `Matched`, `MissingPayment`, `AmountMismatch`, `CurrencyMismatch`, `OrphanPayment` 수를 어떤 metric으로 노출하는가?
- OrderId/PaymentId를 log에 그대로 남겨도 되는가?
- 원본 금액과 판정 정책 버전을 audit할 수 있는가?
- 사람이 해결한 상태는 원본 대사 결과와 어떻게 분리하는가?

그다음 작은 `IReconciliationReportSink` Port와 메모리 Adapter를 추가해 성공 report를 저장합니다.

### 실패 주입 검사

- 저장 직전 취소
- sink가 예외를 던짐
- 동일 report 두 번 저장
- 정책 version 변경 후 재대사

### 완료 기준

- 관측 가능성 때문에 Domain Model을 logging framework에 의존시키지 않습니다.
- idempotency key와 재시도 의미를 명시합니다.
- 개인정보가 log/metric label에 새지 않게 설계합니다.

---

## 마무리 설명 과제

아래 내용을 코드 없이 2분 안에 설명해 보세요.

1. 왜 `Join`이 아니라 `LeftJoin`인가?
2. 왜 고아 결제는 별도 anti-join이 필요한가?
3. 왜 중복 key를 join 전에 검사하는가?
4. `Money`, Result, Repository, Strategy는 각각 어떤 실수를 막는가?
5. Composition Root를 한곳에 둔 이유는 무엇인가?
6. 이 in-memory 설계를 운영 규모로 옮길 때 가장 먼저 바꿀 경계는 무엇인가?
