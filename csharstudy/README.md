# dotnet programming 

- dotnet project sln making example : https://learn.microsoft.com/ko-kr/dotnet/core/tutorials/library-with-visual-studio-code?pivots=dotnet-8-0  
- make dotnet console 


```sh
mkdir testproject
cd testproject
dotnet new console --use-program-main
```

- make dotnet project solution

[reference site - make sln](https://learn.microsoft.com/ko-kr/dotnet/core/tools/dotnet-sln)  

## Dotnet Interface Example 

### IComparer

[example code](./dotnetexample/Interface/Program.cs)  

### IEnumerable 

```csharp
namespace System.Collections;
public interface IEnumerable
{
    IEnumerator GetEnumerator();
}
```

#### IEmulator

```csharp
namespace System.Collections;

public interface IEnumerator
{
    object Current { get; } //current element 
    bool MoveNext(); // move to next element
    void Reset(); //reset element numbers
}
```

### coupling 

#### tight coupling 

- tight coupling it means less flexible    

```csharp
class Computer 
{
    public void TurnOn()
    {
        Console.WriteLine("Conmputer; TurnOn");
    }
}

class Switch
{
    public void PowerOn(Computer machine)
    {
        machine.TurnOn();
    }
}
```

#### loose coupling ( flexible )

```csharp
interface IPower
{ 
    void TurnOn();
}
class Monitor : IPower 
{
    public void TurnOn()
    {
        Console.WriteLine("Monitor: TurnOn");
    }
}
class Desktop : IPower
{
    public void TurnOn()
    {
        Console.WriteLine("Desktop: TrunOn");
    }
}
class Switch
{
    public void PowerOn(IPower machine) // type => interface 
    {
        machine.TurnOn();
    }
}
```

[example code](./dotnetexample/Interface/Program.cs)  

## page-220

- class is reference type
- struct is value type

```csharp
//n1, n2, n3 is same expression
int n1 = new int();

int n2;
n2 = 0;

int n3 = 0;
```

- `error` condition

```csharp
int n; //n vlaue is not initialize, not assigned value of n 

Console.WriteLine(n); //Compile Error Occur!!
```





