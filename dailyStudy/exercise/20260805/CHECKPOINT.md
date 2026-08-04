# 초보자 검증 단계

1. `dotnet run`에서 연체된 `INV-100` 한 건과 `알림 1건 처리 완료`가 보이는지 확인합니다.
2. `dotnet run -- --self-test`에서 `4/4 통과`가 보이는지 확인합니다.
3. `Invoice`의 `string?`와 `DateOnly?`가 왜 nullable인지 말해 봅니다.
4. Repository, Strategy, Application Service를 각각 한 문장으로 설명합니다.
5. 정상적인 대상 제외는 Result, 네트워크 장애는 예외로 표현한 이유를 설명합니다.

막히면 `Program.cs`를 위에서 아래로 읽으며 “이 타입은 어떤 변경 이유를 하나만 가지는가?”를 적어 보세요.
