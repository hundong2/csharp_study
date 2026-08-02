# 초보자 확인 단계와 복습

코드를 보지 않고 답한 뒤 다시 실행해 확인하세요.

1. `record`와 일반 `class` 중 명령·결과 자료형에 record를 쓴 이유는 무엇인가요?
2. `Reservation.Expire`가 상태 변경 규칙을 가진 이유는 무엇인가요?
3. `IExpiryPolicy`가 Strategy인 이유와 새 정책 추가 시 장점은 무엇인가요?
4. `IReservationRepository`가 DB 세부 구현을 숨기면 테스트가 왜 쉬워지나요?
5. 잘못된 BatchSize는 Result, DB 장애는 예외로 다룬 이유는 무엇인가요?
6. `await`와 `CancellationToken`은 서버 자원과 종료 처리에 어떤 도움을 주나요?
7. Composition Root는 어디이며 무엇을 조립하나요?
8. 운영 환경에서 중복 만료 처리를 막는 방법 한 가지를 말해 보세요.

정답 기준: 용어를 외우기보다 “어떤 변경이 어느 클래스에만 영향을 주는지”와 “어떻게 가짜 구현으로 테스트하는지”를 예로 들 수 있으면 충분합니다.
