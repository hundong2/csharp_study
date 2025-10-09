# csharp8.0

## class generic 제약 조건  

- class 타입이어야 한다.
- sealed 타입이 아니어야 한다. 
- System.Array, `System.Delegate`, `System.Enum`은 허용하지 않는다. 
- System.ValueType 은 허용하지 않지만 특별히 struct제약을 대신 사용할 수 있다.  또한, System.Object도 허용하지 않지만 어차피 모든 타입의 기반으로 제약 조건으로써의 의미가 없다.  

- `System.Delegate`, `System.Enum` 은 제약이 풀림. 
1. [Generic의 타입만 매개변수로 받을 수 있는 Example](./GenericEx.cs). 
2. `enum`의 경우 struct제약을 해야 했지만, System.Enum으로 명시할 수 있게 됨. 

```csharp
class EnumValueCache<TEnum> wehere TEnum : System.Enum
{
    Dictionary<TEnum, int> _enumKey = new Dictionary<TEnum, int>();
    public EnumValueCache()
    {
        int[] intValues = Enum.GetValues(typeof(TEnum)) as int [];
        TEnum[] enumValues = Enum.GetValues(typeof(TEnum)) as TEnum[];

        for(int i = 0; i < intValues.Length; i++)
        {
            _enumKey.Add(enumValues[i], intValues[i]);
        }
    }
    public int GetInteger(TEnum value)
    {
        return _enumKey[value];
    }
}
```

3. unmanaged

- [Unamanaged Generic](./GenericUnmanaged.cs). 
- 형식 매개변수에 대한 포인트 연산을 할 수 있다. 
- https://www.sysnet.pe.kr/2/0/11557. 
- https://www.sysnet.pe.kr/2/0/11558. 

## 사용자 정의 타입에 fixed 적용 가능 

```csharp
public class Point
{
    public int X;
    public int Y;
}

private unsafe static void FixedUserClassType()
{
    Point pt = new Point();
    //사용자 타입인 Point는 fixed의 대상이 될 수 없다. 
    //컴파일 에러 
    fixed ( int *pPoint = pt)
    {
    }

    //타입 내부 필드가 fixed로 가능한 유형이라면 다음과 같이 우회 가능 
    Point pt1 = new Point { X = 5, Y = 6};
    fixed ( int* pX = &pt.X)
    fixed ( int* pY = &pt.Y)
    {
        Console.WriteLine($"{*pX}, {*pY}");
    }

}
```

- `C# 7.3` 부터 사용자 타입이 `GetPinnableReference`라는 이름으로 관리 포인터를 반환하는 메서드를 포함하고 있는 경우라면 `fixed`구문에 자연스럽게 사용 가능.  

```csharp
public class Point
{
    public int X;
    public int Y;

    public ref int GetPinnableReference()
    {
        return ref X;
    }
}
class Program
{
    static void Main(string[] args)
    {
        Point pt = new();
        //GetPinnableReference 자동 호출 
        fixed( int* pPoint = pt )
        {
            Console.WriteLine(*pPoint);
        }
    }
}
```

- `Span<T>`의 경우 `GetPinnableReference`를 구현하고 있다. 

```csharp
private unsafe static void FixedSpan()
{
    {
        //fixed가 필요없는 stack 기반으로 하든 
        Span<int> span = stackalloc int[500];
        fixed( int *pSpan = span )
        {
            Console.WriteLine(*(pSpan + 1));
        }
    }
    {
        //managed heap 기반
        Span<int> span = new int[500];
        fixed(int* pSpan = span)
        {
            Console.WriteLine(*(pSpan + 1));
        }
    }

    {
        //fixed될 필요가 없는, 비관리 힙을 기반으로 하든 상관없이 일관성 있는 fixed 구문 제공
        int elemLen = 500;
        int allocLen = sizeof(int) * elemLen;
        Span<int> span = new Span<int>((void*)Marshal.AllocCoTaskMem(allocLen), elemLen);

        fixed(int* pSpan = span)
        {
            Console.WriteLine(*(pSpan + 1));
        }
    }
}
```

## 힙에 할당 된 고정 크기 배열의 인덱싱 개선

```csharp
//CSharpStructType은 값 형식이므로 item instance는 stack에 자리 잡고, fixed 고정 크기 배열에 대한 인덱싱은 직접 접근이 가능 
unsafe struct CSharpStructType
{
    public fixed int fields[2];
    public fixed long dummy[3];
}

class Program
{
    unsafe static void Main(string[] args)
    {
        CSharpStructType item = new();
        item.fields[0] = 5;
        int n = item.fields[2];
    }
}
```

- 다른 타입의 멤버로 포함 되는 경우는...

```csharp
unsafe struct CSharpStructType
{
    public fixed int fields[2];
    public fixed long dummy[3];
}
class Container
{
    CSharpStructType _inst;
    public Container()
    {
        _inst = new();
    }
}
public unsafe void ProcessItem()
{
    //C#7.2 이전까지 컴파일 에러
    //C#7.3 이후 부터 컴파일 가능 
    _inst.fields[0] = 5;
    int n = _inst.fields[2];
}
//fixed 고정 크기 배열이 Container참조 타입 형식에 의해 내부 모든 값 형식이 힙에 할당 되기 때문에 아래와 같이 
//사용 되어야 했음. 
```

