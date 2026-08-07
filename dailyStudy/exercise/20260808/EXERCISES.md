# 단계별 실습

1. `dotnet run`과 `dotnet run -- --self-test`를 실행하고 세 개의 집계 값이 어디서 증가하는지 찾으세요.
2. `SUB-104`를 추가해 `basic`, 15,000원, 자동 갱신 켜짐, 오늘 날짜로 실행 결과를 예측한 뒤 확인하세요.
3. `PercentageDiscountPolicy`의 할인율이 0~100 범위가 아니면 실패하도록 검증을 추가하고 self-test를 한 개 보태세요.
4. `FixedDiscountPolicy`를 새 Strategy로 작성하세요. `RenewSubscriptionsService`는 수정하지 말고 Composition Root의 한 줄만 교체하세요.
5. 결제 요청에 멱등성 키를 추가해 같은 구독·갱신일의 중복 청구를 결제사가 막을 수 있게 설계해 보세요.

막히면 먼저 컴파일 오류의 파일명과 줄 번호를 읽고, 한 번에 한 오류만 수정하세요.
