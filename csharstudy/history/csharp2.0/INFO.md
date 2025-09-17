# history C# 2.0

## GENERIC

- key sentence  
  - Generic 타입을 명시하는 것을 생략 할 수 있는 이유는 c# 컴파일러가 메서드 인자가 T 타입이 전달 된다는 사실을 알고 자동으로 유추해서 대신 처리.  
  - 제네릭의 사용은 박싱/언박싱으로 발생하는 비효율적인 힙 메모리 사용 문제를 없앨 뿐더러, 데이터 타입에 따른 코드 중복문제도 해결해 준다.  

```csharp
int n = 5;
ArrayList ar = new ArrayList(); 
ar.Add(n); //boxing integer to object 
```

- boxing 되는 과정에서 stack 영역에서 heap 영역으로 이동.  
- ArrayList를 다루는 데이터 타입이 고정 되어야 하는 문제! 발생
- `IntArrayList`, `LongArrayList`, ... 
- `C#2.0` Generic type. `<T>`

```csharp
int n = 5;
List<int> list = new List<int>();
list.Add(n);
```

- C#1.0에서는 object를 사용함으로써 boxing을 유발. 
- boxing을 피하려 해도 코드 중복이 발생.  

- [Generic Example](./genericEx.cs). 
- Generic을 사용하면 CLR은 JIT컴파일 시에 클래스가 타입에 따라 정의 될 때 마다 T에 대응되는 타입을 대체하고 확장. ( 기계어 코드 생성 )
- result 

```sh
3
2
1
three
two
one
{X=5,Y=6}
{X=3,Y=4}
{X=1,Y=2}
```

### GENERIC Method

- boxsing problem

```csharp
public class Utility
{
    public static void WriteLog(object item)
    {
        string output = string.Format("{0}: {1}", DateTime.Now, item);
        Console.WriteLine(output);
    }
}
```

- Generic method

```csharp
public class Utilty
{
    public static void WriteLog<T>(T item)
    {
        string output = string.Format("{0}: {1}", DateTime.Now, item);
        Console.WriteLine(output);
    }
}
```

```csharp
Utility.WriteLog<int>(55);
Utility.WriteLog(55); //생략 가능 
```

## Conditional Generic 

- this example is compile error 
- `CompareTo`에 대한 정의가 있는지 알 수 없음.  

```csharp
public static T MAX<T>(T item1, T item2)
{
    if(item1.CompareTo(item2) >= 0) return item1; //compile error
    return item2;
}
```

- `where T: <type value>` 를 통해 조건문 사용
- `where T: IComparable`
- `where 형식매개변수: 제약조건 [,...]`

### Examples

```csharp
public class MyClass<T> where T : ICollection {}
public class MyClass<T> where T : ICollection, IConvertible {}
public class MyClass<T, K> where T: ICollection
                            where K: IComparable
```

### Variable types conditional 

|제약조건|설명|
|---|---|
|`where T:struct`|T형식 매개변수는 반드시 값 형식만 가능|
|`where T:class`|T형식 매개변수는 반드시 참조 형식만 가능|
|`where T: new()`|T형식 매개변수의 타입에는 반드시 매개변수 없는 공용 생성자가 포함돼 있어야 한다. 즉, 기본생성자가 정의되어 있어야 한다.|
|`where T: U`|T형식 매개변수는 반드시 U형식 인수에 해당하는 타입이거나 그것으로부터 상속받은 클래스만 가능|  

#### Example

- `where T: struct`
  - `System.Runtime.InteropServices`
    - `SizeOf` Marshl type

```csharp
public static int GetSizeOf<T>(T item)
{
    return Mashal.SizeOf(item);
}
```

- 위와 같이 할경우 다음 과 같은 실수 가능 

```csharp
Console.WriteLine(GGetSizeOf("size")); //Compile is Ok, But, Runtime execute error.  
```

- 좋은 예

```csharp
public static int GetSizeOf<T>(T item) where T: struct
{
    return Marshal.SizeOf(item);
}
```

##### class type

- bad ( 모든 값형식은 null 상태를 가지는게 아님. )

```csharp
public static void CheckNull<T>(T item)
{
    if(item == null ) throw new ArgumentNullException();
}
int a = 5;
string b= "My";
CheckNull(a);//compile is ok
CheckNull(b); //compile is ok
```

- good 

```csharp
public static void CheckNull<T>(T item) where T: class
{
    if(item == null) throw new ArgumentNullException();
}

int a = 5;
string b = "My";
CheckNull(a); //Compile Error
CheckNull(b); //Compile OK
```

##### where T: new() condition

- 매개변수에서 제네릭 메서드/클래스 내부에서 new연산자를 통해 생성할 떄 사용
- 메서드에 전달 된 인자가 null인 경우 새롭게 생성해서 반환하는 매서드의 경우 
- 모든 타입이 기본 생성자를 가지고 있다고 장담할 수 없음.  

- bad Example 

```csharp
public static T AllocateIfNull<T>(T item) where T : class
{
    if(item==null) item = new T();
    return item;
}
```

- good 

```csharp
public static T AllocateIfNull(T item) where T : class, new()
{
    if(item == null) item = new T(); //기본 생성자가 없는 경우 에러 발생. 
    return item;
}
```

##### 매개 변수 2개 

```csharp
namespace ConsoleApp;

public class BaseClass();
public class DerivedClass: BaseClass {}
class Program
{
    static void Main(string[] args)
    {
        BaseClass dInst = new DerivedClass();
    }
}
```

- T == BaseClass, V=DerivedClass 
- Allocate Method 는 DerivedClass를 new로 할당해서 BaseClass로 형변환해서 반환하는 역할 

```csharp
public class Utility
{
    public static T Allocate<T, V>() where V: T, new()
    {
        return new V();
    }
}

BaseClass dInst2 = Utility.Allocate<BaseClass, DerivedClass>();
```