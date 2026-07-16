# 2026-07-17 C# 기초와 사용자 알림 Strategy 실습

오늘의 목표는 **C#을 처음 배우는 개발자가 기본 문법을 읽고, 이메일과 SMS 중 알맞은 발송 전략을 선택해 결과를 저장하는 흐름을 직접 실행하며 이해하는 것**입니다. 작은 콘솔 앱 안에 실무에서 자주 쓰는 Strategy, 의존성 주입, Application Service, Repository, Composition Root, Result 패턴을 담았습니다.

실행 예제는 이 컴퓨터에 설치된 안정 SDK `.NET 10.0.301`에 맞춰 `net10.0`과 C# 14를 사용합니다. 2026-07-14에 나온 .NET 11 Preview 6과 C# 15 기능은 아래 버전 업데이트에서만 소개하므로 안정 SDK로 그대로 빌드할 수 있습니다.

## 먼저 실행하기

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260717
dotnet run --project .\src\NotificationExercise\NotificationExercise.csproj
dotnet run --project .\src\NotificationExercise\NotificationExercise.csproj -- --self-test
```

첫 명령은 기본 문법 예제와 비밀번호 재설정 이메일 발송 흐름을 보여 줍니다. 두 번째 명령은 정상 이메일, 잘못된 이메일, SMS 전략, 등록되지 않은 Push 전략, 발송 기록 저장을 자동으로 확인합니다. 성공하면 마지막에 `초보자 검증 통과`가 표시됩니다.

## 오늘 배우는 기본 문법

| 문법 | 코드에서 찾을 곳 | 기억할 점 |
| --- | --- | --- |
| 변수와 타입 | `string`, `int`, `bool`, `string?` | 타입은 값의 종류를 정하고, `?`는 null일 수 있음을 나타냅니다. |
| 문자열 보간 | `$"{userName}: {unreadCount}개"` | `$` 문자열 안에 값을 넣어 읽기 좋은 메시지를 만듭니다. |
| 조건과 패턴 | `if`, 삼항 연산자, `switch` 식 | 단순 조건은 `if`, 여러 값 분기는 `switch`가 읽기 쉽습니다. |
| 반복과 컬렉션 | 배열, `List<T>`, `Dictionary<TKey,TValue>`, `foreach` | 같은 타입의 여러 값을 저장하고 하나씩 처리합니다. |
| enum | `NotificationChannel`, `NotificationKind` | 제한된 선택지를 문자열 대신 컴파일러가 확인할 수 있는 값으로 표현합니다. |
| class와 record | `NotificationService`, `SendNotificationCommand` | 행동과 의존성이 있는 객체는 class, 값 묶음은 record가 편리합니다. |
| 인터페이스 | `INotificationSender`, `INotificationLogRepository` | 구현이 아니라 계약에 의존하면 교체와 테스트가 쉬워집니다. |
| LINQ와 람다 | `FirstOrDefault(candidate => ...)` | 목록에서 조건에 맞는 첫 전략을 찾습니다. 결과가 없을 수 있어 nullable입니다. |
| 비동기 | `async`, `await`, `ValueTask`, `CancellationToken` | 외부 API나 DB처럼 기다리는 작업을 막지 않고 처리하는 기본 형태입니다. |

처음 읽을 때는 `BasicSyntaxTour`의 각 변수 값을 바꾸고 출력을 예상해 보세요. 그다음 `NotificationDemo` → `CompositionRoot` → `NotificationService` → 발송기 → 저장소 순서로 읽으면 호출 흐름이 보입니다.

## 아키텍처 흐름

```text
Program
  -> CompositionRoot (구현 선택과 객체 연결)
  -> NotificationService (유스케이스 순서 조정)
     -> 입력 검증
     -> IEnumerable<INotificationSender>에서 Strategy 선택
     -> NotificationTemplate로 메시지 작성
     -> 선택한 발송기 실행
     -> INotificationLogRepository에 결과 기록
