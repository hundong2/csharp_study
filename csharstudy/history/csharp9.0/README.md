# c# 9.0

## Records ( 레코드 ) 

- `struct`보다 `class`를 주로 사용하는데, 
- `Dictionary`에서 키 값으로 사용하고 싶다면 `Equals`와 `GetHashCode` 메서드를 재정의 하여 사용해야 한다. 

```csharp
public override int GetHashCode()
{
    return X ^ Y;
}

public override bool Equals(object obj)
{
    return this.Equals(obj as Point);
}
public virtual bool Equals(Point other)
{
    if(object.ReferenceEquals(other, null))
    {
        return false;
    }
    return ( this.X == other.X && this.Y == other.Y );
}
//==, != 직관적인 비교를 하기 위해서 함수를 더 추가 해야하는 경우 발생 
public static bool operator==(Point r1, Point r2)
{
    if(object.ReferenceEquals(r1, null))
    {
        if(object.ReferenceEquals(r2, null))
        {
            return true;
        }
        return false;
    }
    return r1.Equals(r2);
}
public static bool operator !=(Point r1, Point r2)
{
    return !r1.Equals(r2);
}
//ToString 고려 
public override string ToString()
{
    return $"xxx{xxx}xx";
}
```

- 위 코드들을 `record`하나로 대체 가능 
- `record` == `class` + `기본 생성 코드`

```csharp
public record Point
{
    public int X;
    public int Y;
}
```

## Init 설정자 추가 

- `immutable` : 불변 타입
    - `readonly struct`로 불변 타입을 `struct`에서 강제 할 수 있도록 도와주지만 `class`의 경우 개발자가 직접 작성해야 함. 

```csharp
public class Point 
{
    //get; private set 조합;
    public int X { get; }
    public int Y { get; private set; }
    //or field와 attribute를 분리해 정의 
    readonly int _x;
    readonly int _y;

    public int X => _x;
    public int Y => _y;

    //값 초기화시 반드시 생성자가 필요함. 
    public Point(int x, int y) => { this.X = x; this.Y = y; }
}
```

- 위 생성자 추가 구문의 단점을 보완하기 위해 생성 된 구문 `init`

```csharp
public class Point
{
    public int X { get; init; }
    public int Y { get; init; }
}
Point pt = new Point { X = 3, Y = 5 }; //개체 초기화 구문에서 값 설정 허용 
//별도의 생성자를 정의하지 않아도 프로퍼티에 값 설정이 가능하면서 이후 불변 개체로써 동작할 것을 컴파일러로 부터 보장 받게 됨. 
```

- `record`와 `init` 조합으로 사용

```csharp
public record Point
{
    public int X { get; init; }
    public int Y { get; init; }
}
```

- 위 코드를 더욱 줄여 다음과 같이 정의하게 되면 타입을 마치 생성자와 함께 정의하는 것 처럼 지원. 

```csharp
public record Point(int X, int Y) { }
```

- 위 코드는 컴파일러에 의해 아래의 코드로 변환되어 컴파일 된다. 

```csharp
public class Point
{
    public int X { get; init; }
    public int Y { get; init; }

    public Point (int x, int y ) => (X, Y ) = (x, y);
    public void Deconstruct(out int x, out int y) => (x, y) = X, Y;

}

Point pt1 = new Point(5,6); //생성자 제공 
(int x, int y) = pt1;//deconstruct 제공
Point pt2 = new Point() { X = 5, Y = 6 }; //기본 생성자와 함께 init초기화 가능 

//기본 생성자를 추가도 가능 
public record Point(int X, int Y)
{
    public Point(): this(0, 0) {}
}

```

- `init`연산자도 `set`과 같은 역할을 하기 때문에 블록 사용 가능

```csharp
public class PointF
{
    public int Y
    {
        get => Y;
        init
        {
            Y = value;
        }
    }
}
```

### with 연산자 사용

```csharp
Point pt1 = new Point(5,10);

Point pt2 = pt1 with { Y = pt1.Y + 2 }; //X = 5, Y = 12 
Point pt3 = pt1 with { X = pt1.X + 2 }; //X만 변경 
Point pt4 = pt1 with { X = pt1.X + 2, Y = pt1.Y + 2 }; //X, Y 변경
```

