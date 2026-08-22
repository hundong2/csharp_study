# 2026-08-23: 주문 취소 보상 계획

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 읽습니다.
2. [`Program.cs`](./src/OrderCancellationExercise/Program.cs)를 `조립 → 실행 → 모델 → 계약 → 서비스 → 구현 → 테스트` 순서로 읽습니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)를 1단계부터 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260823/src/OrderCancellationExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용하며 Nullable 경고도 오류로 처리합니다.

## 기초 문법 지도

- `var now = ...;`에서 `var`는 오른쪽 값으로 형식을 추론하고 문장은 보통 `;`로 끝납니다.
- `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. Nullable 분석은 null 오류를 컴파일 시점에 줄입니다.
- `decimal`은 금액, `bool`은 참/거짓, `enum`은 제한된 이름 목록, `DateTimeOffset`은 시간대가 포함된 시각에 적합합니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주거나 메서드를 끝냅니다.
- `record`는 값 중심 불변 데이터, `class`는 행동과 상태를 묶는 객체, `interface`는 교체 가능한 행동의 계약입니다.
- `Task<T>`, `async`, `await`는 DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `ThenBy`, `Count`는 정렬·집계를 의도에 가깝게 표현합니다.
- `Result<T>`는 성공 값 또는 예상 가능한 오류를 담습니다. `IsSuccess`를 확인한 뒤 값을 사용합니다.

## 설계를 읽는 방법

흐름은 `Program`(Composition Root) → `PlanCancellationsService`(Application Service) → `ICancellationPolicy`(Strategy)와 Repository입니다. 생성자 주입은 인터페이스에 의존하는 DI/DIP이며 테스트 대역 교체를 쉽게 합니다.

`CancellationRequest`와 `CancellationPlan`은 Domain Model입니다. record와 읽기 전용 목록은 처리 중 값이 뜻밖에 바뀌는 일을 줄입니다. Application Service는 사용 사례 순서만 담당해 SRP를 지키고, 새 취소 정책은 Strategy 구현 추가로 확장해 OCP를 지킵니다. 이는 SOLID를 작은 코드에 적용한 예입니다.

입력 오류·중복 계획처럼 예상 가능한 실패는 `Result<T>`로 명시합니다. 취소 신호, DB 연결 장애, 프로그래밍 오류는 예외로 전파해 스택과 장애 신호를 보존합니다. 모든 실패를 예외로 만들면 업무 분기까지 경보가 되고, 모든 예외를 Result로 바꾸면 실제 장애를 숨길 수 있습니다.

실무에서는 환불·재고 복구·알림이 각각 다른 시스템일 수 있습니다. 요청 ID 고유 제약과 멱등 키로 중복 환불을 막고, 낙관적 동시성으로 출고와 취소의 경합을 검출합니다. DB 상태 변경과 메시지 발행을 함께 보장하려면 Outbox와 보상 작업을 고려합니다. `CancellationToken`을 끝까지 전달하고 카드·개인정보는 로그에서 제외하며 처리량, 실패율, 재시도 횟수, 보상 지연을 메트릭과 분산 추적으로 관찰합니다.

## 버전 업데이트 (2026-08-23 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 로컬 안정 SDK 10.0.301에서 컴파일되는 기능만 사용했습니다. Microsoft의 2026년 8월 서비스 업데이트 기준 최신 패치는 .NET 10.0.11이므로 운영 환경은 최신 누적 패치를 유지해야 합니다.
- 최신 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, collection-expression arguments, labeled `break`/`continue` 등은 로컬 안정 SDK 대상 실행 코드에서 제외했습니다. 미리보기 기능은 별도 실험 프로젝트에서만 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [2026년 8월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/), [.NET 11 Preview 7](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] `decimal`, nullable, record, enum, LINQ, `async`/`await`, `Result<T>`를 첫 사용 위치에서 설명한다.
- [ ] Repository, Strategy, Application Service, Domain Model, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체와 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 멱등성, 동시성, Outbox, 보상, 취소, 관측성, 민감정보 로그 금지 이유를 말한다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
