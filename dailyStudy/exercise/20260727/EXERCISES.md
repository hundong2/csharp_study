# 단계별 실습

각 단계 뒤 `dotnet run --project .\src\InventoryReservationExercise -- --self-test`를 실행하세요.

1. `requests`에 수량이 0인 요청을 추가하고 실패 메시지를 확인합니다. 변수, 생성자, 배열, 반복, 조건을 익힙니다.
2. `InventoryItem.Reserve`의 재고 부족 조건을 잠시 `>=`로 바꿔 마지막 한 개 예약이 왜 실패하는지 관찰한 뒤 되돌립니다.
3. `VipReservationPolicy`를 만들어 최대 10개까지 허용하고 Composition Root에서 Strategy 구현만 교체합니다.
4. `IAuditLog`의 테스트용 구현을 만들어 생성된 영수증을 리스트에 저장합니다. 콘솔 없이도 결과를 검증할 수 있는 이유를 적습니다.
5. 주문 ID 중복을 막는 Repository 경계를 설계합니다. 예상 가능한 중복은 Result, 저장소 연결 실패는 예외로 처리하는 이유를 적습니다.

도전: 동시에 같은 SKU를 예약하면 현재 메모리 구현에 경쟁 조건이 생길 수 있습니다. 실제 DB에서 트랜잭션과 버전 열을 이용한 낙관적 동시성으로 해결하는 흐름을 의사 코드로 작성하세요.
