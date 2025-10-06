# csharp 7.2


## ref와 readonly 모두의 의미를 갖는 in

```csharp
class Program
{
    static void Main(string[] args)
    {
        Program pg = new Program();
        Vector v1 = new Vector();
        pg.StructParam(in v1);
    }
    void StructParam(in Vector v) //ref + readonly, C# 12에서 ref readonly 가 생김. 
    {
        v.X = 5; //Compile Error 
    }
}
```

- `System.Runtime.CompilerServices.IsReadOnlyAttribute` 특성을 메서드에 추가하는 방식이므로, `in`, `out`, `ref`를 사용하는 동일한 이름의 함수를 여러개 만들 수는 없다. 

## readonly structure

- 문제가 되었던 부분
  - 값형식에서 속성 값을 직접 변경하는 경우 컴파일 에러가 발생.
  - 하지만 호출에 대해서는 허용하는 문제가 발생 

```csharp
class Program
{
    readonly Vector v1 = new Vector();
    static void Main(string[] args)
    {
        Program pg = new Program();
        pg.v1.Increment(); //method 호출은 허용!!! -> 문제가 됨. 하지만 C#에서 컴파일러는 readonly가 적용 된 구조체 인스턴트에 한해 그상태를 유지할 수 있도록 모든 메서드 호출을 자동으로 변경하여 호출하도록 되어 있어. 
        //생각했던 값 변경은 이루어지지 않는다. 
    }
    struct Vector
    {
        public int X;
        public int Y;
        public void Increment()
        {
            X++;
            Y++;
        }
    }
}
```

```csharp
Vector temp = v1; //원본 v1의 값을 변경하지 않도록 방어 복사본 처리
temp.Increment(); // 값 복사된 temp인스턴트에 대해 메서드 호출 
```

- 방어 복사본 처리가 있어 개발자 스스로가 인지 못할 경우 자칫 버그로 이어질 수 있음. 

### readonly structure

```csharp
readonly struct Vector
{
    readonly public int X; //readonly struct내 모든 필드는 readonly 필수
    readonly public int Y; //readonly struct내 모든 필드는 readonly 필수 

    public void Increment()
    {
        //X++; //error
        //Y++; //error
    }
    //readonly 필드의 값을 유일하게 변경할 수 있는 생성자 
    public Vector(int x, int y)
    {
        X = x;
        Y = y;
    }
    public Vector Increment(int x, int y)
    {
        return new Vector(X + x, Y + y);
    }
}
```

- [ref readonly](./refReadonlyEx.cs). 

- 로컬 변수에서도 변수자체가 ref로 받지 못하는 경우가 있으므로, 아래와 같이 변경 

```csharp
vector v2 = pg.GetVector(); //값 복사 
ref readonly vector v2 = ref pg.GetVector();
```

## stack에 만 생성할 수 있는 값 타입 지원, ref struct 

- 값 형식의 struct는 스택을 사용하지만 그 struct가 class안에 정의된 경우는 힙에 데이터가 위치하게 된다. 

```csharp
class Program
{
    static void Main(string[] args)
    {
        Matric matrix = new();
    }
    static Vector
    {
        public int X;
        public int Y;

        public Vector(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
    class Matrix
    {
        public Vector Rx = new Vector(1, 2); //class의 멤버로 정의 되어 힙영역에 생성
        public Vector Ry = new Vector(10, 20); //class의 멤버로 저으이 되어 힙영역에 생성 
    }
}
```

```csharp
ref struct Vector
{
    public int X;
    public int Y;   
    public Vector(int x, int y) { X = x; Y = y; }
}
ref struct Matrix2x2
{
    public Vector v1 = new Vector(); //ref struct
    public Vector v2 = new Vector(); //ref struct 
}
```

```csharp
ref struct RefStruct : IDisposable //compile error
{
    
}
```

