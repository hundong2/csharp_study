# 2026-10-23 이해도 점검 — `LeftJoin`과 주문-결제 대사

먼저 답을 가리고 말이나 글로 설명하세요. API 이름을 외우는 것보다 **어떤 데이터를 잃지 않고, 어떤 잘못된 상태를 막는가**를 설명하는 것이 목표입니다.

---

## 1. 금액 계산에 `double`보다 `decimal`을 선택한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`double`은 이진 부동소수점이어서 `0.1` 같은 10진 소수를 정확히 표현하지 못하는 경우가 있습니다. `decimal`은 금융·회계처럼 10진수 정확성이 중요한 계산에 적합합니다. 다만 `decimal`을 쓴다고 반올림·통화·세금 규칙이 자동으로 정해지는 것은 아닙니다.

</details>

## 2. 금액 숫자와 통화를 `Money` 하나로 묶은 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`100 KRW`와 `100 USD`는 숫자가 같아도 같은 가치라고 단정할 수 없습니다. 값 객체로 묶으면 통화를 빠뜨린 숫자만 전달하기 어려워지고, 생성 시 음수·소수 자리·통화 형식을 한곳에서 검증할 수 있습니다.

</details>

## 3. `record`라고 선언하면 객체가 자동으로 완전히 불변인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. record도 변경 가능한 property나 가변 collection을 가질 수 있습니다. 이 예제는 private 생성자, get-only property, `ImmutableArray` snapshot을 함께 사용해 변경 가능성을 줄였습니다. 불변성은 keyword 하나가 아니라 전체 객체 그래프의 설계입니다.

</details>

## 4. `Payment?`에서 `?`는 무엇을 뜻하나요?

<details>
<summary>정답과 해설</summary>

참조 형식 `Payment` 값뿐 아니라 `null`, 즉 연결된 결제가 없을 수도 있음을 nullable 분석에 알립니다. compiler는 null 확인 없이 멤버에 접근하는 실수를 경고합니다.

</details>

## 5. `?`, `?.`, `??`, `!`의 차이를 설명해 보세요.

<details>
<summary>정답과 해설</summary>

- 형식 뒤 `?`는 값이 null일 수 있음을 표시합니다.
- `?.`는 왼쪽이 null이면 멤버 접근을 실행하지 않고 null을 만듭니다.
- `??`는 왼쪽이 null일 때 오른쪽 대체값을 선택합니다.
- postfix `!`는 compiler의 nullable 경고를 억제할 뿐 runtime null 검사를 하지 않습니다.

따라서 `!`를 붙였다고 값이 안전해지는 것은 아닙니다. `Result.Value`에서는 성공/실패 불변식이 이미 확인된 내부 필드라는 근거로 제한해 사용합니다.

</details>

## 6. `var`는 runtime에 형식이 정해지는 dynamic 변수인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. compiler가 오른쪽 식으로 정확한 정적 형식을 compile-time에 결정합니다. `var amount = 1m`의 `amount`는 `decimal`이며 이후 문자열을 넣을 수 없습니다.

</details>

## 7. `[order1, order2]`는 어떤 문법인가요?

<details>
<summary>정답과 해설</summary>

C# collection expression입니다. 왼쪽 목표 형식에 맞는 배열이나 collection을 간결하게 만듭니다. 예제의 `IReadOnlyList<Order>` 변수와 `List<string>` 필드에서 사용합니다.

</details>

## 8. `ReconciliationStatus`를 임의 문자열 대신 `enum`으로 만든 장점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

가능한 상태 이름을 compile-time에 제한하고 오타를 줄입니다. switch와 pattern matching에서도 compiler 도움을 받을 수 있습니다. 외부 저장·전송 형식이 바뀔 수 있다면 enum 이름을 그대로 영구 계약으로 쓰기보다 명시적 mapping을 두는 것도 검토해야 합니다.

</details>

## 9. `order => order.OrderId`에서 lambda는 어떤 역할을 하나요?

<details>
<summary>정답과 해설</summary>

Order 하나를 받아 join key 문자열 하나를 반환하는 이름 없는 함수입니다. LINQ에 “어느 property로 연결할지”라는 동작을 값처럼 전달합니다. `=>`는 여기서는 lambda의 입력과 결과를 구분합니다.

</details>

## 10. `LeftJoin`이 보장하는 핵심은 무엇인가요?

