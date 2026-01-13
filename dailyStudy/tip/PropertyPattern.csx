#!/usr/bin/env dotnet-script

using System;

enum Category { Book, Food, Toy }

record Product(string Name, Category Category, int Price);

enum OrderStatus { Pending, Paid, Shipped }

// Nullable struct 예시를 위해 struct 사용
readonly struct Order
{
    public OrderStatus Status { get; }
    public int TotalPrice { get; }
    public Order(OrderStatus status, int totalPrice) => (Status, TotalPrice) = (status, totalPrice);
}

static bool IsBook(Product? p) => p is { Category: Category.Book };
static bool IsCheapBook(Product? p) => p is { Category: Category.Book, Price: < 10000 };

static bool IsPaid(Order? o) => o is { Status: OrderStatus.Paid };
static bool IsBigPaid(Order? o) => o is { Status: OrderStatus.Paid, TotalPrice: >= 50000 };

var a = new Product("C# 입문서", Category.Book, 15000);
var b = new Product("사과", Category.Food, 3000);
Product? c = null;

Console.WriteLine($"a IsBook      : {IsBook(a)}");
Console.WriteLine($"b IsBook      : {IsBook(b)}");
Console.WriteLine($"c IsBook(null): {IsBook(c)}");
Console.WriteLine($"a IsCheapBook : {IsCheapBook(a)}");
Console.WriteLine($"b IsCheapBook : {IsCheapBook(b)}");

Order? o1 = new Order(OrderStatus.Paid, 70000);
Order? o2 = new Order(OrderStatus.Pending, 70000);
Order? o3 = null;

Console.WriteLine($"o1 IsPaid     : {IsPaid(o1)}");
Console.WriteLine($"o2 IsPaid     : {IsPaid(o2)}");
Console.WriteLine($"o3 IsPaid(null): {IsPaid(o3)}");
Console.WriteLine($"o1 IsBigPaid  : {IsBigPaid(o1)}");

// “풀어쓴 형태(개념적으로)”
// IsPaid(o)  <=>  o.HasValue && o.Value.Status == OrderStatus.Paid