# 실습 문제

실습은 한 번에 많이 바꾸기보다 작은 변경을 하고 바로 실행하는 방식으로 진행합니다.

## 1단계: 기초 문법 손에 익히기

`Program.cs`의 `BasicSyntaxTour.Run()`을 읽고 아래를 직접 바꿔 보세요.

1. `customerName`을 본인 이름으로 바꿉니다.
2. `retryCount` 값을 0, 1, 3으로 바꿔 보며 조건문 결과가 어떻게 달라지는지 확인합니다.
3. `stockBySku`에 새 상품을 하나 추가하고 `foreach` 출력에 포함되는지 확인합니다.
4. `tags` 배열에 `"nullable"`을 추가하고 `string.Join` 결과를 확인합니다.

검증 질문:

- `var`는 타입이 없는 변수인가요?
- `foreach`는 인덱스가 필요할 때도 항상 최선인가요?
- `string?`와 `string`의 차이는 무엇인가요?

## 2단계: 주문 처리 흐름 바꾸기

`DemoCompositionRoot.Build()`에서 초기 재고를 바꿔 보세요.

1. `"BOOK-CLEAN"` 재고를 0으로 바꾸고 성공 주문이 실패하는지 확인합니다.
2. `"MOUSE-PRO"` 가격을 200000원으로 올리고 결제 한도 실패가 발생하는지 확인합니다.
3. `OrderService.PlaceOrderAsync()`에서 이메일 검증을 제거하면 어떤 self-test가 실패하는지 확인합니다.

검증 질문:

- 입력 검증을 UI에만 두면 왜 위험한가요?
- 결제가 실패했을 때 예약한 재고를 다시 풀어야 하는 이유는 무엇인가요?
- `IInventoryGateway` 인터페이스가 없으면 테스트가 왜 어려워지나요?

## 3단계: 아키텍처 리팩터링

지금은 학습을 위해 한 파일에 모든 타입을 넣었습니다. 실무 프로젝트라면 다음처럼 나눕니다.

```text
Domain/
  Money.cs
  Order.cs
  OrderLine.cs
Application/
  OrderService.cs
  CreateOrderCommand.cs
  Result.cs
Infrastructure/
  InMemoryInventoryGateway.cs
  FakePaymentGateway.cs
Program.cs
```

직접 나눠 보고 `dotnet run -- --self-test`를 다시 통과시켜 보세요.

## 4단계: preview 기능 비교 토론

C# 15 union types가 안정화되면 `Result<T>`를 다음처럼 더 직접적으로 모델링할 수 있습니다.

```csharp
public record Success<T>(T Value);
public record Failure(string Error);
public union OperationResult<T>(Success<T>, Failure);
```

오늘 프로젝트에서는 이 문법을 컴파일하지 않습니다. .NET 11 preview SDK와 `LangVersion=preview`가 필요한 preview 기능이기 때문입니다. 실무에서는 preview 기능을 핵심 서비스에 바로 넣기보다 별도 브랜치나 샘플 프로젝트에서 검증한 뒤 도입 여부를 결정합니다.

