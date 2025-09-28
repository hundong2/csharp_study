# static 

## enum, class const 

- `using static`구문을 통해 선언 생략 가능. [Example Using Static](./UsingStaticEx.cs). 
- `확장 메서드`의 경우 문법적 모호성 때문에 사용불가. 

## dictionary initialize

```csharp
var weekends = new Dictionary<int, string>
{
    { 0, "sunday" },
    { 6, "saturday" },
    { 6, "saturday" } //error
}
```

```csharp
var weekends = new Dictionary<int, string>
{
    [0] = "sunday",
    [6] = "saturday",
    [6] = "saturday" // ok
}
```