<details>
<summary>정답과 해설</summary>

왼쪽 sequence의 모든 원소가 결과에 최소 한 번 나타나도록 보존합니다. 이 예제에서는 결제가 없는 주문도 보고서에서 사라지지 않습니다.

</details>

## 11. `LeftJoin`은 왼쪽 원소를 항상 정확히 한 번만 만들까요?

<details>
<summary>정답과 해설</summary>

아닙니다. 오른쪽에 같은 key가 여러 개 있으면 일치 수만큼 결과가 늘어납니다. 이 예제는 주문별 결제 최대 한 건이라는 계약을 적용해 join 전에 복수 결제를 거절합니다. 부분 결제를 허용한다면 group과 합산 규칙을 새로 설계해야 합니다.

</details>

## 12. 연결된 결제가 없을 때 result selector의 `payment`에는 무엇이 오나요?

<details>
<summary>정답과 해설</summary>

오른쪽 원소 형식의 기본값이 오며, `Payment`는 참조 형식이므로 `null`입니다. `IReconciliationPolicy.Compare`의 `Payment?`가 이 가능성을 계약에 드러냅니다.

</details>

## 13. inner `Join`을 사용하면 `ORD-101`은 어떻게 되나요?

<details>
<summary>정답과 해설</summary>

양쪽에 같은 key가 없으므로 결과에서 완전히 사라집니다. 결제 누락은 찾아야 할 문제인데 join이 조용히 감추므로 이 use case에는 부적합합니다.

</details>

## 14. `LeftJoin`만으로 `PAY-999` 고아 결제를 찾을 수 없는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

LeftJoin은 왼쪽 주문을 보존하지만 오른쪽 결제만 존재하는 경우는 결과에 넣지 않습니다. 주문 ID `HashSet`에 포함되지 않은 결제를 `Where`로 찾는 별도 anti-join이 필요합니다.

</details>

## 15. 중복 `PaymentId`와 한 주문에 결제 두 건은 같은 오류인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. 중복 `PaymentId`는 결제 식별자 유일성 위반이고, 서로 다른 결제 ID가 같은 주문을 가리키는 것은 관계 cardinality 위반입니다. 원인과 해결 방법이 다르므로 `data.duplicate_payment_id`와 `data.multiple_payments_for_order`로 구분합니다.

</details>

## 16. 식별자 비교에 `StringComparer.Ordinal`을 명시한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

사용자 문화권의 언어 규칙과 무관하게 code point 기반으로 대소문자를 구분해 비교한다는 계약을 명확히 합니다. `ORD-1`과 `ord-1`은 다른 ID입니다. join, 중복 검사, HashSet, 정렬, 상태별 factory의 OrderId 검증에서 같은 정책을 사용해야 모순이 없습니다.

</details>

## 17. `Difference`가 `actual - expected`이고 값이 양수라면 무엇을 뜻하나요?

<details>
<summary>정답과 해설</summary>

실제 결제가 주문 기대 금액보다 크므로 과다 결제입니다. 음수면 부족 결제입니다. 부호 정의를 문서와 테스트에 고정하지 않으면 팀마다 반대로 해석할 수 있습니다.

</details>

## 18. 통화가 다를 때 `Difference`를 `0`이 아니라 `null`로 둔 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

환율과 기준 시각 규칙 없이 USD와 EUR 숫자를 바로 빼는 것은 의미가 없습니다. `0`은 차이가 없다는 잘못된 의미를 주지만 null은 현재 규칙으로 계산할 수 없음을 정직하게 표현합니다.

</details>

## 19. LINQ query는 선언한 줄에서 곧바로 모두 실행되나요?

<details>
<summary>정답과 해설</summary>

많은 LINQ 연산은 지연 실행됩니다. sequence를 열거하거나 `ToArray`, `ToImmutableArray` 같은 materialization 연산을 호출할 때 실제로 동작합니다. 그 전까지 원본이 바뀌면 결과도 달라질 수 있고, 여러 번 열거하면 연산도 반복될 수 있습니다.

</details>

## 20. `ReconciliationReport.Create`가 정렬 후 `ToImmutableArray`를 호출하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

어느 호출 경로에서도 OrderId·PaymentId 순서라는 불변식을 보장하고, 원본 collection의 이후 변경과 분리된 snapshot을 만들기 위해서입니다. public factory가 직접 불변식을 지켜야 service 밖에서 호출해도 계약이 깨지지 않습니다.