- interface로의 형변환은 스택 객체가 힙객체로 변환돼야 하는 박싱 작업을 수반하는데 `ref struct` 타입은 힙에 생성할 수 없는 유형이기 때문이다. `ref struct` 타입의 인스턴스는 using문의 대상으로 사용할 수 없다. 
- 관리 포인터의 경우 GC의 현재 구현상 절대 `관리 힙에 놓일 수 없다.`는 제약을 갖는다. 따라서 `Span<T>` 타입을 구현하면서 관리 포인터를 담을 수 있는 특수한 구조체가 필요하게 됐고 그것이 바로 `ref struct`이다. [참고](https://www.sysnet.pe.kr/2/0/11529)

## Span<T>

- 제네릭 관리 포인터를 가진 readonly ref struct.  
- 배열에 대한 참조 뷰(View)를 제공하는 타입.   
- 기본적으로 c#에서 만드는 모든 배열을 Span<T>타입으로 가릴킬 수 있다. 

```csharp
public readonly ref struct Span<T>
{
    //...
}
```

```csharp
var arr = new byte[]{1,2,3,4,5,6,7};
Span<byte> view = arr;
Console.WriteLine(view[5]);
//참조 뷰, 라는 이름에 걸맞게 Span<T> 인스턴스는 원본을 관리 포인터로 가리키고 있다는 점에서 값 변경까지 허용 
view[5] = 17; //Span<T> 인스턴스의 값을 변경
Console.WriteLine(arr[5]);
arr[5] = 15; //원본의 값을 변경
Console.WriteLine(view[5]); //Span<T> 인스턴스 출력 결과 15 
```

### Span<T> 내부 구현 개념과 동작

Span<T>는 “스택 전용, 힙 할당 없이 배열/메모리 구간을 가리키는 뷰”를 제공하는 ref struct입니다. 내부는 참조와 길이(2개의 필드)로 구성되며, 인덱서/슬라이스/복사 등의 연산을 JIT 최적화와 저수준 유틸리티로 구현합니다.  

- 타입: readonly ref struct → 힙 배치/박싱/캡처/필드로 가질 수 없음(스택 전용 보장).  
- 내부 상태: “데이터 시작 참조” + “길이” (실제 구현에서 ByReference<T>와 int).  
- 생성자: 배열(T[]), ref T 시작점, 포인터(void*) + 길이, stackalloc 등 다양한 소스에서 생성.  
- 인덱서: 경계 검사 후 ref 반환. JIT가 루프에서 검사 제거 가능.  
- 슬라이스: 시작 오프셋을 더해 새 Span<T> 구성(데이터 복사 없이 O(1)).  
- 복사/채움: Buffer.Memmove/Unsafe.InitBlockUnaligned 등으로 고성능 처리.  
- Pin: GetPinnableReference() 제공 → fixed 문에서 핀(pin) 가능.  
- 안전성: 런타임이 ref struct 제약으로 힙 유출을 막고, 범위 검사로 메모리 안전 확보.  

### Span<T> 활용 개선 1

```csharp
{
    var arr = new byte[] { 0, 1, 2, 3, 4, 5, 6};
    var arrLeft = arr.Take(4).ToArray();
    var arrRight = arr.Take(4).ToArray();
    Print(arrLeft);
    Print(arrRight);
}
private static void Print(Span<byte> view)
{
    Console.WriteLine(string.Join(",", view.ToArray()));
    Console.WriteLine();
}
```

- Span을 활용하여 중복 선언 제거 

```csharp
var arr = new byte[] {0,2,3,4,5,6};
Span<byte> view = arr;
Span<byte> viewLeft = view.Slice(0, 4); //stack 영역을 가리킴
Span<byte> viewRight = view.Slice(4); //stack 영역을 가리킴
Print(viewLeft);
Print(viewRight);
```

### Span<T> 활용 개선 2

```csharp
string input = "100,200";
int pos = input.IndexOf(',');

string v1 = input.SubString(0, pos); //100 strings heap assign
string v2 = input.SubString(pos + 1); //200 strings heap. assign
{
    Console.WriteLine(int.Parse(v1));
    Console.WriteLine(int.Parse(v2));
}
```

- Span을 이용하여 stack에 접근

```csharp
string input = "100,200";
int pos = input.IndexOf(',');
ReadOnlySpan<char> view = input.AsSpan();
var v1 = view.Slice(0, pos); // heap 할당 없음, ReadOnlySpan<char>
var v2 = view.Slice(pos+1); // heap 할당 없음, ReadOnlySpan<char>
Console.WriteLine(int.Parse(v1));
Console.WriteLine(int.Parse(v2));
```

### Span<T> Unamaged 

- 비관리 메모리를 잘못 접근하게 되면 ( 운에 따라 ) Access Violation 오류가 발생, 비정상 종료 try 이후 catch, finally 절은 실행 되지 않는다. 
- Span<T>로 감싼 경우라면 그 대상이 비관리 메모리 일지라도 내부적인 관리 포인터의 혜택으로 예외처리가 가능! 

```csharp
{ 
    Span<byte> bytes = stackalloc byte[10];
    bytes[0] = 100;
    print(bytes);
}

unsafe
{
    int size = 10;
    IntPtr ptr = Marshal.AllocCoTaskMem(size);

    try
    {
        Span<byte> bytes = new Span<byte>(ptr.ToPointer(), size);
        bytes[9] = 100;
        Print(bytes);
    }
    finally
    {
        Marshal.FreeCoTaskMem(ptr);
    }
}
```

## 3항 연산자에 ref 지원

```csharp
ref int result = ref (part1 != 0 ) ? ref part1 : ref part2;
```

```csharp
((part1 != 0) ? ref part1: ref part2) = 15;
Console.WriteLine(part1);
```

## private protected 접근자 

`public`, `internal`, `protected`, `internal protected`, `private`, `private protected` 

- internal protected : 동일 어셈블리 내에서 정의된 클래스이거나 다른 어셈블리라면 파생 클래스인 경우에 한해 접근을 허용한다. 
- (protected internal로도 지정 가능), 즉 `interanl` 또는, `protected` 조건 이다. 

- `private protected` : 동일 어셈블리 내에서 정의된 파생 클래스인 경우에 한해 접근을 허용한다. 즉, internal 그리고 protected 조건이다. 


# reference 

- [Marshal](./Marshal.md).  