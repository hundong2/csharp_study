using System.Diagnostics.CodeAnalysis;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

BasicSyntaxTour.Run();
await OrderWorkflowDemo.RunAsync();

public static class BasicSyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== Basic syntax tour ==");

        string customerName = "Mina";
        int retryCount = 1;
        decimal dailyBudget = 120_000m;
        bool isVip = true;
        string? optionalMemo = null;

        string retryMessage = retryCount switch
        {
            0 => "first try",
            <= 2 => "safe retry range",
            _ => "needs investigation"
        };

        string[] tags = ["syntax", "architecture", "async"];
        List<string> cart = ["BOOK-CLEAN", "MOUSE-PRO"];
        Dictionary<string, int> stockBySku = new()
        {
            ["BOOK-CLEAN"] = 5,
            ["MOUSE-PRO"] = 2
        };

        foreach (KeyValuePair<string, int> item in stockBySku)
        {
            Console.WriteLine($"stock {item.Key}: {item.Value}");
        }

        Func<decimal, decimal, decimal> addTax = (amount, rate) => amount + (amount * rate);
        decimal budgetWithTax = addTax(dailyBudget, 0.1m);

        Console.WriteLine($"customer: {customerName}, vip: {isVip}, retry: {retryMessage}");
        Console.WriteLine($"tags: {string.Join(", ", tags)} / cart count: {cart.Count}");
        Console.WriteLine($"memo length: {optionalMemo?.Length ?? 0}, budget with tax: {budgetWithTax:N0}");
        Console.WriteLine();
    }
}

public static class OrderWorkflowDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== Order workflow demo ==");

        DemoApp app = DemoCompositionRoot.Build();

        CreateOrderCommand command = new(
            CustomerEmail: "mina@example.com",
            Items:
            [
                new OrderItemRequest("BOOK-CLEAN", 1),
                new OrderItemRequest("MOUSE-PRO", 1)
            ]);

        Result<OrderReceipt> result = await app.Orders.PlaceOrderAsync(command);

        if (result.IsSuccess)
        {
            Console.WriteLine($"paid order {result.Value.OrderId}");
            Console.WriteLine($"total: {result.Value.Total}");
            Console.WriteLine($"status: {result.Value.Status}");
        }
        else
        {
            Console.WriteLine($"order failed: {result.Error}");
        }

        Console.WriteLine();
        Console.WriteLine("Run with --self-test to verify the beginner checkpoints.");
    }
}

public sealed class DemoApp(OrderService orders, InMemoryOrderRepository repository)
{
    public OrderService Orders { get; } = orders;
    public InMemoryOrderRepository Repository { get; } = repository;
}

public static class DemoCompositionRoot
{
    public static DemoApp Build()
    {
        InMemoryProductCatalog catalog = new(
        [
            new Product("BOOK-CLEAN", "Clean Architecture Notes", new Money(35_000m, "KRW")),
            new Product("MOUSE-PRO", "Ergonomic Mouse", new Money(45_000m, "KRW"))
        ]);

        InMemoryInventoryGateway inventory = new(new Dictionary<string, int>
        {
            ["BOOK-CLEAN"] = 5,
            ["MOUSE-PRO"] = 2
        });

        FakePaymentGateway payment = new(limit: new Money(100_000m, "KRW"));
        InMemoryOrderRepository repository = new();
        OrderService service = new(catalog, inventory, payment, repository);

        return new DemoApp(service, repository);
    }
}

public sealed class OrderService(
    IProductCatalog catalog,
    IInventoryGateway inventory,
    IPaymentGateway payment,
    IOrderRepository repository)
{
    public async ValueTask<Result<OrderReceipt>> PlaceOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerEmail) || !command.CustomerEmail.Contains('@'))
        {
            return Result<OrderReceipt>.Fail("A valid customer email is required.");
        }

        if (command.Items.Count == 0)
        {
            return Result<OrderReceipt>.Fail("At least one order item is required.");
        }

        List<OrderLine> lines = [];
        List<OrderItemRequest> reservedItems = [];

        foreach (OrderItemRequest item in command.Items)
        {
            if (item.Quantity <= 0)
            {
                return Result<OrderReceipt>.Fail($"Quantity must be positive: {item.Sku}");
            }

            Product? product = await catalog.FindAsync(item.Sku, cancellationToken);
            if (product is null)
            {
                return Result<OrderReceipt>.Fail($"Unknown product: {item.Sku}");
            }

            bool reserved = await inventory.TryReserveAsync(item.Sku, item.Quantity, cancellationToken);
            if (!reserved)
            {
                await ReleaseReservedItemsAsync(reservedItems, cancellationToken);
                return Result<OrderReceipt>.Fail($"Not enough stock: {item.Sku}");
            }

            reservedItems.Add(item);
            lines.Add(new OrderLine(item.Sku, product.Name, item.Quantity, product.Price));
        }

        Order order = new(command.CustomerEmail, lines);
        Result<string> paymentResult = await payment.CaptureAsync(order.Total, cancellationToken);

        if (!paymentResult.IsSuccess)
        {
            await ReleaseReservedItemsAsync(reservedItems, cancellationToken);
            return Result<OrderReceipt>.Fail(paymentResult.Error);
        }

        order.MarkPaid(paymentResult.Value);
        await repository.SaveAsync(order, cancellationToken);

        return Result<OrderReceipt>.Ok(new OrderReceipt(
            order.Id,
            order.Total,
            order.Status,
            DateTimeOffset.UtcNow));
    }

    private async ValueTask ReleaseReservedItemsAsync(
        IEnumerable<OrderItemRequest> reservedItems,
        CancellationToken cancellationToken)
    {
        foreach (OrderItemRequest item in reservedItems)
        {
            await inventory.ReleaseAsync(item.Sku, item.Quantity, cancellationToken);
        }
    }
}

