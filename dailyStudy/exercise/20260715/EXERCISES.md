# 실습 문제

오늘 실습은 한 번에 많은 코드를 외우는 방식이 아닙니다. 작은 값을 바꾸고, 실행하고, 왜 결과가 달라졌는지 말로 설명하는 방식으로 진행합니다.

## 1단계: 기본 문법 손에 익히기

`src/SupportTicketExercise/Program.cs`의 `BasicSyntaxTour.Run()`을 읽고 아래 값을 직접 바꿔 보세요.

1. `requester`를 본인 이메일 형식의 문자열로 바꿉니다.
2. `openTicketCount`를 `0`, `2`, `5`로 바꾸며 `switch` 결과가 어떻게 달라지는지 확인합니다.
3. `priorityByCategory`에 `"security"` 항목을 추가하고 `foreach` 출력에 포함되는지 확인합니다.
4. `tags` 배열에 `"async"`를 추가하고 `string.Join` 결과를 확인합니다.
5. `optionalPhone`에 `null` 대신 `"010-0000-0000"`을 넣고 null 조건 연산자 결과를 비교합니다.

확인 질문:

- `string?`와 `string`은 어떤 차이가 있나요?
- `List<T>`와 `Dictionary<TKey, TValue>`는 각각 언제 쓰기 좋나요?
- `switch` 식은 긴 `if` 문보다 언제 읽기 쉬운가요?

## 2단계: 티켓 업무 흐름 바꾸기

`SupportTicketDemo.RunAsync()`의 `CreateTicketCommand` 값을 바꿔 보세요.

1. `Category`를 `TicketCategory.Billing`으로 바꾸면 담당자가 누구로 바뀌나요?
2. `Severity`를 `Low`, `Normal`, `High`, `Critical`로 바꾸며 `DueAt` 시간이 어떻게 달라지는지 확인합니다.
3. `RequesterEmail`에서 `@`를 제거하면 어떤 실패 메시지가 나오나요?
4. `Description`을 `"Help"`로 줄이면 왜 실패하나요?

확인 질문:

- 사용자 입력 검증을 화면 코드에만 두면 어떤 문제가 생기나요?
- `Result<T>`는 예외와 무엇이 다른가요?
- `Ticket.Assign()`이 담당자 검증을 다시 하는 이유는 무엇인가요?

## 3단계: 실무 아키텍처로 분리해 보기

지금은 초보자가 읽기 쉽도록 한 파일에 모든 타입을 넣었습니다. 실무 프로젝트라면 아래처럼 나눌 수 있습니다.

```text
Domain/
  Ticket.cs
  SupportAgent.cs
  SeverityRules.cs
Application/
  TicketService.cs
  CreateTicketCommand.cs
  Result.cs
Infrastructure/
  AgentDirectory.cs
  BusinessClock.cs
  InMemoryTicketRepository.cs
Program.cs
```

직접 파일을 나눠 보고 아래 명령이 계속 성공하는지 확인하세요.

```powershell
dotnet run --project .\src\SupportTicketExercise\SupportTicketExercise.csproj -- --self-test
```

## 4단계: preview 기능 비교 토론

C# 15 preview의 union types가 안정화되면 `Result<T>` 같은 성공/실패 모델을 더 직접적으로 표현할 수 있습니다. 하지만 오늘 프로젝트는 안정 SDK에서 바로 실행되는 학습 자료이므로 preview 문법을 코드에 넣지 않았습니다.

토론 질문:

- 팀 프로젝트에서 preview 언어 기능을 바로 쓰면 어떤 장점과 위험이 있나요?
- 성공/실패를 `bool`, 예외, `Result<T>`로 표현할 때 각각 어떤 코드가 읽기 쉬운가요?
- 초보자가 먼저 익혀야 할 것은 최신 문법인가요, 값 흐름과 책임 분리인가요?
