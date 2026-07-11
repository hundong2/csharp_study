using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 이 파일은 dailyStudy의 고급 예제를 보기 전에 먼저 실행하는 기초 문법 투어입니다.
// 목표는 모든 문법을 깊게 외우는 것이 아니라, 날짜별 예제에서 자주 나오는 문법을 읽을 수 있게 만드는 것입니다.

Console.WriteLine("== 1. 변수와 타입 ==");

// string은 문자열 타입입니다.
// int는 정수 타입입니다.
// double은 소수점이 있는 숫자 타입입니다.
string serviceName = "Gateway";
int requestCount = 3;
double latencyMs = 12.5;

// 문자열 보간:
// $"..." 안에 {변수명}을 넣으면 값을 문자열 안에 쉽게 넣을 수 있습니다.
Console.WriteLine($"Service: {serviceName}, Requests: {requestCount}, Latency: {latencyMs}ms");

Console.WriteLine();
Console.WriteLine("== 2. if 조건문 ==");

// if 문은 조건이 true일 때만 내부 코드를 실행합니다.
if (latencyMs < 50)
{
    Console.WriteLine("Latency is healthy.");
}
else
{
    Console.WriteLine("Latency is slow.");
}

Console.WriteLine();
Console.WriteLine("== 3. List와 foreach ==");

// List<T>는 크기가 변할 수 있는 컬렉션입니다.
// 여기서 T는 타입 자리이며, List<string>은 문자열 목록입니다.
var routes = new List<string> { "/orders", "/payments", "/health" };

// foreach는 컬렉션의 모든 값을 하나씩 꺼내 반복합니다.
foreach (string route in routes)
{
    Console.WriteLine($"Route: {route}");
}

Console.WriteLine();
Console.WriteLine("== 4. 메서드 ==");

// 메서드는 자주 쓰는 코드를 이름 붙여 재사용하는 단위입니다.
// static은 객체를 만들지 않고 호출할 수 있다는 뜻입니다.
static int Add(int left, int right)
{
    return left + right;
}

int sum = Add(10, 20);
Console.WriteLine($"10 + 20 = {sum}");

Console.WriteLine();
Console.WriteLine("== 5. 클래스와 객체 ==");

// class는 상태와 동작을 함께 묶는 설계 단위입니다.
// new 키워드는 객체를 생성합니다.
var counter = new RequestCounter();
counter.Increment();
counter.Increment();
Console.WriteLine($"Counter Value: {counter.Value}");

Console.WriteLine();
Console.WriteLine("== 6. 인터페이스 ==");

// interface는 "이 기능을 반드시 제공해야 한다"는 약속입니다.
// 구체 클래스를 직접 쓰지 않고 인터페이스로 받으면 구현을 교체하기 쉬워집니다.
IMessageWriter writer = new ConsoleMessageWriter();
writer.Write("Interface keeps code flexible.");

Console.WriteLine();
Console.WriteLine("== 7. 제네릭 ==");

// 제네릭은 타입을 나중에 정하게 해 주는 문법입니다.
// Box<int>는 int를 담고, Box<string>은 string을 담습니다.
var numberBox = new Box<int>(123);
var textBox = new Box<string>("hello");

Console.WriteLine($"NumberBox: {numberBox.Value}");
Console.WriteLine($"TextBox: {textBox.Value}");

Console.WriteLine();
Console.WriteLine("== 8. async / await ==");

// await는 비동기 작업이 끝날 때까지 기다립니다.
// 기다리는 동안 스레드를 오래 붙잡지 않는 것이 핵심입니다.
string data = await LoadDataAsync();
Console.WriteLine($"Loaded: {data}");

Console.WriteLine();
Console.WriteLine("== 9. using과 리소스 정리 ==");

// using 블록은 파일, 소켓, DB 연결 같은 리소스를 안전하게 정리할 때 사용합니다.
// 여기서는 예제를 단순하게 하기 위해 직접 만든 DisposableSample을 사용합니다.
using (var resource = new DisposableSample())
{
    resource.Use();
}

public sealed class RequestCounter
{
    // private 필드는 클래스 내부에서만 직접 변경할 수 있습니다.
    private int _value;

    // public 속성은 외부에서 읽을 수 있는 값을 제공합니다.
    public int Value => _value;

    public void Increment()
    {
        _value++;
    }
}

public interface IMessageWriter
{
    void Write(string message);
}

public sealed class ConsoleMessageWriter : IMessageWriter
{
    public void Write(string message)
    {
        Console.WriteLine($"Message: {message}");
    }
}

public sealed class Box<T>
{
    public Box(T value)
    {
        Value = value;
    }

    public T Value { get; }
}

static async Task<string> LoadDataAsync()
{
    await Task.Delay(50);
    return "sample-data";
}

public sealed class DisposableSample : IDisposable
{
    public void Use()
    {
        Console.WriteLine("Resource is being used.");
    }

    public void Dispose()
    {
        Console.WriteLine("Resource has been cleaned up.");
    }
}
