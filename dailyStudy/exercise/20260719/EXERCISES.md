# 단계별 실습

각 단계마다 일반 실행과 `--self-test`를 다시 실행하세요.

1. `SyntaxTour`의 수량과 단가를 바꾸고 `switch` 결과를 실행 전에 예상합니다.
2. 비회원 주문으로 바꾸어 할인액이 0원인지 확인합니다.
3. `CartItem` 수량을 0으로 바꾸고 오류 문장이 원인을 알려 주는지 확인합니다.
4. `IDiscountPolicy`를 구현한 `FixedDiscountPolicy`를 만들어 30,000원 이상이면 3,000원을 할인합니다. `CompositionRoot`에서 구현을 교체합니다.
5. 도전: 할인액이 소계보다 커지지 않게 `Math.Min`으로 방어하고 검증 사례를 추가합니다.

막히면 입력(`CheckoutCommand`) → 조정(`CheckoutService`) → 규칙(`IDiscountPolicy`) → 결과(`Result<Receipt>`) 순서로 한 줄씩 따라가세요.
