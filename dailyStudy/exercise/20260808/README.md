# 2026-08-08: 구독 갱신으로 배우는 C#과 .NET 설계

## 초보자 읽기 순서

1. 아래 명령으로 실행 결과를 먼저 봅니다.
2. `Program.cs` 위에서 아래로 데이터(record) → 계약(interface) → 규칙(Strategy) 순서로 읽습니다.
3. `RenewSubscriptionsService`에서 조회 → 계산 → 결제 흐름을 따라갑니다.
4. `--self-test`로 기본 동작을 확인합니다.
5. `EXERCISES.md`의 1~3번을 수정하고 `CHECKPOINT.md`로 복습합니다.

처음에는 이름을 외우기보다 **데이터**, **업무 규칙**, **작업 순서**, **외부 연결**을 왜 나눴는지 찾으세요.

## 실행하기

설치된 안정 SDK 10.0.301에 맞춰 `net10.0`을 대상으로 합니다.

```powershell
cd dailyStudy/exercise/20260808/src/SubscriptionRenewalExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

예상 핵심 출력은 `성공 1건, 거절 1건, 건너뜀 1건`과 `self-test: 4/4 통과`입니다.

## 처음 만나는 문법

- `var result = ...;`: 오른쪽 값으로 지역 변수의 정적 타입을 추론합니다. 타입 검사는 그대로 유지됩니다.
- `new Subscription(...)`: `new`로 객체를 만들며 생성자 인수는 record의 선언 순서와 대응합니다.
- `string? Plan`: `?`는 null 가능성을 타입에 기록합니다. 사용 전에 `IsNullOrWhiteSpace`로 검사합니다.
- `if`, `else`, `foreach`, `continue`: 조건 분기, 반복, 현재 반복 건너뛰기를 표현합니다.
- `decimal`과 `12_000m`: 돈 계산에 적합한 10진 타입이며 `m`은 decimal 리터럴, `_`는 가독성용 구분자입니다.
- `record`: 값 중심 데이터에 적합하고 생성 뒤 변경을 줄여 동시 처리와 추적을 단순화합니다.
- `interface`: 구현이 지켜야 할 계약입니다. 메모리 저장소와 실제 DB 구현을 교체할 수 있습니다.
- `Task<T>`와 `await`: 비동기 I/O 완료 후 얻을 `T`를 표현하며 스레드를 막아 기다리지 않습니다.
- `Result<T>`의 `<T>`: 여러 타입에 같은 성공/실패 모양을 재사용하는 제네릭 문법입니다.
- `Where(...).OrderBy(...).ToArray()`: LINQ로 필터·정렬하고 `ToArray`에서 결과를 확정합니다.

## 설계 지도

```text
Program (Composition Root)
  └─ RenewSubscriptionsService (Application Service)
       ├─ ISubscriptionRepository → InMemorySubscriptionRepository
       ├─ IRenewalPricePolicy     → PercentageDiscountPolicy (Strategy)
       └─ IPaymentGateway         → ConsolePaymentGateway
```

- Domain Model: `Subscription`, `RenewalCharge`가 업무 데이터를 표현합니다.
- Repository: 구독 조회를 저장 기술에서 분리합니다.
- Strategy: 할인 정책을 교체 가능하게 만듭니다.
- Application Service: 유스케이스 순서만 조정합니다.
- Composition Root: 시작점 한 곳에서 구현을 조립합니다.
- DI와 SOLID: 구체 클래스 대신 계약을 생성자로 받아 SRP·OCP·DIP와 테스트 가능성을 높입니다.

## 중급·고급 선택의 이유

nullable 표기는 외부 데이터 누락을 숨기지 않습니다. 불변 record는 처리 중 값이 바뀌는 경우를 줄입니다. LINQ는 “자동 갱신 대상 선별과 순서”라는 의도를 드러내지만, 대량 데이터는 Repository에서 DB 필터링·정렬·페이지 처리를 해야 합니다.

`async/await`와 `CancellationToken`은 DB·결제 같은 I/O를 효율적으로 기다리고 배포나 요청 취소를 끝까지 전달합니다. 결제 거절·잘못된 요금제처럼 예상 가능한 업무 결과는 `Result`, 연결 단절처럼 정상 흐름으로 복구하기 어려운 기술 장애는 예외가 알맞습니다. 취소 예외는 실패로 감추지 않습니다.

운영에서는 구독 ID와 갱신일을 조합한 **멱등성 키**로 중복 결제를 막고, 타임아웃·제한된 재시도·구조화 로그·성공률과 거절률 메트릭·비밀 값 마스킹을 적용해야 합니다. 실제 결제와 DB 갱신은 하나의 로컬 트랜잭션이 아니므로 outbox나 상태 머신을 검토합니다.

## 초보자 검증 단계

- `dotnet build`가 경고 0개, 오류 0개인가?
- 일반 실행 결과가 성공 1, 거절 1, 건너뜀 1인가?
- self-test가 4/4 통과하는가?
- 실패하면 첫 오류의 파일명과 줄 번호부터 읽었는가?

## 버전 업데이트 (2026-08-08 확인)

- 안정 실행 기준: C# 14는 .NET 10에서 지원되는 최신 안정 C#이며 .NET 10은 LTS입니다. 오늘 코드는 로컬 안정 SDK 10.0.301로 컴파일합니다.
- 미리 보기 분리: .NET 11 Preview 6과 C# 15는 preview입니다. C# 15의 union types, closed hierarchies 등은 오늘 실행 코드에 넣지 않았으며 .NET 11 preview SDK에서 별도 실험해야 합니다.
- 공식 자료: [C# 14의 새로운 기능](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14), [.NET 10 개요](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview), [C# 15 미리 보기](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15), [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 오늘의 짧은 복습 체크리스트

- [ ] nullable과 검증의 관계를 설명한다.
- [ ] record·interface·제네릭의 역할을 설명한다.
- [ ] LINQ 실행 시점과 async 취소 전파를 설명한다.
- [ ] 예외와 Result를 구분한다.
- [ ] Repository·Strategy·Application Service·Composition Root를 찾는다.
- [ ] DI와 SOLID가 테스트를 쉽게 하는 이유를 말한다.
- [ ] 결제 멱등성, 로그, 메트릭이 필요한 이유를 말한다.

다음: [`EXERCISES.md`](./EXERCISES.md) · 마무리: [`CHECKPOINT.md`](./CHECKPOINT.md) · 코드: [`Program.cs`](./src/SubscriptionRenewalExercise/Program.cs)
