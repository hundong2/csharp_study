# 단계별 실습

각 단계 뒤 `dotnet run --project .\src\ParcelDispatchExercise -- --self-test`를 실행하세요.

1. 기초: 요청 배열에 제주행 표준 배송을 추가하고 `foreach` 출력에 배송지를 보여 주세요.
2. nullable: 배송 메모가 없으면 `"메모 없음"`을 출력하도록 `??`를 사용하세요.
3. 불변성: 기존 요청을 `with`로 복사해 무게만 5kg으로 바꾸고 원본이 바뀌지 않았음을 출력하세요.
4. LINQ: 저장 목록에서 요금 5,000원 이상 건수와 평균을 구하세요. 빈 목록의 `Average` 문제도 생각하세요.
5. Strategy/OCP: `SameDay`와 당일 배송 정책을 추가하되 `DispatchService`는 수정하지 마세요.
6. Result/예외: 30kg 초과는 Result로 유지하고 DB 연결 예외는 어디서 기록·변환할지 적으세요.
7. async/취소: 취소된 토큰으로 호출해 `OperationCanceledException`이 전달되는지 검증하세요.
8. 테스트 가능성: 고정 요금 Strategy를 주입해 Application Service만 검증하는 시나리오를 쓰세요.

도전: `ExistsAsync`와 `SaveAsync` 사이 중복 경합을 막는 unique 제약, 트랜잭션, 멱등성 키의 역할을 세 문장으로 설명하세요.
