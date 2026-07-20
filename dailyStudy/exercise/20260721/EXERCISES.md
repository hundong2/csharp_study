# 단계별 실습

각 단계 뒤 `dotnet run --project .\src\MeetingRoomExercise -- --self-test`를 실행하세요.

1. `MeetingRoom`에 `Floor` 값을 추가하고 출력에 층을 표시합니다. record 생성자와 문자열 보간을 연습합니다.
2. 참석자 수가 10명 이상이면 제목 앞에 `[대규모]`를 붙입니다. `if`와 불변 record 생성을 연습합니다.
3. `SmallestSuitableRoomStrategy`에서 장비 비교를 대소문자 무시로 바꾸고 검증을 하나 추가합니다. LINQ와 문자열 비교를 연습합니다.
4. `LargestSuitableRoomStrategy`를 새로 만들고 Composition Root에서 교체합니다. 기존 서비스를 수정하지 않는 OCP와 Strategy를 확인합니다.
5. 두 요청이 동시에 같은 방을 예약하는 테스트를 작성한 뒤, 실제 DB라면 어떤 트랜잭션/고유 제약이 필요한지 주석으로 설명합니다. 운영 경쟁 조건을 이해하는 단계입니다.

막히면 한 번에 전체 구조를 바꾸지 말고, 입력 → 선택 → 저장 → 출력 중 한 구간만 수정하세요.