```csharp
public unsafe void ProcessItem()
{
    //previous csharp 7.2
    fixed(int *ptr = _inst.fields)
    {
        prt[0] = 5;
        int n = ptr[2];
    }
}
```

## 초기화 식에서 변수 사용 가능 

- [Initialize Example](./InitializeEx.cs). 

## attribute

- C# 7.3 previous

```csharp
[Serializable]
public class Foo
{
    [NonSerialized]
    string _mySecret;

    public string MySecret
    {
        get { return _mySecret; }
        set { _mySecret = value; }
    }
}
```

- new feature 

```csharp
[Serializable]
public class Foo
{
    [field: NonSerialized] //자돋 생성 된 필드에 특성이 적용 됨. 
    public string MySecret { get; set; }
}
```

## tuple의 ==, != 연산자 지원

```csharp
class Program
{
    static void Main(string[] args)
    {
        var tuple = (13, "kevin" );
        bool result1 = tuple == (13, "Winnie" );//C# 7.2까지 컴파일 에러 
        //compile level changed that code 
        //bool result1 = (tuple.Item1 == 13 ) && ( tuple.Item2 == "Winnie" );
        bool result2 = tuple != (13, "Winnie" );//C# 7.2까지 컴파일 에러 
        //bool result2 = ( tuple.Item1 != 13 ) || ( tuple.Item2 != "Winnie" );

    }
}
```

## ref 지역 변수의 재할당 가능 

```csharp
class Program
{
    static void Main(string[] args)
    {
        int a = 5;
        ref int b = ref a; //a를 가리키는 ref 로컬 변수 b 

        int c = 6;
        b = ref c;
    }
}
```

## stackalloc 배열의 초기화 구문 지원

```csharp
class Program
{
    static unsafe void Main(string[] args)
    {
        int* pArray1 = stackalloc int[3] { 1,2,3 };
        int* pArray2 = stackalloc int[] { 1, 2};
    }
}
```

- 위의 코드는 다음 코드로 사용 될 것을 권장 된다. 

```csharp
Span<int> span1 = stackalloc int[3] { 1,2,3};
Span<int> span2 = stackalloc int[] { 1, 2 };
```

## nuallable

- page 796 

## 비동기 스트림

- [Main에서 비동기 지원하더라도 foreach 사용시 비효율적](./AsyncStream.cs)  
  - 열거에 한해 동기적으로 스레드를 점유 
- [await foreach](./AsyncStream2.cs). 

## 신규 연산자 

- [Example Code](./NewFeature.cs). 

- `^` : `^n` 
  - 인덱스 연산자로 뒤에서부터 n번째 위치를 지정한다. 마지막 위치를 1로 지정한다. 
  - System.Index
- `..` : `n1..n2`
  - 범위 연산자로서 시작 위치 n1은 포함하고 끝 위치 n2는 포함하지 않는 범위 지정 
  - 수학의 구간 기호로 표현하면 [n1, n2) 와 같다. 
  - n1이 생략 되면 기본 값 0 
  - n2가 생략 되면 기본 값은 ^0
  - System.Range

## simply using 

```csharp
class Program
{
    static void Main(string[] args)
    {
        using(var file = new System.IO.StreamReader("test.txt"))
        {
            string txt = file.ReadToEnd();
            Console.WriteLine(txt);
        }

        //changed

        using var file = new System.IO.StreamReader("test.txt");
        string txt = file.ReadToEnd();
        Console.WriteLine(txt);
    }
}
```

## Dispose 호출이 가능한 ref struct

- `public void Dispose()`를 포함한 경우 ref struct를 using문에서 사용 가능 하도록 변경
- page 807

## 정적 로컬 함수 

- Local function은 그것을 포함한 메서드의 지역 변수나 매개변수를 그대로 가져다 사용할 수 있었다. 
- static local function의 경우 명시적으로 parameter를 지정할 수 있다. 

```csharp
private void WriteLog(string txt)
{
    int length = txt.Length;
    WriteConsole(txt, length);
    static void WriteConsole(string txt, int length)
    {
        Console.WriteLine($"# of chars(`{txt}`): {length}");
    }
}
```

## pattern matching ( 패턴 매칭 )

```csharp
public static bool Even(int n) =>
    n switch
    {
        var x when ( x % 2 ) == 1 => false,
        _ => true
    };
```

또는

```csharp
public static bool Even(int n ) =>
    ( n % 2 ) switch
    {
        1 => false,
        _ => true
    };
```

## attribute pattern ( 속성 패턴 )

```csharp
class Point
{
    public int X;
    public int Y;

    public override string ToString() => $"{X},{Y}";
}
class Program
{
    static void Main(string[] args)
    {
        Func<Point, bool> detectZeroOR = (pt) =>
        {
            switch(pt)
            {
                case var pt1 when pt1.X == 0:
                case var pt2 when pt2.Y == 0:
                    return true;
            }

            return false;
        }
        Point pt = new Point { X = 10, Y = 20 };
        Console.WriteLine(detectZeroOR(pt)); 
    }
}
```

