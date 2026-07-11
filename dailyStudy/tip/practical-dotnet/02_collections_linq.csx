using System;
using System.Collections.Generic;
using System.Linq;

public readonly record struct Order(long Id, string Customer, decimal Amount);

var orders = new List<Order>
{
    new(1, "Kim", 12000m),
    new(2, "Lee", 5000m),
    new(3, "Kim", 30000m),
    new(4, "Park", 7000m)
};

// Where:
// 조건에 맞는 항목만 필터링합니다.
var expensiveOrders = orders.Where(order => order.Amount >= 10000m);

Console.WriteLine("[LINQ] Expensive orders");
foreach (Order order in expensiveOrders)
{
    Console.WriteLine($"{order.Id}: {order.Customer} {order.Amount}");
}

// Dictionary:
// Id를 key로 주문을 빠르게 찾을 수 있게 만듭니다.
Dictionary<long, Order> orderById = orders.ToDictionary(order => order.Id);

if (orderById.TryGetValue(3, out Order found))
{
    Console.WriteLine($"[Dictionary] Found order: {found.Id}, {found.Amount}");
}

// GroupBy:
// 고객 이름별로 주문을 묶습니다.
var totalByCustomer = orders
    .GroupBy(order => order.Customer)
    .Select(group => new
    {
        Customer = group.Key,
        Total = group.Sum(order => order.Amount)
    });

Console.WriteLine("[GroupBy] Total by customer");
foreach (var item in totalByCustomer)
{
    Console.WriteLine($"{item.Customer}: {item.Total}");
}