```

- **Strategy 패턴**: `EmailSender`와 `SmsSender`는 같은 `INotificationSender` 계약을 구현합니다. 서비스는 긴 `if/else`로 채널별 세부 동작을 품지 않고 목록에서 맞는 전략을 고릅니다.
- **의존성 주입(DI)**: `NotificationService`는 발송기, 템플릿, 저장소, 시계를 생성자로 받습니다. 실제 이메일 API나 EF Core 저장소로 바꿔도 서비스 흐름은 그대로 유지할 수 있습니다.
- **Application Service**: 입력 검사 → 전략 선택 → 발송 → 기록이라는 한 유스케이스의 순서를 조정합니다. 채널별 전송 세부 사항이나 저장 기술은 직접 구현하지 않습니다.
- **Repository 패턴**: 발송 기록 저장 계약을 `INotificationLogRepository`로 분리합니다. 오늘은 메모리 구현이지만 실제 앱에서는 EF Core 같은 구현으로 교체할 수 있습니다.
- **Composition Root**: 구현을 선택하고 연결하는 코드를 `CompositionRoot.Build()` 한곳에 둡니다. ASP.NET Core에서는 보통 `Program.cs`의 `builder.Services` 등록이 이 역할을 합니다.
- **Result 패턴**: 잘못된 입력이나 미등록 채널처럼 예상 가능한 실패는 예외 대신 `Result<T>`로 반환합니다. 호출자는 `IsSuccess`를 확인해 성공과 실패를 분명하게 처리합니다.

## 파일 안내

| 파일 | 용도 |
| --- | --- |
| [src/NotificationExercise/Program.cs](./src/NotificationExercise/Program.cs) | 실행 가능한 문법 예제, 알림 아키텍처, 자체 검증 |
| [src/NotificationExercise/NotificationExercise.csproj](./src/NotificationExercise/NotificationExercise.csproj) | .NET 10 / C# 14 프로젝트 설정 |
| [EXERCISES.md](./EXERCISES.md) | 값 변경부터 Push 전략 추가까지 단계별 실습 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 실행 확인, 말로 답하는 질문, 복습 체크리스트 |

## 버전 업데이트 (2026-07-17 확인)

- 2026-07-14 Microsoft의 [.NET 7월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/)에 따르면 안정 LTS인 .NET 10의 최신 런타임 패치는 `10.0.10`이며 보안·비보안 수정이 포함됩니다. 로컬 안정 SDK는 `10.0.301`이고 프로젝트 대상은 `net10.0`입니다. 로컬 패치가 최신이 아니어도 이 예제 문법은 컴파일되지만, 실제 개발 환경은 최신 보안 패치로 갱신해야 합니다.
- [.NET 10 다운로드 페이지](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)는 .NET 10을 안정 LTS로 제공하며 SDK 10.0.301 계열을 안내합니다. 오늘 예제는 이 설치된 안정 SDK에서 C# 14로 빌드하고 실행했습니다.
- 2026-07-14의 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)에는 비동기 DataAnnotations 검증, C# union 지원 타입 내장, extension indexer, 테스트 CLI 개선 등이 포함됩니다. Preview는 운영용 안정판이 아니며 로컬에 .NET 11 SDK도 없으므로 실행 예제에는 넣지 않았습니다.
- Microsoft Learn의 [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)은 C# 15를 Preview로 안내하며 union types와 collection expression arguments를 소개합니다. union은 오늘의 `Result<T>` 같은 여러 경우의 모델링을 더 직접적으로 표현할 수 있지만, 안정 SDK에서 컴파일되지 않으므로 별도 Preview 환경에서만 실험하세요.

## 완료 기준

- 일반 실행에서 기본 문법과 이메일 발송 결과가 순서대로 출력된다.
- `--self-test`에서 네 시나리오와 저장 여부가 모두 통과한다.
- `INotificationSender` 구현을 추가할 때 `NotificationService`를 바꾸지 않아도 되는 이유를 설명할 수 있다.
- `NotificationService`가 저장소와 시계를 생성자로 받는 이유를 설명할 수 있다.
- [CHECKPOINT.md](./CHECKPOINT.md)의 질문에 코드를 보지 않고 짧게 답할 수 있다.