- new feature 

```csharp
Func<Point, bool> detectZeroOR = (pt) =>
{
    switch (pt)
    {
        //case { X: 0, Y: 0}:
        case { X: 0}:
        case { Y: 0}:
            return true;
    }
    return false;
}
```

- or 

```csharp
Func<Point, bool> detectZeroOR = (pt) =>
    pt switch
    {
        { X: 0 } => true,
        { Y: 0 } => true,
        _ => false
    };
```

### is 연산자에서 when 

```csharp
//is 연산자에서 when 조건을 지원하지 않으므로 컴파일 오류 발생 
if(pt is Point when pt.X == 500 )
{
    Console.WriteLine(pt);
}

//대신 속성 패턴을 이용하면 다음과 같이 구현 가능 
if( pt is { X: 500 })
{
    Console.WriteLine(pt.X + " == 500" );
}

if( pt is { X: 10, Y: 0 })
{
    Console.WriteLine(pt.X + " == 10" );
}
```

### tuple Pattern

```csharp
Func<(int, int), bool> detectZeroOR = (arg) =>
    (arg) switch
    {
        {Item1: 0 } => true,
        {Item2: 1 } => true,
        _ => false
    };
```

- changed from 8.0

```csharp
Func<(int, int), bool > detectZeroOR = (arg) =>
    (arg) switch 
    {
        (0, _) => true,
        (_, 0) => false,
        _ => false 
    };
```

```csharp
Func<(int, int), bool> detectZeroOR = (arg) =>
    (arg) switch
    {
        (var X, var Y ) when X == 0 || Y == 0 => true,
        _ => false
    };
```

```csharp
Func<(int, int), bool> detectZeroOR = (arg) =>
    (arg is (0, 0)) || arg is (0, _) || arg is (_, 0);
```

### 위치 패턴 

```csharp
Func<Point, bool> dectectZeroOR = (arg) =>
    (pt.X, pt.Y) switch
    {
        (0, _) => true,
        (_, 0) => true,
        _ => false
    };
```

- Decontruct method를 사용하면 더 쉽게 표현 가능 

```csharp
class Point 
{
    //생략
    public void Deconstruct(out int x, out int y ) => ( x, y ) = (X, Y );
}

Func<Point, bool> detectZeroOR = (pt) =>
    pt switch
    {
        // (0, 0) => true,
        (0, _) => true,
        (_, 0) => true,
        _ => false
    };
```

## 기본 인터페이스 메서드

- 인터페이스의 메서드에 구현 코드를 추가 할 수 있다. 
- 자바에서는 interface의 디폴트 메서드, 다른 언어들에서는 trait이라는 문법과 유사.  
- %유의% 
  - 인터페이스의 멤버이기 때문에 상속받은 클래스에서 기본 인터페이스 메서드를 구현하지 않았다면 그 메서드는 반드시 인터페이스로 형변환해 호출 해야 한다. 

```csharp
(x as ILog ).Log("test");
```

## null 병합 할당 연산자 ??==

```csharp
string txt = null;
if(txt == null )
{
    txt = "(default value)";
}
```

```csharp
txt ??== "(default value)";
```

## stackalloc

- 문법적으로 식(expression)의 위치로 변경하여 다양한 표현식이 가능해 짐 

```csharp
{
    int length = ( stackalloc int [] { 1, 2, 3}).Length;
}
{
    if(stackalloc int[10] == stackalloc int[10] ){ } 
}
```

## 제네릭 구조체의 unmanaged 지원 

- Page 828
- https://www.sysnet.pe.kr/2/0/12478


```csharp
#GC에 부담을 주지 않는 코드, GC힙이 아닌 운영체제로 부터 직접 메모리 할당을 받음. 
using System.Runtime.InteropServices;
public unsafe ref struct NativeMemory<T> where T : unmanaged
{
    int _size;
    IntPtr _ptr;

    public NativeMemory(int size)
    {
        _size = size;
        _ptr = Marshal.AllocHGlocal(size * sizeof(T));
    }
    public Span<T> GetView()
    {
        return new Span<T>(_ptr.ToPointer(), _size);
    }
    public void Dispose()
    {
        if(_ptr == IntPtr.Zero)
        {
            return;
        }
        Marshal.FreeHGlobal(_ptr);
        _ptr = IntPtr.Zero;
    }
}
```

## 구조체의 읽기 전용 메서드 

- readonly method 에서는 구조체의 값을 바꾸는것을 허용하지 않는다. 

## Reference 

- [Managed/Unmanaged](./unmanaged.md). 
  - `fixed`, `unsafe` 설명 
- [Marshal AllocCoTaskMem](./MarshalAllocCoTaskMem.md)  
- [fixed 문과 fixed크기 버퍼의 차이](./fixed.md). 
- [Serializable Attribute](./serializableAttribute.md). 
- [Attribute Deepdive](./AttributeDeepDive.md). 
- [Thread.Sleep vs Task.Delay](./ThreadSleep_TaskDelay.md). 
- [@$ 문자열 보간](./Verbatin_Interpolated.md). 
