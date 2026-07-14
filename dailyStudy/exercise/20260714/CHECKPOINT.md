# 초보자 완전 학습 검증

이 문서는 기초 지식이 없는 개발자가 오늘 자료를 제대로 이해했는지 확인하는 단계입니다. 아래 질문에 말로 답하고, 마지막에 self-test를 실행해 보세요.

## A. 기초 문법

1. `int`, `decimal`, `bool`, `string`은 각각 어떤 값을 담나요?
2. `List<T>`와 `Dictionary<TKey, TValue>`는 언제 쓰나요?
3. `if`와 `switch`는 어떤 상황에서 각각 읽기 쉬운가요?
4. `string?`처럼 `?`가 붙으면 어떤 의미인가요?
5. 람다식 `(x) => x * 2`는 왜 메서드처럼 사용할 수 있나요?

정답 기준:

- 값의 종류, null 가능성, 컬렉션 선택 기준을 예제로 설명할 수 있으면 통과입니다.

## B. 객체와 타입 설계

1. `class`와 `record`는 어떤 차이가 있나요?
2. `record struct Money`가 금액 표현에 잘 맞는 이유는 무엇인가요?
3. `enum OrderStatus`를 문자열 대신 쓰면 좋은 점은 무엇인가요?
4. `IInventoryGateway` 같은 인터페이스는 왜 필요한가요?

정답 기준:

- 값 비교가 필요한 타입은 record가 편하고, 외부 구현을 교체해야 하는 지점은 인터페이스로 감싸면 테스트와 유지보수가 쉬워진다고 설명할 수 있으면 통과입니다.

## C. 아키텍처

1. Domain 계층은 무엇을 책임지나요?
2. Application 계층은 무엇을 조립하나요?
3. Infrastructure 계층은 왜 가장 바뀌기 쉬운가요?
4. 결제 실패 시 재고 예약을 되돌리는 코드는 어느 흐름에 있어야 하나요?

정답 기준:

- 업무 규칙, 유스케이스 흐름, 외부 시스템 구현을 구분할 수 있으면 통과입니다.

## D. async와 오류 처리

1. `async` / `await`는 스레드를 새로 만드는 문법인가요?
2. `CancellationToken`은 왜 메서드 인자로 전달하나요?
3. 복구 가능한 실패를 예외 대신 `Result<T>`로 표현하면 어떤 장점이 있나요?
4. `Result<T>.IsSuccess`를 확인하지 않고 `Value`만 읽으면 어떤 위험이 있나요?

정답 기준:

- 비동기는 대기 중인 작업을 효율적으로 이어 가기 위한 모델이고, 취소 토큰은 호출자가 작업 중단 의사를 전달하는 계약이며, `Result<T>`는 예상 가능한 실패를 명시적으로 다룰 수 있게 한다고 설명하면 통과입니다.

## E. 실행 검증

아래 명령이 성공해야 합니다.

```powershell
dotnet run --project .\src\OrderProcessingExercise\OrderProcessingExercise.csproj -- --self-test
```

성공 메시지:

```text
Self-test passed: beginner syntax, order workflow, failure handling, and architecture contracts are valid.
```

## F. 최종 판정

| 항목 | 통과 기준 |
| --- | --- |
| 기초 문법 | 변수, 조건문, 반복문, 컬렉션을 간단한 예제로 설명 |
| 타입 설계 | class, record, enum, interface의 역할 구분 |
| 아키텍처 | Domain / Application / Infrastructure 책임 구분 |
| 비동기 | async/await와 CancellationToken의 목적 설명 |
| 오류 처리 | 예외와 Result 패턴의 용도 차이 설명 |
| 실행 | self-test 통과 |

모든 항목을 통과하면 오늘 자료는 완성 학습으로 봅니다. 하나라도 막히면 `Program.cs`에서 해당 타입을 찾아 읽고, `EXERCISES.md`의 관련 단계를 다시 실행하세요.

