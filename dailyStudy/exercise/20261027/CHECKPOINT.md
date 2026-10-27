# 2026-10-27 이해도 검증 — `Task.WhenEach`, 부분 실패, Port와 Strategy

먼저 답을 말하거나 적은 뒤 펼쳐 보세요. 정답 문장을 외우기보다 **코드의 어느 지점이 그 계약을 보장하는지** 설명할 수 있으면 통과입니다.

---

## 1단계 — Syntax와 기본 Grammar

### Q1. `var quote = ...`라고 쓰면 `quote`의 형식은 runtime에 정해지나요?

<details>
<summary>정답과 설명</summary>

아닙니다. compiler가 오른쪽 식을 보고 compile-time에 정적 형식을 확정합니다. `var`는 `dynamic`이 아니므로 이후 아무 형식이나 넣을 수 없습니다. 형식이 오른쪽에서 명확할 때 반복을 줄이는 문법입니다.

</details>

### Q2. 단가를 `decimal`, 수량을 `int`로 표현한 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

수량은 정수 개수이므로 `int`가 맞고, 돈은 십진수 계산이 중요하므로 `decimal`이 적합합니다. `double`은 이진 부동소수점이어서 일부 십진 금액을 정확히 표현하지 못합니다.

</details>

### Q3. `string?`의 `?`는 어떤 계약을 나타내나요?

<details>
<summary>정답과 설명</summary>

문자열 값이 있을 수도 있고 `null`일 수도 있음을 형식에 표시합니다. `<Nullable>enable</Nullable>`과 함께 쓰면 compiler가 null 확인 없는 접근을 경고합니다. 이것은 runtime 값 검증을 대신하지 않으므로 외부 입력은 여전히 Domain factory에서 검사해야 합니다.

</details>

### Q4. `record`를 썼다고 내부 상태가 자동으로 완전히 불변이 되나요?

<details>
<summary>정답과 설명</summary>

아닙니다. `record`는 값 중심 equality와 편의 기능을 제공하지만, settable property나 `List<T>`를 노출하면 내용은 바뀔 수 있습니다. get-only 값과 변경 불가능한 snapshot을 함께 사용해야 불변 계약이 강해집니다.

</details>

### Q5. collection expression `[]`는 언제 어떤 실제 collection 형식이 되나요?

<details>
<summary>정답과 설명</summary>

왼쪽 변수, parameter, 반환 형식 같은 목표 형식에 따라 compiler가 결정합니다. `SupplierQuote[] quotes = [];`에서는 배열이고, `List<SupplierQuote> quotes = [];`에서는 List를 만듭니다. 목표 형식이 불명확하면 compile 오류가 날 수 있습니다.

</details>

### Q6. `value is not null`은 무엇인가요?

<details>
<summary>정답과 설명</summary>

pattern matching으로 값이 null이 아닌지를 검사합니다. nullable reference를 안전하게 좁히므로 해당 branch 안에서 compiler도 값이 존재한다고 판단할 수 있습니다.

</details>

### Q7. `=>` 식 본문은 언제 쓰는 것이 좋은가요?

<details>
<summary>정답과 설명</summary>

하나의 간단한 식으로 의미가 분명한 method나 property에 적합합니다. 여러 단계, guard, 오류 처리까지 한 줄로 압축하면 초보자가 흐름을 놓치므로 block 본문이 더 낫습니다. 짧음 자체보다 읽기 쉬움이 기준입니다.

</details>

### Q8. LINQ의 `Where`와 `OrderBy`는 원본 collection을 직접 바꾸나요?

<details>
<summary>정답과 설명</summary>

보통 원본을 바꾸지 않고 조건과 정렬을 표현하는 새 sequence를 만듭니다. 많은 LINQ 연산은 실제 열거 시 실행되는 지연 실행이므로, 안정된 snapshot이 필요하면 `ToArray`나 immutable collection으로 물질화하는 시점을 의식해야 합니다.

</details>

---

