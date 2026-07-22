# 2026-07-23 C# 배치 작업 예약 실습

작은 작업 예약 프로그램을 실행하며 기초 문법부터 실무 구조와 운영 안전성까지 연결하는 입문 자료입니다.

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자동 검증을 먼저 통과시킵니다.
2. `Program.cs`의 실행 코드와 `[기초]` 주석을 읽습니다.
3. `ScheduledJob` → `Result<T>` → Strategy/Repository → `JobScheduler` 순서로 책임 분리를 따라갑니다.
4. `EXERCISES.md`를 1번부터 수행하고 매번 `--self-test`를 실행합니다.
5. 마지막에 `CHECKPOINT.md` 질문을 코드 없이 답합니다.

## 실행

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260723
dotnet run --project .\src\JobSchedulerExercise
dotnet run --project .\src\JobSchedulerExercise -- --self-test
```

마지막 줄이 `초보자 검증 통과 (4/4)`이면 시작 코드가 정상입니다.

## 처음 만나는 문법

| 문법 | 첫 사용 | 뜻과 이유 |
| --- | --- | --- |
| 변수와 타입 | `var jobs`, `string Name` | 변수는 값에 이름과 타입을 붙입니다. `var`도 컴파일 때 타입이 고정됩니다. |
| 조건과 반복 | `if`, `switch`, `foreach` | 조건에 따라 분기하고 여러 항목을 순회합니다. |
| nullable | `T?`, `string?`, `?.`, `??` | 값이 없을 가능성을 타입으로 드러내고 안전하게 처리합니다. |
| record와 불변성 | `record`, `with` | 값 중심 객체를 원본 변경 없이 복사해 예측 가능하게 공유합니다. |
| LINQ | `Any`, `Count` | 컬렉션 질문을 “존재하는가”, “몇 개인가”처럼 의도 중심으로 표현합니다. |
| 비동기 | `Task`, `async`, `await` | DB·네트워크 대기 중 스레드를 점유하지 않아 서버 자원을 효율적으로 씁니다. |

## 실무 설계 지도

```text
Program → JobScheduler (Application Service)
              ├─ ScheduledJob (Domain Model: 검증)
              ├─ IDispatchDelayStrategy (Strategy: 지연 정책)
              └─ IJobRepository (Repository: 저장 계약)
Composition Root가 구현을 선택하고 생성자 주입(DI)으로 연결
```

- `ScheduledJob`은 자기 상태의 유효성을 책임지고, `record`와 `with`로 불변에 가까운 변경을 만듭니다.
- 예상 가능한 중복·입력 오류는 `Result<T>`로, null 계약 위반이나 취소처럼 정상 흐름으로 처리하기 어려운 상황은 예외로 구분합니다.
- `JobScheduler`는 사용 사례 순서만 조정하는 Application Service입니다. 책임 하나에 집중하는 SRP를 적용합니다.
- Repository와 Strategy 인터페이스에 의존하는 DIP 덕분에 DB·정책을 교체할 수 있고, 확장은 구현 추가로 처리하는 OCP에 가까워집니다.
- Composition Root에서만 객체를 조립하는 DI는 고정 시계와 메모리 저장소를 주입할 수 있어 빠르고 결정적인 테스트를 만듭니다.

실제 운영에서는 메모리 중복 검사만 믿으면 안 됩니다. DB unique 제약과 트랜잭션으로 경쟁 조건을 막고, 취소 토큰과 제한된 재시도를 전파해야 합니다. 로그에는 작업 ID와 결과를 남기되 민감한 입력은 제외하고, 예약 성공률·지연·실패 원인 메트릭과 경보를 준비합니다. 시간은 UTC로 저장하고 표시할 때만 사용자 시간대로 변환합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 확인 단계](./CHECKPOINT.md)
- [실행 코드](./src/JobSchedulerExercise/Program.cs)

## 버전 업데이트 (2026-07-23 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`, 런타임은 `10.0.9`입니다. 예제는 `net10.0`, nullable 경고 활성화, C# 14 기본 버전으로 빌드합니다.
- Microsoft의 [.NET 10 소개](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)와 [.NET 10 발표](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)에 따르면 .NET 10은 2025년 11월 출시된 LTS이며 2028년 11월 10일까지 지원됩니다.
- [C# 14의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어 버전이며 extension members, null 조건부 대입 등을 포함한다고 설명합니다.
- [.NET 11 Preview 5](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-5/)는 2026년 6월 공개된 미리 보기입니다. 로컬에 .NET 11 SDK가 없으므로 C# 15/.NET 11 전용 기능은 실행 예제에 섞지 않았습니다.

## 짧은 복습 체크리스트

- [ ] 일반 실행과 `--self-test`가 모두 성공한다.
- [ ] nullable, record, LINQ, async/await를 왜 쓰는지 설명할 수 있다.
- [ ] Result와 예외의 선택 기준을 예로 말할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy, DI, SOLID, Composition Root의 역할을 연결할 수 있다.
- [ ] DB 동시성, UTC, 취소, 재시도, 로그와 메트릭이 필요한 이유를 설명할 수 있다.
