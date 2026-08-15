# 직접 해보기

1. `Quantity >= 10`이면 수동 검토가 되도록 정책을 바꾸고 self-test 기대값도 수정하세요.
2. 목적지 창고가 빈 문자열인 요청을 거부하는 검증과 테스트를 추가하세요.
3. `IApprovalStrategy`를 구현하는 `AlwaysManualReviewStrategy`를 만들고 Composition Root에서 교체하세요.
4. 처리 결과를 승인 유형별로 `GroupBy`하여 개수를 출력하세요.
5. 심화: Repository 저장 중 `IOException`이 발생했다고 가정하고, Result와 예외 중 무엇으로 처리할지 이유를 적으세요.

힌트: 한 번에 하나만 바꾸고 `dotnet build`, `dotnet run -- --self-test`를 반복하세요.
