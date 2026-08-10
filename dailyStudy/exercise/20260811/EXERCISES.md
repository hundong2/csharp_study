# 실행 연습

1. `E-103`을 일반 실행 목록에 추가하고 이미 보유한 `developer`가 다시 부여되지 않는지 확인하세요.
2. `DepartmentAccessPolicy`에서 Engineering에 `incident-reader`를 추가하고 self-test의 예상 개수를 고치세요.
3. `HumanResources` 직원을 Repository 초기 데이터에 추가하고 필요한 권한을 확인하세요.
4. 도전: 호출 횟수를 기록하는 `IAccessGateway` 가짜 구현을 만들어 권한이 모두 있는 직원에게는 호출되지 않음을 검증하세요.

힌트: 먼저 실패를 확인하고, 한 번에 한 부분만 바꾼 뒤 `dotnet run -- --self-test`로 다시 검증하세요.
