# 단계별 실습: 비용 승인 흐름

각 단계 뒤에 `dotnet run --project .\src\ExpenseApprovalExercise -- --self-test`를 실행하세요. 한 번에 여러 단계를 바꾸지 않는 편이 원인을 찾기 쉽습니다.

1. `commands` 배열에 `new ApproveExpenseCommand("EXP-100", "your-name")`을 하나 더 추가하고 결과를 읽어 보세요. `new`, 배열, `foreach`를 확인하는 단계입니다.
2. `AmountBasedApprovalPolicy`의 한도를 `50_000m`으로 바꾸고 `EXP-100`의 결정이 어떻게 달라지는지 관찰하세요. 왜 `decimal`에 `m`이 붙는지도 설명해 보세요.
3. `ExpenseStatus.Rejected`를 추가하고, 설명에 `개인`이 포함되면 거절하는 새 `IApprovalPolicy` 구현을 만들어 Program에서 교체하세요. 서비스 코드를 고치지 않았는지 확인하세요(Strategy/OCP).
4. `InMemoryExpenseRepository`에 같은 ID를 저장할 때 어떤 일이 일어나는지 살펴보고, 실제 DB의 unique constraint가 왜 필요한지 README 운영 항목과 연결해 보세요.
5. `ApproveAsync`에 `CancellationToken`을 취소한 호출을 직접 만들어 보세요. 예상 가능한 Result 실패와 취소 예외가 왜 다른지 한 문장으로 적어 보세요.

완료 후 변경을 되돌리거나, 자기 답을 별도 메모에 남겨 다음 학습 때 비교하세요.
