# 초보자 확인 단계

코드를 보지 않고 답해 보세요.

- `string?`가 `string`과 다른 이유는 무엇인가요?
- `record`가 예약 요청처럼 값 중심 데이터에 어울리는 이유는 무엇인가요?
- 예상 가능한 예약 거절에는 Result, DB 장애에는 예외를 쓰는 이유는 무엇인가요?
- Application Service, Repository, Strategy의 책임을 각각 한 문장으로 말할 수 있나요?
- 생성자 주입과 Composition Root가 테스트를 쉽게 만드는 이유는 무엇인가요?
- `async`/`await`가 DB 대기 중 스레드를 붙잡지 않는다는 말은 무슨 뜻인가요?

## 간단 복습 체크리스트

- [ ] 변수, 배열, `if`, `foreach`, `bool`, 날짜·기간의 역할을 설명한다.
- [ ] nullable 경고 없이 빌드한다.
- [ ] 정상 실행과 self-test 4/4를 확인한다.
- [ ] LINQ 필터·정렬과 비동기 흐름을 따라간다.
- [ ] SOLID의 SRP, OCP, DIP가 코드 어디에 적용됐는지 찾는다.
- [ ] 동시성, 멱등성, 취소, 재시도, 로그, 개인정보, Outbox 중 세 가지 이상을 설명한다.
