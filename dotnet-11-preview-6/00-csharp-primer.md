# 0.5 C# 코드를 읽기 위한 최소 문법

이 장은 프로그래밍을 처음 시작한 학습자를 위한 다리입니다. 먼저 [00_BeginnerSyntaxLab.csx](./00_BeginnerSyntaxLab.csx)를 한 줄씩 읽고 실행한 뒤 CLR/JIT 장으로 넘어갑니다.

## 코드 한 줄을 읽는 순서

```csharp
int total = price * count;
```

왼쪽부터 외우기보다 다음 질문으로 나눕니다.

1. **형식**은 무엇인가? `int`이므로 정수입니다.
2. **새 이름**은 무엇인가? `total`이라는 변수를 선언합니다.
3. **값은 어디서 오는가?** `price * count`라는 식을 계산합니다.
4. **무슨 동작인가?** `=`가 오른쪽 결과를 왼쪽 변수에 대입합니다.
5. **문장은 어디서 끝나는가?** `;`에서 끝납니다.

`=`는 “양쪽이 같다”는 비교가 아니라 대입입니다. 같은지 비교할 때는 `==`를 씁니다.

## 자주 보는 기호

| 문법 | 읽는 법 | 예 |
|---|---|---|
| `;` | 한 문장의 끝 | `int age = 20;` |
| `{ ... }` | 여러 문장을 묶은 block | `if (ok) { ... }` |
| `( ... )` | method 인수 또는 조건 | `WriteLine(name)`, `if (ok)` |
| `[ ... ]` | 배열/indexer의 위치 | `items[0]` |
| `<T>` | generic 형식 자리 | `List<string>` |
| `.` | 객체/형식의 member 접근 | `name.Length` |
| `=>` | 짧은 함수 또는 식 본문 | `x => x * 2` |
| `?` | nullable 또는 조건 연산 일부 | `string?`, `a ? b : c` |
| `!` | 논리 부정, 문맥에 따라 null-forgiving | `!isReady`, `value!` |
| `//` | 줄 끝까지 주석 | `// 설명` |
| `/* ... */` | 여러 줄 주석 | 파일 머리말 |

`!`처럼 같은 기호가 문맥에 따라 다르게 동작할 수 있습니다. 이름과 주변 코드를 함께 읽습니다.

## 값과 변수

```csharp
int count = 3;
double rate = 1.5;
bool enabled = true;
char grade = 'A';
string name = "Ada";
```

- `int`, `double`, `bool`, `char`는 값 형식입니다.
- `string`은 참조 형식이지만 언어가 문자열 리터럴 문법을 제공합니다.
- 작은따옴표 `'A'`는 문자 하나, 큰따옴표 `"Ada"`는 문자열입니다.
- `var`는 동적 형식이 아닙니다. 컴파일러가 오른쪽 식에서 정적 형식을 추론합니다.

```csharp
var count = 3;       // 컴파일러가 int로 결정
var name = "Ada";    // 컴파일러가 string으로 결정
```

## 연산자

```csharp
int sum = 2 + 3;
int remainder = 7 % 3;
bool same = sum == 5;
bool allowed = same && remainder == 1;
```

| 분류 | 연산자 | 의미 |
|---|---|---|
| 산술 | `+ - * / %` | 사칙연산과 나머지 |
| 비교 | `== != < <= > >=` | 결과는 `bool` |
| 논리 | `&& || !` | AND, OR, NOT |
| 대입 | `= += -= *= /=` | 변수의 값 설정/갱신 |
| null | `?. ??` | 안전한 member 접근, 대체값 |

정수끼리 `7 / 2`를 계산하면 결과는 `3`입니다. 소수 결과가 필요하면 한쪽을 `double`로 바꿉니다.

## 조건문

```csharp
if (score >= 80)
{
    Console.WriteLine("통과");
}
else
{
    Console.WriteLine("복습");
}
```

`if`의 괄호 안에는 `bool` 식이 옵니다. 조건이 참이면 첫 block, 거짓이면 `else` block을 실행합니다.

`switch`는 한 값을 여러 case로 나눌 때 유용합니다.

```csharp
string label = score switch
{
    >= 90 => "A",
    >= 80 => "B",
    _ => "C"
};
```

`_`는 앞 case에 맞지 않는 나머지를 뜻합니다.

## 반복문과 collection

```csharp
int[] numbers = { 10, 20, 30 };

for (int i = 0; i < numbers.Length; i++)
{
    Console.WriteLine(numbers[i]);
}

foreach (int number in numbers)
{
    Console.WriteLine(number);
}
```

