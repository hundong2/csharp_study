# 단계별 실습

각 단계마다 먼저 결과를 예상하고 코드를 바꾼 뒤, 일반 실행과 `--self-test`를 다시 실행하세요.

## 1단계: 기본 문법 값 바꾸기

`BasicSyntaxTour`에서 `stock`을 `2`, `optionalDiscountRate`를 `0.1m`으로 바꾸세요.

- 예상: 상태는 `재고 부족`, 할인율은 `10%`입니다.
- 확인할 문법: 변수, nullable, `??`, `switch` 식, 문자열 보간

## 2단계: 검증 규칙 추가하기

한 번에 최대 10개만 예약할 수 있도록 `InventoryService.ReserveAsync`에 검증을 추가하세요. 오류 문장은 `한 번에 최대 10개까지 예약할 수 있습니다.`로 만드세요.

- 먼저 수량 `11`이 실패하는 self-test를 작성하세요.
- 입력 형식 검증은 Application Service에 두는 이유를 말해 보세요.

## 3단계: 판매 중지 상품 확인하기

`CompositionRoot`의 상품 `IsActive`를 `false`로 바꾸고 실행하세요.

- `InventoryItem.Reserve`가 어떤 오류를 반환하는지 확인하세요.
- 판매 가능 여부 규칙이 Service가 아니라 Domain Model에 있는 장점을 적으세요.

## 4단계: 예약 취소 구현하기

`CancelReservationAsync(requestId)` 유스케이스를 설계하세요.

1. 예약을 조회합니다.
2. 이미 취소되었는지 확인합니다.
3. 예약 수량만큼 재고를 되돌립니다.
4. 예약 상태와 재고를 저장합니다.
5. 같은 취소 요청을 반복해도 재고가 두 번 늘지 않는 self-test를 추가합니다.

실무 확장 질문: 두 저장 작업 중 하나만 실패하면 데이터가 어긋납니다. 실제 DB에서는 트랜잭션이나 Unit of Work로 어떻게 묶을지 조사해 보세요.
