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

## deconstruct method 

- [Deconstruct method](./DeconstructEx.cs). 

## Expression bodied members 

- 람다 식을 이용한 메서드 정의 확대 
- [Expression bodied members](./ExpressionBodiedMember.cs). 

## Local function 

- [Local function](./LocalFunctionEx.cs). 