# 컬렉션 식과 컬렉션 초기화 구문의 차이
C# 12의 “컬렉션 식(collection expression)”([…])과 기존 “컬렉션 초기화 구문(new T { … })”은 둘 다 여러 요소로 컬렉션을 만드는 문법이지만, 번역 방식·적용 대상·표현력에서 차이가 있습니다.

## 예시 파일
[Collection expressions (C# 12, 공식 문서)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions)

## 답변
핵심 비교
- 컬렉션 식([…], C# 12)
  - 목적: 배열·List<T>·Span/ReadOnlySpan<T>·Immutable 계열 등 “여러 컬렉션을 하나의 리터럴처럼” 통일해 만들기.
  - 특징:
    - 대상 형식에 맞춰 “타깃-타입”으로 변환됨. 예: int[] a = [1,2,3]; List<int> b = [1,2,3];
    - 확장(spread) .. 지원: [1, ..other, 4]
    - Add 호출 없이 “한 번에” 구성(배열 복사/빌더 사용). Add 메서드가 필요하지 않음.
    - Span/ReadOnlySpan<T>에도 바로 사용 가능: ReadOnlySpan<int> s = [1,2,3];
    - 사용자 정의 컬렉션도 “컬렉션 빌더 패턴”을 구현하면 대상 가능(.NET 8+).
- 컬렉션 초기화 구문(new T { … })
  - 목적: 특정 타입 인스턴스를 만든 뒤 Add(…)를 반복 호출해 요소 채우기.
  - 특징:
    - new로 생성자를 호출한 뒤, 컴파일러가 순서대로 Add(…)를 호출하는 코드로 변환.
    - Add 오버로드(또는 인덱서 초기화)가 반드시 있어야 함. 사전(Dictionary) 키-값 초기화에 특히 유용.
    - 개체 초기화(속성 설정)와 혼합 가능: new List<int> { Capacity = 10, 1, 2, 3 }

현실적인 선택 기준
- 간단한 시퀀스/병합/Span 대상: 컬렉션 식([…]) 권장. 가독성·성능(사이즈 예측/한 번에 구성) 유리.
- Dictionary/특수 Add 패턴/속성도 함께 초기화: 컬렉션 초기화(new T { … })가 자연스러움.
- var 사용 시: var xs = [1,2,3]; → 컴파일러가 배열(int[])로 둡니다. List가 필요하면 타입을 명시: List<int> xs = [1,2,3];

버전/호환
- 컬렉션 식은 C# 12 필요. 일부 타입(예: ImmutableDictionary 등)은 .NET 8의 “컬렉션 빌더” 지원이 있어야 자연스럽게 동작.
- 컬렉션 초기화는 모든 현대 C#에서 사용 가능.

예제 코드
````csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class Demo
{
    public static void Main()
    {
        // 1) 배열/리스트: 컬렉션 식
        int[] a1 = [1, 2, 3];                 // 배열 생성
        List<int> l1 = [1, 2, 3];             // List<T>로 타깃-타입 변환
        var a2 = [1, 2, 3];                   // var → int[] (배열)

        // 2) 확장(spread) .. 로 병합
        var l2 = new List<int> { 10, 20 };
        int[] a3 = [0, ..l2, 30];             // [0, 10, 20, 30]

        // 3) Span/ReadOnlySpan 대상(할당 최소화)
        ReadOnlySpan<int> rs = [7, 8, 9];
        Console.WriteLine(rs[1]);             // 8

        // 4) Immutable 계열(.NET 8+ 빌더 지원 필요)
        ImmutableArray<int> imm = [1, 2, 3];
        Console.WriteLine(imm.Length);        // 3

        // 5) Dictionary: 컬렉션 초기화 구문이 자연스러움
        var dict1 = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }; // 인덱서 초기화
        var dict2 = new Dictionary<string, int> { { "x", 9 }, { "y", 10 } }; // Add(key,value)

        // 컬렉션 식으로 Dictionary를 쓴다면 요소는 KeyValuePair여야 함(표현력이 떨어짐)
        Dictionary<string, int> dict3 = new() { 
            new KeyValuePair<string,int>("p", 1),
            new KeyValuePair<string,int>("q", 2)
        }; // ← 이건 '초기화 구문' 예시(컬렉션 식 아님)

        // 6) 개체 초기화 + 컬렉션 초기화 혼합
        var listWithCap = new List<int> { Capacity = 4, 1, 2, 3, 4 };

        Console.WriteLine(string.Join(",", a3)); // 0,10,20,30
    }
}
````

요약
- 컬렉션 식([…])은 “타깃-타입 기반으로 한 번에 구성하고, ..로 병합 가능한 통합 문법”.
- 컬렉션 초기화(new T { … })는 “Add 호출 기반”이며, Dictionary/인덱서 초기화·속성 설정에 강함.

### 추가 자료
- [Collection expressions (C# 12)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions)
- [Object and collection initializers](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers)
- [Immutable collections](https://learn.microsoft.com/dotnet/standard/collections/immutable)
- [Spread element(..) in collection expressions](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions#spread-elements)