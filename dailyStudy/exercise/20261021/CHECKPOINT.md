# 2026-10-21 이해도 체크포인트

먼저 답을 말하거나 적은 뒤 `<details>`를 펼치세요. 외우기보다 코드에서 근거가 있는지 확인합니다.

---

## 1. C# 기초와 비동기

### Q1. `var`는 runtime에 형식이 바뀌는 dynamic 변수인가요?

<details>
<summary>정답 보기</summary>

아닙니다. compiler가 오른쪽 식으로 정적 형식을 한 번 결정합니다. `var result = RentalWindow.Create(...)`의 형식은 compile time에 `Result<RentalWindow>`로 고정됩니다.

</details>

### Q2. `string?`는 왜 HTTP DTO에 필요하나요?

<details>
<summary>정답 보기</summary>

외부 JSON은 property를 누락하거나 명시적으로 null을 보낼 수 있습니다. nullable annotation은 그 가능성을 type에 드러내고, Domain 경계에서 업무 값으로 바뀌기 전 반드시 검사하게 돕습니다.

</details>

### Q3. `!`를 붙이면 runtime null 검사가 생기나요?

<details>
<summary>정답 보기</summary>

아닙니다. compiler warning만 억제합니다. `IsSuccess`를 먼저 확인했다는 논리적 근거가 있을 때만 `Value!` 또는 `Error!`를 사용해야 합니다.

</details>

### Q4. `async` test가 `Task`를 반환해야 하는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

runner가 Task 완료와 그 안의 예외를 기다려야 하기 때문입니다. `async void`는 완료 시점과 예외를 test runner가 안정적으로 관찰하기 어렵습니다.

</details>

### Q5. `with` 식은 기존 record를 수정하나요?

<details>
<summary>정답 보기</summary>

아닙니다. 지정한 값만 다른 새 복사본을 만듭니다. 공유 입력의 뜻밖의 변경을 줄여 테스트 격리에 유리합니다.

</details>

### Q6. `ValueTask<T>`를 사용했다고 작업이 자동으로 빨라지나요?

<details>
<summary>정답 보기</summary>

아닙니다. 자주 동기 완료되는 경로에서 Task allocation을 줄일 수 있는 선택일 뿐이며 잘못 사용하면 복잡성이 늘어납니다. 오늘 메모리 Repository는 즉시 완료되지만 실제 DB Adapter는 보통 Task 기반 I/O가 더 자연스러울 수 있습니다.

</details>

---

## 2. 테스트 기초

### Q7. `[TestClass]`와 `[TestMethod]`의 역할은 무엇인가요?

<details>
<summary>정답 보기</summary>

MSTest가 test container class와 실행할 test method를 발견하게 하는 attribute입니다. 일반 helper method는 `[TestMethod]`가 없으므로 독립 test로 실행되지 않습니다.

</details>

### Q8. Arrange–Act–Assert에서 Act가 여러 개인 것은 항상 잘못인가요?

<details>
<summary>정답 보기</summary>

항상은 아닙니다. 충돌 test처럼 첫 예약으로 상태를 만든 뒤 두 번째 행동을 검증할 수 있습니다. 다만 무엇이 검증 대상이고 무엇이 사전조건 생성인지 구분되어야 하며, 실패 원인이 모호하면 test를 나눕니다.

</details>

### Q9. assertion을 많이 넣을수록 좋은 test인가요?

<details>
<summary>정답 보기</summary>

아닙니다. 하나의 행동에서 같은 계약을 설명하는 여러 assertion은 괜찮지만, 무관한 규칙을 한 test에 모으면 실패 원인이 흐려지고 유지보수 비용이 커집니다.

</details>

### Q10. `[DataRow]`가 유용한 경우는 언제인가요?

<details>
<summary>정답 보기</summary>

동일한 행동과 assertion을 여러 경계 입력에 반복할 때입니다. 종료가 시작과 같은 경우와 더 이른 경우처럼 입력만 달라지는 계약에 적합합니다.

</details>

### Q11. test method 이름에 조건과 기대를 넣는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

CI 실패 목록만 보아도 어떤 조건에서 어떤 계약이 깨졌는지 빠르게 알 수 있습니다. `Target_WithCondition_ReturnsOutcome`은 한 가지 읽기 쉬운 방식입니다.

</details>

### Q12. private method를 직접 test해야 하나요?

<details>
<summary>정답 보기</summary>

