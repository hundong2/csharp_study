# record class 최신 문법 예시와 사용 방법

record class는 값 기반 동등성, with 복사-수정, Deconstruct, 간결한 선언을 제공하는 참조형입니다. 최신 문법(C# 11/12 호환)으로 기본 사용을 보여드립니다.

## 예시 파일

[Record types – official samples](https://github.com/dotnet/samples/blob/main/snippets/csharp/records/record-types/Program.cs)

## 답변

아래 예시는 record class의 핵심 기능(값 기반 동등성, with, 상속, sealed ToString, required, 패턴 매칭, 딕셔너리 키)을 간단히 시연합니다.  

````csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RecordClassDemo;

// 1) 기본 record class: 주 생성자(포지셔널)로 구성 요소 정의
public record class Person(string FirstName, string LastName, int Age);

// 2) 파생 record class: 값 동등성은 계층 전체에 대해 작동
public record class Employee(string FirstName, string LastName, int Age, string Role)
    : Person(FirstName, LastName, Age)
{
    // C# 11: 필수 멤버(required). 객체 생성 시 반드시 채워야 함.
    public required decimal Salary { get; init; }

    // record는 기본 ToString을 제공하지만, 맞춤 출력이 필요하면 재정의 가능
    public sealed override string ToString()
        => $"{Role} {FirstName} {LastName} ({Age}), Salary={Salary.ToString("C0", CultureInfo.InvariantCulture)}";
}

public static class Program
{
    public static void Main()
    {
        // 생성
        var p1 = new Person("Kim", "Jisoo", 30);

        // with: 비파괴적 복사-수정
        var p2 = p1 with { Age = 31 };

        Console.WriteLine(p1); // Person { FirstName = Kim, LastName = Jisoo, Age = 30 }
        Console.WriteLine(p2); // Person { FirstName = Kim, LastName = Jisoo, Age = 31 }

        // 값 기반 동등성(구성 요소가 모두 같으면 true)
        var p3 = new Person("Kim", "Jisoo", 31);
        Console.WriteLine(p2 == p3); // True

        // Deconstruct 지원
        var (first, last, age) = p2;
        Console.WriteLine($"{first} {last}: {age}");

        // 패턴 매칭(속성 패턴)
        if (p2 is Person { FirstName: "Kim", Age: >= 30 })
            Console.WriteLine("Matched: Kim and age >= 30");

        // 파생 record 사용
        var e1 = new Employee("Lee", "Sangjun", 40, "Manager") { Salary = 120_000m };
        var e2 = e1 with { Role = "Director", Salary = 150_000m };
        Console.WriteLine(e1); // Manager Lee Sangjun (40), Salary=120,000
        Console.WriteLine(e2); // Director Lee Sangjun (40), Salary=150,000

        // 딕셔너리 키로 사용(값 동등성 기반 해시/Equals 제공)
        var dict = new Dictionary<Person, string>
        {
            [p1] = "user-1",
            [p2] = "user-2"
        };
        Console.WriteLine(dict[new Person("Kim", "Jisoo", 30)]); // user-1

        // 다형성: 기반 타입 참조로도 값 동등성 유지
        Person asBase = e2;
        Console.WriteLine(asBase == e2); // True
    }
}
````

요점. 
- record class는 참조형이지만 값 기반 동등성(==, Equals, GetHashCode)을 자동 제공합니다.
- with로 일부 속성만 바꾼 새 인스턴스를 쉽게 생성합니다.
- 파생 record 간에도 동등성/복사 규칙이 자연스럽게 동작합니다.
- required로 필수 init 속성을 강제할 수 있습니다(C# 11+).

### 추가 자료

- [Record types (C# reference)]https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record
- [with 식(비파괴적 변경)]https://learn.microsoft.com/dotnet/csharp/language-reference/operators/with-expression
- [Required members(C# 11)]https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/required
- [Records – equality and printing]https://learn.microsoft.com/dotnet/csharp/whats-new/tutorials/records#value-based-equality-and-immutability