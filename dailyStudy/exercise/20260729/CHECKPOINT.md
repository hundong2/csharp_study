# 초보자 검증 단계

## 1. 실행 확인

- [ ] `dotnet run --project .\src\ExpenseApprovalExercise`에서 성공 2개와 실패 1개를 읽었다.
- [ ] `--self-test`에서 `PASS` 4개가 출력된다.

## 2. 코드 따라가기

- [ ] `ApproveExpenseCommand`는 입력 명령, `ApprovalReceipt`는 결과라는 점을 설명할 수 있다.
- [ ] `FindByIdAsync`의 `Expense?`가 왜 필요한지 설명할 수 있다.
- [ ] `Expense.Decide`가 상태를 직접 바꾸는 이유를 설명할 수 있다.
- [ ] Application Service가 Repository와 Policy를 호출하는 순서를 말할 수 있다.

## 3. 설계 판단

- [ ] 없는 ID와 이미 처리한 요청은 Result로 처리하는 것이 사용자 안내에 알맞다는 것을 이해했다.
- [ ] 취소와 저장소 장애는 호출 경계에서 예외·로그·재시도 정책을 검토해야 함을 안다.
- [ ] 실제 승인 기능에는 권한, 감사 보존, 멱등성, 관찰 가능성이 추가로 필요함을 안다.