public sealed class Order
{
    private readonly List<OrderLine> _lines;

    public Order(string customerEmail, IEnumerable<OrderLine> lines)
    {
        CustomerEmail = customerEmail;
        _lines = [.. lines];

        if (_lines.Count == 0)
        {
            throw new ArgumentException("Order must have at least one line.", nameof(lines));
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string CustomerEmail { get; }
    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;
    public string? PaymentReference { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public Money Total => _lines.Aggregate(new Money(0m, "KRW"), (sum, line) => sum + line.LineTotal);

    public void MarkPaid(string paymentReference)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOperationException("Only pending orders can be paid.");
        }

        PaymentReference = paymentReference;
        Status = OrderStatus.Paid;
    }
}

public sealed record OrderLine(string Sku, string Name, int Quantity, Money UnitPrice)
{
    public Money LineTotal => new(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money operator +(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot add money with different currencies.");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public override string ToString() => $"{Amount:N0} {Currency}";
}

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Cancelled
}

public sealed record Product(string Sku, string Name, Money Price);
public sealed record OrderItemRequest(string Sku, int Quantity);
public sealed record CreateOrderCommand(string CustomerEmail, IReadOnlyList<OrderItemRequest> Items);
public sealed record OrderReceipt(Guid OrderId, Money Total, OrderStatus Status, DateTimeOffset PaidAt);

public sealed record Result<T>(T? Value, string? Error)
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

public interface IProductCatalog
{
    ValueTask<Product?> FindAsync(string sku, CancellationToken cancellationToken);
}

public interface IInventoryGateway
{
    ValueTask<bool> TryReserveAsync(string sku, int quantity, CancellationToken cancellationToken);
    ValueTask ReleaseAsync(string sku, int quantity, CancellationToken cancellationToken);
    int GetAvailable(string sku);
}

public interface IPaymentGateway
{
    ValueTask<Result<string>> CaptureAsync(Money amount, CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    ValueTask SaveAsync(Order order, CancellationToken cancellationToken);
    ValueTask<Order?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class InMemoryProductCatalog(IReadOnlyList<Product> products) : IProductCatalog
{
    public ValueTask<Product?> FindAsync(string sku, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Product? product = products.FirstOrDefault(x => x.Sku == sku);
        return ValueTask.FromResult(product);
    }
}

public sealed class InMemoryInventoryGateway(Dictionary<string, int> stockBySku) : IInventoryGateway
{
    public ValueTask<bool> TryReserveAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!stockBySku.TryGetValue(sku, out int available) || available < quantity)
        {
            return ValueTask.FromResult(false);
        }

        stockBySku[sku] = available - quantity;
        return ValueTask.FromResult(true);
    }

    public ValueTask ReleaseAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        stockBySku[sku] = GetAvailable(sku) + quantity;
        return ValueTask.CompletedTask;
    }

    public int GetAvailable(string sku) => stockBySku.GetValueOrDefault(sku);
}

public sealed class FakePaymentGateway(Money limit) : IPaymentGateway
{
    public ValueTask<Result<string>> CaptureAsync(Money amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (amount.Amount > limit.Amount)
        {
            return ValueTask.FromResult(Result<string>.Fail($"Payment limit exceeded: {amount}"));
        }

        string paymentReference = $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        return ValueTask.FromResult(Result<string>.Ok(paymentReference));
    }
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = [];

    public ValueTask SaveAsync(Order order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _orders[order.Id] = order;
        return ValueTask.CompletedTask;
    }

    public ValueTask<Order?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _orders.TryGetValue(id, out Order? order);
        return ValueTask.FromResult(order);
    }
}

public static class SelfTest
{
    public static async Task RunAsync()
    {
        DemoApp app = DemoCompositionRoot.Build();

        Result<OrderReceipt> success = await app.Orders.PlaceOrderAsync(new CreateOrderCommand(
            "learner@example.com",
            [new OrderItemRequest("BOOK-CLEAN", 1)]));

        Assert(success.IsSuccess, "valid order should succeed");
        Assert(success.Value.Status == OrderStatus.Paid, "successful order should be paid");
        Assert(await app.Repository.FindAsync(success.Value.OrderId, CancellationToken.None) is not null,
            "successful order should be persisted");

        Result<OrderReceipt> invalidEmail = await app.Orders.PlaceOrderAsync(new CreateOrderCommand(
            "not-an-email",
            [new OrderItemRequest("BOOK-CLEAN", 1)]));

        Assert(!invalidEmail.IsSuccess, "invalid email should fail");

        Result<OrderReceipt> noStock = await app.Orders.PlaceOrderAsync(new CreateOrderCommand(
            "learner@example.com",
            [new OrderItemRequest("MOUSE-PRO", 99)]));

        Assert(!noStock.IsSuccess, "out of stock order should fail");
        Assert(noStock.Error.Contains("stock", StringComparison.OrdinalIgnoreCase),
            "out of stock error should explain stock failure");

        Result<OrderReceipt> tooExpensive = await app.Orders.PlaceOrderAsync(new CreateOrderCommand(
            "learner@example.com",
            [
                new OrderItemRequest("BOOK-CLEAN", 2),
                new OrderItemRequest("MOUSE-PRO", 1)
            ]));

        Assert(!tooExpensive.IsSuccess, "payment limit should fail");

        Console.WriteLine("Self-test passed: beginner syntax, order workflow, failure handling, and architecture contracts are valid.");
    }

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {message}");
        }
    }
}

