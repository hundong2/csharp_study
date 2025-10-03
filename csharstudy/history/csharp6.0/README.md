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

## 사용자 정의 Task타입을 async method의 반환 타입으로 사용 가능 

- `ValueTask<T>`  
    - Async 메서드 내에서 await을 호출하지 않은 경우라면 불필요한 Task 객체 생성을 하지 않음으로써 성능을 높임.  
    - [ValueTask Example](./ValueTaskEx.cs). 

## Throw expression

- [Throw Example](./ThrowEx.cs). 

## literal expression

```csharp
int number1 = 10000000;
int number2 = 10_000_000;
int number3 = 1_0_0_0_0_000;
```

### using hexadecimal

```csharp
uint hex1 = 0xFFFFFFFF;
uint hex2 = 0xFF_FF_FF_FF;
uint hex3 = 0xFFFF_FFFF;
```

### using binary

```csharp
uint bin1 = 0b0001000100010001;
uint bin2 = 0b0001_0001_0001_0001;
```

## Pattern matching 

- `~에 대한`
  - 이미지에 대한 패턴 매칭
  - 문자열에 대한 패턴 매칭
  - 객체에 대한 패턴 매칭
  - 상수 패턴(Constant Patterns)
  - 타입 패턴(Type Patterns)
  - Var 패턴(Var Patterns)

### previous pattern matching 

- `as`

```csharp
object obj = new List<string>();
List<string> list = obj as List<string>;
list?.ForEach((e)=>Console.WriteLine(e));
```

- `c#7.0`

```csharp
if(obj is List<string> list)
{
    list.ForEach((e)=>Console.WriteLine(e));
}
```

```csharp
object[] objList = new object[] { 100, null, DateTime.Now, new ArrayList() };

foreach(var item in objList)
{
    if( item is 100)
    {
        Console.WriteLine(item);
    }
    else if(item is null)
    {
        Console.WriteLine("null");
    }
    else if(item is DateTime dt)
    {
        Console.WriteLine(dt);
    }
    else if(item is ArrayList arr)
    {
        Console.WriteLine(arr.Count);
    }
}
```

```csharp
if( item is var elem)
{
    Console.WriteLine(elem);
}
if( item is var _)
{
    //nothing 
}
```

- `var` type의 경우 is연산자가 의미가 없다. 
- 컴파일러 변환 코드에서는 항상 if문에서 true로 되기 때문이다. 

```csharp
object elem = item;
if(true)
{
    Console.WriteLine(elem);
}
```

## switch/case 문의 패턴 매칭

```csharp
foreach(object item in objList)
{
    switch(item)
    {
        case 100: //constant pattern
            break;
        case null://constant pattern
            break;
        case DateTime dt: //type pattern, using dt variable possible omission _, if didn't using value
            break;
        case DateTime _:
            break;
        case ArrayList arr: //type pattern
            break;
        case var elem:
            break;
    }
}
```

- using `when`

```csharp
switch(j)
{
    case int i when i > 300:
        break;
    default:
        break;
}
```

```csharp
{
    string text = "....";
    switch(text)
    {
        case var item when ( await ContainsAt(item, "https://www.naver.com")):
            Console.WriteLine("In Naver");
            break;
        case var item when ( await ContainsAt(item, "https://www.daum.net")):
            Console.WriteLine("In Daum");
            break;
        default:
            Console.WriteLine("None");
            break;
    }
}

static HttpClient _client = new HttpClient();
private async static Task<bool> ContainsAt(string item, string url)
{
    string text = await _client.GetStringAsync(url);
    return text.IndexOf(item) != -1;
}

```