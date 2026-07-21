# 2026-07-22 C# 구독 갱신 실습

구독 갱신 프로그램을 실행하며 C# 기본 문법부터 실무 아키텍처와 운영 안전성까지 익히는 입문 자료입니다.

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자체 검증을 먼저 통과시킵니다.
2. `Program.cs`의 맨 위 실행 코드와 `[기초]` 주석을 읽습니다.
3. `Subscription` → `Result<T>` → `Renewal` 순서로 데이터와 실패 표현을 봅니다.
4. Strategy → Repository → Application Service → Composition Root 순서로 책임 분리를 따라갑니다.
5. `EXERCISES.md` 1~3단계를 수정하고 `CHECKPOINT.md`로 코드 없이 복습합니다.

## 실행

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260722
dotnet run --project .\src\SubscriptionExercise
dotnet run --project .\src\SubscriptionExercise -- --self-test
```

마지막 줄이 `초보자 검증 통과 (4/4)`이면 시작 코드가 정상입니다.

## 처음 만나는 문법

| 문법 | 첫 사용 | 의미 |
| --- | --- | --- |
| 변수/타입 | `var service`, `decimal Amount` | 값에 이름과 타입을 부여합니다. `var`도 컴파일 때 타입이 고정됩니다. |
| 조건/반복 | `if`, `switch`, `foreach` | 상태에 따라 분기하고 여러 항목을 순회합니다. |
| nullable | `T?`, `string?`, `??` | 값이 없을 가능성을 타입으로 드러내고 처리합니다. |
| record/with | `record Subscription`, `this with` | 값 중심의 불변 데이터를 만들고 복사해 변경합니다. |
| LINQ/람다 | `Any(x => ...)` | 컬렉션 질문을 선언적으로 표현합니다. |
| 비동기 | `Task`, `async`, `await` | DB·네트워크 대기를 논블로킹 계약으로 표현합니다. |

## 실무 설계 지도

```text
Program → RenewalService(Application Service)
             ├─ Subscription(Domain Model: 검증)
             ├─ IRenewalPriceStrategy(Strategy: 가격 정책)
             └─ ISubscriptionRepository(Repository: 저장 계약)
CompositionRoot가 구현을 선택해 의존성 주입
```

- record와 `with`는 공유 데이터를 불변에 가깝게 유지해 추론과 테스트를 쉽게 합니다.
- Result는 잘못된 이메일·중복 갱신처럼 예상 가능한 업무 실패에, 예외는 null 인수·취소·알 수 없는 enum처럼 계약 위반이나 비정상 흐름에 사용합니다.
- Application Service는 갱신 순서만 조정하고 Domain Model, Strategy, Repository에 세부 책임을 위임합니다. 이는 SRP에 해당합니다.
- 인터페이스에 의존하는 Repository와 Strategy는 DIP를 적용하며, 정책 확장은 서비스 수정보다 새 구현 추가로 처리해 OCP를 지향합니다.
- Composition Root에서 객체를 조립하는 의존성 주입은 테스트에 메모리 저장소·고정 시계를 넣을 수 있게 합니다.

운영에서는 DB의 구독 ID 고유 제약과 트랜잭션으로 동시 갱신을 막고, 외부 결제 호출에는 타임아웃·재시도·멱등 키를 둡니다. UTC 저장과 사용자 시간대 표시를 분리하고, 개인정보를 제외한 구조화 로그·성공률/지연 메트릭·분산 추적을 추가해야 합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 확인 단계](./CHECKPOINT.md)
- [실행 코드](./src/SubscriptionExercise/Program.cs)

## 버전 업데이트 (2026-07-22 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`, 런타임은 `10.0.9`입니다. 예제는 `net10.0`과 기본 C# 14로 빌드합니다.
- [.NET 10의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS이며 런타임·라이브러리·SDK 개선을 포함합니다.
- [C# 14의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 최신 안정 C# 릴리스이며 .NET 10에서 지원된다고 설명합니다.
- [C# 언어 버전 규칙](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning)에 따르면 .NET 10의 기본 언어 버전은 C# 14이고, C# 15는 .NET 11 이상이 필요합니다.
- [.NET 11 Preview 5](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-5/)는 2026-06-09 공개된 미리 보기입니다. 로컬에 .NET 11 SDK가 없으므로 C# 15 등 미리 보기 전용 기능은 실행 코드에서 분리했습니다.

## 짧은 복습 체크리스트

- [ ] 일반 실행과 `--self-test`가 모두 성공한다.
- [ ] nullable, record, LINQ, async/await의 위치와 이유를 말할 수 있다.
- [ ] Result와 예외의 사용 기준을 설명한다.
- [ ] Application Service, Domain Model, Repository, Strategy, DI, SOLID, Composition Root를 한 문장씩 설명한다.
- [ ] 운영 환경에서 DB 제약, 멱등성, 시간대, 로그·메트릭이 필요한 이유를 설명한다.