보통은 공개 행동을 통해 검증합니다. private 구현을 직접 고정하면 안전한 refactoring까지 test가 막습니다. private 로직이 독립된 중요한 개념이면 별도 Domain type으로 추출할 신호일 수 있습니다.

</details>

---

## 3. 테스트 피라미드와 경계

### Q13. `RentalWindow.Overlaps`는 왜 HTTP test보다 unit test가 적합한가요?

<details>
<summary>정답 보기</summary>

외부 자원 없이 입력과 출력이 결정되는 작은 규칙이기 때문입니다. unit test가 더 빠르고 실패 원인이 정확하며 많은 경계를 값싸게 확인할 수 있습니다.

</details>

### Q14. `Location` header는 왜 integration test에서 검증하나요?

<details>
<summary>정답 보기</summary>

Endpoint mapping과 HTTP result serialization이 함께 만들어 내는 공개 계약입니다. Domain test는 route와 header를 실행하지 않으므로 이 결함을 잡을 수 없습니다.

</details>

### Q15. WebApplicationFactory는 실제 Kestrel port를 여나요?

<details>
<summary>정답 보기</summary>

기본적으로 TestServer의 메모리 transport를 사용합니다. routing, middleware, JSON, DI는 실행하지만 OS socket, TLS, reverse proxy 동작은 별도 검증이 필요합니다.

</details>

### Q16. 통합 테스트만 많이 쓰면 충분하지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

실행이 느리고 준비가 복잡하며 실패 원인이 넓습니다. 작은 규칙까지 HTTP로만 확인하면 경계 조합 수를 충분히 검사하기 어렵습니다. 필요한 통합 신뢰와 빠른 unit feedback을 함께 사용합니다.

</details>

### Q17. 반대로 unit test만으로 충분하지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

DI 등록 누락, route 오타, JSON property, middleware 순서, HTTP 상태·header 같은 조립 계약은 개별 class test로 실행되지 않기 때문입니다.

</details>

### Q18. test pyramid는 정확한 test 개수 비율인가요?

<details>
<summary>정답 보기</summary>

아닙니다. 빠르고 좁은 test를 기반으로 하고 비싸고 넓은 test를 필요한 만큼 두라는 휴리스틱입니다. 시스템 위험과 도구 비용에 따라 모양은 달라집니다.

</details>

---

## 4. Test Double, 시간, 격리

### Q19. `FixedTimeProvider`가 없으면 어떤 flaky test가 생길 수 있나요?

<details>
<summary>정답 보기</summary>

현재 시각과 시작 시각의 경계가 test 실행 순간에 따라 달라질 수 있습니다. 자정, 느린 CI, 시간대 설정, 실행 지연 때문에 같은 입력이 성공과 실패를 오갈 수 있습니다.

</details>

### Q20. integration test에서 모든 서비스를 fake로 바꾸지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

Production wiring과 실제 정책 협력을 검증하기 위해서입니다. 시간과 저장 상태처럼 비결정적·외부적인 경계만 교체하고 핵심 코드는 그대로 실행합니다.

</details>

### Q21. 각 HTTP test가 새 Factory를 만드는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

Singleton 메모리 Repository 상태를 test마다 격리하기 위해서입니다. 다른 test가 남긴 예약에 영향을 받지 않아 순서 독립성이 생깁니다.

</details>

### Q22. mock과 fake의 차이를 오늘 예제로 설명해 보세요.

<details>
<summary>정답 보기</summary>

`FixedTimeProvider`는 정해진 값을 제공하는 stub 성격이고, `InMemoryEquipmentRentalRepository`는 실제 단순 구현으로 동작하는 fake입니다. mock은 보통 기대 호출을 미리 정의하고 상호작용을 검증하는 객체를 뜻하지만 용어는 팀마다 조금 다를 수 있어 역할을 구체적으로 말하는 편이 좋습니다.

</details>

### Q23. TestData helper가 너무 커지면 어떤 문제가 생기나요?

<details>
<summary>정답 보기</summary>

각 test의 중요한 입력이 숨고 helper 기본값 변경이 많은 test 의미를 동시에 바꿀 수 있습니다. 공통의 중요하지 않은 값만 제공하고, 검증 대상 차이는 test 본문에 드러냅니다.

</details>

---

## 5. Domain과 아키텍처

### Q24. `[start, end)` 반열린 구간을 쓰는 장점은 무엇인가요?

<details>
<summary>정답 보기</summary>

