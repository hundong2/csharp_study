# 단계별 실습

각 단계 후 `dotnet run -- --self-test`로 기존 동작이 깨지지 않았는지 확인하세요.

1. `ORD-105` 특급 주문을 추가하고 `Quick` 운송사가 선택되는지 출력으로 확인합니다.
2. 20kg 초과 주문을 `Heavy` 운송사로 보내도록 enum과 Strategy를 확장하고 self-test를 추가합니다.
3. `Result<T>`의 문자열 오류를 `ErrorCode` enum과 메시지로 분리합니다. 호출자가 코드로 분기할 수 있어야 합니다.
4. `IClock`을 주입해 출고 계획에 생성 시각을 넣고, 테스트에서 고정 시각을 사용합니다.
5. 메모리 Repository에 동시에 같은 주문이 들어오는 상황을 생각해 보세요. 실제 DB의 unique 제약과 트랜잭션이 왜 필요한지 두 문장으로 적습니다.

힌트: 새 규칙은 Application Service의 반복문보다 `IShippingStrategy` 구현에 두는 편이 책임이 선명합니다.
