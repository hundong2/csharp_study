# 2026-07-26 C# 고객 지원 티켓 SLA 분류 실습

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자동 검증을 먼저 통과합니다.
2. `Program.cs`의 맨 위 실행 코드 → enum과 record → Result → Strategy → Repository → Application Service 순서로 읽습니다.
3. `EXERCISES.md`를 한 단계씩 수정하고 매번 검증합니다.
4. 마지막에 `CHECKPOINT.md`를 코드 없이 답합니다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260726
dotnet run --project .\src\TicketTriageExercise
dotnet run --project .\src\TicketTriageExercise -- --self-test
```

## 처음 만나는 기본 문법

| 문법 | 첫 사용 | 의미와 이유 |
| --- | --- | --- |
| 변수와 생성 | `var service = new ...` | 값을 이름으로 보관합니다. `var`를 써도 실제 타입은 컴파일 때 고정됩니다. |
| 조건과 반복 | `if`, `foreach` | 조건에 따라 분기하고 여러 요청을 하나씩 처리합니다. |
| enum과 switch 식 | `Severity`, `severity switch` | 허용된 상태를 제한하고 각 상태를 값으로 변환합니다. |
| 메서드와 반환값 | `CreateAsync`, `Result<T>` | 입력을 받아 한 책임을 수행하고 결과를 호출자에게 돌려줍니다. |
| nullable | `string?`, `?.` | 값이 없을 가능성을 타입에 드러내고 사용 전에 검사합니다. |
| record와 불변성 | `record`, `init`, `required` | 값 중심 데이터를 비교하기 쉽고 생성 뒤 변경을 제한합니다. |
| 컬렉션과 LINQ | 배열, `Any`, `OrderBy` | 여러 값을 저장하고 검색·정렬 의도를 간결하게 표현합니다. |
| async/await | `Task`, `await` | DB나 네트워크 대기 중 스레드를 붙잡지 않으며 취소 신호도 전달합니다. |

## 실무 구조와 설계 선택

```text
Program (Composition Root)
  └─ TicketApplicationService (사용 사례 순서)
      ├─ Ticket (Domain Model과 유효성)
      ├─ ITriageStrategy (변경되는 SLA 정책)
      ├─ ITicketRepository (저장 기술 경계)
      └─ IAuditLog (운영 관찰 경계)
```

- 맨 위 조립부가 Composition Root입니다. 구현 선택을 한곳에 모은 DI로 서비스는 인터페이스에만 의존하며 가짜 저장소와 로그로 테스트할 수 있습니다.
- Domain Model은 유효한 티켓 생성 규칙을 지키고 Application Service는 중복 확인→생성→저장→감사라는 흐름만 조정합니다. 책임 하나에 집중하는 SRP입니다.
- Strategy는 SLA 정책 추가 시 기존 서비스 변경을 줄여 OCP를, Repository와 로그 인터페이스는 상위 정책이 세부 기술에 의존하지 않게 하여 DIP를 보여 줍니다.
- 예상 가능한 빈 제목·중복은 Result로 처리합니다. 취소, DB 단절, 프로그래밍 오류처럼 중앙 로깅·재시도 판단이 필요한 비정상 실패는 예외가 적합합니다.
- 불변 record는 동시 처리 중 뜻밖의 변경을 줄이고, LINQ는 큐 정렬 규칙을 선언적으로 표현합니다.

운영에서는 제목 중복을 DB unique 제약과 트랜잭션으로 보장하고, 외부 알림은 timeout·지수 backoff·최대 재시도·outbox를 둡니다. 로그에는 correlation ID와 오류 코드를 넣되 개인정보는 제외합니다. 티켓 처리 시간, SLA 위반률, 오류율을 메트릭과 trace로 관찰하고 시간은 UTC로 저장합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 확인 단계](./CHECKPOINT.md)
- [실행 코드](./src/TicketTriageExercise/Program.cs)

## 버전 업데이트 (2026-07-26 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`이며 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하고 C# 14로 빌드합니다.
- Microsoft의 [.NET 10 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS 안정 릴리스입니다.
- [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어이며 extension members, null 조건부 할당, `field` 지원 등을 포함한다고 설명합니다.
- 공식 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)는 2026-07-14 공개된 미리 보기입니다. 로컬에 .NET 11 SDK가 없고 preview API·C# 15 기능은 바뀔 수 있으므로 오늘 실행 코드에는 넣지 않았습니다.

## 5분 복습 체크리스트

- [ ] nullable 검사와 record 불변성의 목적을 설명한다.
- [ ] LINQ, async/await, CancellationToken의 쓰임을 설명한다.
- [ ] Result와 예외의 선택 기준을 설명한다.
- [ ] Application Service, Domain Model, Repository, Strategy, DI, Composition Root를 연결한다.
- [ ] SRP·OCP·DIP가 테스트 가능성에 주는 이점을 설명한다.
- [ ] 중복, 트랜잭션, timeout, 재시도, outbox, UTC, 로그·메트릭·trace를 말할 수 있다.
