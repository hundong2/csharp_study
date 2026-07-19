# 단계별 실습

각 단계마다 `dotnet run --project ./src/WorkBoardExercise`로 확인하세요.

1. `SyntaxTour`에 `for` 또는 `foreach`를 추가해 점수를 한 줄씩 출력합니다.
2. `Priority.Urgent`를 추가하고, 작업 보드가 High/Urgent 작업을 먼저 보여 주도록 LINQ `OrderByDescending`을 적용합니다.
3. `IWorkItemRepository`에 삭제 기능을 계약으로 추가하고 메모리 저장소와 서비스에 구현합니다.
4. `CompleteAsync(999)` 호출 시 발생하는 예외를 `try/catch`로 처리해 사용자 친화적 메시지를 출력합니다.
5. 도전: `InMemoryWorkItemRepository` 대신 JSON 파일 구현을 만들고 `CompositionRoot` 한 곳만 바꿔 교체합니다. `bin/`, `obj/`, 실행 중 생성한 JSON은 커밋하지 마세요.

막히면 한 단계씩만 바꾸고 `--self-test`를 다시 실행하세요. 컴파일 오류는 보통 메시지의 첫 오류부터 고치면 됩니다.
