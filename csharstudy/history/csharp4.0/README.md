# csharp 4.0

- `CLR` based `DLR(Dynamic Language Runtime)`. 
- 동적 언어도 닷넷에서 실행 가능하도록 바뀜 ( Ruby, Python ). 

## dynamic 

- [Dynamic](./dynamicEx.cs). 
- `var` : 컴파일 시점
- `dynamic` : 실행 시점 

- dynamic이 왜 compile time에 에러가 나지 않는지 예제
    - [Dynamic Original](./dynamicOriginalEx.cs). 

## reflection 에서의 dynamic

```csharp
string txt = "Test Func";

Type type = txt.GetType();
MethodInfo containsMethodInfo = type.GetMethod("Contains");
if(containsMethodInfo != null)
{
    object returnValue = containsMethodInfo.Invoke(txt, new object[] {"Test"});
    bool callResult = (bool)returnValue;
}
```

- dynamic을 통해 simple하게 바뀜
    - 아래의 특성은 `Plug-in` 확장 모듈을 사용하기 쉽게 만들어 준다.  
```csharp
dynamic txt = "Test Func";
bool result = txt.Contains("Test");
```

## Duck Typing

- 일반적으로 Interface를 상속하여 공통 된 function을 호출 

```csharp
interface IBird
{
    void Fly();
}

class Duck : IBird
{
    void Fly() { Console.WriteLine("Fly Duck"); }
}
class Goose : IBird
{
    void Fly() { Console.WriteLine("Goose Fly"); }
}

void StrongTypeCall(IBird bird)
{
    bird.Fly();
}
StrongTypeCall(new Duck());
StrongTypeCall(new Goose());
```

- 이런 경우외에 IndexOf의 List<T>와 string은 모두 가지고 있지만, 기반 타입으로 구성 된 것이 아니기 때문에 
- 위와 같이 사용 불가 
- 이때 `dynamic`을 통해 해결 

```csharp
int DuckTypingCall(dynamic target, dynamic item)
{
    return target.IndexOf(item);
}
string txt = "Test Func";
List<string> list = new List<string> {1,2,3,4,5};
Console.WriteLine(DuckTypingCall(txt, "Test")); // 0
Console.WriteLine(DuckTypingCall(list, 3)); //2
```

## reference 

- [Strong Type](./strongType.md). 

