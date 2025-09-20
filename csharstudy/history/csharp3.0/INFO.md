# csharp c# 3.0

## var 

- `type inference` 기능이 추가 되면서 var예약어 사용 가능. 

```csharp
foreach( KeyValuePair<string, List<int>> element in dict) {}
//보다 간결 
foreach(var elemet in dict) {}
```

## 자동 구현 속성 ( Auto Implemented properties )

- old version

```csharp
class Person
{
    string _name;
    public string Name { get {return _name;} set { _name = value; }}
}
```

- 아래의 코드를 컴파일러는 자동으로 확장 해준다. 

```csharp
class Person
{
    public string Name {set; get;}
}
//changed from compiler 

class Person
{
    private string ...[Name 자동 속성에 대응 되는 고유 식별자]...
    public string Name
    {
        get { return ....고유식별자...; }
        set { ...고유식별자...=value; }
    }
}
```

- 자동 구현 속성을 사용할 시 외부에서는 오직 읽기만 가능하고, 내부에서는 읽기/쓰기 모두 가능하게 가능.

```csharp
class Person
{
    public string Name { get; protected set; }
}
```

## 개체 초기화 (Object initializers)

- old version 

```csharp
class Person
{
    string _name;
    int _age;

    public string Name
    {
        set{ _name = value; } get { return _name; }
    }
    public int Age
    {
        set(_age = value;) get {return _age;}
    }
    public Person() //default construct
        : this(string.Empty, 0)
    {

    }

    public Person(string name)
        : this(name, 0) {} 

    public Person(string name, int age)
    {
        _name = name;
        _age = age;
    }
}

class Program
{
    static void Main(string[] args)
    {
        Person p = new Person("lle", 33);
    }
}
```

## 개체 초기화 사용

```csharp
Person p1 = new Person();
Person p3 = new Person("hello", 33);
```

- 생성자 객체 초기화 함께 사용

```csharp
Person p1 = new Person(30) { Name = "name" }
```

## collection initialize ( 컬렉션 초기화 )

```csharp
List<int> numbers = new List<int>();
numbers.Add(0); numbers.Add(1); //...
```

```csharp
List<int> numbers = new List<int>{ 0, 1, 2, 3 }; //ICollection<T> Interface 
```

- 객체 초기화 구문도 가능. 

```csharp
class Person
{
    public int Age {get; set;}
    public string Name {get; set;}
}

class Program
{
    static void Main(string[] args)
    {
        List<Person> list = new List<Person>()
        {
            new Person { Name = "lee", Age = 22 },
            new Person { Name = "lll", Age = 33 }
        }   
    }
}
```

## 익명 타입
  - [anonymous type](./anony.cs). 

## partial method 
    - [partial method example](./partialmethod.cs). 
    - 제약 사항
      - 부분 메서드는 반환값을 가질 수 없다
      - ref 매개변수는 사용할 수 있지만, out매서드는 사용 할 수 없다.
      - 부분 메서드는 private 접근자만 허용

## Extension method 

- 제약 사항 
  - sealead로 봉인 된 클래스는 확장할 수 없다. 
  - 클래스를 상속받아 확장하면 기존 소스코드를 새롭게 상속받은 클래스명으로 바꿔야 한다.  
  - [Extension Example](./ExtensionExample.cs)

```csharp
Console.WriteLine(text.GetWordCount()); 
//changed from compiler
Console.WriteLine(ExtensionMethodSample.GetWordCount(text));
```

- 사용 불가 
  - protected member call 
  - method override 

- ExtensionMethod Example `System.Linq` `Min` for `IEnumerable`
  - [ExtensionMethod2.cs](./ExtensionMethod2.cs). 
  - 

## Lambda expression

### code로서의 람다 식 

- 익명 메서드의 간단한 표기용도로 사용된다. 

```csharp
Thread thread = new Thread(
    delegate(object obj)
    {
        Console.WriteLine("ThreadFunc in anonymous method called!");
    }
);
```

- simple version

```csharp
Thread thread = new Thread(
    (obj) =>
    {
        Console.WriteLine("ThreadFunc in anonymous method called!");
    }
)
```

### Lambda expression

- [simple version 1](./DelegateExample.cs). 
- [simple version 2](./LambdaExpressionEx.cs). 

### Action, Func

- delegate를 일일이 정의하는 것을 편하게 하기 위해, 제네릭을 통해 선언. 

```csharp
public delegate void Action<T>(T obj); //반환값 존재
public delegate TResult Func<TResult>(); //반환값 미존재 
```

- [Action, Func Example](./ActionExample.cs). 
- result 

```sh
Action delegate example
Value of pi: 3.14
3 * 4 = 12
```

- [Action, Func Example](./ActionFunc.md). 

#### Collection and Lambda method

- `List<T> Foreach`. 
- `Array Foreach`. 

- [Collection Labmda](./CollectionLambda.cs). 
    - List의 요소를 하나씩 차례대로 `Action<T>`에 전달 
    - ForEach((elemnt) => {...});
      - Action<T> 를 수행 

- [FindAll Example](./ActionFuncEx1.cs). 
  - `FindAll` method는 `delegate bool Predicate<T>(T obj)`. 
  - `public List<T> FindAll(Predicate<T> match);`. 
    - `Func<T, bool`
- [Example Count](./ActionExample.cs)
  - `Count` Exmaple 
  - `public static int Count<TSource>(this IEnumerable<TSource> source)`. 
  - `public static int Count<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)`. 

- `where` example

```csharp
public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
```

  - [Where Example](./WhereExample.cs). 
- [Where vs FindAll explain](./WhereFindAllDiff.md). 
    - `where` is lazy evaluation.  
    - `FindAll` is immediately

- `FindAll` -> `where`. 
- `ConvertAll` -> `select`. 


### Expression

- [Expression Example](./ExpressionExample.cs)  
- [Expression Explain](./Expression.md)  
- [Expression DeepDive](./ExpressionDeppDive.md)  