## 2단계 — `async`, `Task`, `Task.WhenEach`

### Q9. `async` method를 호출하면 항상 새 thread가 생기나요?

<details>
<summary>정답과 설명</summary>

아닙니다. `async` / `await`는 비동기 완료를 이어서 처리하는 상태 기계를 만드는 문법입니다. I/O를 기다리는 동안 thread를 붙잡지 않을 수 있지만, 새 thread 생성 자체를 뜻하지 않습니다.

</details>

### Q10. `Task<T>`는 무엇을 나타내나요?

<details>
<summary>정답과 설명</summary>

나중에 완료되어 `T` 값, 예외, 또는 취소 중 하나의 상태가 될 비동기 작업을 나타냅니다. `await`하면 성공 시 `T`를 받고, faulted면 예외가 다시 던져지며, canceled면 취소 예외를 관찰합니다.

</details>

### Q11. Gateway Task를 하나 만들고 기다린 뒤 다음 Task를 만들면 fan-out이 되나요?

<details>
<summary>정답과 설명</summary>

아닙니다. 첫 작업이 끝날 때까지 다음 호출이 시작되지 않아 순차 실행입니다. 이 예제는 모든 Gateway 호출로 Task 배열을 먼저 만든 다음 `Task.WhenEach`로 완료 순서를 소비합니다.

</details>

### Q12. `Task.WhenEach(tasks)`가 반환하는 것은 최종 결과 배열인가요?

<details>
<summary>정답과 설명</summary>

아닙니다. 제공한 Task들이 끝나는 순서대로 **완료된 Task**를 내보내는 `IAsyncEnumerable<Task<T>>`입니다. 따라서 `await foreach`로 열거하고, 각 완료 Task를 다시 `await`해 실제 결과나 예외를 관찰합니다.

</details>

### Q13. 왜 `await foreach`가 필요한가요?

<details>
<summary>정답과 설명</summary>

다음 원소가 즉시 준비된 동기 sequence가 아니라 비동기로 준비되는 `IAsyncEnumerable<T>`를 소비하기 때문입니다. loop는 다음 완료 Task가 없으면 thread를 block하지 않고 기다렸다가 하나가 준비되면 본문을 실행합니다.

</details>

### Q14. 입력 목록이 A, B, C 순이면 `Task.WhenEach`도 항상 A, B, C를 내보내나요?

<details>
<summary>정답과 설명</summary>

아닙니다. 실제 완료 순서로 내보냅니다. B가 가장 빨리 끝났다면 B가 먼저 옵니다. 동시에 완료된 Task 사이의 세부 순서에 업무 의미를 두지 않아야 하며, 결정적 최종 선택은 별도 Strategy의 정렬 기준으로 정해야 합니다.

</details>

### Q15. `Task.WhenAll`과 가장 큰 차이는 무엇인가요?

<details>
<summary>정답과 설명</summary>

`WhenAll`은 모든 작업이 끝나는 하나의 Task를 제공하므로 보통 전체 완료 후 결과를 다룹니다. `WhenEach`는 하나가 끝날 때마다 처리할 수 있어 진행 표시와 부분 결과 관찰에 알맞습니다. 둘 다 작업을 먼저 시작해 두어야 병렬 fan-out 효과가 있습니다.

</details>

### Q16. `Task.WhenEach`가 Task 내부 예외를 자동으로 Result 실패로 바꾸나요?

<details>
<summary>정답과 설명</summary>

아닙니다. 완료 Task를 `await`하면 원래 예외가 전파됩니다. 재고 없음 같은 예상 가능한 상황은 Gateway가 명시적 `Result.Failure`로 반환하고, programmer bug나 예상 밖 인프라 결함은 예외로 남겨야 합니다. 이 예제는 첫 fault 뒤 linked token으로 sibling Gateway를 취소하고 모든 Task를 끝까지 관찰한 다음, 정리 중 부수 예외가 아니라 최초 예외를 다시 던집니다.

</details>

