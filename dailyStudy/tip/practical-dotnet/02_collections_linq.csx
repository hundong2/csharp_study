using System;
using System.Collections.Generic;
using System.Linq;

// readonly record struct:
// - 주문 한 건을 표현하는 작은 값 타입입니다.
// - decimal은 돈 계산에 자주 씁니다. double보다 소수점 오차 위험이 작습니다.
public readonly record struct Order(long Id, string Customer, decimal Amount);

// List<T>:
// 순서가 있는 데이터 목록입니다. DB 조회 결과나 API 응답 목록을 다룰 때 자주 사용합니다.
var orders = new List<Order>
{
    new(1, "Kim", 12000m),
    new(2, "Lee", 5000m),
    new(3, "Kim", 30000m),
    new(4, "Park", 7000m)
};

// Where:
// 조건에 맞는 항목만 필터링합니다.
// order => order.Amount >= 10000m 은 람다식입니다.
// "order를 받아 Amount가 10000 이상인지 검사한다"는 뜻입니다.
var expensiveOrders = orders.Where(order => order.Amount >= 10000m);

Console.WriteLine("[LINQ] Expensive orders");
foreach (Order order in expensiveOrders)
{
    Console.WriteLine($"{order.Id}: {order.Customer} {order.Amount}");
}

// Dictionary:
// Id를 key로 주문을 빠르게 찾을 수 있게 만듭니다.
// ToDictionary는 컬렉션을 Dictionary로 변환합니다.
Dictionary<long, Order> orderById = orders.ToDictionary(order => order.Id);

// TryGetValue:
// key가 있으면 true를 반환하고, out 변수에 찾은 값을 넣어 줍니다.
// key가 없을 때 예외를 던지지 않으므로 실무에서 안전하게 자주 씁니다.
if (orderById.TryGetValue(3, out Order found))
{
    Console.WriteLine($"[Dictionary] Found order: {found.Id}, {found.Amount}");
}

// GroupBy:
// 고객 이름별로 주문을 묶습니다.
// Select는 각 그룹을 새로운 모양의 객체로 변환합니다.
// new { ... }는 익명 타입입니다. 짧은 임시 결과를 만들 때 유용합니다.
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
