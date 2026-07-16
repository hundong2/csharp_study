# 2026-07-16 C# 기초와 비용 승인 파이프라인 실습

오늘의 목표는 **C#을 처음 배우는 개발자가 기본 문법을 읽고, 비용 신청이 여러 검증 규칙을 통과해 저장되는 흐름을 직접 실행하며 이해하는 것**입니다. 실무에서 자주 쓰는 의존성 주입, 책임 분리, 검증 파이프라인(Chain of Responsibility), Repository 패턴을 작은 콘솔 앱에 담았습니다.

실행 예제는 이 컴퓨터에 설치된 안정 SDK `.NET 10.0.301`에 맞춰 `net10.0`과 C# 14를 사용합니다. .NET 11 / C# 15 Preview 기능은 아래 버전 업데이트에서만 소개하므로 안정 SDK로 그대로 빌드할 수 있습니다.

## 먼저 실행하기

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260716
dotnet run --project .\src\ExpenseApprovalExercise\ExpenseApprovalExercise.csproj
dotnet run --project .\src\ExpenseApprovalExercise\ExpenseApprovalExercise.csproj -- --self-test
```

첫 명령은 문법 예제와 65만원 장비 비용 신청을 실행합니다. 두 번째 명령은 성공, 영수증 누락, 한도 초과, 승인 경로, 저장 여부를 자동 확인합니다. 성공하면 마지막에 `초보자 검증 통과`가 표시됩니다.

## 오늘 배우는 기본 문법

| 문법 | 코드에서 찾을 곳 | 기억할 점 |
| --- | --- | --- |
| 변수와 타입 | `string`, `decimal`, `bool`, `string?` | 돈은 반올림 오차를 줄이기 위해 보통 `decimal`을 사용합니다. `?`는 null 가능성을 표시합니다. |
| 문자열 보간 | `$"{amount:N0}원"` | `$` 문자열 안에서 값을 읽기 쉽게 조합합니다. |
| 조건과 패턴 | `switch` 식, 삼항 연산자 | 값의 범위에 따라 승인 경로를 고릅니다. |
| 반복과 컬렉션 | 배열, `List<T>`, `Dictionary<TKey,TValue>`, `foreach` | 여러 비용과 분류별 한도를 저장하고 순회합니다. |
| 클래스와 record | `ExpenseReport`, `SubmitExpenseCommand` | 상태와 행동이 있는 객체는 class, 값 묶음은 record가 편리합니다. |
| 인터페이스 | `IExpenseRule`, `IExpenseRepository` | 사용하는 쪽이 구체 구현 대신 계약에 의존하게 합니다. |
| 비동기 | `async`, `await`, `ValueTask`, `CancellationToken` | 실제 DB나 외부 API처럼 기다림이 있는 작업의 기본 형태입니다. |
| nullable 검사 | `string?`, `is not null`, `Result<T>` | 값이 없을 수 있음을 컴파일러와 호출자에게 알립니다. |

## 아키텍처 흐름

```text
Program
  -> Composition Root (객체 생성과 연결)
  -> ExpenseService (유스케이스 순서 조정)
     -> IExpenseRule 목록 (입력/금액/영수증/한도 검증)
     -> IApprovalRouter (금액별 승인 경로 결정)
     -> ExpenseReport (도메인 상태)
     -> IExpenseRepository (저장 계약)
```

- **검증 파이프라인 / Chain of Responsibility**: `IExpenseRule` 구현을 차례로 실행하고 첫 실패에서 멈춥니다. ASP.NET Core 미들웨어, 요청 필터, 유효성 검사기에서도 비슷한 구조를 자주 봅니다.
- **의존성 주입(DI)**: `ExpenseService`가 규칙이나 저장소를 직접 `new`하지 않고 생성자로 받습니다. 테스트에서 메모리 저장소나 고정 시계로 쉽게 교체할 수 있습니다.
- **Repository**: 저장 방법을 인터페이스 뒤로 숨깁니다. 오늘은 메모리지만 나중에 EF Core 구현으로 교체할 수 있습니다.
- **Composition Root**: 구현 선택과 객체 연결은 `CompositionRoot.Build()` 한곳에 모읍니다. ASP.NET Core에서는 보통 `Program.cs`의 서비스 등록이 이 역할을 합니다.
- **Result 패턴**: 예상 가능한 입력 실패는 예외 대신 `Result<T>`로 돌려 호출자가 성공과 실패를 명시적으로 처리하게 합니다.

처음에는 `Program.cs`를 위에서 아래로 읽으세요. `BasicSyntaxTour` → `ExpenseApprovalDemo` → `CompositionRoot` → `ExpenseService` → 규칙과 저장소 순서가 가장 이해하기 쉽습니다.

## 파일 안내

| 파일 | 용도 |
| --- | --- |
| [src/ExpenseApprovalExercise/Program.cs](./src/ExpenseApprovalExercise/Program.cs) | 실행 가능한 문법 예제, 아키텍처 구현, 자체 검증 |
| [src/ExpenseApprovalExercise/ExpenseApprovalExercise.csproj](./src/ExpenseApprovalExercise/ExpenseApprovalExercise.csproj) | .NET 10 / C# 14 프로젝트 설정 |
| [EXERCISES.md](./EXERCISES.md) | 작은 변경부터 새 규칙 추가까지 단계별 문제 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 확인 질문과 짧은 복습 체크리스트 |

## 버전 업데이트 (2026-07-16 확인)

- Microsoft의 [.NET 공식 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)에 따르면 .NET 10은 LTS 안정판이며, 현재 패치는 `10.0.9`, 지원 종료일은 2028-11-14입니다. 지원 상태를 유지하려면 최신 패치를 적용해야 합니다. 로컬에는 SDK 10.0.301과 런타임 10.0.9가 설치되어 있어 오늘 예제는 `net10.0`을 대상으로 합니다.
- Microsoft Learn의 [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 extension members, null 조건부 대입, `Span<T>` 변환 등 C# 14의 안정 기능을 설명합니다. 오늘은 기초에 집중해 컬렉션 식과 primary constructor처럼 읽기 쉬운 안정 문법만 사용합니다.
- 2026-07-14의 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)에는 runtime-async 성능 개선, 비동기 DataAnnotations 검증, C# union 지원 타입 내장, extension indexer 등이 포함됐습니다. Preview는 Microsoft 지원 대상이 아니므로 오늘 실행 코드에는 넣지 않았습니다.
- Microsoft Learn의 [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)은 C# 15가 Preview이며 collection expression arguments와 union types를 제공한다고 안내합니다. union은 오늘의 `Result<T>` 같은 성공/실패 모델을 더 직접적으로 표현할 가능성이 있지만, 로컬 안정 SDK에서는 컴파일하지 않으므로 별도 Preview 프로젝트에서만 시험하는 편이 안전합니다.

## 완료 기준

- 일반 실행에서 기본 문법과 비용 승인 결과가 순서대로 출력된다.
- `--self-test`에서 네 가지 검증이 모두 통과한다.
- `IExpenseRule`이 규칙 하나만 책임지는 이유를 말할 수 있다.
- `ExpenseService`가 구체 저장소 대신 인터페이스를 받는 이유를 말할 수 있다.
- [CHECKPOINT.md](./CHECKPOINT.md)의 질문에 코드를 보지 않고 짧게 답할 수 있다.