### Q17. callback에서 완료 순서를 보여 주는 것과 최종 선택 순서는 왜 분리해야 하나요?

<details>
<summary>정답과 설명</summary>

완료 순서는 네트워크와 부하에 따라 바뀌는 관찰 정보입니다. “가장 저렴하고, 납기가 빠르고, 공급업체 ID가 앞선 견적” 같은 업무 선택은 결정적 규칙이어야 합니다. 먼저 왔다는 이유만으로 선택하면 실행마다 결과가 달라질 수 있습니다.

</details>

---

## 3단계 — Domain, Result, 불변성

### Q18. SKU와 수량 검증을 `Program` 대신 `QuoteRequest`에서 하는 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

어떤 진입점에서 만들든 잘못된 요청이 Domain 안으로 들어오지 못하게 한 곳에서 불변식을 지키기 위해서입니다. Console, API, test가 각각 검증하면 규칙이 달라지거나 하나가 빠질 수 있습니다.

</details>

### Q19. `Result<T>`가 적합한 실패 예시는 무엇인가요?

<details>
<summary>정답과 설명</summary>

잘못된 SKU, 범위를 벗어난 수량·단가, 공급업체 재고 없음, Gateway ID와 응답 ID 불일치처럼 입력이나 외부 연동에서 예상 가능한 실패입니다. 호출자가 성공/실패를 분기해 복구하거나 사용자에게 설명할 수 있습니다.

</details>

### Q20. 예외가 적합한 실패 예시는 무엇인가요?

<details>
<summary>정답과 설명</summary>

성공 Result에서 오류 값을 읽는 계약 위반, 알 수 없는 상태, 깨진 protocol 구현처럼 정상 업무 분기로 취급하면 안 되는 결함입니다. 광범위하게 catch해 업무 실패로 꾸미면 장애를 숨깁니다.

</details>

### Q21. 취소를 `Result.Failure("cancelled")`로 바꾸면 왜 문제가 되나요?

<details>
<summary>정답과 설명</summary>

취소는 호출자가 결과를 더 이상 원하지 않는다는 제어 신호입니다. 이를 업무 실패로 바꾸면 host나 상위 workflow가 취소와 실제 실패를 구분하지 못하고, 불완전한 report를 정상적으로 저장할 위험이 있습니다.

</details>

### Q22. 일부 Gateway가 실패해도 성공 견적을 report에 남기는 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

공급업체별 재고 없음은 서로 독립적인 업무 결과입니다. 하나의 예상 가능한 실패 때문에 다른 공급업체의 유효한 견적을 버리면 부분 성공의 정보를 잃습니다. report는 각 시도를 보존하고 성공들 사이에서 선택합니다.

</details>

### Q23. 성공 견적이 하나도 없을 때 선택 값을 nullable로 두는 것이 왜 정직한가요?

<details>
<summary>정답과 설명</summary>

존재하지 않는 견적을 가짜 기본 객체로 만들면 “실제 선택됨”과 구분하기 어렵습니다. `SupplierQuote?`는 선택이 없을 수 있다는 계약을 형식에 드러내고 호출자에게 명시적 확인을 요구합니다.

</details>

### Q24. report에서 변경 가능한 `List<T>`를 그대로 노출하면 어떤 문제가 생기나요?

<details>
<summary>정답과 설명</summary>

저장 뒤 외부 코드가 항목을 추가·삭제해 성공 수, 실패 수, 선택 근거가 서로 모순될 수 있습니다. 생성 시 snapshot을 만들고 read-only/immutable collection으로 노출하면 관찰 시점마다 같은 report를 볼 수 있습니다.

</details>

---

## 4단계 — 아키텍처와 SOLID

### Q25. `CollectSupplierQuotesService`의 핵심 책임은 무엇인가요?

<details>
<summary>정답과 설명</summary>

