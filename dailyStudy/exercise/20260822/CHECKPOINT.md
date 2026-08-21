# 초보자 확인 단계

코드를 보지 않고 다음 질문에 한두 문장으로 답하세요.

1. `string`과 `string?`, `var`는 각각 무엇을 뜻하나요?
2. `record`와 `enum`을 사용한 이유는 무엇인가요?
3. LINQ와 `async`/`await`는 이 코드에서 어떤 역할을 하나요?
4. `Result<T>`와 예외는 각각 어떤 실패에 적합한가요?
5. Repository, Strategy, Application Service의 책임을 구분해 보세요.
6. Composition Root와 DI가 테스트 가능성을 어떻게 높이나요?
7. 계정 열거 방지, 속도 제한, 멱등성, 동시성, Outbox, 취소, 민감정보 로그 금지가 왜 필요한가요?

## 완료 체크리스트

- [ ] `dotnet build` 경고와 오류가 모두 0이다.
- [ ] `dotnet run` 결과가 발송 1건, 동일 응답 1건, 차단 2건이다.
- [ ] `dotnet run -- --self-test`가 4/4 통과한다.
- [ ] 실습 1~2단계를 직접 수정하고 다시 실행했다.
- [ ] 각 설계 요소와 보안 운영 원칙을 코드 없이 설명할 수 있다.
