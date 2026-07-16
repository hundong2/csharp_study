# 초보자 검증과 복습 체크리스트

## 1. 실행 검증

```powershell
dotnet build .\src\NotificationExercise\NotificationExercise.csproj
dotnet run --project .\src\NotificationExercise\NotificationExercise.csproj
dotnet run --project .\src\NotificationExercise\NotificationExercise.csproj -- --self-test
```

확인할 출력:

- 일반 실행에 `기본 문법 둘러보기`와 `사용자 알림 발송 흐름`이 보인다.
- 이메일 발송 로그 뒤에 `Sent` 상태와 제목이 보인다.
- 자체 검증 마지막 줄이 `초보자 검증 통과`로 시작한다.

오류가 생기면 다음 순서로 확인하세요.

1. 명령을 `20260717` 폴더에서 실행했는지 확인합니다.
2. 첫 번째 컴파일 오류의 파일과 줄만 먼저 봅니다.
3. 괄호 `()`, 중괄호 `{}`, 큰따옴표 `"`, 세미콜론 `;`이 빠졌는지 봅니다.
4. enum 값을 추가했다면 관련 `switch`에도 경우를 추가했는지 봅니다.
5. Sender를 추가했다면 `CompositionRoot` 배열에 등록했는지 봅니다.
6. 수정한 시나리오를 일반 실행한 뒤 전체 자체 검증을 다시 실행합니다.

## 2. 코드를 보지 않고 말로 답하기

1. `string?`와 `string`은 어떤 차이가 있는가?
2. 배열, `List<T>`, `Dictionary<TKey,TValue>`는 각각 어떤 상황에 알맞은가?
3. `FirstOrDefault` 결과가 null일 수 있는 이유는 무엇인가?
4. enum을 채널 이름 문자열 대신 쓰면 어떤 실수를 줄일 수 있는가?
5. `EmailSender`와 `SmsSender`의 공통 계약은 무엇인가?
6. `NotificationService`가 구체 발송기를 직접 `new`하지 않는 이유는 무엇인가?
7. `CancellationToken`은 어떤 오래 걸리는 작업에서 유용한가?
8. 잘못된 이메일은 `Result<T>`로, 네트워크 장애는 예외로 처리할 수 있는 이유를 설명해 보라.

## 3. concise review checklist

- [ ] 변수, nullable, 조건, 반복, 컬렉션을 코드에서 찾을 수 있다.
- [ ] class, record, enum, interface의 역할을 한 문장씩 말할 수 있다.
- [ ] `async` / `await`가 기다리는 작업에서 쓰이는 이유를 안다.
- [ ] Strategy가 채널별 동작을 어떻게 분리하는지 설명할 수 있다.
- [ ] DI, Application Service, Repository, Composition Root가 나누는 책임을 안다.
- [ ] 정상 이메일, 잘못된 이메일, SMS, 미등록 Push 시나리오를 실행했다.
- [ ] 안정 .NET/C# 기능과 Preview 기능을 운영 코드에서 구분해야 하는 이유를 안다.
