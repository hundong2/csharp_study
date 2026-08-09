# 실습 문제

1. `DefaultRiskStrategy`에서 실패 3회 이상이면 최소 `Medium`이 되게 바꾸고 self-test를 추가하세요.
2. `IRiskStrategy`를 구현하는 `StrictRiskStrategy`를 만들고 Composition Root의 한 줄만 바꿔 교체하세요.
3. 잘못된 입력 결과도 버리지 말고 성공/실패 건수를 반환하도록 Application Service의 반환 record를 설계하세요.
4. 저장소가 `OperationCanceledException`을 던질 때 서비스가 이를 삼키지 않는지 검증하세요.

힌트: 한 번에 하나씩 수정하고 `dotnet run -- --self-test`로 회귀를 확인하세요.
