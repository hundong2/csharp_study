# 15. readonly record, interface 다중 상속, 아키텍처 기초

실무 코드는 문법만으로 구성되지 않습니다. 데이터 모델, 인터페이스, 서비스, 저장소가 어떤 방향으로 의존해야 하는지 이해해야 유지보수 가능한 구조를 만들 수 있습니다.

## 1. `readonly record struct`

```csharp
public readonly record struct Money(decimal Amount, string Currency);
```

의미:

- `record`: 값 비교와 `ToString()` 출력이 편합니다.
- `struct`: 값 타입입니다. 작은 값 객체에 적합합니다.
- `readonly`: 생성 후 내부 값을 바꾸지 않겠다는 뜻입니다.

실무 사용 예:

- `Money`
- `OrderId`
- `CustomerId`
- `Coordinate`
- `Percentage`

메모리 관점:

- 작은 `readonly record struct`는 별도 힙 객체를 만들지 않을 수 있습니다.
- 값이 복사되므로 너무 큰 데이터에는 적합하지 않습니다.
- 불변 값 객체라서 멀티스레드에서 공유해도 상태 변경 위험이 줄어듭니다.

## 2. 인터페이스 다중 상속

C# 클래스는 여러 클래스를 동시에 상속할 수 없습니다.

```csharp
// 불가능
// public class MyClass : BaseA, BaseB
```

하지만 인터페이스는 여러 인터페이스를 상속할 수 있습니다.

```csharp
public interface IEntity
{
    long Id { get; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
}

public interface IAuditableEntity : IEntity, IAuditable
{
}
```

이 구조는 “이 타입은 ID도 있고 감사 시간도 있다”는 계약을 표현합니다.

## 3. 아키텍처 기본 방향

가장 단순한 실무 구조는 다음 네 층으로 생각할 수 있습니다.

```text
UI / API
  -> Application Service
      -> Domain Model
      -> Repository Interface
          -> Infrastructure Repository Implementation
```

의존 방향:

- API는 Application Service를 호출합니다.
- Application Service는 도메인 규칙을 실행합니다.
- Application Service는 Repository 인터페이스에 의존합니다.
- 실제 DB 구현은 Infrastructure 쪽에 둡니다.

중요한 점:

> 핵심 비즈니스 코드는 구체적인 DB, 파일, 외부 API 구현에 직접 묶이지 않는 것이 좋습니다.

## 4. 왜 이렇게 나누는가

- 테스트가 쉬워집니다.
- DB 구현을 바꿔도 비즈니스 로직 변경이 줄어듭니다.
- 도메인 규칙이 Controller나 UI에 흩어지지 않습니다.
- 인터페이스를 기준으로 의존성이 정리됩니다.

## 5. 초심자 기준 기억할 것

- `readonly record struct`는 작고 변하지 않는 값 객체에 적합합니다.
- 인터페이스는 여러 개를 상속할 수 있습니다.
- 클래스 다중 상속은 안 됩니다.
- 서비스는 “무엇을 할지”를 담당하고, 저장소는 “어디에 저장할지”를 담당합니다.
- 비즈니스 로직이 DB 코드에 직접 묶이지 않게 하는 것이 아키텍처의 출발점입니다.
