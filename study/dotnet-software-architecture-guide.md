# .NET 소프트웨어 아키텍처 완벽 가이드북

---

## 목차

1. [공변성(Covariance)과 반공변성(Contravariance)](#1-공변성과-반공변성)
2. [SOLID 원칙: DIP와 LSP](#2-solid-원칙-dip와-lsp)
3. [UML 다이어그램 화살표 가이드](#3-uml-다이어그램-화살표-가이드)
4. [C# 제네릭 타입 매개변수 네이밍](#4-c-제네릭-타입-매개변수-네이밍)
5. [소프트웨어 아키텍처 학습 로드맵](#5-소프트웨어-아키텍처-학습-로드맵)

---

# 1. 공변성과 반공변성

## 1.1 기본 개념

### 공변성 (Covariance) - `out` 키워드

**비유**: 과일 가게에서 "사과 상자"를 "과일 상자"로 취급할 수 있는 것

```csharp
// 공변성 예제 - 반환 타입에 사용
public interface IRepository<out T>    
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

// 구현
public class AnimalRepository : IRepository<Animal>     
{
    public Animal GetById(int id) => new Animal();
    public IEnumerable<Animal> GetAll() => new List<Animal>();
}

public class DogRepository : IRepository<Dog>
{
    public Dog GetById(int id) => new Dog();
    public IEnumerable<Dog> GetAll() => new List<Dog>();
}

// 사용 - 공변성 덕분에 가능
IRepository<Animal> repo = new DogRepository(); // Dog는 Animal의 하위 타입
Animal animal = repo.GetById(1); // Dog를 Animal로 받을 수 있음
```

### 반공변성 (Contravariance) - `in` 키워드

**비유**: "과일 처리기"를 "사과 처리기"로 사용할 수 있는 것

```csharp
// 반공변성 예제 - 매개변수 타입에 사용
public interface IValidator<in T>   
{
    bool Validate(T item);
}

public class AnimalValidator : IValidator<Animal>     
{
    public bool Validate(Animal animal)
    {
        return animal.Age > 0;
    }
}

// 사용 - 반공변성 덕분에 가능
IValidator<Dog> dogValidator = new AnimalValidator(); // Animal 검증기를 Dog 검증기로 사용
bool isValid = dogValidator.Validate(new Dog()); // Dog를 검증할 수 있음
```

## 1.2 실무 아키텍처 활용

### Repository 패턴에서의 공변성

```csharp
// 공변성을 활용한 유연한 Repository 패턴
public interface IReadOnlyRepository<out TEntity>           where TEntity : Entity
{
    TEntity FindById(Guid id);
    IEnumerable<TEntity> FindAll();
    IQueryable<TEntity> Query();
}

public interface IRepository<TEntity> : IReadOnlyRepository<TEntity>       
    where TEntity : Entity
{
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Delete(TEntity entity);
}

// 사용 예시
public class ProductService
{
    private readonly IReadOnlyRepository<Product> _productRepo;
    
    // 공변성 덕분에 IRepository<Product>도 주입 가능
    public ProductService(IReadOnlyRepository<Product> productRepo)
    {
        _productRepo = productRepo;
    }
    
    public IEnumerable<Product> GetAllProducts()
    {
        return _productRepo.FindAll();
    }
}
```

### 이벤트 핸들러에서의 반공변성

```csharp
// 반공변성을 활용한 이벤트 처리
public interface IEventHandler<in TEvent>        
{
    Task HandleAsync(TEvent @event);
}

// 기본 이벤트
public class DomainEvent
{
    public DateTime OccurredAt { get; set; }
    public string UserId { get; set; }
}

public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}

// 모든 도메인 이벤트를 처리하는 공통 핸들러
public class LoggingEventHandler : IEventHandler<DomainEvent>          
{
    public async Task HandleAsync(DomainEvent @event)
    {
        Console.WriteLine($"Event occurred at {@event.OccurredAt}");
        await Task.CompletedTask;
    }
}

// 이벤트 버스 구현
public class EventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        if (!_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = new List<object>();
        }
        _handlers[eventType].Add(handler);
    }
    
    public async Task PublishAsync<TEvent>(TEvent @event)
    {
        var eventType = typeof(TEvent);
        if (_handlers.ContainsKey(eventType))
        {
            foreach (var handler in _handlers[eventType])
            {
                await ((IEventHandler<TEvent>)handler).HandleAsync(@event);
            }
        }
    }
}

// 사용 예시
var eventBus = new EventBus();
var loggingHandler = new LoggingEventHandler();

// 반공변성 덕분에 DomainEvent 핸들러를 OrderCreatedEvent 핸들러로 사용 가능
eventBus.Subscribe<OrderCreatedEvent>(loggingHandler);

await eventBus.PublishAsync(new OrderCreatedEvent 
{ 
    OrderId = Guid.NewGuid(), 
    Amount = 100.00m,
    OccurredAt = DateTime.UtcNow,
    UserId = "user123"
});
```

## 1.3 실전 팁

### 공변성과 반공변성 선택 가이드

```csharp
// ✅ 공변성 (out) - 타입을 반환만 할 때
public interface IProducer<out T>
{
    T Produce();           // ✅ 반환만 함
    IEnumerable<T> GetAll(); // ✅ 반환만 함
    // void Consume(T item);  // ❌ 매개변수로 사용 불가
}

// ✅ 반공변성 (in) - 타입을 매개변수로만 받을 때
public interface IConsumer<in T>
{
    void Consume(T item);    // ✅ 매개변수로만 사용
    bool Process(T item);    // ✅ 매개변수로만 사용
    // T Produce();          // ❌ 반환 타입으로 사용 불가
}

// ✅ 불변(Invariant) - 둘 다 사용할 때
public interface IProcessor<T>
{
    T Process(T item);       // ✅ 둘 다 사용
    void Update(T item);     // ✅ 매개변수
    T Get();                 // ✅ 반환
}
```

---

# 2. SOLID 원칙: DIP와 LSP

## 2.1 의존성 역전 원칙 (Dependency Inversion Principle)

### 잘못된 예: 구체적인 클래스에 의존

```csharp
// ❌ 나쁜 예: 구체 클래스에 직접 의존
public class OrderService
{
    private readonly SqlServerOrderRepository _repository;
    private readonly SmtpEmailService _emailService;
    
    public OrderService()
    {
        _repository = new SqlServerOrderRepository(); // 강한 결합
        _emailService = new SmtpEmailService();       // 강한 결합
    }
    
    public void CreateOrder(Order order)
    {
        _repository.Save(order);
        _emailService.SendOrderConfirmation(order);
    }
}
```

### 올바른 예: 추상화에 의존

```csharp
// ✅ 좋은 예: 인터페이스(추상화)에 의존
public interface IOrderRepository
{
    void Save(Order order);
    Order GetById(Guid id);
}

public interface IEmailService
{
    Task SendOrderConfirmationAsync(Order order);
}

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;
    
    // 생성자 주입을 통한 의존성 역전
    public OrderService(IOrderRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public async Task CreateOrderAsync(Order order)
    {
        _repository.Save(order);
        await _emailService.SendOrderConfirmationAsync(order);
    }
}

// 구현체들
public class SqlServerOrderRepository : IOrderRepository
{
    public void Save(Order order) { /* SQL Server 구현 */ }
    public Order GetById(Guid id) { /* SQL Server 구현 */ return null; }
}

public class MongoDbOrderRepository : IOrderRepository
{
    public void Save(Order order) { /* MongoDB 구현 */ }
    public Order GetById(Guid id) { /* MongoDB 구현 */ return null; }
}

public class SmtpEmailService : IEmailService
{
    public Task SendOrderConfirmationAsync(Order order) 
    { 
        /* SMTP 구현 */ 
        return Task.CompletedTask;
    }
}

public class SendGridEmailService : IEmailService
{
    public Task SendOrderConfirmationAsync(Order order) 
    { 
        /* SendGrid 구현 */ 
        return Task.CompletedTask;
    }
}
```

### DI 컨테이너 설정 (ASP.NET Core)

```csharp
// Program.cs 또는 Startup.cs
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // DIP를 지원하는 의존성 등록
        builder.Services.AddScoped<IOrderRepository, SqlServerOrderRepository>();
        builder.Services.AddScoped<IEmailService, SendGridEmailService>();
        builder.Services.AddScoped<OrderService>();
        
        var app = builder.Build();
        app.Run();
    }
}

// 컨트롤러에서 사용
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    
    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        await _orderService.CreateOrderAsync(order);
        return Ok();
    }
}
```

## 2.2 리스코프 치환 원칙 (Liskov Substitution Principle)

### LSP의 핵심

> 하위 타입은 상위 타입을 완전히 대체할 수 있어야 한다.

### 잘못된 예: LSP 위반

```csharp
// ❌ 나쁜 예: 정사각형-직사각형 문제
public class Rectangle
{
    public virtual int Width { get; set; }
    public virtual int Height { get; set; }
    
    public int GetArea() => Width * Height;
}

public class Square : Rectangle
{
    private int _size;
    
    public override int Width
    {
        get => _size;
        set => _size = value; // Height도 같이 변경되어야 함
    }
    
    public override int Height
    {
        get => _size;
        set => _size = value; // Width도 같이 변경되어야 함
    }
}

// 문제가 발생하는 코드
public void TestRectangle(Rectangle rect)
{
    rect.Width = 5;
    rect.Height = 10;
    
    // Rectangle이라면 50을 기대하지만
    // Square라면 100이 나옴 (LSP 위반!)
    Assert.Equal(50, rect.GetArea()); // Square일 때 실패
}
```

### 올바른 예: LSP 준수

```csharp
// ✅ 좋은 예: 공통 추상화 사용
public interface IShape
{
    int GetArea();
}

public class Rectangle : IShape
{
    public int Width { get; set; }
    public int Height { get; set; }
    
    public int GetArea() => Width * Height;
}

public class Square : IShape
{
    public int Size { get; set; }
    
    public int GetArea() => Size * Size;
}

// 올바른 사용
public void TestShape(IShape shape)
{
    int area = shape.GetArea(); // 각 구현체가 올바르게 동작
}
```

### 실전 예시: 결제 시스템

```csharp
// ✅ LSP를 준수하는 결제 시스템
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessPaymentAsync(decimal amount, PaymentDetails details);
    Task<bool> CanProcessAsync(decimal amount);
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; }
    public string Message { get; set; }
}

public class CreditCardPaymentProcessor : IPaymentProcessor
{
    public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, PaymentDetails details)
    {
        // 신용카드 결제 로직
        if (amount <= 0)
            return new PaymentResult { Success = false, Message = "Invalid amount" };
            
        // 실제 결제 처리
        return new PaymentResult 
        { 
            Success = true, 
            TransactionId = Guid.NewGuid().ToString(),
            Message = "Payment processed successfully"
        };
    }
    
    public async Task<bool> CanProcessAsync(decimal amount)
    {
        return amount > 0 && amount <= 10000; // 신용카드 한도
    }
}

public class PayPalPaymentProcessor : IPaymentProcessor
{
    public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, PaymentDetails details)
    {
        // PayPal 결제 로직
        if (amount <= 0)
            return new PaymentResult { Success = false, Message = "Invalid amount" };
            
        // 실제 결제 처리
        return new PaymentResult 
        { 
            Success = true, 
            TransactionId = Guid.NewGuid().ToString(),
            Message = "PayPal payment processed successfully"
        };
    }
    
    public async Task<bool> CanProcessAsync(decimal amount)
    {
        return amount > 0; // PayPal은 금액 제한 없음
    }
}

// LSP를 준수하는 사용 코드
public class PaymentService
{
    private readonly IEnumerable<IPaymentProcessor> _processors;
    
    public PaymentService(IEnumerable<IPaymentProcessor> processors)
    {
        _processors = processors;
    }
    
    public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, PaymentDetails details)
    {
        // 어떤 구현체를 사용해도 동일하게 동작 (LSP 준수)
        foreach (var processor in _processors)
        {
            if (await processor.CanProcessAsync(amount))
            {
                return await processor.ProcessPaymentAsync(amount, details);
            }
        }
        
        return new PaymentResult 
        { 
            Success = false, 
            Message = "No processor available for this amount" 
        };
    }
}
```

## 2.3 DIP와 LSP의 시너지

```csharp
// DIP + LSP를 모두 준수하는 아키텍처
public interface INotificationService
{
    Task SendAsync(Notification notification);
    bool SupportsChannel(NotificationChannel channel);
}

public enum NotificationChannel
{
    Email,
    SMS,
    Push
}

public class Notification
{
    public string Recipient { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public NotificationChannel Channel { get; set; }
}

// 각 구현체는 LSP를 준수하며 동일한 방식으로 동작
public class EmailNotificationService : INotificationService
{
    public async Task SendAsync(Notification notification)
    {
        // 이메일 전송 로직
        Console.WriteLine($"Sending email to {notification.Recipient}");
        await Task.CompletedTask;
    }
    
    public bool SupportsChannel(NotificationChannel channel)
        => channel == NotificationChannel.Email;
}

public class SmsNotificationService : INotificationService
{
    public async Task SendAsync(Notification notification)
    {
        // SMS 전송 로직
        Console.WriteLine($"Sending SMS to {notification.Recipient}");
        await Task.CompletedTask;
    }
    
    public bool SupportsChannel(NotificationChannel channel)
        => channel == NotificationChannel.SMS;
}

public class PushNotificationService : INotificationService
{
    public async Task SendAsync(Notification notification)
    {
        // Push 알림 전송 로직
        Console.WriteLine($"Sending push notification to {notification.Recipient}");
        await Task.CompletedTask;
    }
    
    public bool SupportsChannel(NotificationChannel channel)
        => channel == NotificationChannel.Push;
}

// DIP를 활용한 의존성 주입
public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationService> _services;
    
    public NotificationDispatcher(IEnumerable<INotificationService> services)
    {
        _services = services; // DIP: 추상화에 의존
    }
    
    public async Task DispatchAsync(Notification notification)
    {
        // LSP: 어떤 구현체를 사용해도 동일하게 동작
        var service = _services.FirstOrDefault(s => 
            s.SupportsChannel(notification.Channel));
            
        if (service == null)
            throw new InvalidOperationException(
                $"No service found for channel {notification.Channel}");
                
        await service.SendAsync(notification);
    }
}

// DI 등록
services.AddScoped<INotificationService, EmailNotificationService>();
services.AddScoped<INotificationService, SmsNotificationService>();
services.AddScoped<INotificationService, PushNotificationService>();
services.AddScoped<NotificationDispatcher>();
```

---

# 3. UML 다이어그램 화살표 가이드

## 3.1 클래스 다이어그램의 화살표

### 일반화 (Generalization) - 상속
```
실선 + 빈 삼각형 (─────▷)
```

```csharp
// 일반화: Vehicle ◁─── Car
public class Vehicle
{
    public string Brand { get; set; }
    public void Start() { }
}

public class Car : Vehicle
{
    public int Doors { get; set; }
}
```

**UML 표현:**
```
┌─────────────┐
│   Vehicle   │
├─────────────┤
│ + Brand     │
│ + Start()   │
└─────────────┘
       △
       │
       │
┌─────────────┐
│     Car     │
├─────────────┤
│ + Doors     │
└─────────────┘
```

### 실체화 (Realization) - 인터페이스 구현
```
점선 + 빈 삼각형 (┄┄┄┄┄▷)
```

```csharp
// 실체화: IRepository ◁┄┄┄ SqlRepository
public interface IRepository
{
    void Save(object entity);
}

public class SqlRepository : IRepository
{
    public void Save(object entity)
    {
        // SQL 저장 로직
    }
}
```

**UML 표현:**
```
┌──────────────────┐
│  «interface»     │
│   IRepository    │
├──────────────────┤
│ + Save(entity)   │
└──────────────────┘
         △
         ┊
         ┊
┌──────────────────┐
│  SqlRepository   │
├──────────────────┤
│ + Save(entity)   │
└──────────────────┘
```

### 연관 (Association) - 클래스 간 관계
```
실선 + 화살표 (─────>)
```

```csharp
// 연관: Order ────> Customer
public class Order
{
    public Customer Customer { get; set; }
}

public class Customer
{
    public string Name { get; set; }
}
```

**UML 표현:**
```
┌─────────────┐          ┌──────────────┐
│    Order    │─────────>│   Customer   │
├─────────────┤          ├──────────────┤
│ + Customer  │          │ + Name       │
└─────────────┘          └──────────────┘
```

### 집합 (Aggregation) - 전체와 부분
```
실선 + 빈 다이아몬드 (◇─────)
```

```csharp
// 집합: Department ◇───── Employee
public class Department
{
    public List<Employee> Employees { get; set; }
}

public class Employee
{
    public string Name { get; set; }
    // Employee는 Department 없이도 존재 가능
}
```

**UML 표현:**
```
┌──────────────┐          ┌──────────────┐
│  Department  │◇────────│   Employee   │
├──────────────┤          ├──────────────┤
│ + Employees  │  1    * │ + Name       │
└──────────────┘          └──────────────┘
```

### 합성 (Composition) - 강한 소유
```
실선 + 찬 다이아몬드 (◆─────)
```

```csharp
// 합성: House ◆───── Room
public class House
{
    private List<Room> _rooms;
    
    public House()
    {
        _rooms = new List<Room>
        {
            new Room("Living Room"),
            new Room("Bedroom")
        };
    }
    // Room은 House가 파괴되면 함께 파괴됨
}

public class Room
{
    public string Name { get; private set; }
    
    public Room(string name)
    {
        Name = name;
    }
}
```

**UML 표현:**
```
┌──────────────┐          ┌──────────────┐
│    House     │◆────────│     Room     │
├──────────────┤          ├──────────────┤
│ - _rooms     │  1    * │ + Name       │
└──────────────┘          └──────────────┘
```

### 의존 (Dependency) - 일시적 관계
```
점선 + 화살표 (┄┄┄┄┄>)
```

```csharp
// 의존: OrderService ┄┄┄> EmailService
public class OrderService
{
    public void ProcessOrder(Order order, EmailService emailService)
    {
        // 메서드 매개변수로만 사용 (일시적 의존)
        emailService.SendConfirmation(order);
    }
}

public class EmailService
{
    public void SendConfirmation(Order order) { }
}
```

**UML 표현:**
```
┌──────────────────┐          ┌──────────────────┐
│  OrderService    │┄┄┄┄┄┄┄┄>│  EmailService    │
├──────────────────┤          ├──────────────────┤
│ + ProcessOrder() │          │ + SendConfirm()  │
└──────────────────┘          └──────────────────┘
```

## 3.2 시퀀스 다이어그램의 화살표

### 동기 호출
```
실선 + 닫힌 화살표 (─────▶)
```

```csharp
public class Controller
{
    public ActionResult GetUser(int id)
    {
        var user = _service.GetUser(id); // 동기 호출
        return Ok(user);
    }
}
```

### 비동기 호출
```
실선 + 열린 화살표 (─────>)
```

```csharp
public class Controller
{
    public async Task<ActionResult> GetUserAsync(int id)
    {
        var user = await _service.GetUserAsync(id); // 비동기 호출
        return Ok(user);
    }
}
```

### 반환
```
점선 + 열린 화살표 (┄┄┄┄┄>)
```

**시퀀스 다이어그램 예시:**
```
Client          Controller        Service          Database
  │                  │                │                │
  │─────GetUser()──▶│                │                │
  │                  │──GetUser()────▶│                │
  │                  │                │──Query()──────▶│
  │                  │                │◀┄┄Result┄┄┄┄┄┄│
  │                  │◀┄┄User┄┄┄┄┄┄┄┄│                │
  │◀┄┄User┄┄┄┄┄┄┄┄┄┄│                │                │
```

## 3.3 화살표 방향의 의미

### 정보 흐름 방향

```csharp
// A ────> B : A가 B를 알고 있음 (A는 B에 의존)
public class OrderService  // A
{
    private readonly IEmailService _emailService;  // B를 알고 있음
    
    public OrderService(IEmailService emailService)
    {
        _emailService = emailService;
    }
}

// B는 A를 모름
public interface IEmailService  // B
{
    void SendEmail(string to, string message);
}
```

### 의존성 방향

```csharp
// 고수준 ────> 저수준 (잘못된 설계)
public class OrderController
{
    private SqlServerOrderRepository _repository;  // ❌ 구체적인 구현에 의존
}

// 고수준 ◁┄┄┄ 저수준 (DIP 적용)
public class OrderController
{
    private IOrderRepository _repository;  // ✅ 추상화에 의존
}

public class SqlServerOrderRepository : IOrderRepository
{
    // 저수준 모듈이 고수준의 인터페이스를 구현
}
```

## 3.4 실전 아키텍처 다이어그램

### Clean Architecture 레이어 다이어그램

```
┌─────────────────────────────────────────┐
│            Presentation Layer            │
│  (Controllers, Views, ViewModels)        │
└─────────────────┬───────────────────────┘
                  │ depends on
                  ▼
┌─────────────────────────────────────────┐
│          Application Layer               │
│     (Use Cases, Services, DTOs)          │
└─────────────────┬───────────────────────┘
                  │ depends on
                  ▼
┌─────────────────────────────────────────┐
│            Domain Layer                  │
│   (Entities, Value Objects, Events)      │
└─────────────────△───────────────────────┘
                  │ implements
                  │
┌─────────────────┴───────────────────────┐
│         Infrastructure Layer             │
│  (Repositories, External Services)       │
└─────────────────────────────────────────┘
```

### 코드로 표현

```csharp
// Domain Layer (핵심)
public class Order
{
    public Guid Id { get; private set; }
    public decimal Total { get; private set; }
}

public interface IOrderRepository  // Domain에 정의된 인터페이스
{
    Task SaveAsync(Order order);
}

// Application Layer
public class CreateOrderUseCase
{
    private readonly IOrderRepository _repository;
    
    public CreateOrderUseCase(IOrderRepository repository)
    {
        _repository = repository;
    }
    
    public async Task ExecuteAsync(CreateOrderDto dto)
    {
        var order = new Order(/* ... */);
        await _repository.SaveAsync(order);
    }
}

// Infrastructure Layer (Domain 인터페이스 구현)
public class SqlOrderRepository : IOrderRepository
{
    public async Task SaveAsync(Order order)
    {
        // SQL 구현
    }
}

// Presentation Layer
public class OrdersController
{
    private readonly CreateOrderUseCase _useCase;
    
    public OrdersController(CreateOrderUseCase useCase)
    {
        _useCase = useCase;
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        await _useCase.ExecuteAsync(dto);
        return Ok();
    }
}
```

---

# 4. C# 제네릭 타입 매개변수 네이밍

## 4.1 기본 네이밍 규칙

### 단일 타입 매개변수는 `T` 사용

```csharp
// ✅ 단순한 제네릭 클래스
public class Repository<T>
{
    public T GetById(int id) { return default; }
    public void Save(T entity) { }
}

// ✅ 단순한 제네릭 메서드
public T Clone<T>(T source) where T : ICloneable
{
    return (T)source.Clone();
}
```

### 여러 타입 매개변수는 의미 있는 이름 사용

```csharp
// ✅ 의미 있는 이름 사용
public interface IConverter<TInput, TOutput>
{
    TOutput Convert(TInput input);
}

public class Dictionary<TKey, TValue>
{
    public void Add(TKey key, TValue value) { }
    public TValue Get(TKey key) { return default; }
}

// ✅ Repository 패턴
public interface IRepository<TEntity, TKey> 
    where TEntity : class
{
    TEntity GetById(TKey id);
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Delete(TKey id);
}
```

## 4.2 도메인별 네이밍 컨벤션

### 엔티티와 도메인 객체

```csharp
// ✅ Entity, Aggregate, VO (Value Object)
public interface IRepository<TEntity> where TEntity : Entity
{
    TEntity FindById(Guid id);
}

public interface IAggregateRepository<TAggregate> 
    where TAggregate : IAggregateRoot
{
    TAggregate GetById(Guid id);
}

public class Specification<TEntity> where TEntity : Entity
{
    public bool IsSatisfiedBy(TEntity entity) { return true; }
}
```

### 이벤트와 메시지

```csharp
// ✅ Event, Command, Query
public interface IEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event);
}

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command);
}

public interface IQueryHandler<TQuery, TResult> 
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query);
}

// 사용 예시
public class CreateOrderCommand : ICommand
{
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand>
{
    public async Task HandleAsync(CreateOrderCommand command)
    {
        // 주문 생성 로직
    }
}
```

### 요청과 응답

```csharp
// ✅ Request, Response
public interface IRequestHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request);
}

// 사용 예시
public class GetUserRequest
{
    public Guid UserId { get; set; }
}

public class GetUserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class GetUserRequestHandler : IRequestHandler<GetUserRequest, GetUserResponse>
{
    public async Task<GetUserResponse> HandleAsync(GetUserRequest request)
    {
        // 사용자 조회 로직
        return new GetUserResponse();
    }
}
```

## 4.3 제약 조건과 함께 사용

```csharp
// ✅ 제약 조건으로 의도를 명확히
public class EntityRepository<TEntity, TKey> 
    where TEntity : Entity<TKey>
    where TKey : IEquatable<TKey>
{
    public TEntity GetById(TKey id) { return default; }
}

// ✅ 여러 제약 조건
public class JsonSerializer<T> 
    where T : class, new()
{
    public string Serialize(T obj) { return ""; }
    public T Deserialize(string json) { return new T(); }
}

// ✅ Enum 제약
public class EnumHelper<TEnum> where TEnum : struct, Enum
{
    public static TEnum Parse(string value)
    {
        return Enum.Parse<TEnum>(value);
    }
    
    public static IEnumerable<TEnum> GetValues()
    {
        return Enum.GetValues<TEnum>();
    }
}
```

## 4.4 함수형 프로그래밍 스타일

```csharp
// ✅ Source, Target (변환)
public interface IMapper<TSource, TTarget>
{
    TTarget Map(TSource source);
}

// ✅ From, To (변환)
public static class Converter
{
    public static TTo Convert<TFrom, TTo>(TFrom value) 
        where TTo : IConvertible
    {
        return (TTo)System.Convert.ChangeType(value, typeof(TTo));
    }
}

// ✅ T1, T2, TResult (함수 조합)
public delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);

public static class FunctionExtensions
{
    public static Func<T1, TResult> Compose<T1, T2, TResult>(
        this Func<T1, T2> f, 
        Func<T2, TResult> g)
    {
        return x => g(f(x));
    }
}
```

## 4.5 실전 예시: CQRS 패턴

```csharp
// ✅ 명확한 네이밍으로 CQRS 구현
public interface ICommand { }
public interface IQuery<TResult> { }

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public Dictionary<string, string[]> Errors { get; set; }
}

// Command 예시
public class CreateProductCommand : ICommand
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand>
{
    private readonly IProductRepository _repository;
    
    public CreateProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<CommandResult> HandleAsync(
        CreateProductCommand command, 
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = command.Name,
            Price = command.Price,
            StockQuantity = command.StockQuantity
        };
        
        await _repository.AddAsync(product);
        
        return new CommandResult { Success = true };
    }
}

// Query 예시
public class GetProductByIdQuery : IQuery<ProductDto>
{
    public Guid ProductId { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IReadOnlyProductRepository _repository;
    
    public GetProductByIdQueryHandler(IReadOnlyProductRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<ProductDto> HandleAsync(
        GetProductByIdQuery query, 
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(query.ProductId);
        
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price
        };
    }
}

// Mediator 패턴으로 통합
public interface IMediator
{
    Task<CommandResult> SendAsync<TCommand>(TCommand command) 
        where TCommand : ICommand;
        
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
}
```

## 4.6 피해야 할 네이밍

```csharp
// ❌ 나쁜 예: 의미 없는 단일 문자
public class Processor<A, B, C, D>
{
    public D Process(A a, B b, C c) { return default; }
}

// ✅ 좋은 예: 명확한 이름
public class OrderProcessor<TOrder, TCustomer, TPayment, TResult>
{
    public TResult Process(TOrder order, TCustomer customer, TPayment payment) 
    { 
        return default; 
    }
}

// ❌ 나쁜 예: 헝가리안 표기법
public interface IMapper<tSource, tDestination>  // 소문자 't'
{
    tDestination Map(tSource source);
}

// ✅ 좋은 예: PascalCase
public interface IMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
}
```

---

# 5. 소프트웨어 아키텍처 학습 로드맵

## 5.1 기초 단계 (1-3개월)

### 1단계: OOP 원칙 마스터

```csharp
// ✅ SOLID 원칙을 코드로 체화
// 실습 프로젝트: 간단한 도서관 관리 시스템

// Single Responsibility Principle
public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string ISBN { get; set; }
}

public class BookRepository
{
    public void Save(Book book) { }
    public Book GetById(Guid id) { return null; }
}

public class BookValidator
{
    public bool Validate(Book book)
    {
        return !string.IsNullOrEmpty(book.Title) 
            && !string.IsNullOrEmpty(book.ISBN);
    }
}

// Open/Closed Principle
public abstract class BookPriceCalculator
{
    public abstract decimal Calculate(Book book);
}

public class StandardPriceCalculator : BookPriceCalculator
{
    public override decimal Calculate(Book book)
    {
        return 10.00m; // 기본 가격
    }
}

public class DiscountPriceCalculator : BookPriceCalculator
{
    private readonly decimal _discountRate;
    
    public DiscountPriceCalculator(decimal discountRate)
    {
        _discountRate = discountRate;
    }
    
    public override decimal Calculate(Book book)
    {
        return 10.00m * (1 - _discountRate);
    }
}
```

### 학습 자료
- Clean Code (Robert C. Martin)
- Head First Design Patterns
- C# 공식 문서의 Design Patterns 섹션

## 5.2 중급 단계 (3-6개월)

### 2단계: 디자인 패턴 실전 적용

```csharp
// ✅ 실전 디자인 패턴
// 실습 프로젝트: 전자상거래 주문 시스템

// Strategy Pattern
public interface IShippingStrategy
{
    decimal CalculateCost(Order order);
    TimeSpan EstimateDeliveryTime(Order order);
}

public class StandardShipping : IShippingStrategy
{
    public decimal CalculateCost(Order order) => 5.00m;
    public TimeSpan EstimateDeliveryTime(Order order) => TimeSpan.FromDays(5);
}

public class ExpressShipping : IShippingStrategy
{
    public decimal CalculateCost(Order order) => 15.00m;
    public TimeSpan EstimateDeliveryTime(Order order) => TimeSpan.FromDays(2);
}

// Factory Pattern
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessAsync(decimal amount);
}

public class PaymentProcessorFactory
{
    public IPaymentProcessor Create(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.CreditCard => new CreditCardProcessor(),
            PaymentMethod.PayPal => new PayPalProcessor(),
            PaymentMethod.BankTransfer => new BankTransferProcessor(),
            _ => throw new ArgumentException("Invalid payment method")
        };
    }
}

// Observer Pattern (Events)
public class Order
{
    public event EventHandler<OrderCreatedEventArgs> OrderCreated;
    public event EventHandler<OrderShippedEventArgs> OrderShipped;
    
    public void Create()
    {
        // 주문 생성 로직
        OrderCreated?.Invoke(this, new OrderCreatedEventArgs(this));
    }
    
    public void Ship()
    {
        // 배송 시작 로직
        OrderShipped?.Invoke(this, new OrderShippedEventArgs(this));
    }
}

public class EmailNotificationService
{
    public EmailNotificationService(Order order)
    {
        order.OrderCreated += OnOrderCreated;
        order.OrderShipped += OnOrderShipped;
    }
    
    private void OnOrderCreated(object sender, OrderCreatedEventArgs e)
    {
        // 주문 생성 이메일 발송
    }
    
    private void OnOrderShipped(object sender, OrderShippedEventArgs e)
    {
        // 배송 시작 이메일 발송
    }
}
```

### 학습 자료
- Design Patterns: Elements of Reusable Object-Oriented Software (GoF)
- Refactoring: Improving the Design of Existing Code (Martin Fowler)

## 5.3 고급 단계 (6-12개월)

### 3단계: 아키텍처 패턴 마스터

```csharp
// ✅ Clean Architecture 구현
// 실습 프로젝트: 블로그 플랫폼

// Domain Layer - 가장 안쪽
namespace BlogPlatform.Domain
{
    public class Post : Entity
    {
        public string Title { get; private set; }
        public string Content { get; private set; }
        public DateTime PublishedAt { get; private set; }
        public Author Author { get; private set; }
        
        private Post() { } // EF Core
        
        public Post(string title, string content, Author author)
        {
            Title = title;
            Content = content;
            Author = author;
            PublishedAt = DateTime.UtcNow;
        }
        
        public void UpdateContent(string newContent)
        {
            Content = newContent;
            // Domain event
            AddDomainEvent(new PostUpdatedEvent(this));
        }
    }
    
    public interface IPostRepository
    {
        Task<Post> GetByIdAsync(Guid id);
        Task AddAsync(Post post);
        Task UpdateAsync(Post post);
    }
}

// Application Layer - Use Cases
namespace BlogPlatform.Application
{
    public class CreatePostCommand : ICommand
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public Guid AuthorId { get; set; }
    }
    
    public class CreatePostCommandHandler : ICommandHandler<CreatePostCommand>
    {
        private readonly IPostRepository _postRepository;
        private readonly IAuthorRepository _authorRepository;
        private readonly IUnitOfWork _unitOfWork;
        
        public CreatePostCommandHandler(
            IPostRepository postRepository,
            IAuthorRepository authorRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _authorRepository = authorRepository;
            _unitOfWork = unitOfWork;
        }
        
        public async Task<CommandResult> HandleAsync(CreatePostCommand command)
        {
            var author = await _authorRepository.GetByIdAsync(command.AuthorId);
            if (author == null)
                return CommandResult.Failure("Author not found");
                
            var post = new Post(command.Title, command.Content, author);
            await _postRepository.AddAsync(post);
            await _unitOfWork.CommitAsync();
            
            return CommandResult.Success();
        }
    }
}

// Infrastructure Layer - 외부 시스템
namespace BlogPlatform.Infrastructure
{
    public class PostRepository : IPostRepository
    {
        private readonly BlogDbContext _context;
        
        public PostRepository(BlogDbContext context)
        {
            _context = context;
        }
        
        public async Task<Post> GetByIdAsync(Guid id)
        {
            return await _context.Posts
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
        
        public async Task AddAsync(Post post)
        {
            await _context.Posts.AddAsync(post);
        }
        
        public async Task UpdateAsync(Post post)
        {
            _context.Posts.Update(post);
        }
    }
}

// Presentation Layer - API
namespace BlogPlatform.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IMediator _mediator;
        
        public PostsController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
        {
            var command = new CreatePostCommand
            {
                Title = request.Title,
                Content = request.Content,
                AuthorId = request.AuthorId
            };
            
            var result = await _mediator.SendAsync(command);
            
            return result.Success 
                ? Ok(result) 
                : BadRequest(result);
        }
    }
}
```

### 학습 자료
- Clean Architecture (Robert C. Martin)
- Domain-Driven Design (Eric Evans)
- Implementing Domain-Driven Design (Vaughn Vernon)

## 5.4 전문가 단계 (12개월+)

### 4단계: 마이크로서비스와 분산 시스템

```csharp
// ✅ 마이크로서비스 통신 패턴
// 실습 프로젝트: 전자상거래 마이크로서비스

// API Gateway Pattern
public class ApiGateway
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    
    public async Task<OrderDetailsDto> GetOrderDetails(Guid orderId)
    {
        // 여러 마이크로서비스에서 데이터 수집
        var orderTask = GetOrderAsync(orderId);
        var customerTask = GetCustomerAsync(orderId);
        var productTask = GetProductsAsync(orderId);
        
        await Task.WhenAll(orderTask, customerTask, productTask);
        
        return new OrderDetailsDto
        {
            Order = orderTask.Result,
            Customer = customerTask.Result,
            Products = productTask.Result
        };
    }
    
    private async Task<OrderDto> GetOrderAsync(Guid orderId)
    {
        var url = $"{_config["OrderService:Url"]}/api/orders/{orderId}";
        return await _httpClient.GetFromJsonAsync<OrderDto>(url);
    }
}

// Event-Driven Architecture
public class OrderService
{
    private readonly IEventBus _eventBus;
    
    public async Task CreateOrderAsync(CreateOrderCommand command)
    {
        var order = new Order(command);
        await _repository.SaveAsync(order);
        
        // 이벤트 발행 - 다른 서비스들이 구독
        await _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.Total,
            Items = order.Items
        });
    }
}

// Inventory Service가 이벤트를 구독
public class InventoryService
{
    public async Task HandleAsync(OrderCreatedEvent @event)
    {
        foreach (var item in @event.Items)
        {
            await _inventoryRepository.ReduceStockAsync(
                item.ProductId, 
                item.Quantity);
        }
        
        // 재고 감소 완료 이벤트 발행
        await _eventBus.PublishAsync(new InventoryReservedEvent
        {
            OrderId = @event.OrderId
        });
    }
}

// Circuit Breaker Pattern (Polly 사용)
public class ResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    
    public ResilientHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        _circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
    
    public async Task<T> GetAsync<T>(string url)
    {
        var response = await _circuitBreakerPolicy.ExecuteAsync(() =>
            _httpClient.GetAsync(url));
            
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}

// Saga Pattern (Orchestration)
public class OrderSaga
{
    public async Task<SagaResult> ExecuteAsync(CreateOrderCommand command)
    {
        var sagaState = new OrderSagaState();
        
        try
        {
            // 1. 주문 생성
            sagaState.OrderId = await _orderService.CreateOrderAsync(command);
            
            // 2. 결제 처리
            sagaState.PaymentId = await _paymentService.ProcessPaymentAsync(
                command.CustomerId, 
                command.TotalAmount);
            
            // 3. 재고 예약
            await _inventoryService.ReserveInventoryAsync(
                sagaState.OrderId, 
                command.Items);
            
            // 4. 배송 준비
            await _shippingService.PrepareShipmentAsync(sagaState.OrderId);
            
            return SagaResult.Success(sagaState.OrderId);
        }
        catch (Exception ex)
        {
            // 보상 트랜잭션 실행
            await CompensateAsync(sagaState);
            return SagaResult.Failure(ex.Message);
        }
    }
    
    private async Task CompensateAsync(OrderSagaState state)
    {
        if (state.OrderId != Guid.Empty)
            await _orderService.CancelOrderAsync(state.OrderId);
            
        if (state.PaymentId != Guid.Empty)
            await _paymentService.RefundPaymentAsync(state.PaymentId);
    }
}
```

### 학습 자료
- Building Microservices (Sam Newman)
- Microservices Patterns (Chris Richardson)
- Designing Data-Intensive Applications (Martin Kleppmann)

## 5.5 지속적 학습 로드맵

### Phase 1: 기초 (1-3개월)
- [ ] SOLID 원칙 완전 이해
- [ ] 기본 디자인 패턴 (Singleton, Factory, Strategy, Observer)
- [ ] Git과 버전 관리
- [ ] 단위 테스트 작성

### Phase 2: 중급 (3-6개월)
- [ ] 모든 GoF 디자인 패턴 학습
- [ ] Repository 패턴과 Unit of Work
- [ ] Dependency Injection 심화
- [ ] 통합 테스트와 Mock 사용

### Phase 3: 고급 (6-12개월)
- [ ] Clean Architecture 구현
- [ ] Domain-Driven Design 적용
- [ ] CQRS 패턴
- [ ] Event Sourcing
- [ ] 성능 최적화와 프로파일링

### Phase 4: 전문가 (12개월+)
- [ ] 마이크로서비스 아키텍처
- [ ] Event-Driven Architecture
- [ ] 분산 시스템 패턴
- [ ] Container와 Orchestration (Docker, Kubernetes)
- [ ] Cloud Native 아키텍처 (Azure, AWS)

### 실전 프로젝트 추천

1. **기초 프로젝트**: Todo List 애플리케이션
   - CRUD 구현
   - Repository 패턴
   - 기본 디자인 패턴

2. **중급 프로젝트**: 블로그 플랫폼
   - 사용자 인증/인가
   - 파일 업로드
   - RESTful API
   - Entity Framework Core

3. **고급 프로젝트**: 전자상거래 시스템
   - Clean Architecture
   - CQRS + Event Sourcing
   - Payment Gateway 통합
   - 실시간 알림

4. **전문가 프로젝트**: 마이크로서비스 기반 소셜 미디어
   - 여러 마이크로서비스
   - Message Queue (RabbitMQ/Kafka)
   - API Gateway
   - Service Mesh
   - CI/CD 파이프라인

### 학습 팁

```csharp
// ✅ 매일 코드 리뷰 연습
public class CodeReviewPractice
{
    // 1. 매일 오픈소스 프로젝트 코드 읽기
    // 2. 자신의 코드를 다시 보고 개선점 찾기
    // 3. 동료와 코드 리뷰 교환
    
    public void DailyRoutine()
    {
        ReadOpenSourceCode();      // 30분
        WriteCleanCode();          // 2시간
        RefactorOldCode();         // 30분
        LearnNewPattern();         // 1시간
        DocumentLearning();        // 30분
    }
}

// ✅ 학습 추적
public class LearningTracker
{
    public void TrackProgress()
    {
        // 매주 학습한 내용 정리
        // 구현한 패턴과 원칙 기록
        // 개선할 점 식별
        // 다음 주 학습 목표 설정
    }
}
```

---

## 참고 자료

### 필독 도서
1. Clean Code - Robert C. Martin
2. Clean Architecture - Robert C. Martin
3. Design Patterns - Gang of Four
4. Domain-Driven Design - Eric Evans
5. Building Microservices - Sam Newman

### 온라인 리소스
- Microsoft Learn (공식 C# 문서)
- Pluralsight (아키텍처 코스)
- GitHub (오픈소스 프로젝트 분석)
- Stack Overflow (실전 문제 해결)

### 커뮤니티
- C# Discord 커뮤니티
- .NET Korea 사용자 그룹
- Software Architecture 관련 컨퍼런스

---

**마지막 조언**: 아키텍처는 한 번에 마스터할 수 없습니다. 작은 프로젝트부터 시작해서 점진적으로 복잡한 시스템을 구축하며 배우세요. 실패를 두려워하지 말고, 매일 조금씩 개선하는 것이 중요합니다.
