# 단계별 실습

각 단계 뒤에 `dotnet run --project .\src\TicketTriageExercise -- --self-test`를 실행하세요.

1. `requests`에 제목과 등급이 다른 티켓을 추가하고 `foreach`, 생성자 호출, enum 사용을 확인합니다.
2. `Ticket.Create`가 100자를 넘는 제목을 거부하도록 만들고 검증 항목을 하나 추가합니다.
3. `SlaTriageStrategy`에서 VIP의 Low 티켓은 P2가 되게 정책을 바꾸고 테스트로 고정합니다.
4. `GetQueueAsync`의 LINQ에 같은 우선순위라면 VIP를 먼저 두는 `ThenByDescending`을 추가합니다.
5. 도전: `IAuditLog` 가짜 구현에 호출된 티켓을 저장하고 “성공 시 한 번, 실패 시 0번”을 검증합니다.

힌트: 예상 가능한 제목 오류와 중복은 `Result<T>`가 어울립니다. 취소는 `OperationCanceledException`을 그대로 전파해야 호출자가 요청 중단을 구별할 수 있습니다.
