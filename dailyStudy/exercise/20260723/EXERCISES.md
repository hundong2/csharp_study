# 단계별 실습

먼저 각 단계를 수정한 뒤 `dotnet run --project .\src\JobSchedulerExercise -- --self-test`로 확인하세요.

1. **기초 문법**: `JobPriority.Low`를 추가하고 `switch`에서 60초 지연을 반환하세요. enum과 switch의 모든 선택지가 연결되는지 확인합니다.
2. **도메인 규칙**: 이름 길이가 3자 미만이면 실패하는 규칙을 `ScheduledJob.Validate`에 추가하고 검증 사례도 하나 추가하세요.
3. **LINQ**: 저장된 작업 중 `High` 우선순위만 고르는 `Where` 예제를 출력하세요. `Where`는 원본 컬렉션을 바꾸지 않습니다.
4. **설계 확장**: 테스트용 `NoDelayStrategy`를 만들고 생성자에 주입하세요. 기존 클래스를 수정하지 않고 정책을 교체하는 OCP와 DI를 확인합니다.
5. **운영 사고**: 실제 DB Repository라면 작업 이름에 unique 제약을 두고, 동시 요청 충돌을 어떻게 Result로 바꿀지 두 문장으로 적으세요.

막히면 먼저 컴파일 오류의 파일명과 줄 번호를 읽고, 한 단계씩 되돌려 마지막 통과 상태와 비교하세요.