- C# 컴파일러만 사용할 수 있는 Clone 메서드를 protected 생성자와 함께 제공한다.

```csharp
public virtual Point <Clone>$()
{
    return new Point(this);
}

protected Point(Point original)
{
    this.X = original.X;
    this.Y = original.Y;
}
```

#### with 연산자 사용 시 컴파일 시퀀스

- `pt1 with { Y = pt1.Y + 2};` 코드는 아래와 같이 변경하여 컴파일 된다.

```csharp
Point pt2 = pt1.<Clone>$();
pt2.Y = pt1.Y + 2;
```

## 대상에 따라 new 식 추론 ( Target-typed new expressions )

```csharp
class Program
{
    static void Main(string[] args)
    {
        Point pt1 = new Ponit(5, 6); //C#2.0 이하 타입 모두 지정
        var pt2 = new Point(5,6); //C# 3.0 이상 : new 대상 타입을 추론해 var 결정
        Point pt3 = new(5,6); //c# 9.0 변수의 타입에 따라 new 연산자 타입을 결정 
    }
    public record Point(int X, int Y )
    {
        public Point() : this(0,0) {}
    }
}
```

- 배열 및 컬렉션의 초기화 코드를 더 단순하게 만들어 준다.  

```csharp
var linePt = new Point[]
{
    new(5,6),
    new() { X = 7, Y = 0 }
}

var dict = new Dictionary<Point, bool>()
{
    [new(5,6)] = true,
    [new(7,3)] = false,
    [new(){ X = 3, Y = 2}] = false
}
```

## 조건식 평가 

```csharp
class Program
{
    static void Main(string[] args)
    {
        var note = new Notebook();
        var desk = new Desktop();
        Computer prd = ( note ! = null )? note : desk; //C#8.0이하에서는 컴파일 오류 
        //8.0 이상에서는 타입 추론이 가능해져서 컴파일 오류 X 
        // 8.0 이하에서는 다음과 같이 
        //Computer prd = (note != null )? (Computer)note : desk;
        object returnvalue = (args.Length == 0 ) ? "empty": 1; //암시적 형변환이 가능 
        int? result = (args.Length != 0 ) ? 1: null;
    }
}

public class Computer() {}
public class Notebook : Computer {}
public class Desktop : Computer {}
```

## 로컬 함수에 특성 지정 가능 ( Attribute on local functions )

```csharp
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

class Program
{
    static void Main(string[] args)
    {
        Log("Main");
        Log(null);
    }
    [Conditional("DEBUG")]
    static void Log([AllowNull] string text)
    {
        string logText = $"[{Thread.CurrentThread.ManagedThreadId}] {text}";
        Console.WriteLine(logText);
    }
}
```

- extern 유형도 같이 쓸수 있게 되었다.  

```csharp
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
        MessageBox(IntPtr.Zero, "message", "title", 0 );
        //특성을 부여할 수 있으므로 extern P/Invoke 정의를 로컬 함수로도 가능 
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr h, string m, string c, int type);
    }
}
```

## static 익명 함수 

- [Example Static Anomy](./StaticAnomy.cs). 

## 익명 함수의 매개변수 무시 

```csharp
public class Class1
{
    void M(int _)
    {

    }
    public void M()
    {
        int _;
        _ = 5;
        Console.WriteLine(_); // print result : 5
    }
}

public class Class2
{
    void _()
    {

    }
}
```

- 위 `_`은 out 구문에서의 `_`과 다르다. `여러개`를 쓰면 `에러` 발생
- `out` 구문에서는 `여러개` `사용 가능`

```csharp
Class1 c1 = new Class1();
c1.TryParse("5", out _, out _); //식별자가 아니므로 두개 이상 사용 가능 

public class Class1
{
    public bool TryParse(string txt, out int n, out System.Net.IPAddress address)
    {
        n = 0;
        address = System.Net.IPAddress.Any;
        return true;
    }
    public void M(int _) {} //유효한 식별자
    public void M(int _, int _ ){} //2개 이상동일한 식별자 
}
```

## reference 

### 식별자(identifier)란?

