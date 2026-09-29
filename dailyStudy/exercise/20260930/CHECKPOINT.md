# 2026-09-30 이해도 체크 — 중앙 오류 처리

먼저 답을 말하거나 적은 뒤 펼쳐 보세요. 단어 암기보다 “왜 이 경계인가?”를 설명하는 것이 목표입니다.

---

## 1. 수량 0은 왜 예외 대신 Result인가요?

<details>
<summary>정답 보기</summary>

수량 0은 서버가 망가진 상황이 아니라 외부 사용자가 충분히 만들 수 있고 고칠 수 있는 입력 실패입니다. 호출자가 정상적인 분기처럼 처리할 수 있으므로 `Result<T>`가 의도를 드러냅니다. 예외로 만들면 흔한 400 요청마다 stack trace와 error log가 생겨 실제 장애 신호가 흐려질 수 있습니다.

</details>

## 2. 카탈로그 일시 장애는 왜 400이 아닌 503인가요?

<details>
<summary>정답 보기</summary>

요청 JSON을 고쳐도 외부 시스템 장애는 해결되지 않습니다. 현재 서버가 의존 서비스 때문에 일시적으로 처리할 수 없다는 뜻이므로 503이 맞습니다. 예제는 `Retry-After: 5`도 보내 재시도 시점을 안내합니다.

</details>

## 3. `IExceptionHandler`와 `UseExceptionHandler()`는 같은 것인가요?

<details>
<summary>정답 보기</summary>

아닙니다. `IExceptionHandler`는 “예외 하나를 어떻게 분류하고 응답할지” 구현하는 서비스 계약입니다. `UseExceptionHandler()`는 요청 pipeline에 예외 처리 middleware를 넣어 아래 단계에서 올라오는 예외를 잡게 합니다. 서비스만 등록하고 middleware를 넣지 않으면 요청 예외 경계가 동작하지 않습니다.

</details>

## 4. middleware를 endpoint 뒤에 두면 왜 문제인가요?

<details>
<summary>정답 보기</summary>

middleware는 등록 순서대로 요청을 내려 보내고 역순으로 응답을 올립니다. 예외 처리 middleware가 endpoint보다 앞에서 감싸야 endpoint 실행 중 예외를 볼 수 있습니다. endpoint가 이미 요청을 끝내면 뒤의 middleware는 그 실행을 감쌀 기회가 없습니다.

</details>

## 5. `AddProblemDetails`와 `UseStatusCodePages`의 역할 차이는 무엇인가요?

<details>
<summary>정답 보기</summary>

`AddProblemDetails`는 Problem Details를 작성할 서비스와 writer를 DI에 등록합니다. `UseStatusCodePages`는 404처럼 상태 코드는 있지만 본문이 비어 있는 응답을 발견해 그 서비스를 사용하도록 요청 pipeline에 동작을 추가합니다. 하나는 서비스 등록, 다른 하나는 middleware 동작입니다.

</details>

## 6. `code`가 있는데 `title`과 `detail`도 필요한가요?

<details>
<summary>정답 보기</summary>

소비자가 다릅니다. 프로그램은 변하지 않는 `code`로 분기하고, 사람은 `title`과 `detail`로 상황과 다음 행동을 이해합니다. 문구는 번역·개선될 수 있으므로 클라이언트 로직이 문자열을 비교하면 깨지기 쉽습니다.

</details>

## 7. 500 응답에 원래 예외 message를 넣으면 왜 위험한가요?

<details>
<summary>정답 보기</summary>

예외 message에는 SQL, 파일 경로, 호스트 이름, 내부 형식, 때로는 개인정보나 credential 일부가 들어갈 수 있습니다. 공격자에게 내부 구조를 알려 주기도 합니다. 원래 예외는 접근 통제된 server log에 남기고, HTTP에는 허용한 일반 문구와 `traceId`만 보냅니다.

</details>

## 8. `traceId`는 무엇을 해결하나요?

<details>
<summary>정답 보기</summary>

사용자가 받은 안전한 오류 응답과 운영자의 상세 로그·분산 trace를 연결합니다. 응답에 stack trace를 노출하지 않아도 운영자는 `traceId`로 같은 요청을 찾아 원인을 조사할 수 있습니다. 다만 로그 시스템 접근 권한은 별도로 보호해야 합니다.

</details>

## 9. 요청 취소를 500으로 만들면 무엇이 잘못되나요?

<details>
<summary>정답 보기</summary>

브라우저 닫기나 상위 timeout은 서버 코드 버그가 아닙니다. 이를 500과 error log로 기록하면 실패율과 alert가 부풀고 실제 장애를 가립니다. 예제는 `RequestAborted`가 취소된 `OperationCanceledException`에 `false`를 반환해 server failure 응답으로 바꾸지 않습니다.

