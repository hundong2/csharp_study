# 2026-07-29 C# 비용 승인 흐름 실습

## 초보자용 읽기 순서

1. 아래 명령으로 먼저 실행과 자동 검증을 통과시킨다.
2. `src/ExpenseApprovalExercise/Program.cs`의 실행부와 `var`, 배열, `foreach`, `if`를 읽는다.
3. `record`, nullable(`Expense?`), `Result<T>`를 읽고 실패가 어떻게 표현되는지 확인한다.
4. `Expense`(Domain Model), `IApprovalPolicy`(Strategy), `IExpenseRepository`(Repository)를 차례로 읽는다.
5. 마지막으로 `ExpenseApprovalApplicationService`가 유스케이스를 조립하는 방식을 보고 `EXERCISES.md`를 한 단계씩 풀어 본다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260729
dotnet run --project .\src\ExpenseApprovalExercise
dotnet run --project .\src\ExpenseApprovalExercise -- --self-test
```

## 처음 만나는 기본 문법과 문법 규칙

| 문법 | 첫 사용 | 뜻과 이유 |
| --- | --- | --- |
| `var` | `var repository = ...` | 우변으로 타입을 컴파일러가 확정한다. 동적 타입이 아니며, 읽기 쉬운 이름과 함께 쓴다. |
| `new` / 배열 | `new[] { ... }` | 객체와 여러 값을 만든다. 배열은 같은 타입의 값을 순서대로 담는다. |
| `if` / `foreach` | 실행부 | 조건에 따라 분기하고, 각 명령을 하나씩 처리한다. |
| `decimal` | `45_000m` | 금액은 이진 부동소수점 오차를 피하기 위해 `decimal`을 사용한다. `m`은 decimal 리터럴 표시다. |
| nullable | `Task<Expense?>` | 조회 실패가 정상일 수 있음을 타입에 표시한다. `null` 검사 후에만 값을 사용한다. |
| `record` | `ApproveExpenseCommand` | 명령·결과 같은 값 중심 데이터를 간결하고 불변에 가깝게 전달한다. |
| LINQ | `Where().OrderBy().Select()` | 목록에서 거르기·정렬·변환을 선언적으로 표현한다. |
| `async` / `await` | `ApproveAsync` | 저장소 I/O를 기다리는 동안 스레드를 막지 않는다. `CancellationToken`은 중단 요청을 전달한다. |

## 실무 구조와 설계 선택

```text
Program (Composition Root: 실제 구현을 조립)
  └─ ExpenseApprovalApplicationService (Application Service: 유스케이스 순서)
       ├─ Expense (Domain Model: 상태 전이 규칙)
       ├─ IApprovalPolicy (Strategy: 금액별 규칙 교체)
       ├─ IExpenseRepository (Repository: 저장 기술 경계)
       └─ IAuditLog (운영/감사 경계)
```

- DI(의존성 주입)와 Composition Root: `Program`에서 구현을 넣으므로 서비스는 구체적 DB나 로그 도구를 모른다. 이는 DIP와 테스트 대역 사용을 돕는다.
- SOLID: Domain Model은 상태 규칙만, Application Service는 절차만 맡아 SRP를 지킨다. 새 정책은 Strategy 구현 추가로 넣어 OCP에 가깝게 확장한다.
- nullable 안전성: `Expense?`를 검사하고 `!`는 이미 검사한 좁은 지점에서만 쓴다. `Nullable`과 경고-오류 처리는 버그를 빌드에서 잡는 안전망이다.
- records와 불변성: command/receipt는 생성 뒤 바뀌지 않는 값으로 다뤄 동시성·전달 실수를 줄인다. 상태가 바뀌는 `Expense`는 규칙 메서드로만 바꾼다.
- Result 대 예외: 없는 요청·이미 처리됨처럼 예상 가능한 업무 실패는 `Result`로 반환한다. 취소, 네트워크 장애, 프로그래밍 오류처럼 정상 흐름이 아닌 실패는 예외로 전파하고 경계에서 기록·복구한다.
- 운영: 실제 승인에는 인증/권한, 금액 단위와 통화, idempotency 키, 감사 로그의 보존·마스킹, timeout/retry, 메트릭·trace·correlation ID가 필요하다. 재시도는 외부 저장이 멱등인지 확인한 뒤 적용한다.

## 실습 자료

- [단계별 과제](./EXERCISES.md)
- [초보자 검증 단계](./CHECKPOINT.md)
- [실행 코드](./src/ExpenseApprovalExercise/Program.cs)

## 버전 업데이트 (2026-07-29 확인)

- 설치된 안정 SDK는 `.NET SDK 10.0.301`이므로 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하는 C# 14 코드로 빌드한다.
- Microsoft의 [.NET 10 새 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원 LTS이다.
- [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되며 extension members, null 조건부 대입, `field` 등을 제공한다고 설명한다.
- 공식 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)에는 extension indexers, unions 지원 형식, 비동기 검증 등이 소개되어 있다. 로컬에는 Preview 7 SDK가 있지만 미리 보기 API와 SDK는 변경될 수 있으므로 오늘의 실행 예제에는 쓰지 않았다.

## 5분 복습 체크리스트

- [ ] `var`, `decimal`, `if`, `foreach`가 예제에서 하는 일을 설명할 수 있다.
- [ ] `Expense?`를 검사하는 이유와 nullable 경고의 가치를 말할 수 있다.
- [ ] record의 값 중심 성격과 Domain Model의 상태 규칙을 구분할 수 있다.
- [ ] LINQ와 `async`/`await`/`CancellationToken`의 목적을 설명할 수 있다.
- [ ] 예상 가능한 업무 실패에는 Result, 비정상 실패에는 예외를 쓰는 기준을 말할 수 있다.
- [ ] DI, SOLID, Application Service, Domain Model, Repository, Strategy, Composition Root가 연결되는 방식을 설명할 수 있다.
