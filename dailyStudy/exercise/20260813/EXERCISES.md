# 직접 해보는 연습

1. 금액이 `100000` 이상이면 거절하는 규칙을 `StandardImportPolicy`에 추가하고 self-test도 한 개 추가하세요.
2. LINQ로 `Express` 주문 금액의 합계를 계산해 출력하세요.
3. 같은 주문 ID가 두 번 들어오면 `Result`로 거절하도록 변경하세요. Strategy와 Repository 중 어디에 책임을 둘지 이유도 적으세요.
4. 모든 입력을 거절하는 `RejectAllPolicy`를 만들어 Application Service 수정 없이 교체해 보세요.
5. 심화: 10만 행 파일을 한 번에 메모리에 올리지 않는 `IAsyncEnumerable<ImportRow>` 설계를 스케치하세요.

작게 변경할 때마다 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요. 정답 하나보다 선택한 책임의 위치를 설명하는 연습이 중요합니다.