- 코드 요소를 가리키는 이름입니다. 규칙: 문자/밑줄(_)로 시작, 대소문자 구분, 키워드는 @로 이스케이프 가능(예: @class).
- 식별자는 “선언”으로 만들어지고, 같은 스코프에선 중복 선언 불가.

### discard(_)란?

- C# 7+에서 도입된 “버리기 자리표시자”입니다. out, 패턴 매칭, 튜플 분해 등 특정 문맥에서 _를 쓰면 “값을 무시”하겠다는 뜻이고, 어떤 변수도 선언되지 않습니다. 
- 변수(식별자)가 아니므로 중복 충돌 개념이 없고, 여러 번 사용해도 됩니다.
- 질문 코드가 왜 “식별자가 아니라서 여러 번 가능”인가?
- 여기의 _는 “out 변수 선언”이 아니라 “out 값을 버린다”는 discard입니다.
- 로컬 변수가 만들어지지 않으므로 같은 호출식 안에서 out _를 여러 번 써도 스코프 충돌이 없습니다.
- 비교 예시: 식별자 '' vs discard ''


```csharp
using System;

public static class IdentifierVsDiscardDemo
{
    public static void Main()
    {
        // 1) discard: 변수 선언 없음 → 여러 번 가능
        if (int.TryParse("123", out _)) { /* 값 무시 */ }
        if (int.TryParse("456", out _)) { /* 또 무시 */ }

        // 2) 실제 변수(식별자) 사용: 한 번만 선언 가능
        if (int.TryParse("789", out var n)) Console.WriteLine(n);
        // if (int.TryParse("000", out var n)) { } // 컴파일 오류: n 중복 선언

        // 3) '_'를 식별자로 쓰는 경우(메서드 파라미터 등)
        M(10);             // OK
        // void M(int _, int _) { } // 컴파일 오류: 파라미터 이름 중복(둘 다 식별자)

        // 4) 튜플/패턴에서도 discard는 여러 번 가능
        var (_, y, _) = (1, 2, 3);   // 첫/셋째 요소 버리기
        Console.WriteLine(y);        // 2
    }

    static void M(int _) { /* 여기의 _는 '식별자' */ }
}
```

#### C#9.0부터는 익명 메서드와 람다 메서드에 대해서는 밑줄을 식별자가 아닌, 무시 구문으로 다룬다. 

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        Machine cl = new Machine();

        //c#8.0까지는 반드시 매개변수 명을 나열 
        cl.Run((string name, int time) => {return 0;});
        cl.Run((arg1, arg2) => { return 0;});
        //c# 9.0 부터 사용하지 않는 매개변수는 이름 생략 가능
        cl.Run((string _, int _) => { return 0; });
        cl.Run((_, _) => 5 );
        cl.Run(delegate(string _, int _ ){return 0;});
    }
    public delegate int RunDelegate(string name, int time);
    public class Machine
    {
        void M(int _){}
        public void Run(RunDelegate runnable)
        {
            Console.WriteLine(runnable(nameof(Machine), 1));
        }
    }

}
```

## TOP-Level statements ( 최상위 문 )

```csharp
System.Console.WriteLine("Hello World"); //위와 같이 한줄로 컴파일이 가능 
```

- 컴파일러는 위의 코드를 다음과 같이 변환하여 컴파일 해줌

```csharp
static class <Program>$
{
    static void <Main>$(string[] args)
    {
        //Input Top-level statements
        System.Console.WriteLine("Hello World");
    }
}
```

- 최상위 문에서 바로 사용 가능한 부분

```csharp
int argLen = args.Length; //최상위 문에서 args 접근 사용 가능
Console.WriteLine(args[0]);
```

|최상위문 포함 코드|선택 된 <Main>$ 유형|
|---|---|
|return 1;|static int <Main>$(string[] args) {...}|
|await Task.Delay(10); | static async Task <Main>$(string[] args){...}|
|await Task.Delay(10); return 1;| static async Task<int> <Main>$(string[] args){...}|
|그외|static void <Main>$(string[] args) {...}|

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

[DllImport("User32.dll", CharSet=CharSet.Unicode)]
static extern int MessageBox(IntPtr h, string m, string c, int type);

MessageBox(IntPtr.Zero,"c#9.0", "Top-level statements", 0);

Log("Hello World");

[Conditional("DEBUG")]
static void Log(string text)
{
    File.WriteAllText($"test.log", text);
}
```

