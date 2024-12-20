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
## page-240

### Enum 

[enum example](./dotnetexample/Enum/Program.cs)  

## page-284 unsafe code

- if you want to use unsafe code, then, you should be set AlowUnsafeBlocks Option.

```csharp
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
</Project>
```

## page 311 32, 64 bit check

```csharp
Console.WriteLine($"64 bit process : {Environment.Is64BitProcess}");
```

- `EXE` is 64 bit then True, it's 32 bit then False.

## page 362 DateTime 

- [Example for DateTime](./dotnetexample/Datetime/Program.cs)  

|Kind|Description|
|---|---|
|Unspecified|Some kind|
|Utc|그니치 천문대 시간|
|Local|시간대를 반영한 지역 시간|

- Dotnet DateTime reference value : `1 year 1 month 1 day`
- Unix and JAVA Platform and etc... : `1970 year 1 month 1 day`

- JAVA Code 

```java
System.println(System.currentTimeMillis()); //result : 1361077426483
```

- .NET Code 

```csharp
Console.WriteLine(DateTime.UtcNow.Tricks / 10000); //result : 63496674226482
```

- Convert .NET milllis to JAVA millis 

```
long javaMillis = (DateTime.UtcNow.Ticks - 621355968000000000) / 10000;
```

## StringBuilder 

- `System.Text.StringBuilder`
- `string + string` is too many copy, plus action
- StringBuilder have enough memory allocate.  
- [StringBuilder Example](./dotnetexample/StringExample/Program.cs)  

## System.IO.MemoryStream

- [Convert Example](./dotnetexample/ConvertExample/Program.cs)  

```sh
Hello, World!
01
00-7D
4C-36-19-00
1652300
string result 4C-36-19-00
32000
1652300
Memory Stream Example : Using ToArray of MemoryStream
32000
1652300
ExampleStreamEncoding
Hello World
StreamWriter to StreamReader
Hello world
Anderson
32000
ExampleXmlSerializer
Name : Anderson, Age : 30
<?xml version="1.0" encoding="utf-8"?>
<Person xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Name>Anderson</Name>
  <Age>30</Age>
</Person>
ExampleJsonSerializer
{
  "Name": "Anderson",
  "Age": 30
}
Name : Anderson, Age : 30
```

## System.Collection

> ### Example File 
> [File usage exmaple](./dotnetexample/ExampleCollection/ExampleCollection/ExampleFileTest.cs)  

|static method|description|
|---|---|
|ChangeExtension|Replaces the extension of the given path (first argument) with the string provided as the second argument.|
|Combine|Combines all provided string arguments into a single path|
|GetDirectoryName||


## System.Threading 

- [Threading](./dotnetexample/chapter6Example/ExampleThreading/ExampleThreading.cs)  

### is Atomic variable? ( 32 bit OS )

```csharp
n32 = 1//n32 is Int32 type. -> Atomic 
n64 = 1//n64 is Int64 type. -> not Atomic
```

- `n64` is work, write memory bit 0x00000000, and write 0x05000000. ( two times write )
- if you want atomic calculate, that above problem. 

```csharp
long n = 0;
Interlocked.Exchange(ref n, 5);
```

- [System.Threading.ThreadPool](./dotnetexample/chapter6Example/ExampleThreading/ExampleThreading.cs)  
    - `ExampleThreadPool` function. 
- [System.Threading.EventWaitHandle](./dotnetexample/chapter6Example/ExampleThreading/ExampleEventHandle.cs)  
  - `AutoReset` :  The event resets automatically after releasing a single waiting thread.  
  - `ManualReset` : The event remains signaled until it is manually reset.  
  - [AutoReset, ManualReset, Monitor.Wait의 차이](https://www.sysnet.pe.kr/2/0/1015)  
- ***In the case of EventWaitHandle, it can compensate for the drawback of not being able to call Join on threads in the ThreadPool.***

## Async, sync example

- [Sync Example](./dotnetexample/chapter6Example/ExampleAysnc/SyncExample/ExampleSync.cs)  
  - `FileStream` is sync function. 
  - `FileStream` provide `BeginRead/EndRead`, `BeginWrite/EndWrite` method for async. 

- [Async Example](./dotnetexample/chapter6Example/ExampleAysnc/AsyncExample/ExampleAync.cs)  
  - `BeginRead/EndRead` page 449.