첫 예약이 11시에 끝나고 다음 예약이 11시에 시작할 수 있습니다. 경계가 맞닿았을 뿐 겹치지 않는다는 규칙을 단순한 두 비교식으로 표현할 수 있습니다.

</details>

### Q25. `IRentalPolicy`가 Strategy인 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

같은 검증 역할을 분류별 알고리즘으로 바꾸어 끼울 수 있기 때문입니다. Application Service는 구체 기간 숫자 대신 interface와 category key만 압니다.

</details>

### Q26. Repository Port가 테스트 가능성을 어떻게 높이나요?

<details>
<summary>정답 보기</summary>

Application이 DB client나 Dictionary에 직접 묶이지 않습니다. 운영 Adapter, 메모리 fake, 실패 double을 같은 계약으로 교체해 업무 흐름을 각기 검증할 수 있습니다.

</details>

### Q27. 예상 충돌을 예외 대신 Result로 표현한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

장비 시간 충돌은 호출자가 다른 시간이나 장비로 고칠 수 있는 정상적인 업무 결과입니다. Result는 이를 type과 코드로 드러내고 exception monitoring의 잡음을 줄입니다.

</details>

### Q28. 취소를 `Result.Failure`로 바꾸면 왜 안 되나요?

<details>
<summary>정답 보기</summary>

취소는 업무 규칙 위반이 아니라 더 이상 결과가 필요 없다는 제어 흐름입니다. 오류로 바꾸면 불필요한 retry, 잘못된 4xx 기록, 취소 전파 중단이 생길 수 있습니다.

</details>

### Q29. Composition Root는 왜 구체 구현을 알아도 되나요?

<details>
<summary>정답 보기</summary>

추상 Port와 구체 Adapter를 최종 조립하는 것이 책임이기 때문입니다. 그 지식을 Program 한곳에 모아 Domain/Application으로 새지 않게 합니다.

</details>

---

## 6. 동시성과 운영 한계

### Q30. 겹침 확인과 저장을 두 lock으로 나누면 어떤 문제가 생기나요?

<details>
<summary>정답 보기</summary>

두 요청이 모두 “겹침 없음”을 본 뒤 각각 저장할 수 있습니다. 확인과 상태 변경이 하나의 원자적 임계 구역이어야 합니다.

</details>

### Q31. 24개 Task test가 모든 race condition 부재를 증명하나요?

<details>
<summary>정답 보기</summary>

아닙니다. 대표 경쟁 회귀를 반복 가능하게 찾는 검증입니다. 메모리 모델과 모든 interleaving에 대한 형식 증명은 아니며 stress, model checking, code review가 추가될 수 있습니다.

</details>

### Q32. 여러 API process에서 `lock`이 부족한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

각 process가 서로 다른 memory와 lock을 갖기 때문입니다. 공통 DB의 transaction/constraint 또는 분산 조정 메커니즘이 필요합니다.

</details>

### Q33. DB에서 먼저 SELECT하고 INSERT하면 안전한가요?

<details>
<summary>정답 보기</summary>

그 두 단계만으로는 안전하지 않습니다. 별도 transaction과 적절한 isolation/constraint가 없으면 두 transaction이 모두 없음으로 읽고 저장할 수 있습니다.

</details>

### Q34. 실패 test에 무조건 retry를 붙이면 왜 위험한가요?

<details>
<summary>정답 보기</summary>

공유 상태, 실제 시간, thread race, network 의존 같은 원인을 숨겨 신뢰를 낮춥니다. 먼저 결정적 입력과 진단 증거를 확보하고 정말 허용된 일시 오류만 제한적으로 재시도합니다.

</details>

---

## 최종 자기 점검

- [ ] MSTest framework와 MTP runner를 구분한다.
- [ ] AAA와 데이터 기반 test를 설명한다.
- [ ] unit·Application·integration 경계를 고를 수 있다.
- [ ] TimeProvider와 DI override의 이유를 설명한다.
- [ ] 테스트 상태 격리와 순서 독립성을 설명한다.
- [ ] Result·예외·취소를 구분한다.
- [ ] 반열린 시간 구간과 원자적 저장을 설명한다.
- [ ] TestServer와 실제 Kestrel 검증의 차이를 안다.
- [ ] 여러 process에서는 DB 수준 동시성 제어가 필요함을 안다.
- [ ] 전체 17개 test와 Release build를 직접 통과시켰다.
