# 2026-09-02 공급업체 등록 심사 C# 실습

## 초보자 읽는 순서

1. 이 README의 **오늘 만들 것**과 **첫 실행**을 읽습니다.
2. [`Program.cs`](./src/VendorOnboardingExercise/Program.cs)를 위에서 아래로 읽고 실행 흐름을 따라갑니다.
3. [`EXERCISES.md`](./EXERCISES.md)를 1번부터 수정하며 매번 self-test를 실행합니다.
4. [`CHECKPOINT.md`](./CHECKPOINT.md)로 말로 설명하고 실행 결과를 확인합니다.

## 오늘 만들 것

공급업체 신청을 승인·수동 검토·거절로 분류하고 결과를 저장하는 콘솔 앱입니다. 작은 예제 안에서 C# 기본 문법부터 실무의 Domain Model, Application Service, Repository, Strategy, DI, 운영 안전성까지 연결합니다.

## 첫 실행

```powershell
dotnet run --project ./src/VendorOnboardingExercise
dotnet run --project ./src/VendorOnboardingExercise -- --self-test
```

예상 요약은 `승인 1건, 수동 검토 1건, 거절 2건`, 자체 테스트는 `4/4 통과`입니다.

## 처음 만나는 C# 문법

- `var repository = ...;`에서 `var`는 오른쪽 값으로 정적 형식을 추론합니다. 동적 타입이 아니며 컴파일 시 형식이 고정됩니다. 문장은 보통 `;`로 끝납니다.
- `new("REQ-101", ...)`는 문맥이 알려 주는 `VendorApplication` 생성자를 호출합니다. `12_000_000m`의 `_`는 읽기 구분자이고 `m`은 정확한 금융 계산용 `decimal`입니다.
- `string? ContactEmail`은 값이 없을 수 있음을 나타냅니다. nullable 분석이 켜져 있어 null을 무시한 사용을 컴파일러가 경고합니다.
- `if`는 조건 분기, `foreach`는 컬렉션 순회, `return`은 현재 메서드를 끝냅니다. `$"{value}"`는 문자열 보간입니다.
- `enum`은 결정의 선택지를 제한하고, `record`는 값 비교와 불변 데이터 전달에 적합합니다.
- `List<T>`와 `IReadOnlyList<T>`의 `<T>`는 원소 형식을 지정하는 제네릭입니다. 읽기 전용 계약은 호출자의 우발적 변경을 줄입니다.
- LINQ의 `OrderBy`, `Count`는 반복문보다 정렬·집계 의도를 직접 표현합니다. 큰 데이터베이스 조회에서는 실행 위치와 쿼리 비용도 확인해야 합니다.
- `Task<T>`, `async`, `await`는 I/O 대기 중 스레드를 붙잡지 않게 합니다. `CancellationToken`을 전달해 종료·시간 초과 요청에 협력합니다.

## 설계 지도를 따라가기

| 구성 요소 | 책임 | 설계 이유 |
| --- | --- | --- |
| `VendorApplication`, `VendorReview` | 업무 데이터를 표현하는 Domain Model | 불변 record로 상태 변경 지점을 줄임 |
| `StandardVendorRiskPolicy` | 승인 규칙을 결정하는 Strategy | 규칙 변경을 처리 흐름과 분리(OCP) |
| `IVendorRepository` | 저장 기술의 계약 | 메모리·DB 구현을 교체하고 테스트 가능하게 함(DIP) |
| `ReviewVendorsService` | 조회→판단→저장 유스케이스 | Application Service가 흐름에만 집중(SRP) |
| 파일 맨 위 조립 코드 | Composition Root | DI를 한곳에서 수행해 의존 관계를 드러냄 |

DI(의존성 주입)는 서비스가 필요한 객체를 생성자에서 받는 방식입니다. 인터페이스가 무조건 좋은 것은 아니지만, 정책·저장소처럼 변화하거나 외부 I/O가 있는 경계에 두면 SOLID의 SRP/OCP/DIP와 테스트 가능성을 실질적으로 높입니다.

## Result와 예외

지원 국가가 아니거나 중복 결과가 충돌하는 일은 예상 가능한 업무 실패이므로 `Result<T>`로 반환합니다. 네트워크 단절, 저장소 장애, 프로그래밍 버그처럼 정상 분기로 처리할 수 없는 문제는 예외로 전파해 중앙 로깅과 재시도 정책이 다루게 합니다. 예외를 모든 분기 제어에 쓰거나, 반대로 모든 장애를 문자열 Result로 감추지 마세요.

## 중급·전문가 설계 선택

- nullable과 불변 record는 “없는 값”과 상태 변경을 형식 수준에서 통제합니다. 외부 입력은 Domain Model 생성 전에도 검증하는 것이 좋습니다.
- Strategy와 Repository는 구현 교체점을 만들고, Application Service는 업무 순서를 명시합니다. 도메인이 복잡해지면 검증을 값 객체와 집합체로 옮기되 작은 CRUD에 과도한 계층을 만들지는 않습니다.
- 인메모리 Repository의 사전은 같은 요청/같은 결과 재저장을 허용해 멱등성을 보여 줍니다. 실제 DB에서는 요청 ID의 unique 제약과 트랜잭션이 필요합니다.
- `ExpectedVersion`은 낙관적 동시성의 출발점입니다. 실제 저장 시 `WHERE Version = @expected` 또는 EF Core concurrency token으로 충돌을 감지해야 합니다.
- DB 저장과 이벤트 발행이 함께 필요하면 Outbox로 한 트랜잭션에 기록하고 별도 발행기가 재시도해야 유실을 줄일 수 있습니다.
- 운영에서는 구조화 로그, 요청별 correlation ID, 처리 건수·실패율·지연 시간 메트릭, 추적을 추가하세요. 이메일·회사명·계약 금액 원문은 로그에서 제거하거나 마스킹합니다.
- 시간 제한, 취소 전달, 제한된 재시도와 backoff를 적용하되 검증 실패처럼 재시도해도 바뀌지 않는 결과는 반복하지 않습니다.

## 실무 확장 질문

1. 정책 버전을 결과에 저장하지 않으면 나중에 같은 입력의 결정이 달라졌을 때 어떻게 감사할까요?
2. 두 작업자가 같은 신청을 동시에 처리하면 어떤 unique 제약과 동시성 검사가 필요할까요?
3. 승인 후 알림 발송이 실패하면 Outbox와 재시도는 어디에 배치할까요?

## 버전 업데이트 (2026-09-02 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 로컬 안정 SDK 10.0.301에서 컴파일되는 기능만 실행 예제에 사용했습니다. 2026년 8월 서비스 업데이트에는 .NET 10·9·8 대상 보안 및 비보안 수정이 포함되므로 운영 환경은 지원되는 최신 누적 패치를 유지해야 합니다.
- 최신 공개 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, extension indexers, collection-expression arguments, labeled `break`/`continue`, memory-safety 변경은 안정 코드에서 제외했습니다. 미리보기는 별도 실험 프로젝트에서만 평가하세요.
- 공식 출처: [.NET 2026년 8월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/), [.NET 11 Preview 7](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] nullable, decimal, record, enum, 튜플 `switch`, LINQ, `async`/`await`, `Result<T>`를 설명한다.
- [ ] Repository, Strategy, Application Service, Domain Model, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체와 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 멱등성, 동시성, Outbox, 취소, 개인정보 보호, 관측성의 필요성을 말한다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
