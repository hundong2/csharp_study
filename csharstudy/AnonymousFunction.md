# C#의 익명 함수(Anonymous Function)란?

이름 없이 즉시 정의해 넘길 수 있는 “함수 값”입니다. 대표적으로 람다 식(lambda)과 익명 메서드(delegate 키워드)가 있으며, 델리게이트(Action, Func<...>)와 함께 사용됩니다. 외부 변수를 캡처(클로저)할 수 있습니다.

## 예시 파일

[람다/익명 함수 샘플(공식 docs)](https://github.com/dotnet/docs/blob/main/docs/csharp/language-reference/operators/snippets/csharp/lambda-expressions/Program.cs)

## 답변

핵심 정리
- 형태
  - 람다 식: (x) => x + 1 또는 x => { ... } (식 본문/문 본문)
  - 익명 메서드: delegate(int x) { return x + 1; } (C# 2.0)
- 타입
  - 델리게이트로 변환됩니다. ex) Action, Func<int,string> 등
- 클로저(캡처)
  - 바깥 변수(로컬/this 등)를 사용할 수 있습니다. 이 경우 캡처용 힙 객체가 생길 수 있어 성능/수명에 주의.
- 언제 사용하나
  - 콜백, 이벤트 핸들러, LINQ, 비동기 흐름 등 “함수를 값처럼” 넘길 때
- 관련 개념과 차이
  - 로컬 함수: 이름 있는 중첩 함수. 캡처 가능, 제네릭/overload 가능, JIT이 최적화하기 쉬운 경우가 있음.
  - static 람다: 캡처 금지로 성능·안전 강화(C# 9+).

실전 예제와 베스트 프랙티스
````csharp
using System;
using System.Linq;
using System.Collections.Generic;

public static class AnonymousFunctionsDemo
{
    public static void Main()
    {
        // 1) 람다 식 기본
        Func<int,int> inc = x => x + 1;
        Console.WriteLine(inc(10)); // 11

        // 2) 문 본문 람다(여러 문장)
        Func<int,int> square = x => { var y = x * x; return y; };
        Console.WriteLine(square(4)); // 16

        // 3) 익명 메서드(delegate) 구문
        Func<int,int> dec = delegate(int x) { return x - 1; };
        Console.WriteLine(dec(10)); // 9

        // 4) 이벤트/콜백
        Action<string> log = s => Console.WriteLine($"LOG: {s}");
        log("hello");

        // 5) LINQ에서의 사용
        int[] nums = {1,2,3,4,5};
        var evens = nums.Where(n => n % 2 == 0).Select(n => n * 10);
        Console.WriteLine(string.Join(",", evens)); // 20,40

        // 6) 비동기 람다
        Func<int,Task<int>> delayDouble = async n => { await Task.Delay(50); return n * 2; };
        Console.WriteLine(delayDouble(7).Result); // 14

        // 7) 캡처(클로저)와 주의점
        int factor = 3; // 바깥 변수 캡처
        Func<int,int> mul = x => x * factor;
        Console.WriteLine(mul(5)); // 15
        factor = 10; // 캡처는 "참조"라 나중 값 영향을 받음
        Console.WriteLine(mul(5)); // 50

        // 8) for 루프 캡처 함정(각 반복 변수 공유 문제) - C# 5 이후 foreach는 개선
        var acts = new List<Action>();
        for (int i = 0; i < 3; i++)
            acts.Add(() => Console.Write(i + " ")); // i를 하나로 캡처
        acts.ForEach(a => a()); // 예: "3 3 3 " 출력 가능

        // 해결: 지역 복사본 사용
        acts.Clear();
        for (int i = 0; i < 3; i++)
        {
            int copy = i;
            acts.Add(() => Console.Write(copy + " ")); // "0 1 2 "
        }
        acts.ForEach(a => a());
        Console.WriteLine();

        // 9) static 람다로 캡처 금지(C# 9+)
        // Func<int,int> bad = x => x + factor; // 캡처 발생
        Func<int,int> good = static x => x + 1; // 캡처 불가 → 더 안전/빠름

        // 10) 로컬 함수: 이름/제네릭/오버로드 가능, 캡처도 가능
        int Twice(int n) => n * 2;
        Console.WriteLine(Twice(21)); // 42
    }
}
````

요약 가이드
- 간단 콜백/식 변환 → 람다 식 권장
- 긴 본문/재사용/오버로드 → 로컬 함수 고려
- 캡처 회피 → static 람다 또는 인자로 전달
- 루프 캡처 → 반복 변수는 지역 복사 후 캡처
- 성능 민감 경로 → 캡처 유무를 확인(캡처 시 힙 할당 발생 가능)

### 추가 자료
- [C# 익명 함수(문서)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/anonymous-functions)
- [람다 식(문서)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions)
