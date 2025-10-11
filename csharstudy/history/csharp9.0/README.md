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