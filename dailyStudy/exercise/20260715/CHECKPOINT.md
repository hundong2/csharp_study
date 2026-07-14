# 초보자 안전 학습 검증

아래 질문에 짧게라도 말로 답할 수 있으면 오늘 자료를 제대로 따라온 것입니다. 답을 외우기보다 `Program.cs`의 코드를 다시 보며 근거를 찾으세요.

## A. 기본 문법

1. `int`, `decimal`, `bool`, `string`은 각각 어떤 값을 담나요?
2. `var`는 타입이 없다는 뜻인가요?
3. `string? optionalPhone = null;`에서 `?`는 무엇을 알려 주나요?
4. `foreach`는 어떤 컬렉션을 순회할 때 쓰나요?
5. 람다식 `hours => hours + 0.5m`은 어떤 일을 하나요?

통과 기준:

- 값의 종류, null 가능성, 컬렉션 선택 기준을 간단한 예로 설명할 수 있습니다.

## B. 객체와 타입 설계

1. `class Ticket`은 왜 `record`가 아니라 `class`인가요?
2. `record SupportAgent`는 왜 값 비교에 어울리나요?
3. `enum Severity`를 문자열로만 저장하면 어떤 실수가 생기기 쉬운가요?
4. `SeverityRules.ResponseHours()`는 업무 규칙을 어디에 모아 두나요?

통과 기준:

- 상태가 바뀌는 객체와 값처럼 비교하는 객체를 구분해서 설명할 수 있습니다.

## C. 실무 아키텍처

1. Domain 계층은 무엇을 책임지나요?
2. Application 계층의 `TicketService`는 어떤 흐름을 조립하나요?
3. Infrastructure 계층의 저장소와 외부 조회는 왜 인터페이스 뒤에 두나요?
4. `DemoCompositionRoot.Build()`는 실무에서 어떤 설정 코드와 비슷한가요?

통과 기준:

- 업무 규칙, 유스케이스 흐름, 외부 시스템 구현을 분리하는 이유를 설명할 수 있습니다.

## D. async와 오류 처리

1. `async` / `await`는 왜 쓰나요?
2. `CancellationToken`은 호출자가 어떤 의사를 전달할 때 쓰나요?
3. 복구 가능한 실패를 `Result<T>`로 표현하면 호출자는 무엇을 반드시 확인해야 하나요?
4. `Result<T>.Value`를 `IsSuccess` 확인 없이 읽으면 어떤 위험이 있나요?

통과 기준:

- 비동기 작업, 취소, 성공/실패 분기를 실제 호출 흐름과 연결해서 설명할 수 있습니다.

## E. 실행 검증

아래 명령이 성공해야 합니다.

```powershell
dotnet run --project .\src\SupportTicketExercise\SupportTicketExercise.csproj -- --self-test
```

성공 메시지:

```text
Self-test passed: syntax tour, validation, ticket workflow, and layer contracts are valid.
```

## F. 최종 리뷰 체크리스트

| 항목 | 통과 기준 |
| --- | --- |
| 기본 문법 | 변수, 조건문, 반복문, 컬렉션, nullable을 예제로 설명 |
| 타입 설계 | class, record, enum, interface의 역할 구분 |
| 아키텍처 | Domain / Application / Infrastructure 책임 구분 |
| 비동기 | async/await와 CancellationToken 목적 설명 |
| 오류 처리 | 예외와 Result 패턴의 용도 차이 설명 |
| 실행 | 일반 실행과 self-test 모두 성공 |

막히는 항목이 있으면 `Program.cs`에서 관련 타입을 찾아 다시 읽고, `EXERCISES.md`의 해당 단계를 한 번 더 실행하세요.
