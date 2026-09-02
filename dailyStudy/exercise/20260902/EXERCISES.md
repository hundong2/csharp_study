# 단계별 실습

1. `StandardVendorRiskPolicy`에서 연 계약액 20,000,000원 이상이면 `ManualReview`가 되도록 바꾸고 self-test를 추가하세요.
2. `ContactEmail`이 `null`일 때 `?.`, `??` 또는 패턴 매칭으로 안내 문구를 만드는 작은 메서드를 작성하세요.
3. `IVendorRiskPolicy`의 다른 구현인 `StrictVendorRiskPolicy`를 만들고 Composition Root에서 교체하세요.
4. `SaveAsync`를 같은 결과로 두 번 호출해 멱등성을 검증하고, 다른 결과를 저장하면 실패하는지 확인하세요.
5. 인메모리 저장소가 특정 요청에서 실패하도록 테스트 대역을 만들어 Application Service의 실패 경로를 검증하세요.

각 단계 후 `dotnet run --project src/VendorOnboardingExercise -- --self-test`를 실행하세요.