Gateway fan-out 시작, 완료 순서 소비, 성공/실패 집계, 선택 Strategy 호출, report 저장이라는 **유스케이스 순서**를 조율하는 것입니다. 실제 공급업체 통신, 가격 선택 세부 규칙, 저장 기술은 Port 뒤의 구현에 맡깁니다.

</details>

### Q26. Gateway를 Port interface로 둔 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

Application이 HTTP SDK나 특정 공급업체 구현에 직접 의존하지 않게 하기 위해서입니다. demo에서는 scripted Adapter를, test에서는 제어 가능한 fake를, 운영에서는 HTTP Adapter를 같은 계약 뒤에 연결할 수 있습니다. 이는 DIP와 테스트 용이성을 돕습니다.

</details>

### Q27. `IQuoteSelectionPolicy`는 어떤 패턴을 보여 주나요?

<details>
<summary>정답과 설명</summary>

교체 가능한 알고리즘을 interface 뒤에 두는 Strategy 패턴입니다. 최저 총액, 최단 납기 같은 정책을 Application Service의 비동기 수집 흐름과 분리하므로 새 정책을 추가해도 service 분기를 늘리지 않을 수 있습니다.

</details>

### Q28. Repository Port와 메모리 Adapter를 분리한 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

Application은 “report를 저장한다”는 업무상 필요만 알고, `List`, SQL, cloud database 같은 기술 선택을 몰라도 됩니다. 메모리 Adapter는 학습과 테스트에 빠르고, 운영 Adapter로 교체할 때 유스케이스를 덜 수정합니다.

</details>

### Q29. Composition Root는 어디이며 무슨 일을 하나요?

<details>
<summary>정답과 설명</summary>

이 예제에서는 [`Program.cs`](./src/SupplierQuoteExercise/Program.cs)가 Composition Root입니다. 구체 Gateway, 선택 Strategy, Repository Adapter를 생성하고 생성자 주입으로 Application Service에 연결합니다. 객체 조립을 한곳에 모아 안쪽 계층이 구체 구현을 직접 만들지 않게 합니다.

</details>

### Q30. DI는 반드시 framework container를 써야 하나요?

<details>
<summary>정답과 설명</summary>

아닙니다. 필요한 협력자를 class 안에서 `new`하지 않고 바깥에서 전달하는 원칙 자체가 핵심입니다. 작은 console 예제의 수동 생성자 주입도 명확한 DI이며, 객체 graph가 커질 때 container를 선택할 수 있습니다.

</details>

### Q31. Strategy를 바꿀 때 Service를 수정하지 않는 것이 어떤 SOLID 원칙과 연결되나요?

<details>
<summary>정답과 설명</summary>

확장에는 열려 있고 기존 코드 수정에는 닫혀 있도록 하는 OCP, 그리고 고수준 정책이 구체 구현 대신 추상화에 의존하는 DIP와 연결됩니다. 단, interface를 만드는 것 자체보다 실제 변화 축을 올바르게 분리했는지가 중요합니다.

</details>

---

## 5단계 — 테스트와 운영 경계

### Q32. 비동기 완료 순서 test를 고정 millisecond 지연만으로 만들면 왜 불안정한가요?

<details>
<summary>정답과 설명</summary>

CI 부하, timer 해상도, scheduler 차이로 예상 순서가 흔들릴 수 있습니다. `TaskCompletionSource` 같은 제어 가능한 신호로 “모두 시작됨”과 “B를 지금 완료”를 직접 조정하면 시간 추측 없이 계약을 검증할 수 있습니다.

</details>

### Q33. 취소 test에서 Repository 저장 횟수도 확인해야 하는 이유는 무엇인가요?

<details>
<summary>정답과 설명</summary>

취소 예외만 확인하면 service가 먼저 불완전한 report를 저장한 뒤 예외를 던지는 bug를 놓칠 수 있습니다. 반환/예외뿐 아니라 중요한 side effect가 일어나지 않았다는 것도 함께 검증해야 합니다.

</details>

### Q34. 수천 개 Gateway Task를 한꺼번에 시작할 때 어떤 위험이 있나요?

