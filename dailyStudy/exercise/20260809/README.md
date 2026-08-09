# 2026-08-09: 장바구니 가격 산정으로 배우는 C#과 .NET 설계

## 초보자 읽기 순서

1. 아래 명령으로 결과를 먼저 봅니다.
2. `Program.cs`의 record에서 값·변수·타입을 확인합니다.
3. `CouponDiscountStrategy`의 조건문과 Result를 읽습니다.
4. `PriceCartService`의 조회 → 검증 → 합계 → 할인 흐름을 따라갑니다.
5. self-test 후 `EXERCISES.md`, `CHECKPOINT.md` 순서로 풉니다.

처음에는 이름보다 **데이터**, **규칙**, **작업 순서**, **저장 기술**을 왜 나눴는지 찾으세요.

## 실행하기

```powershell
cd dailyStudy/exercise/20260809/src/CartPricingExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. `CART-101`은 결제 `76,500원`, self-test는 `4/4 통과`해야 합니다.

## 처음 만나는 기본 문법

- `var`: 오른쪽 값으로 지역 변수 타입을 추론하지만 정적 타입 검사는 유지됩니다.
- `new Cart(...)`: 객체 생성이며 인수는 record 선언 순서에 대응합니다.
- `string?`: null 가능성을 타입에 기록하고 첫 사용 근처에서 검사하게 합니다.
- `decimal`, `45_000m`: 돈에 적합한 10진 타입입니다. `m`은 타입, `_`는 가독성 표시입니다.
- `if`, `foreach`, `return`: 조건, 반복, 메서드 종료와 결과 반환입니다.
- `record`: 값 중심 불변 데이터에 적합해 변경 추적을 단순화합니다.
- `interface`: 구현의 계약이라 저장소·정책을 교체할 수 있습니다.
- `Result<T>`: 성공 값 타입만 바꿔 같은 성공/실패 구조를 재사용하는 제네릭입니다.
- `Task<T>`와 `await`: I/O를 기다리는 동안 스레드를 막지 않고 완료 뒤 `T`를 얻습니다.
- `Any`, `Sum`: LINQ로 검증 질문과 합계 의도를 드러냅니다.

## 설계 지도와 선택 이유

```text
Program (Composition Root)
  └─ PriceCartService (Application Service)
       ├─ ICartRepository   → InMemoryCartRepository
       └─ IDiscountStrategy → CouponDiscountStrategy
```

`Cart`·`CartLine`은 Domain Model, 저장 조회는 Repository, 할인은 Strategy, 작업 순서는 Application Service입니다. Composition Root가 실제 구현을 조립하고 생성자 DI가 구체 구현 대신 계약을 전달합니다. 이는 SOLID의 SRP·OCP·DIP와 테스트 가능성을 높입니다.

nullable은 쿠폰 누락을 숨기지 않고, 불변 record는 계산 중 입력 변경을 줄입니다. LINQ는 메모리 목록에서 의도가 잘 보이지만 대량 데이터는 Repository가 DB에서 필터·집계해야 합니다. `async/await`와 `CancellationToken`은 I/O와 요청 취소를 저장소까지 전달합니다.

없는 장바구니·잘못된 수량·미지원 쿠폰은 예상 가능한 `Result`, DB 단절은 예상 밖 예외가 알맞습니다. 취소 예외는 실패로 감추지 않습니다. 운영에서는 서버 재계산, 통화·반올림 규칙, 장바구니 버전 동시성, 구조화 로그·추적 ID·지연/실패 메트릭이 필요합니다. 결제로 확장하면 주문 ID 기반 멱등성과 outbox를 검토합니다.

## 초보자 검증 단계

- `dotnet build`가 경고 0, 오류 0인가?
- `CART-101` 결제 금액이 76,500원인가?
- 잘못된 수량과 없는 장바구니가 친절한 실패인가?
- self-test가 4/4 통과하는가?
- 실패하면 첫 오류의 파일명과 줄 번호부터 읽었는가?

## 버전 업데이트 (2026-08-09 확인)

- 안정 기준: .NET 10은 LTS이고 C# 14를 지원합니다. 최신 안정 패치는 .NET 10.0.10, SDK 10.0.302(2026-07-14)이며 오늘 코드는 로컬 SDK 10.0.301로 검증합니다.
- 미리 보기 분리: .NET 11 Preview 6과 C# 15는 preview입니다. collection expression arguments, union types, closed hierarchies 등은 실행 코드에서 제외했습니다.
- 공식 자료: [C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14), [.NET 10 다운로드](https://dotnet.microsoft.com/download/dotnet/10.0), [C# 15 미리 보기](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15), [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 짧은 복습 체크리스트

- [ ] nullable·record·interface·제네릭을 설명한다.
- [ ] LINQ 실행과 async 취소 전파를 설명한다.
- [ ] 예외와 Result를 구분한다.
- [ ] Repository·Strategy·Application Service·Composition Root를 찾는다.
- [ ] DI와 SOLID가 테스트를 쉽게 하는 이유를 말한다.
- [ ] 가격 재계산, 동시성, 로그·메트릭이 필요한 이유를 말한다.

다음: [`EXERCISES.md`](./EXERCISES.md) · 마무리: [`CHECKPOINT.md`](./CHECKPOINT.md) · 코드: [`Program.cs`](./src/CartPricingExercise/Program.cs)
