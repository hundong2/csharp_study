# 실습 문제

1. `Amount`가 0 이하이면 `Reminder`를 만들지 않도록 검증하고 self-test를 추가하세요.
2. 7일 이상 연체된 청구서만 고르는 `SevereOverduePolicy`를 구현해 Strategy를 교체하세요.
3. `ConsoleReminderSender`가 특정 이메일에서 예외를 던지게 하고 Result 실패가 출력되는지 확인하세요.
4. 보너스: 여러 알림을 동시에 보내되 외부 API 제한을 위해 `SemaphoreSlim`으로 동시성을 3개로 제한하세요.

각 변경 뒤 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요.