- 배열 index는 0부터 시작합니다. 길이 3의 마지막 index는 2입니다.
- `for`는 초기화, 계속 조건, 증감을 직접 관리합니다.
- `foreach`는 collection의 원소를 차례로 읽습니다.
- 범위를 벗어난 배열 접근은 `IndexOutOfRangeException`을 발생시킵니다.
- `List<T>`는 크기를 늘리고 줄일 수 있는 generic collection입니다.

## 메서드

```csharp
static int Add(int left, int right)
{
    int result = left + right;
    return result;
}
```

- `static`: 객체를 만들지 않고 형식에 속한 기능으로 호출합니다.
- `int`: 반환 형식입니다.
- `Add`: 메서드 이름입니다.
- `(int left, int right)`: 입력 parameter 두 개입니다.
- `return`: 호출자에게 결과를 돌려줍니다.
- 반환값이 없으면 `void`, 비동기 완료만 나타내면 흔히 `Task`를 사용합니다.

**parameter**는 선언부의 입력 이름이고 **argument**는 호출할 때 실제로 넘기는 값입니다.

## 클래스와 객체

```csharp
public sealed class Product
{
    public Product(string name, int price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; }
    public int Price { get; }
}

var product = new Product("책", 20_000);
```

- class는 객체의 데이터와 동작을 정의하는 설계도입니다.
- `new`는 생성자를 호출해 인스턴스를 만듭니다.
- property는 `get`/`set`/`init`으로 읽기와 쓰기 정책을 표현합니다.
- `public`은 다른 코드에서 접근 가능, `private`은 선언 형식 안에서만 접근 가능입니다.
- `sealed`는 더 이상 상속하지 못하게 합니다.
- interface는 여러 형식이 지킬 능력의 계약을 선언합니다.

## null과 nullable

`null`은 참조가 어떤 객체도 가리키지 않음을 뜻합니다.

```csharp
string? nickname = null;
int length = nickname?.Length ?? 0;
```

- `string?`는 null 가능성을 소스에 표시합니다.
- `?.`는 null이면 member 접근을 건너뛰고 null을 만듭니다.
- `??`는 왼쪽이 null일 때 오른쪽 대체값을 사용합니다.
- `value!`는 컴파일러 경고만 억제합니다. 런타임 null을 객체로 바꾸지 않으므로 남용하지 않습니다.

## 예외와 자원 정리

```csharp
try
{
    int value = int.Parse("123");
}
catch (FormatException exception)
{
    Console.WriteLine(exception.Message);
}
```

예외는 정상 반환으로 처리할 수 없는 실패를 호출 stack 위로 전달합니다. 잡을 수 있는 구체적 예외만 처리하고, 오류를 숨긴 채 계속하지 않습니다.

`Stream`, `Process`, `CancellationTokenSource`처럼 OS handle이나 callback registration을 가진 객체는 `IDisposable`일 수 있습니다.

```csharp
using (Stream stream = OpenStream())
{
    // block을 나갈 때 Dispose가 호출됩니다.
}
```

`using`은 GC를 강제로 실행하는 문법이 아닙니다. 자원을 결정적으로 정리하기 위한 `try/finally` 형태로 변환됩니다.

## 동기와 비동기

```csharp
static async Task<string> LoadAsync()
{
    await Task.Delay(100);
    return "완료";
}
```

- `async`는 메서드 안에서 `await`를 사용할 수 있게 하고 컴파일러가 상태 머신을 만듭니다.
- `await`는 작업이 끝날 때까지 현재 메서드의 나머지를 보류합니다.
- `Task<T>`는 미래에 `T` 결과 또는 예외로 완료될 작업을 나타냅니다.
- `Async` suffix는 비동기 메서드라는 .NET naming convention입니다.

## 다음 단계

1. [00_BeginnerSyntaxLab.csx](./00_BeginnerSyntaxLab.csx)를 실행합니다.
2. 출력 전에 각 줄의 결과를 예상합니다.
3. 숫자와 문자열을 바꿔 다시 실행합니다.
4. [C# 기초와 CLR/JIT](./01-foundations-clr-jit.md)에서 이 문법이 런타임 안에서 어떻게 동작하는지 연결합니다.

## 공식 기초 링크

- [Microsoft Learn: C# 언어 둘러보기](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/overview)
- [Microsoft Learn: Nullable reference types](https://learn.microsoft.com/dotnet/csharp/fundamentals/null-safety/nullable-reference-types)
- [Microsoft Learn: async와 await](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)

> 이전: [시작·설치·실행](./00-getting-started.md) · 다음: [C# 기초와 CLR/JIT](./01-foundations-clr-jit.md)
