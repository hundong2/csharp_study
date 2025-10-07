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



## Reference 

- [Managed/Unmanaged](./unmanaged.md). 
  - `fixed`, `unsafe` 설명 
- [Marshal AllocCoTaskMem](./MarshalAllocCoTaskMem.md)  
- [fixed 문과 fixed크기 버퍼의 차이](./fixed.md). 
- [Serializable Attribute](./serializableAttribute.md). 
- [Attribute Deepdive](./AttributeDeepDive.md). 


