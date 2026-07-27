# 단계별 실습

각 단계 뒤 `dotnet run --project .\src\WebhookDeliveryExercise -- --self-test`를 실행하세요.

1. `commands`에 빈 payload 명령을 추가하고 실패 메시지를 확인합니다. 변수, 생성, 배열, 반복, 조건을 익힙니다.
2. `FakeWebhookClient`의 성공 조건을 `attempt >= 3`으로 바꾸고 영수증의 시도 횟수가 달라지는지 확인합니다.
3. `ExponentialRetryStrategy`를 만들어 시도 번호뿐 아니라 대기 시간도 계산하도록 설계하고 Composition Root에서 구현을 교체합니다.
4. `IDeliveryLog`의 테스트 구현을 만들어 영수증을 리스트에 저장합니다. 콘솔 출력 없이 결과를 검증할 수 있는 이유를 적습니다.
5. 메시지 ID 중복을 막는 Repository 경계를 추가합니다. 예상 가능한 중복은 Result, 저장소 연결 실패는 예외인 이유를 적습니다.

도전: 실제 HTTP 전송에 timeout, 지수 backoff와 jitter, circuit breaker를 적용할 때 무한 재시도와 동시 재시도 폭주를 어떻게 피할지 의사 코드로 작성하세요.
