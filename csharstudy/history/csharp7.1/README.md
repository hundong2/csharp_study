# C# 7.1

## Main Method async possible

- previous Main Method 

```csharp
static void Main() {}
static void Main(string[] ) {}
static int Main() {}
static int Main(string[]) {}
```

- if want to use then 

```csharp
class Program
{
    static HttpClient _client = new HttpClient();
    static void Main(string[] args)
    {
        MainAsync(); //other async method call
    }

    private static async Task MainAsync()
    {
        string text = await _client.GetStringAsync("https://www.microsoft.com");
        Console.WriteLine(text);
    }
}
```

- Possible `async` to Main Method 

```csharp
class Program
{
    static HttpClient _client = new HttpClient();

    static async Task Main(string[] args)
    {
        string text = await _client.GetStringAsync("https://www.microsoft.com");
        Console.WriteLine(text);
    }
}
```

```csharp
static async Task Main(string[] args) => Console.WriteLine(await _client.GetStringAsync("https://www.microsoft.com"));
```

```csharp
static async Task Main() {}
static async Task Main(string[]){}
static async Task<int> Main() {}
static async Task<int> Main(string[]) {}
```

## type inference 

```csharp
int age = 20;
string name = "honeoel";

var t = (age, name);
Console.WriteLine($"{t.age}, {t.name}");
```
