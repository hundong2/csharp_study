# 초보자 검증 단계

코드를 보지 않고 답한 뒤 README와 `Program.cs`에서 근거를 찾으세요.

1. `decimal` 리터럴의 `m`은 왜 필요한가요?
2. `string?`와 `string`의 차이는 무엇인가요?
3. record와 불변 데이터가 규칙 처리에 유리한 이유는 무엇인가요?
4. LINQ 정렬을 명시하면 테스트가 왜 안정적인가요?
5. `async`/`await`는 I/O 대기 중 무엇을 개선하나요?
6. 업무 거절은 Result, DB 단절은 예외로 구분하는 이유는 무엇인가요?
7. DI와 Strategy는 정책 교체와 테스트에 어떻게 도움을 주나요?
8. Application Service, Domain Model, Repository의 책임을 각각 말해 보세요.
9. 멱등성, 낙관적 동시성, Outbox는 서로 어떤 문제를 해결하나요?
10. 운영에서 확인할 메트릭과 로그에서 제외할 개인정보를 하나씩 말해 보세요.

## 통과 기준

- `dotnet build`: 경고 0, 오류 0
- `dotnet run`: 승인 1건, 검토 1건, 거절 2건
- `dotnet run -- --self-test`: 4/4 통과
- 위 질문 중 8개 이상을 자신의 말로 설명
