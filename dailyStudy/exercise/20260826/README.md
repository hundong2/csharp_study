# 2026-08-26: 재고 실사 차이 조정

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 읽습니다.
2. [`Program.cs`](./src/StockReconciliationExercise/Program.cs)를 `실행 → 모델 → 계약 → 서비스 → 구현 → 테스트` 순서로 읽습니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)를 1단계부터 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260826/src/StockReconciliationExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용하며 Nullable 경고도 오류로 처리합니다.

## 기초 문법 지도

- `var difference = ...;`에서 `var`는 오른쪽 값으로 형식을 추론하고 문장은 보통 `;`로 끝납니다.
- `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. Nullable 분석은 null 오류를 컴파일 시점에 줄입니다.
- `int`는 정수, `bool`은 참/거짓, `enum`은 제한된 이름 목록, `DateTimeOffset`은 시간대가 있는 시각입니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주거나 메서드를 끝냅니다. `Math.Abs`는 음수 차이도 크기로 비교합니다.
- `record`는 값 중심 불변 데이터, `class`는 행동과 상태를 묶는 객체, `interface`는 교체 가능한 행동의 계약입니다.
- `Task<T>`, `async`, `await`는 DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `ThenBy`, `Count`는 정렬·집계를 의도에 가깝게 표현합니다.
- `Result<T>`는 성공 값 또는 예상 가능한 오류를 담습니다. `IsSuccess` 확인 뒤 값을 사용합니다.

## 설계를 읽는 방법

흐름은 `Program`(Composition Root) → `ReconcileStockService`(Application Service) → `IReconciliationPolicy`(Strategy)와 Repository입니다. 생성자 주입은 인터페이스에 의존하는 DI/DIP이며 테스트 대역 교체를 쉽게 합니다.

`StockCount`와 `Reconciliation`은 Domain Model입니다. record와 읽기 전용 목록은 처리 중 값이 뜻밖에 바뀌는 일을 줄입니다. Application Service는 사용 사례 순서만 맡아 SRP를 지키고, 정책 구현 추가로 기준을 확장해 OCP를 지킵니다. 이는 SOLID를 작은 코드에 적용한 예입니다.

입력 오류·충돌처럼 예상 가능한 실패는 `Result<T>`로 명시합니다. 취소 신호, DB 연결 장애, 프로그래밍 오류는 예외로 전파해 스택과 장애 신호를 보존합니다. 업무 분기를 전부 예외로 만들면 정상 거절까지 경보가 되고, 모든 예외를 Result로 바꾸면 실제 장애를 숨길 수 있습니다.

실무에서는 `(창고, SKU, 실사 회차)` 고유 키와 멱등 키로 중복 조정을 막습니다. `ExpectedVersion`을 이용한 낙관적 동시성으로 실사 이후 입출고와의 경합을 검출하고, 재고 변경과 감사 이벤트를 함께 보장하려면 트랜잭션과 Outbox를 고려합니다. `CancellationToken`을 DB까지 전달하고, 차이율·수동 검토 적체·처리 지연·충돌률을 메트릭과 분산 추적으로 관찰합니다. 감사 로그에는 변경 전후 수량과 승인 근거를 남기되 개인정보와 비밀은 제외합니다.

## 버전 업데이트 (2026-08-26 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 로컬 안정 SDK 10.0.301에서 컴파일되는 기능만 사용했습니다. 2026년 8월 서비스 업데이트 기준 최신 패치는 .NET 10.0.11이므로 운영 환경은 최신 누적 패치를 유지해야 합니다.
- 최신 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, extension indexers, collection-expression arguments, labeled `break`/`continue`, memory-safety 변경은 안정 실행 코드에서 제외했습니다. 미리보기 기능은 별도 실험 프로젝트에서 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [2026년 8월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/), [.NET 11 Preview 7](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] nullable, record, enum, LINQ, `async`/`await`, `Result<T>`를 첫 사용 위치에서 설명한다.
- [ ] Repository, Strategy, Application Service, Domain Model, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체와 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 멱등성, 낙관적 동시성, Outbox, 취소, 감사 로그, 관측성의 필요성을 말한다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
