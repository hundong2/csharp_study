# 단계별 실습

각 단계 뒤에 `dotnet run --project .\src\SubscriptionExercise -- --self-test`를 실행하세요.

1. **기초 문법**: `Basic` 가격을 `10_900m`으로 바꾸고 대응 테스트의 기대값도 수정합니다.
2. **nullable**: 빈 이메일과 `@` 없는 이메일이 실패하는지 테스트를 하나 추가합니다.
3. **LINQ**: Repository에 특정 금액 이상 갱신만 반환하는 메서드를 만들고 `Where`와 `OrderByDescending`을 사용합니다.
4. **Strategy/OCP**: `Enterprise` 요금제를 추가하고 가격 규칙을 별도 Strategy 구현으로 분리합니다.
5. **운영 설계**: 같은 구독을 동시에 갱신해도 한 건만 저장되도록 DB 고유 제약과 트랜잭션을 어디에 둘지 글로 설명합니다.

도전: `CancellationTokenSource.Cancel()`로 취소된 호출이 `OperationCanceledException`을 내는 테스트를 작성하세요. 취소는 업무 실패가 아니라 호출 흐름 중단이므로 Result로 바꾸지 않습니다.
