# 직접 해보는 연습

1. `Sms` 선호 고객의 전화번호가 없으면 `Email`로 대체하는 정책을 별도 Strategy로 구현하고 테스트를 추가하세요.
2. LINQ로 채널별 계획 수를 `Dictionary<DeliveryChannel, int>`로 반환하세요.
3. 메시지가 100자를 넘으면 `Result` 실패로 처리하세요. 왜 예외보다 Result가 적합한지 적어 보세요.
4. 모든 입력을 거절하는 `RejectAllStrategy`를 만들고 Application Service 수정 없이 교체해 보세요.
5. 심화: DB 저장과 외부 메시지 발송 사이의 불일치를 막는 Outbox 테이블과 처리 상태를 설계하세요.

작게 변경할 때마다 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요. 정답 하나보다 책임을 어느 객체에 둘지 설명하는 연습이 중요합니다.