<details>
<summary>정답과 설명</summary>

socket, 연결 pool, memory, 상대 공급업체의 rate limit을 동시에 압박할 수 있습니다. `Task.WhenEach`는 완료 순서를 제공하지만 동시성 제한을 자동으로 제공하지 않으므로 `SemaphoreSlim`, worker pool, batch 같은 별도 bulkhead가 필요합니다.

</details>

### Q35. 단순 batch와 sliding bounded concurrency는 어떤 차이가 있나요?

<details>
<summary>정답과 설명</summary>

batch는 묶음 안의 가장 느린 작업이 끝날 때까지 다음 묶음을 시작하지 못할 수 있습니다. sliding 방식은 한 작업이 끝나는 즉시 다음 작업을 시작해 제한 수를 유지할 수 있지만 구현과 취소·정리 계약이 더 복잡합니다.

</details>

### Q36. callback 소비자가 느리거나 예외를 던지면 어떤 정책이 필요한가요?

<details>
<summary>정답과 설명</summary>

현재 thread에서 동기 호출하면 느린 callback이 다음 완료 결과 처리를 지연시키고, 예외는 집계 자체를 중단할 수 있습니다. 운영 설계에서는 callback 예외를 전파할지 격리할지, buffer/backpressure가 필요한지 계약으로 정해야 합니다. 임의로 예외를 삼키면 관찰 문제를 숨길 수 있습니다.

</details>

### Q37. 메모리 Repository가 보장하지 않는 것은 무엇인가요?

<details>
<summary>정답과 설명</summary>

process 재시작 뒤 내구성, 여러 instance 사이 일관성, transaction, 인증·권한을 보장하지 않습니다. 이 예제의 Adapter는 패턴과 test를 배우기 위한 process-local 구현이며 운영 DB의 대체물이 아닙니다.

</details>

### Q38. 같은 견적 집계를 재시도할 때 고려할 운영 설계는 무엇인가요?

<details>
<summary>정답과 설명</summary>

요청 ID, 중복 저장 방지, 공급업체 호출의 멱등성, timeout과 retry budget, 이미 받은 부분 결과의 재사용 여부를 정해야 합니다. 무조건 retry하면 공급업체 부하와 중복 side effect를 키울 수 있습니다.

</details>

### Q39. 가장 저렴한 견적이 여러 개면 tie-breaker가 왜 필요한가요?

<details>
<summary>정답과 설명</summary>

완료 순서처럼 비결정적인 값에 선택이 좌우되지 않게 하기 위해서입니다. 납기일과 공급업체 ID 같은 추가 정렬 기준을 명시하면 입력 열거 순서가 달라져도 같은 결과를 얻고 테스트도 안정적입니다.

</details>

### Q40. 이 예제의 핵심 문장을 한 문장으로 요약하면 무엇인가요?

<details>
<summary>정답과 설명</summary>

“모든 독립 Task를 먼저 시작하고 `Task.WhenEach`로 끝난 순서대로 관찰하되, 업무 실패는 값으로 보존하고 취소·결함은 숨기지 않으며, 최종 업무 선택은 교체 가능한 결정적 Strategy에 맡긴다.”입니다.

</details>

---

## ✅ 통과 기준

- [ ] Q1~Q8: 기본 문법을 자신의 말로 설명한다.
- [ ] Q9~Q17: Task 시작 순서, 완료 순서, 소비 순서를 구분한다.
- [ ] Q18~Q24: Result·예외·취소·nullable·불변성을 구분한다.
- [ ] Q25~Q31: Application Service, Port, Adapter, Strategy, DI의 의존 방향을 그릴 수 있다.
- [ ] Q32~Q39: 결정적 테스트와 bounded concurrency 등 운영 한계를 설명한다.
- [ ] Q40의 요약을 실제 [`CollectSupplierQuotesService.cs`](./src/SupplierQuoteExercise/Application/CollectSupplierQuotesService.cs)의 코드 줄과 연결해 설명한다.
