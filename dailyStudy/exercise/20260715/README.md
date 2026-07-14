# 2026-07-15 C# 기본기와 실무 티켓 처리 아키텍처 실습

오늘의 목표는 **기초 문법이 약한 개발자도 C# 콘솔 앱을 실행하면서 값 흐름, 타입 설계, 계층 분리, 비동기 호출, 오류 처리를 한 번에 연결해서 이해하는 것**입니다.

예제는 설치된 안정 SDK인 `.NET 10.0.301`에서 실행되도록 `net10.0`과 C# 14를 사용합니다. .NET 11 / C# 15 preview 내용은 버전 업데이트 섹션에서만 별도로 설명하며, 오늘 실행 코드에는 preview 문법을 넣지 않았습니다.

## 실행 방법

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260715
dotnet run --project .\src\SupportTicketExercise\SupportTicketExercise.csproj
dotnet run --project .\src\SupportTicketExercise\SupportTicketExercise.csproj -- --self-test
```

첫 번째 명령은 문법 투어와 티켓 생성 데모를 실행합니다. 두 번째 명령은 초보자가 놓치기 쉬운 검증, 담당자 배정, 저장, 실패 처리 흐름을 자동으로 확인합니다.

## 오늘 배우는 것

| 구분 | 핵심 내용 | 실무 연결 |
| --- | --- | --- |
| 기본 문법 | 변수, 조건문, 반복문, 배열, `List<T>`, `Dictionary<TKey, TValue>`, nullable | API 입력값과 업무 데이터를 안전하게 다루기 |
| 타입 설계 | `class`, `record`, `enum`, 확장 메서드, 인터페이스 | 변경되는 업무 객체와 값 객체 구분 |
| 아키텍처 | Domain / Application / Infrastructure / Composition Root | 테스트 가능한 서비스 구조 만들기 |
| 비동기 | `async`, `await`, `ValueTask`, `CancellationToken` | 저장소, 외부 API, 큐 작업 호출 모델 이해 |
| 오류 처리 | 예외 대신 `Result<T>`로 예상 가능한 실패 표현 | 사용자에게 설명 가능한 실패 메시지 전달 |
| 버전 감각 | .NET 10 LTS, C# 14 안정 기능, .NET 11 / C# 15 preview | 안정 기능과 preview 기능을 분리해서 판단 |

## 파일 구성

| 파일 | 용도 |
| --- | --- |
| [src/SupportTicketExercise/Program.cs](./src/SupportTicketExercise/Program.cs) | 실행 가능한 학습 예제와 자체 검증 코드 |
| [src/SupportTicketExercise/SupportTicketExercise.csproj](./src/SupportTicketExercise/SupportTicketExercise.csproj) | .NET 10 콘솔 프로젝트 설정 |
| [EXERCISES.md](./EXERCISES.md) | 직접 값을 바꾸며 확인하는 실습 문제 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 검증 질문과 최종 리뷰 체크리스트 |

## 실무 아키텍처 요약

오늘 예제는 초보자가 한 화면에서 흐름을 읽기 쉽도록 한 파일에 작성했습니다. 실제 서비스에서는 아래 책임으로 나눕니다.

1. Domain: `Ticket`, `SupportAgent`, `SeverityRules`, `TicketStatus`
2. Application: `TicketService`, `CreateTicketCommand`, `Result<T>`
3. Infrastructure: `AgentDirectory`, `BusinessClock`, `InMemoryTicketRepository`
4. Composition Root: `DemoCompositionRoot.Build()`

Domain은 업무 규칙을 담습니다. Application은 입력 검증, 담당자 조회, 티켓 생성, 저장 순서를 조립합니다. Infrastructure는 나중에 DB, 외부 담당자 API, 실제 시간 서비스로 교체될 구현입니다. 이 분리를 이해하면 ASP.NET Core API, worker service, desktop app에서도 같은 생각으로 코드를 나눌 수 있습니다.

## 버전 업데이트

- Microsoft .NET Blog의 `Announcing .NET 10` 글에 따르면 .NET 10은 LTS 릴리스이며 2028-11-10까지 3년간 지원됩니다. 따라서 오늘 실습은 안정 실행을 위해 .NET 10을 기준으로 작성했습니다. 출처: [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- Microsoft Learn의 C# 14 문서는 C# 14의 주요 기능으로 extension members, null-conditional assignment, unbound generic type의 `nameof`, `Span<T>` / `ReadOnlySpan<T>` 암시적 변환, lambda parameter modifier, field-backed properties 등을 설명합니다. 오늘 예제는 초보자에게 안정적인 기본기를 우선 전달하기 위해 확장 메서드와 C# 14 호환 프로젝트 설정만 사용했습니다. 출처: [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- Microsoft Learn의 언어 버전 문서는 target framework가 기본 C# 언어 버전을 정한다고 설명합니다. `net10.0`은 C# 14가 기본이므로 프로젝트도 `LangVersion`을 `14.0`으로 맞췄습니다. 출처: [C# language versioning](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning)
- .NET Blog의 최신 preview 글 기준으로 .NET 11 Preview 5는 2026-06-09에 공개되었고, C# preview 영역에는 closed class hierarchies, union declarations and union patterns, unsafe evolution 등이 포함됩니다. 이 기능들은 오늘 로컬 안정 SDK 빌드 대상에 넣지 않고 비교 학습용으로만 다룹니다. 출처: [.NET 11 Preview 5 is now available](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-5/)
- Microsoft Learn의 C# 15 문서는 C# 15가 preview이며 .NET 11 preview SDK 또는 Visual Studio 2026 Insiders로 시도하라고 안내합니다. 주요 preview 항목은 collection expression arguments, union types, closed hierarchies, memory safety입니다. 출처: [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## Preview 기능을 실무에서 보는 법

C# 15 union types는 `Result<T>`처럼 성공/실패가 명확한 모델을 더 직접적으로 표현할 가능성이 있습니다. 하지만 preview 기능은 문법, 컴파일러 진단, IDE 지원이 바뀔 수 있으므로 운영 코드에 바로 넣기보다 별도 브랜치나 샘플 프로젝트에서 검증하는 편이 안전합니다.

오늘은 안정 기능만으로 같은 문제를 해결합니다. `Result<T>` record를 사용해 실패를 값으로 돌려주고, 호출자는 `IsSuccess`를 확인한 뒤 `Value` 또는 `Error`를 읽습니다.

## 완료 기준

- `dotnet run`으로 문법 투어와 티켓 처리 데모가 실행됩니다.
- `dotnet run -- --self-test`가 모든 검증을 통과합니다.
- `CHECKPOINT.md`의 기본 문법, 타입 설계, 아키텍처, async, 오류 처리 질문에 말로 답할 수 있습니다.
- `EXERCISES.md`의 1단계와 2단계를 직접 수정해 보고 결과 차이를 확인했습니다.
