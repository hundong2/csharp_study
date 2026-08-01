# 2026-08-02 중복 결제 탐지 C# 실습

## 초보자 읽는 순서

1. 아래 **처음 만나는 문법**을 먼저 읽습니다.
2. `Program.cs` 맨 위의 실행 코드와 `ReviewPaymentCommand`를 읽습니다.
3. `PaymentFactory` → `SameCustomerAmountRule` → `PaymentReviewService` 순서로 따라갑니다.
4. Repository, DI, Composition Root가 테스트하기 쉬운 구조를 만드는 이유를 확인합니다.
5. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260802/src/DuplicatePaymentExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 기초 문법

- `var result = ...;`에서 `var`는 오른쪽 값으로 변수 형식을 추론하고, `;`는 문장 끝입니다.
- `new("PAY-101", ...)`는 생성자 인수로 객체를 만들며, 문자열은 `"..."`, 소수 금액은 `35_000m`처럼 씁니다.
- `string?`는 `null`을 허용합니다. `is null`로 검사하고, 검증 뒤의 `!`는 null이 아님을 컴파일러에 알립니다.
- `if`는 조건 분기, `return`은 메서드 종료, `try/catch`는 예상 밖 장애의 경계입니다.
- `async Task<T>`와 `await`는 DB·네트워크 I/O를 기다리는 동안 스레드를 붙잡지 않도록 합니다.
- `record`는 값 중심의 불변 데이터에 적합하고, `enum`은 제한된 상태 집합을 이름으로 표현합니다.
- LINQ의 `Where`, `OrderByDescending`, `FirstOrDefault`는 필터링·정렬·첫 항목 선택을 선언적으로 표현합니다.
- `interface`는 “무엇을 할 수 있는가”라는 계약이며, 구현을 교체할 수 있게 합니다.

## 실무 설계 지도

- **Domain Model**: `PaymentFactory`가 유효한 결제만 만들어 불변 조건을 보호합니다.
- **Application Service**: `PaymentReviewService`는 검증, 조회, 판정, 저장의 유스케이스 순서만 조정합니다.
- **Strategy**: `IDuplicateRule`은 카드사나 국가별 판정 정책을 교체할 수 있게 합니다(SOLID의 SRP/OCP).
- **Repository**: 저장 기술을 업무 규칙에서 분리합니다. 메모리 구현은 빠르고 결정적인 테스트를 돕습니다.
- **DI/DIP**: 서비스가 구체 저장소가 아닌 인터페이스에 의존합니다. 맨 위의 **Composition Root**만 구현체를 조립합니다.
- **Result 대 예외**: 잘못된 입력처럼 예상 가능한 실패는 `Result`, DB 장애처럼 예상 밖이거나 복구하기 어려운 실패는 예외로 표현합니다.
- **nullable 안전성**: `<Nullable>enable</Nullable>`과 명시적인 분기로 null 참조 오류를 컴파일 단계에서 줄입니다.
- **불변 record**: 생성 뒤 데이터가 바뀌지 않아 동시 처리와 테스트에서 추론이 쉬워집니다.
- **테스트 가능성**: 규칙과 저장소, 로그를 인터페이스로 나누어 실제 DB 없이 경계값을 검증합니다.

실운영에서는 결제사 고유 키를 이용한 멱등성, DB 유니크 제약과 트랜잭션, 시간대·시계 오차, 개인정보 마스킹, timeout·취소, 재시도와 circuit breaker, 구조화 로그·중복 탐지율 metric·trace, 수동 검토 SLA를 함께 설계해야 합니다. 중복 “의심”만으로 자동 환불하지 않고 사람이 확인할 수 있는 상태를 둔 것도 안전한 운영 선택입니다.

## 버전 업데이트 (2026-08-02 확인)

- 최신 안정 언어는 **C# 14**, 안정 플랫폼은 **.NET 10 LTS**입니다. 공식 다운로드 페이지 기준 최신 패치는 10.0.10, 최신 SDK는 10.0.302입니다.
- 이 저장소에는 안정 SDK 10.0.301이 설치되어 있어 예제는 `net10.0`으로 빌드합니다.
- **C# 15 / .NET 11 Preview 6**은 프리뷰입니다. union types, closed hierarchies, extension indexers 등은 별도 실험 환경에서만 살펴보고 이 안정 예제에는 넣지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 완료 기준

- `dotnet build`가 경고와 오류 없이 끝납니다.
- 일반 실행은 `ManualReview`를 출력하고 self-test는 `4/4 통과`합니다.
- Result/예외, Domain/Application/Repository/Strategy, DI/Composition Root 차이를 말로 설명할 수 있습니다.
