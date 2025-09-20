# C#의 Predicate<T>란?
T를 입력으로 받아 bool을 반환하는 “조건 검사” 대리자 타입입니다. 필터링, 검색, 삭제 등에서 조건식을 전달할 때 표준적으로 사용됩니다. Func<T,bool>과 사실상 동일한 의미이며, 매개변수 형식에 대해 반공변성(contravariance)을 가집니다.

## 예시 파일
[System.Predicate<T> 원본 코드(dotnet/runtime)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Predicate.cs)

## 답변
핵심 요약
- 정의: public delegate bool Predicate<in T>(T obj);
  - in T → 반공변성: Predicate<object>를 Predicate<string> 자리에서 사용할 수 있음.
- 역할: “이 항목을 채택할지(bool)”를 결정하는 콜백을 전달.
- 자주 쓰는 BCL API(List<T> 계열):
  - Find/FindLast/FindIndex/FindLastIndex
  - FindAll
  - Exists
  - TrueForAll
  - RemoveAll
- Func<T,bool>과의 관계: 의미 동일. LINQ(Where 등)는 Func<T,bool>을 쓰므로 필요 시 서로 어댑트해서 사용.

간단 예제
````csharp
using System;
using System.Collections.Generic;
using System.Linq;

class Person { public string Name { get; set; } = ""; public int Age { get; set; } }

class Demo
{
    static bool IsAdult(Person p) => p.Age >= 20;

    static void Main()
    {
        var people = new List<Person>
        {
            new() { Name = "A", Age = 18 },
            new() { Name = "B", Age = 25 },
            new() { Name = "C", Age = 30 },
        };

        // 1) Predicate<T> 사용(메서드 그룹/람다 모두 가능)
        Predicate<Person> adultPred = IsAdult;         // 메서드 그룹
        var adults = people.FindAll(adultPred);        // List<T>.FindAll(Predicate<T>)
        Console.WriteLine(string.Join(",", adults.ConvertAll(p => p.Name))); // B,C

        // 2) Exists/RemoveAll/TrueForAll
        Console.WriteLine(people.Exists(p => p.Age < 20));    // True
        int removed = people.RemoveAll(p => p.Age < 20);      // 조건에 맞는 항목 삭제
        Console.WriteLine(removed);                           // 1
        Console.WriteLine(people.TrueForAll(p => p.Age >= 20)); // True

        // 3) Func<T,bool>과 상호 운용(LINQ는 Func 사용)
        Func<Person, bool> f = adultPred.Invoke;     // Predicate → Func 어댑트
        var names = people.Where(f).Select(p => p.Name);
        Console.WriteLine(string.Join(",", names));  // B,C

        // 4) 반공변성(contravariance) 예시
        Predicate<object> anyObj = _ => true;
        Predicate<string> strPred = anyObj; // in T 덕분에 가능
        Console.WriteLine(strPred("hello")); // True
    }
}
````

현업 팁
- 컬렉션(List<T>) API에는 Predicate<T>가, LINQ에는 Func<T,bool>이 주로 쓰입니다. 필요 시 Invoke를 이용해 간단히 어댑트하세요.
- 많이 호출되는 Predicate는 캡처(클로저) 비용을 줄이고, 가능한 메서드 그룹(정적/인스턴스)을 권장합니다.

### 추가 자료
- [Predicate<T> 문서](https://learn.microsoft.com/dotnet/api/system.predicate-1)
- [List<T>.FindAll 문서](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.findall)
- [반공변성/공변성 개념](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/covariance-contravariance/)