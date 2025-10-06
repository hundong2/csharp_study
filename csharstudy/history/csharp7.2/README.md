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
