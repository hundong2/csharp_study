# 2026-07-19 C# 기초와 결제 금액 계산 실습

기초 문법을 읽고, 실무의 작은 결제 유스케이스를 직접 실행하는 입문 자료입니다. 변수·조건·반복·컬렉션·null·record·LINQ를 연습하면서 Application Service, Strategy, 생성자 의존성 주입, Composition Root, Result 패턴을 함께 익힙니다.

## 먼저 실행하기

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260719
dotnet run --project .\src\CheckoutExercise\CheckoutExercise.csproj
dotnet run --project .\src\CheckoutExercise\CheckoutExercise.csproj -- --self-test
```

두 번째 명령의 마지막에 `초보자 검증 통과 (4/4)`가 나오면 준비가 끝난 것입니다.

## 기본 문법 지도

| 문법 | 예제 | 핵심 |
| --- | --- | --- |
| 타입과 변수 | `string`, `int`, `decimal`, `bool` | 값의 종류를 컴파일러에 알립니다. 금액에는 보통 `decimal`을 씁니다. |
| nullable과 기본값 | `string?`, `couponCode ?? "없음"` | 값이 없을 가능성을 타입과 코드에 드러냅니다. |
| 조건과 패턴 | `if`, `switch` 식 | 입력을 검증하고 값에 따라 결과를 고릅니다. |
| 컬렉션과 반복 | `List<T>`, `foreach` | 같은 종류의 여러 값을 저장하고 순회합니다. |
| record와 계산 속성 | `CartItem`, `LineTotal` | 관련 데이터를 묶고 계산 의미를 이름으로 표현합니다. |
| LINQ | `Any`, `Sum` | 컬렉션 검증과 합계를 읽기 좋게 표현합니다. |
| 인터페이스 | `IDiscountPolicy` | 필요한 동작을 계약으로 만들어 구현을 교체합니다. |

`SyntaxTour`부터 실행하고, `CheckoutDemo` → `CompositionRoot` → `CheckoutService` → `IDiscountPolicy` 순서로 읽으세요.

## 실무 아키텍처 흐름

```text
Program → CompositionRoot → CheckoutService → IDiscountPolicy
                              ↓
                         Result<Receipt>
```

- **Application Service**는 입력 검증, 합계 계산, 할인 호출, 결과 생성의 순서를 조정합니다.
- **Strategy 패턴**은 할인 규칙을 인터페이스 뒤에 두어 회원 할인, 쿠폰 할인 등으로 교체하기 쉽게 합니다.
- **생성자 DI**는 서비스가 구체 클래스 대신 계약을 받게 합니다.
- **Composition Root**는 실제 구현을 선택하고 객체를 연결하는 한 장소입니다. ASP.NET Core에서는 보통 `Program.cs`가 이 역할을 합니다.
- **Result 패턴**은 빈 장바구니 같은 예상 가능한 실패를 명시적으로 전달합니다.

## 학습 파일

| 파일 | 용도 |
| --- | --- |
| [Program.cs](./src/CheckoutExercise/Program.cs) | 실행 코드와 자동 검증 |
| [EXERCISES.md](./EXERCISES.md) | 작은 변경부터 정책 교체까지 단계별 실습 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 검증과 복습 체크리스트 |

## 버전 업데이트 (2026-07-19 확인)

- 로컬의 안정 SDK는 `.NET SDK 10.0.301`, 런타임은 `10.0.9`입니다. 예제는 `net10.0`과 C# 14로 작성해 이 환경에서 직접 검증합니다.
- Microsoft의 [.NET 10 발표](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)에 따르면 .NET 10은 2025-11-11 출시된 LTS이며 2028-11-10까지 지원됩니다.
- Microsoft Learn의 [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 최신 안정 C#이며 .NET 10에서 지원된다고 안내합니다. 오늘 실행 코드의 collection expression과 primary constructor도 안정 기능입니다.
- 프리뷰 전용 문법은 오늘의 안정 실행 코드에 넣지 않았습니다. 프리뷰를 시험할 때는 별도 프로젝트와 프리뷰 SDK를 사용하고, 업무 프로젝트의 언어 버전을 무심코 올리지 마세요.

## 완료 기준

- 일반 실행과 `--self-test`가 모두 성공한다.
- 기본 문법 표의 예제를 코드에서 찾을 수 있다.
- 서비스와 할인 정책의 책임 차이를 설명할 수 있다.
- [EXERCISES.md](./EXERCISES.md)의 1~3단계를 수행했다.
- [CHECKPOINT.md](./CHECKPOINT.md)를 코드 없이 짧게 답할 수 있다.
