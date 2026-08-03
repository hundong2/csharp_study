# 2026-08-04 배송비 견적 C# 실습

## 초보자 읽는 순서

1. 아래 **처음 만나는 문법**을 읽습니다.
2. `Program.cs` 맨 위 실행 코드와 `CreateDeliveryQuoteCommand`를 읽습니다.
3. `Product` → `StandardShippingPolicy` → `CreateDeliveryQuoteService` 순서로 책임을 따라갑니다.
4. Repository, DI, Composition Root가 테스트하기 쉬운 구조를 만드는 이유를 확인합니다.
5. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260804/src/DeliveryQuoteExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 기초 문법

- `var result = ...;`에서 `var`는 오른쪽 값으로 형식을 추론하고 `;`은 문장 끝입니다. 문자열은 `"..."`, 정수는 `2`, 소수는 `1.2m`처럼 씁니다.
- `if`는 조건 분기, `return`은 메서드 종료, `new`는 객체 생성입니다. `is < 1 or > 100`은 범위 밖 값을 읽기 쉽게 검사하는 패턴입니다.
- `string?`은 `null`을 허용합니다. `<Nullable>enable</Nullable>`은 null 가능성을 컴파일러가 추적하게 해 실수를 줄입니다.
- 클래스는 데이터와 동작을 묶고, `interface`는 구현이 지켜야 할 계약입니다. 생성자로 계약을 받는 방식이 **의존성 주입(DI)**입니다.
- `record`는 값 중심 데이터에 적합하며 불변 명령·결과를 표현하기 좋습니다. `decimal`은 돈과 정확한 소수 계산에 적합합니다.
- LINQ의 `ToDictionary`는 모음을 키 기반 조회 구조로 바꿉니다. `async Task<T>`와 `await`는 DB·네트워크 I/O 중 스레드를 붙잡지 않습니다.

## 실무 설계 지도

- **Domain Model**: `Product`와 `DeliveryQuote`가 업무에서 의미 있는 값을 명시합니다.
- **Application Service**: `CreateDeliveryQuoteService`가 검증, 조회, 계산, 기록 순서만 조정합니다.
- **Strategy**: `IShippingPolicy`가 요금 규칙을 분리해 특급·해외 정책을 서비스 수정 없이 추가하게 합니다.
- **Repository**: 저장 기술을 업무 흐름에서 분리합니다. 메모리 구현은 테스트에, EF Core 구현은 운영 DB에 쓸 수 있습니다.
- **DI와 Composition Root**: 서비스는 추상화에 의존하고 시작 지점만 구현체를 조립합니다. 이는 SOLID의 SRP, OCP, DIP를 실천합니다.
- **Result와 예외**: 잘못된 입력·없는 상품 같은 예상 가능한 실패는 `Result`, DB 단절 같은 예상 밖 장애는 예외로 구분합니다.
- **nullable 안전성과 불변성**: 이메일의 선택 여부를 형식에 표시하고 record 결과를 변경하지 않아 중간 상태 오류를 줄입니다.
- **테스트 가능성**: 메모리 Repository와 조용한 로그를 주입하여 DB나 콘솔 없이 경계값을 검증합니다.

## 운영 환경에서는 무엇을 더 생각할까?

실제 요금표에는 적용 시작일과 버전을 두어 과거 견적을 재현해야 합니다. 외부 배송사 API에는 timeout, 제한된 재시도, circuit breaker를 적용하고 요청 ID로 trace와 구조화 로그를 연결합니다. 금액·정책 버전·처리 시간을 metric으로 관찰하되 이메일 같은 개인정보는 로그에 남기지 않습니다. 견적 저장과 이벤트 발행이 함께 필요하면 Outbox와 멱등 소비자를 고려하고, 환율·세금 반올림 규칙은 도메인 값 객체로 명확히 고정합니다.

## 버전 업데이트 (2026-08-04 확인)

- 최신 안정 언어는 **C# 14**, 안정 플랫폼은 **.NET 10 LTS**입니다. 최신 패치는 .NET 10.0.10, 최신 SDK는 10.0.302이며 .NET 10 지원 종료일은 2028-11-14입니다.
- 로컬의 최신 안정 SDK는 **10.0.301**이므로 오늘 예제는 안정 기능만 사용하고 `net10.0`으로 빌드합니다.
- **C# 15 / .NET 11 Preview 6**은 프리뷰입니다. union types, closed hierarchies, extension indexers 등은 별도 프리뷰 SDK 환경에서 실험하고 오늘의 안정 예제에는 넣지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [.NET 10 다운로드와 최신 패치](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [.NET 11 Preview 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)

## 완료 기준과 간단 복습 체크리스트

- [ ] `dotnet build`가 경고와 오류 없이 끝난다.
- [ ] 일반 실행이 제주 배송비 8,100원을 출력한다.
- [ ] self-test가 `4/4 통과`한다.
- [ ] nullable, record/불변성, LINQ, async/await를 한 문장씩 설명할 수 있다.
- [ ] Result와 예외를 언제 구분하는지 설명할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy의 책임을 구분할 수 있다.
- [ ] DI, Composition Root, SOLID가 테스트 가능성에 주는 이점을 설명할 수 있다.
