#nullable enable

using System;
using System.Collections.Generic;

// readonly record struct:
// - readonly: 생성 후 값을 바꾸지 않겠다는 뜻입니다.
// - record: 값 비교, ToString 출력, 생성자 문법이 편합니다.
// - struct: 값 타입입니다. 작은 값 객체에 적합합니다.
// 메모리 관점:
// - class는 보통 힙에 객체가 생기고 GC 대상이 됩니다.
// - 작은 struct는 값 자체가 전달되므로 별도 객체 할당을 줄일 수 있습니다.
// - 하지만 큰 struct는 복사 비용이 커질 수 있으므로 작고 불변인 값에만 쓰는 편이 좋습니다.
public readonly record struct OrderId(long Value);

public readonly record struct Money(decimal Amount, string Currency)
{
    public override string ToString()
    {
        return $"{Amount:N0} {Currency}";
    }
}

// interface:
// "이 속성/메서드를 반드시 제공해야 한다"는 계약입니다.
public interface IEntity
{
    OrderId Id { get; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
}

// 인터페이스 다중 상속:
// 클래스는 여러 클래스를 동시에 상속할 수 없지만,
// 인터페이스는 여러 인터페이스를 조합해서 새 계약을 만들 수 있습니다.
public interface IAuditableEntity : IEntity, IAuditable
{
}

// sealed:
// 이 클래스를 다른 클래스가 상속하지 못하게 합니다.
// 모델이 단순하고 확장 지점이 필요 없다면 sealed로 의도를 명확히 할 수 있습니다.
public sealed class Order : IAuditableEntity
{
    public Order(OrderId id, Money total, DateTimeOffset createdAt)
    {
        Id = id;
        Total = total;
        CreatedAt = createdAt;
    }

    public OrderId Id { get; }
    public Money Total { get; }
    public DateTimeOffset CreatedAt { get; }
}

// Repository interface:
// Application Service가 구체 DB 구현을 몰라도 되게 만드는 경계입니다.
public interface IOrderRepository
{
    void Save(Order order);
    Order? Find(OrderId id);
}

// Infrastructure 구현:
// 예제에서는 메모리 Dictionary를 쓰지만, 실무에서는 DB/Redis/API 구현이 들어갈 수 있습니다.
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<OrderId, Order> _storage = new();

    public void Save(Order order)
    {
        _storage[order.Id] = order;
    }

    public Order? Find(OrderId id)
    {
        return _storage.TryGetValue(id, out Order? order)
            ? order
            : null;
    }
}

// Application Service:
// "주문을 생성한다" 같은 사용 사례를 담당합니다.
// 저장 방식은 IOrderRepository 인터페이스 뒤로 숨깁니다.
public sealed class OrderApplicationService
{
    private readonly IOrderRepository _repository;

    public OrderApplicationService(IOrderRepository repository)
    {
        _repository = repository;
    }

    public Order CreateOrder(decimal amount, string currency)
    {
        var order = new Order(
            id: new OrderId(1),
            total: new Money(amount, currency),
            createdAt: DateTimeOffset.UtcNow);

        _repository.Save(order);
        return order;
    }
}

IOrderRepository repository = new InMemoryOrderRepository();
var service = new OrderApplicationService(repository);

Order created = service.CreateOrder(25000m, "KRW");
Order? loaded = repository.Find(created.Id);

Console.WriteLine($"[Architecture] Created order: {created.Id.Value}, Total={created.Total}");
Console.WriteLine($"[Architecture] Loaded order exists: {loaded is not null}");