</details>

## 10. `OrderHttpMapper`가 모르는 오류 코드를 400으로 기본 처리하지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

새 오류의 의미와 상태를 설계하지 않은 누락을 조용히 숨기지 않기 위해서입니다. 무조건 400으로 바꾸면 서버 버그나 외부 장애도 사용자 입력 탓처럼 보일 수 있습니다. 예제는 누락을 예외로 드러내 중앙 500 경계에서 안전하게 처리하고 로그로 발견합니다.

</details>

## 11. Repository Port가 있는데도 DB unique constraint가 필요한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

두 요청이 동시에 “없음”을 보고 모두 insert할 수 있기 때문입니다. 프로세스 여러 대에서는 메모리 lock도 공유되지 않습니다. 최종 저장소의 unique constraint와 transaction이 원자적 진실을 보장해야 하고, Repository Adapter는 그 특정 위반만 `order.duplicate` 의미로 번역해야 합니다.

</details>

## 12. Strategy를 interface로 분리하면 어떤 테스트가 쉬워지나요?

<details>
<summary>정답 보기</summary>

배송비 경계값은 `ThresholdShippingFeePolicy`만 빠르게 단위 검증할 수 있고, Application Service에는 고정값을 돌려주는 fake Strategy를 넣어 조율만 검사할 수 있습니다. 이벤트 정책이나 고객 등급 정책을 추가해도 주문 접수 코드를 복사하지 않습니다.

</details>

## 13. .NET 10에서 handler가 `true`를 반환한 예외의 진단은 기본적으로 어떻게 되나요?

<details>
<summary>정답 보기</summary>

exception handler middleware의 진단이 기본 억제됩니다. 따라서 이 예제는 handler가 503 warning 또는 500 error를 한 번 직접 남깁니다. 조직이 middleware 진단을 반드시 유지해야 한다면 `SuppressDiagnosticsCallback`을 명시적으로 검토하되 중복 로그가 생기지 않게 해야 합니다.

</details>

## 14. `DemoProductCatalog` 장애 route를 두 조건으로 막는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

Development 환경이라는 조건만 믿으면 잘못된 배포 설정에서 노출될 수 있고, opt-in 설정만 믿으면 Production에서 켜질 수 있습니다. 환경과 명시적 활성화가 모두 맞아야 등록되게 해 우발적 노출 가능성을 줄입니다. 실제 운영에서는 배포 artifact에서 완전히 제외하는 방안도 좋습니다.

</details>

---

## 마지막 말하기 연습

다음 문장을 코드 없이 완성해 보세요.

> “예상 가능한 실패는 **Result**로, 현재 흐름에서 복구할 수 없는 실패는 **예외**로 표현한다. 요청 **취소**는 500으로 위장하지 않는다. `IExceptionHandler`는 예외를 안전한 **Problem Details**로 바꾸고, `code`는 클라이언트 분기, `traceId`는 운영 추적에 쓴다.”

### README Validation stage 빠른 답안

| 질문 | 답 |
| --- | --- |
| 수량 0 | 400 `order.quantity.out_of_range` |
| 같은 `orderId` 두 번 | 두 번째 요청은 409 `order.duplicate` |
| 카탈로그 일시 장애 | 503과 `Retry-After: 5` |
| 없는 route 본문 작성자 | Status Code Pages + Problem Details writer |
| 클라이언트 연결 종료 | 500으로 바꾸지 않고 취소 전파 |

| 찾아볼 책임 | 파일 / 메서드 |
| --- | --- |
| SKU·수량 검증 | `Domain/Order.cs`의 `RequestedOrderLine.Create` |
| 가격 조회 취소 전달 | `Application/PlaceOrderService.cs`의 `PlaceAsync` |
| 중복 저장 방지 | `Infrastructure/InMemoryOrderRepository.cs`의 `TryAddAsync` |
| 400·503·500 예외 분류 | `ErrorHandling/ApiExceptionHandler.cs`의 `Classify` |
| 공통 `traceId` | `Program.cs`의 `CustomizeProblemDetails` |
| Production demo 차단 | `Program.cs`의 환경 + opt-in 조건 |

### 복습 체크

- [ ] 400, 404, 409, 503, 500 각각의 예를 하나씩 들 수 있다.
- [ ] Result와 예외의 선택 기준을 말할 수 있다.
- [ ] Problem Details 공통 필드의 소비자를 설명할 수 있다.
- [ ] middleware 순서를 그림 없이 설명할 수 있다.
- [ ] Port/Adapter, Repository, Strategy, DI의 이유를 오늘 코드에서 찾을 수 있다.
