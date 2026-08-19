# 2026-08-20: 서비스 장애 우선순위와 담당 팀 배정

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 먼저 읽습니다.
2. [`Program.cs`](./src/IncidentTriageExercise/Program.cs)를 위에서 아래로 읽으며 `조립 → 실행 → 모델 → 계약 → 구현 → 테스트` 흐름을 찾습니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 1단계부터 한 번에 하나씩 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 설명하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260820/src/IncidentTriageExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 취급해 null 위험을 컴파일 단계에서 찾습니다.

## 기초 문법 지도

- `var repository = ...;`에서 `var`는 오른쪽 값으로 변수 형식을 추론하고, 문장은 보통 `;`로 끝납니다.
- `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. `IsNullOrWhiteSpace`는 null·빈 문자열·공백을 함께 검사합니다.
- `int`는 정수, `bool`은 참/거짓, `enum`은 제한된 이름 목록입니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주거나 메서드를 끝냅니다.
- `record`는 값 중심의 불변 데이터에, `class`는 행동과 변경 가능한 상태를 묶을 때 알맞습니다.
- `interface`는 필요한 행동의 계약입니다. 구현 교체가 쉬워져 테스트와 확장에 유리합니다.
- `Task<T>`, `async`, `await`는 DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `Count`는 정렬과 집계를 의도에 가깝게 표현합니다.
- 이 예제의 `Result<T>`에는 성공 값 또는 오류가 들어갑니다. `IsSuccess`를 확인한 뒤 값을 사용합니다.

## 설계를 읽는 방법

실행 흐름은 `Program`(Composition Root) → `TriageIncidentsService`(Application Service) → `IIncidentPriorityPolicy`(Strategy)와 Repository 순서입니다. 생성자 주입은 서비스가 구체 클래스가 아닌 인터페이스에 의존하게 하는 DI/DIP 방식입니다. 정책과 저장소를 테스트 대역으로 바꿀 수 있습니다.

`Incident`와 `IncidentAssignment`는 Domain Model입니다. record와 읽기 전용 결과 목록은 처리 중 데이터가 뜻밖에 바뀌는 일을 줄입니다. Application Service는 사용 사례의 순서만 맡아 SRP를 지키고, 새 우선순위 정책은 Strategy 구현 추가로 확장해 OCP를 따릅니다. 이것이 실무에서 SOLID를 작게 적용하는 방식입니다.

열린 장애가 없는 경우처럼 예상 가능한 업무 실패는 `Result<T>`로 명시합니다. 반면 취소, 저장소 장애, 코드 계약 위반처럼 정상 분기로 보기 어려운 실패는 예외가 적합합니다. 모든 실패를 예외로 만들면 운영 경보가 시끄러워지고, 모든 예외를 Result로 바꾸면 장애의 스택 정보가 손실될 수 있습니다.

메모리 Repository는 학습용입니다. 실제 운영에서는 장애 ID에 고유 제약을 두고 저장을 트랜잭션으로 처리해 동시 분류 충돌을 막아야 합니다. 재시도에는 같은 결과를 한 번만 반영하는 멱등성이 필요합니다. 알림이 필요하면 DB 변경과 함께 Outbox에 기록하는 방식을 고려합니다. `CancellationToken`은 끝까지 전달하고, 로그·추적에는 사고 ID, 우선순위, 처리 시간은 남기되 고객 정보와 상세 장애 설명은 제외합니다. 처리 지연, 실패율, 즉시 대응 건수에 경보를 설정해야 운영이 가능합니다.

## 버전 업데이트 (2026-08-20 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 이 자료는 로컬의 안정 SDK 10.0.301에서 컴파일되는 기능만 사용합니다. Microsoft 다운로드 페이지는 최신 패치와 SDK를 제공하므로 운영 환경은 보안 패치를 정기적으로 확인하세요.
- 최신 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. Preview 7에는 SDK 테스트 옵션과 런타임·라이브러리 개선이 포함되며, C# 15에는 union types, closed hierarchies, extension indexers, labeled `break`/`continue` 등이 소개됐습니다. 현재 로컬 안정 SDK로 컴파일되지 않으므로 실행 코드에는 넣지 않았습니다. 미리보기 기능은 사양이 바뀔 수 있어 별도 실험 프로젝트에서 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 11 Preview 7 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] `string?`, record, LINQ, `async`/`await`, `Result<T>`를 첫 사용 위치에서 설명할 수 있다.
- [ ] Repository, Strategy, Application Service, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체 및 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 멱등성, 동시성, 취소, Outbox, 관측성의 운영 이유를 말할 수 있다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
