# 단계별 실습

각 단계 뒤에 `dotnet build`와 `dotnet run -- --self-test`를 실행하세요.

1. **초급 검증**: `JOB-101`의 `AttemptCount`를 2로 바꾸고 대기 시간이 40초인지 확인합니다.
2. **기본 문법**: 임시 작업 중 `AttemptCount == 0`인 작업만 고르는 `Where`를 `OrderBy` 앞에 추가합니다.
3. **nullable**: 이름이 빈 문자열인 작업을 self-test에 추가하고 실패 Result인지 검사합니다.
4. **불변성**: `with` 식으로 원본 `FailedJob`을 바꾸지 않고 시도 횟수가 1 증가한 복사본을 만듭니다.
5. **Strategy**: 항상 30초 뒤 한 번만 재시도하는 `FixedRetryPolicy`를 만들고 Composition Root에서 교체합니다.
6. **Repository**: JSON 파일 저장소를 추가하되 Application Service는 수정하지 않습니다.
7. **운영 설계**: 최대 지연과 jitter를 추가하고, 같은 `Id`가 중복 실행되지 않도록 멱등성 저장 위치를 적습니다.
8. **테스트**: 취소된 토큰을 전달했을 때 `OperationCanceledException`이 유지되는지 검증합니다.
