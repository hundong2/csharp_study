# 실습

각 단계 뒤 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요.

1. `PriceQuote`에 `HasFreeShipping`을 추가하고 결제액 50,000원 이상이면 `true`로 만드세요.
2. `FLAT5000` 정액 쿠폰 Strategy를 만들고 할인액이 소계를 넘지 않게 하세요.
3. 수량 3개 이상 상품에만 5% 할인을 먼저 적용하고 self-test를 추가하세요.
4. 쿠폰과 회원 정책을 순서대로 적용하는 `CompositeDiscountStrategy`를 설계하세요.
5. `Cart.Version`으로 동시 수정을 감지하는 Repository 계약을 글로 설계하세요.

힌트: 규칙 불일치는 Result, 저장소 응답 불능은 예외로 구분하고 테스트에서는 가짜 구현을 주입하세요.