</details>

## 21. `async` method를 호출하면 두 Repository가 자동으로 병렬 실행되나요?

<details>
<summary>정답과 해설</summary>

아닙니다. `async/await`는 비동기 대기를 표현하지만 자동 병렬화를 뜻하지 않습니다. 현재 service는 주문 조회를 await한 다음 결제 조회를 await하므로 순차 실행입니다. 병렬 실행이 정말 안전하고 이득인지 저장소·transaction·부하를 검토한 뒤 명시적으로 설계해야 합니다.

</details>

## 22. Repository 조회 뒤에도 `CancellationToken.ThrowIfCancellationRequested()`를 다시 호출하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

두 번째 Repository가 값을 반환하는 순간 취소될 수 있고, 이후 GroupBy·LeftJoin·정렬은 CPU 작업이라 token을 자동 관찰하지 않습니다. 단계 사이 checkpoint가 없으면 호출자가 취소했는데도 성공 보고서를 만들 수 있습니다.

</details>

## 23. 예상 가능한 입력 오류에 Result를 사용한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

음수 금액이나 중복 ID는 호출자가 정상 분기로 확인하고 고칠 수 있는 결과입니다. Result는 성공 또는 오류를 return type에 드러내 처리 누락을 줄입니다. 모든 예외를 Result로 바꾸는 도구는 아닙니다.

</details>

## 24. 실패 Result의 `Value`를 읽을 때 예외가 발생하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

원래 입력 실패가 아니라 `IsSuccess`를 확인하지 않은 프로그래머가 Result API 계약을 어긴 상황이기 때문입니다. 이런 잘못은 조용한 기본값보다 즉시 예외로 드러내는 편이 안전합니다.

</details>

## 25. 취소를 실패 Result로 바꾸지 않는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

취소는 주문·결제 업무 규칙이 실패한 것이 아니라 호출자가 작업 중단을 요청한 제어 흐름입니다. `OperationCanceledException`을 유지해야 상위 host나 job runner가 취소와 오류를 정확히 구분할 수 있습니다.

</details>

## 26. Repository Port가 주는 핵심 이점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

Application Service가 메모리, SQL, 파일, HTTP 같은 저장 기술을 직접 알지 않게 합니다. 생성자에 test double이나 다른 Adapter를 넣을 수 있어 저장 기술 교체와 단위 검사가 쉬워집니다.

</details>

## 27. Strategy가 주는 핵심 이점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

데이터 조회·join·보고서 조율 흐름과 금액 판정 정책을 분리합니다. `ReconciliationLine.ForPolicyMatch`도 정확한 금액 일치를 중복 강제하지 않으므로, 예를 들어 1원 허용 오차 정책을 새 구현으로 추가해 service와 Domain factory를 수정하지 않고 교체·검사할 수 있습니다.

</details>

## 28. DI container가 없는데도 이 코드를 의존성 주입이라고 부를 수 있나요?

<details>
<summary>정답과 해설</summary>

그렇습니다. 핵심은 객체가 필요한 의존성을 내부에서 직접 만들지 않고 생성자를 통해 밖에서 받는 것입니다. `Program`이 수동으로 Repository와 Strategy를 만들어 service에 넣습니다. container는 이 조립을 자동화하는 선택 도구이지 DI 자체와 같은 뜻이 아닙니다.

</details>

## 29. Composition Root를 `Program` 한곳에 둔 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

어떤 interface에 어떤 구체 구현을 연결하는지 한곳에서 보이게 하기 위해서입니다. Domain과 Application이 `new InMemory...`를 직접 호출하지 않으므로 구체 기술 의존이 안쪽으로 새지 않습니다.

</details>

## 30. 주문이 수천만 건이면 현재 `ListAsync`와 LINQ-to-Objects 설계를 그대로 써도 되나요?

<details>
<summary>정답과 해설</summary>

보통 적합하지 않습니다. 모든 데이터를 메모리에 올리기 때문입니다. 날짜·tenant 범위를 제한하고, DB가 join/filter를 수행하게 하거나 page/streaming/batch query를 사용하며, index·unique constraint·재시작 기준점·메모리 상한을 함께 설계해야 합니다.

</details>
