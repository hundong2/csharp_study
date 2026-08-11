# 직접 해보는 연습

1. `KeywordTicketRoutingStrategy`에 `shipping` 팀 규칙을 추가하고 self-test도 한 개 늘리세요.
2. `TicketPriority.High`인 결정만 출력하는 LINQ 식을 작성하세요.
3. `IRoutingNotifier`가 첫 호출에서 예외를 던지는 테스트 대역을 만들고, 예외가 숨겨지지 않는지 확인하세요.
4. `FakeTicketRoutingStrategy`를 만들어 모든 티켓을 `training` 팀으로 보내 보세요. Application Service를 수정하지 않아도 되는 이유를 설명하세요.
5. 심화: 저장은 성공했지만 알림이 실패한 상황을 안전하게 재처리하려면 Outbox 패턴이 왜 필요한지 짧게 적으세요.

정답 하나만 있는 문제가 아닙니다. 매 변경 뒤 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요.
