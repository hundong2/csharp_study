# 초보자 확인 단계와 복습 체크리스트

코드를 보지 않고 다음 질문에 한 문장씩 답해 보세요.

- `string?`와 `string`은 무엇이 다른가?
- `record`를 주문과 출고 계획에 사용한 이유는 무엇인가?
- `async`/`await`가 저장소 같은 I/O 경계에서 필요한 이유는 무엇인가?
- 예상 가능한 입력 오류를 예외 대신 `Result<T>`로 표현한 이유는 무엇인가?
- Repository, Strategy, Application Service는 각각 어떤 책임을 갖는가?
- Composition Root에서 객체를 조립하면 테스트가 왜 쉬워지는가?
- LINQ의 `GroupBy`와 `Select`는 무엇을 계산하는가?
- SRP, OCP, DIP가 이 코드의 어느 부분에 드러나는가?

완료 기준:

- `dotnet build`가 경고와 오류 없이 성공한다.
- 기본 실행에서 계획 2건, 제외 3건이 출력된다.
- self-test가 `4/4`로 끝난다.
- 위 질문 중 6개 이상을 자신의 말로 설명할 수 있다.
