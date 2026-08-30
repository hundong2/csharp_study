# 2026-08-31: 쿠폰 발급 자격 판정

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 읽습니다.
2. [`Program.cs`](./src/CouponIssuanceExercise/Program.cs)를 `실행 → 모델 → 계약 → 서비스 → 구현 → 테스트` 순서로 읽습니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)를 1단계부터 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260831/src/CouponIssuanceExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용하며 Nullable 경고도 오류로 처리합니다.

## 기초 문법 지도

- `var now = ...;`에서 `var`는 오른쪽 값으로 형식을 추론하고 문장은 보통 `;`로 끝납니다.
- `decimal`은 금액, `int`는 정수, `bool`은 참/거짓, `enum`은 제한된 이름 목록입니다. `300_000m`의 `_`는 읽기 구분자이고 `m`은 decimal 리터럴 표시입니다.
- `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. `?.`는 null이면 호출하지 않고 null을 돌려주는 nullable 안전 문법입니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주거나 메서드를 끝냅니다. 튜플 `switch` 식은 여러 입력을 한 결과로 바꿉니다.
- `record`는 값 중심 불변 데이터, `class`는 행동과 상태를 묶는 객체, `interface`는 교체 가능한 행동의 계약입니다.
- `Task<T>`, `async`, `await`는 DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `ThenBy`, `Count`는 정렬·집계를 의도에 가깝게 표현합니다.
- `Result<T>`는 성공 값 또는 예상 가능한 오류를 담습니다. `IsSuccess` 확인 뒤 값을 사용합니다.

## 설계를 읽는 방법

흐름은 `Program`(Composition Root) → `IssueCouponsService`(Application Service) → `ICouponPolicy`(Strategy)와 Repository입니다. 생성자 주입은 인터페이스에 의존하는 DI/DIP이며 테스트 대역 교체를 쉽게 합니다.

`CouponRequest`와 `CouponPlan`은 Domain Model입니다. record와 읽기 전용 목록은 처리 중 값이 뜻밖에 바뀌는 일을 줄입니다. Application Service는 사용 사례 순서만 맡아 SRP를 지키고, 정책 구현 추가로 기준을 확장해 OCP를 지킵니다. 이는 SOLID를 작은 코드에 적용한 예입니다.

빈 작업 목록이나 저장 충돌처럼 예상 가능한 실패는 `Result<T>`로 표현합니다. 취소 신호, DB 연결 장애, 프로그래밍 오류는 예외로 전파해 스택과 장애 신호를 보존합니다. 업무 제외를 모두 예외로 만들면 정상 흐름까지 경보가 되고, 모든 예외를 Result로 바꾸면 실제 장애를 숨길 수 있습니다.

실무에서는 요청 ID와 캠페인 버전으로 멱등 키를 만들고 `ExpectedVersion` 낙관적 동시성으로 중복 처리 경합을 검출합니다. 계획 저장과 실제 쿠폰 발급 이벤트를 함께 보장하려면 트랜잭션과 Outbox를 고려합니다. `CancellationToken`을 DB까지 전달하고 발급률·수동 검토율·중복 차단률·처리 지연을 메트릭과 분산 추적으로 관찰합니다. 고객 ID·구매 내역·동의 정보는 최소 수집, 암호화, 접근 통제, 보존·삭제 정책을 적용하고 원문을 로그에 남기지 않습니다. 캠페인 규칙과 동의 근거는 정책 버전과 함께 감사 가능하게 보존해야 합니다.

## 버전 업데이트 (2026-08-31 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 로컬 안정 SDK 10.0.301에서 컴파일되는 기능만 사용했습니다. 2026년 8월 최신 안정 서비스 릴리스는 .NET 10.0.11이며 보안 및 비보안 수정이 포함되므로 운영 환경은 최신 누적 패치를 유지해야 합니다.
- 최신 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, extension indexers, collection-expression arguments, labeled `break`/`continue`, memory-safety 변경은 안정 실행 코드에서 제외했습니다. 미리보기 기능은 별도 실험 프로젝트에서 평가하세요.
- 공식 출처: [.NET 2026년 8월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/), [.NET 11 Preview 7](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] nullable, decimal, record, enum, 튜플 `switch`, LINQ, `async`/`await`, `Result<T>`를 첫 사용 위치에서 설명한다.
- [ ] Repository, Strategy, Application Service, Domain Model, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체와 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 멱등성, 낙관적 동시성, Outbox, 취소, 개인정보 보호, 관측성의 필요성을 말한다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