## Pattern matching Improvement

```csharp
var t = (args.Length, "# of Args");

if(t is (int n, string _ )) {} //C#8.0 이전 
if(t is (int, string)) {} //C#9.0 변수명을 생략해 타입만 지정 가능 
object objValue = args.Length;

//switch 구문에서도 타입만 지정 가능
switch ( objValue)
{
    case int: break;
    case System.String: break;
}

static bool GreaterThan10(int number) =>
    number is > 10;
static bool GreaterThan10(int number) =>
    number switch
    {
        > 10 => true,
        _ => false
    }
```

```csharp
//is pattern(상수 비교가 아닌 경우)
static bool GreaterThanTarget(int number, int target) =>
    number is int value && ( value > target ); //value > target 이 더 효율 적인 예시 임. ( 참고 ) 

//switch패턴 ( 상수 비교가 아닌 경우 )
static bool GreaterThanTarget(int number, int target) =>
    number switch
    {
        // > target => true, 상수 제약 ( target )
        int value when value > target => true,
        _ => false
    }
```

- 기존의 `!`,`&&`,`||` 도 `not`, `and`, `or` 예약어를 도입하여 사용. 
- 기존의 `when`예약어로 사용하던 복잡성을 제거 

```csharp
//is pattern
static bool IsLetter(char c) =>
    c is (>= 'a' and <= 'z') or ( >= 'A' and <= 'Z') => true;
//switch pattern
static bool IsLetter(char c) =>
    c switch
    {
        ( >= 'a' and <= 'z' ) or ( >= 'A' and <= 'Z' ) => true,
        _ => false
    };
```

## Module Initializer ( 모듈 이니셜라이저 )

- `ModuleInitializer`라는 이름의 특성을 정적 메서드에 부여하면 C# 컴파일러는 해당 메서드를 <Module> 타입의 정적 생성자에서 호출하는 코드를 넣어 컴파일 한다.
- 참고: https://www.sysnet.pe.kr/2/0/11335
- [Example Code](./ModuleInitalizerEx.cs). 
  - 제약 사항
    - 반드시 static method
    - 반환 타입은 void, 매개 변수는 없어야한다.
    - 제네릭 유형은 허용 안됨
    - <Module> 타입에서 호출이 가능해야 하므로 internal 또는 public 접근자만 허용 
  - 어떤 순서로 호출 될 지 제어할 수 없기 때문에 실행 순서에 의존하는 코드를 작성해서는 안됨. 

## 공변 반환 형식 ( Covariant return types )

- 상속 관계에서 return 값에 대해 override 사용 시 일치 시켜야 컴파일이 되었지만 
- 일치 시키지 않더라도 컴파일이 가능 ( C#9.0 .NET5 이상 )  
  - 반환 타입이 상속 관계의 하위 타입 인 경우 사용 가능

```csharp
class Product
{
    public virtual Product GetDevice() { return this; }
}
class Hashset : Product 
{
    public override Hashset GetDevice() //Product를 상속하는 child class 인 경우 가능 
    {
        return this;
    }
}
```

## Extension GetEnumerator

- `foreach`루프에 대해 `IEnumerable`을 구현하지 않더라도 `GetEnumerator`확장 메서드를 사용해서 사용 가능. 

```csharp
Notebook notebook = new();
foreach(var device in notebook)
{
    System.Console.WriteLine(device.Name);
}
public static class NotebookExtension
{
    //외부 개발자가 GetEnumerator 확장 메서드를 제공
    public static IEnumerator<Device> GetEnumerator(this Notebook instance)
    {
        foreach( Device device in instance.GetDevices())
        {
            yield return device;
        }
    }
}

public class Notebook
{
    List<Device> _parts;
    public Notebook()
    {
        _parts = new List<Device>()
        {
            new Device() { Name = "CPU" },
            new Device() { Name = "GPU" }
        };
    }
    public Device[] GetDevices()
    {
        return _parts.ToArray();
    }
}
```



