# .NET의 BCL(Base Class Library)이란?
.NET에서 가장 기초가 되는 표준 라이브러리 집합으로, 컬렉션, 파일/네트워크 I/O, 스레딩, 반사(Reflection), 수학, 텍스트 처리 등 “대부분의 앱이 공통으로 쓰는 기능”을 제공하는 타입들의 모음입니다.

## 예시 파일
[HelloMsBuild Program.cs (간단 콘솔, BCL 사용)](https://github.com/dotnet/samples/blob/main/core/console-apps/HelloMsBuild/Program.cs)

## 답변
- 정의와 역할
  - BCL은 .NET 런타임 위에서 동작하는 “표준 라이브러리”입니다. Object, String, DateTime, Stream, List<T>, Task, HttpClient, Regex, BigInteger 등 필수 타입과 API를 제공합니다.
  - .NET Framework 시절의 FCL(Framework Class Library)에서 핵심 하위 집합이 BCL로 불렸고, 현대 .NET(.NET 5+)에서는 System.* 어셈블리/패키지로 구성된 표준 라이브러리 전체를 실무에서 관습적으로 BCL이라 부릅니다.
- 포함 영역(대표)
  - 기본 타입/텍스트: System, System.Text, System.Text.RegularExpressions
  - 컬렉션/LINQ: System.Collections.*, System.Linq
  - I/O/직렬화: System.IO.*, System.Text.Json
  - 네트워크: System.Net.*, System.Net.Http
  - 스레딩/비동기: System.Threading.*, System.Threading.Tasks
  - 반사/런타임: System.Reflection, System.Runtime
  - 수치/암호 보조: System.Numerics(BigInteger), System.Security.Cryptography
- 배포/호환
  - 현대 .NET에서 Target Framework(예: net8.0)를 지정하면 해당 TFM에 맞는 BCL이 자동 포함됩니다.
  - 교차 플랫폼 API를 표준화한 .NET Standard는 “BCL 표면”을 여러 런타임에서 일관되게 쓰기 위한 계약입니다.
- 왜 중요한가
  - 앱의 80%는 BCL로 해결됩니다. 외부 라이브러리 도입 전에 BCL에 동일/유사 기능이 있는지 먼저 확인하는 습관이 품질·보안·유지보수에 유리합니다.

간단 종합 예제(BCL 핵심 API 맛보기)
````csharp
using System;
using System.IO;                 // File/Directory/Path
using System.Linq;               // LINQ
using System.Net.Http;           // HttpClient
using System.Numerics;           // BigInteger
using System.Text.Json;          // JsonSerializer
using System.Threading.Tasks;    // Task/async

class Demo
{
    static async Task Main()
    {
        // 1) 컬렉션 + LINQ
        var nums = Enumerable.Range(1, 10).ToList();
        Console.WriteLine($"Sum={nums.Sum()}, EvenCount={nums.Count(n => n % 2 == 0)}");

        // 2) 파일 I/O
        string path = Path.Combine(AppContext.BaseDirectory, "sample.txt");
        await File.WriteAllTextAsync(path, "hello BCL");
        Console.WriteLine(await File.ReadAllTextAsync(path));

        // 3) 네트워크 I/O
        using var http = new HttpClient();
        string json = await http.GetStringAsync("https://api.github.com/repos/dotnet/runtime");
        using var doc = JsonDocument.Parse(json);
        Console.WriteLine(doc.RootElement.GetProperty("name").GetString());

        // 4) 시간/수학
        Console.WriteLine(DateTime.UtcNow.ToString("O"));
        BigInteger big = BigInteger.Pow(2, 200);
        Console.WriteLine($"2^200 has {big.ToString().Length} digits");
    }
}
````

### 추가 자료
- [.NET API 브라우저(전체 BCL 검색)](https://learn.microsoft.com/dotnet/api/?view=net-8.0)
- [.NET Standard 개요(호환성 계약)](https://learn.microsoft.com/dotnet/standard/net-standard)
- [.NET 클래스 라이브러리 개요](https://learn.microsoft.com/dotnet/standard/class-libraries)
- [.NET 컬렉션 개요](https://learn.microsoft.com/dotnet/standard/collections)
- [.NET I/O 개요](https://learn.microsoft.com/dotnet/standard/io/)