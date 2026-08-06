# 2026-08-07: 배치 실패 재시도로 배우는 C#과 .NET 설계

## 초보자 읽기 순서

1. **실행하기** 명령을 그대로 따라 정상 출력을 확인합니다.
2. `Program.cs` 위쪽의 `FailedJob`, `Result<T>`, 인터페이스를 차례로 읽습니다.
3. `ExponentialRetryPolicy`에서 한 작업의 판단 과정을 손으로 따라갑니다.
4. `ProcessFailedJobsService`에서 조회 → 판단 → 재시도/격리 흐름을 읽습니다.
5. `--self-test`를 실행하고 `EXERCISES.md`의 초급부터 수정합니다.
6. 막히면 `CHECKPOINT.md`로 최소 개념을 다시 확인합니다.

패턴 이름을 먼저 외우기보다 **데이터**, **업무 규칙**, **작업 순서**, **외부 연결**이 분리된 이유를 찾으세요.

## 오늘 만들 것

실패한 배치 작업을 오래된 순서로 읽어 일시 오류는 지수 백오프로 재시도하고, 영구 오류·횟수 초과·잘못된 데이터는 수동 검토 대상으로 격리합니다. Application Service, Domain Model, Repository, Strategy, DI, Composition Root를 작은 콘솔 앱에서 연결합니다.

## 실행하기

설치된 안정 SDK(.NET 10.0.301)에 맞춰 `net10.0`을 대상으로 합니다.

```powershell
cd dailyStudy/exercise/20260807/src/BatchRetryExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

예상 핵심 출력:

```text
JOB-101 invoice-export: 20초 후 재시도
처리 완료: 재시도 1건, 격리 2건
self-test: 4/4 통과
```

## 첫 문법 안내

- `var result = ...;`: 오른쪽 값에서 지역 변수 타입을 추론합니다. 정적 타입 검사는 유지됩니다.
- `new FailedJob(...)`: `new`는 객체를 만듭니다. 문맥상 타입이 분명하면 `new(...)`처럼 이름을 생략할 수 있습니다.
- `string? Name`: `?`는 null 가능성을 타입에 기록합니다. `IsNullOrWhiteSpace` 검사 후 사용합니다.
- `if`, `else`, `continue`: 조건에 따라 실행하며, `continue`는 현재 반복의 나머지를 건너뜁니다.
- `enum`: 가능한 상태를 이름 있는 제한된 값으로 표현해 잘못된 문자열을 줄입니다.
- `record`: 값 중심 데이터에 적합하며 변경을 줄여 추적과 동시성 처리를 돕습니다.
- `interface`: 구현이 지켜야 할 계약입니다. 메모리 저장소를 DB 저장소로 교체할 수 있습니다.
- `Task<T>`와 `await`: 비동기 작업 완료 뒤 `T` 값을 얻는 문법입니다.
- `Result<T>`의 `<T>`: 여러 타입에 같은 성공/실패 구조를 재사용하는 제네릭입니다.
- `jobs.OrderBy(...).ToArray()`: LINQ로 정렬하고, 지연 실행 결과를 배열로 한 번 확정합니다.

## 설계 지도

```text
Program (Composition Root)
  └─ ProcessFailedJobsService (Application Service)
       ├─ IJobRepository → InMemoryJobRepository
       ├─ IRetryPolicy   → ExponentialRetryPolicy (Strategy)
       └─ IJobDispatcher → ConsoleJobDispatcher
```

- **Domain Model**: `FailedJob`, `RetryDecision`이 업무 사실과 판단 결과를 표현합니다.
- **Repository**: 실패 작업 조회를 저장 기술에서 분리합니다.
- **Strategy**: 재시도 정책을 교체 가능하게 만듭니다.
- **Application Service**: 유스케이스 순서만 조정합니다.
- **Composition Root**: 시작점 한 곳에서 구현을 조립합니다.
- **DI와 SOLID**: 구체 구현 대신 계약을 생성자로 받아 SRP, OCP, DIP와 테스트 가능성을 돕습니다.

## 중급·고급 선택의 이유

### nullable 안전성과 불변 record

외부 작업 데이터의 이름이 누락될 수 있어 `string?`로 현실을 드러냅니다. record는 처리 중 값 변경을 줄입니다. 단, record 내부에 변경 가능한 컬렉션이 있다면 컬렉션까지 자동 불변이 되지는 않습니다.

### LINQ

`OrderBy(...).ToArray()`는 정렬 의도와 실행 시점을 분명히 합니다. 대량 작업은 전부 메모리에 올리지 말고 Repository에서 페이지 단위로 읽거나 DB에서 정렬해야 합니다.

### async/await와 취소

실제 저장소와 메시지 큐는 I/O입니다. `CancellationToken`을 끝까지 전달하면 배포·종료 시 빠르게 멈출 수 있습니다. `OperationCanceledException`은 실패로 번역하지 않고 다시 던져 상위 제어 흐름을 보존합니다.

### 예외와 Result

이름 누락처럼 예상 가능한 업무 검증 실패는 `Result<T>`로 처리합니다. 연결 단절처럼 정상 경로로 복구하기 어려운 기술 장애는 예외로 두고 서비스 경계에서 안전한 실패로 번역합니다.

### 테스트 가능성과 운영 관심사

생성자 주입으로 저장소와 실행기를 가짜 구현으로 바꿔 빠른 테스트를 만듭니다. 운영에서는 구조화 로그, 처리량·재시도·격리 메트릭, 분산 추적, 타임아웃, 최대 지연, 무작위 jitter, 격리 큐 알림이 필요합니다. 재시도는 작업 ID 기반 멱등성을 확보한 뒤 적용해야 중복 청구 같은 사고를 막습니다.

## 초보자 검증 단계

- `dotnet build`가 경고 0개, 오류 0개인가?
- 일반 실행에서 재시도 1건, 격리 2건인가?
- `dotnet run -- --self-test`가 `4/4 통과`인가?
- 실패하면 첫 오류의 파일명과 줄 번호를 먼저 읽었는가?

## 버전 업데이트 (2026-08-07 확인)

- **안정 실행 기준**: C# 14는 .NET 10에서 지원되는 최신 안정 C#이고, .NET 10은 3년 지원되는 LTS입니다. 오늘 코드는 로컬 안정 SDK 10.0.301로 컴파일합니다.
- **미리 보기 분리**: .NET 11 Preview 6과 C# 15 후보 기능은 아직 preview이므로 실행 코드에 넣지 않았습니다. 별도 실험 프로젝트와 preview SDK에서만 평가하세요.
- 공식 자료: [C# 14의 새로운 기능](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14), [.NET 10의 새로운 기능](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview), [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 오늘의 짧은 복습 체크리스트

- [ ] nullable 표기와 검사 이유를 말할 수 있다.
- [ ] record, enum, 제네릭의 역할을 설명할 수 있다.
- [ ] LINQ 실행 시점과 async 취소 전파를 설명할 수 있다.
- [ ] 예상 실패는 Result, 기술 장애는 예외로 나눈 이유를 말할 수 있다.
- [ ] Repository, Strategy, Application Service, Composition Root를 짚을 수 있다.
- [ ] DI와 SOLID가 테스트 가능성을 높이는 방식을 말할 수 있다.
- [ ] 백오프, jitter, 멱등성, 로그·메트릭이 필요한 이유를 말할 수 있다.

다음: [`EXERCISES.md`](./EXERCISES.md) · 막힐 때: [`CHECKPOINT.md`](./CHECKPOINT.md) · 소스: [`Program.cs`](./src/BatchRetryExercise/Program.cs)
