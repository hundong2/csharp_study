# C#의 Func, Action 총정리
Microsoft가 제공하는 범용 델리게이트로, 람다/메서드를 변수처럼 전달할 때 표준적으로 사용하는 타입들입니다. Func은 “반환값 있는 함수”, Action은 “반환값 없는 함수”를 표현합니다.

## 예시 파일
[System.Func 사용 예제 (dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/VS_Snippets_CLR_System/system.func/cs/program.cs)

## 답변
개념 요약
- Func: 입력 매개변수 n개 + 마지막 타입이 반환형. 최소 0개 매개변수(Func<TResult>)부터 최대 16개 매개변수(Func<T1,…,T16,TResult>) 지원.
- Action: 입력 매개변수 n개, 반환형 없음(void). 최소 0개 매개변수(Action)부터 최대 16개 매개변수(Action<T1,…,T16>) 지원.
- 사용처: LINQ(Where/Select), 콜백, 이벤트 핸들러(일반적으론 EventHandler 권장), 전략 주입, 비동기/지연 실행.

주요 시그니처
- Func: Func<TResult>, Func<T, TResult>, Func<T1, T2, TResult>, …, Func<T1,…,T16, TResult>
- Action: Action, Action<T>, Action<T1, T2>, …, Action<T1,…,T16>

실전 예제
````csharp
using System;
using System.Collections.Generic;
using System.Linq;

class Demo
{
    static void Main()
    {
        // 1) Func 기본
        Func<int, int, int> add = (x, y) => x + y;
        Func<string> nowText = () => DateTime.Now.ToString("O");
        Console.WriteLine(add(3, 5));      // 8
        Console.WriteLine(nowText());      // ISO8601

        // 2) Action 기본
        Action hello = () => Console.WriteLine("Hello");
        Action<int, string> log = (code, msg) => Console.WriteLine($"[{code}] {msg}");
        hello();
        log(200, "OK");

        // 3) LINQ에서 Func 활용
        var nums = Enumerable.Range(1, 10).ToList();
        Func<int, bool> isEven = n => n % 2 == 0;
        var evens = nums.Where(isEven).Select(n => n * n);
        Console.WriteLine(string.Join(",", evens)); // 4,16,36,64,100

        // 4) Predicate/Comparison과의 비교
        Predicate<string> nonEmpty = s => !string.IsNullOrEmpty(s);
        Func<string, bool> nonEmpty2 = nonEmpty.Invoke; // 사실상 동등
        var list = new List<string?> { "a", "", null, "b" };
        Console.WriteLine(string.Join(",", list.Where(s => s is not null && nonEmpty2(s)))); // a,b

        // List<T>.Sort는 Comparison<T>를 사용하지만 Func<T,T,int>도 어댑트 가능
        var people = new List<(string Name, int Age)> { ("A", 30), ("B", 20), ("C", 40) };
        Func<(string,int),(string,int),int> byAge = (p1, p2) => p1.Item2.CompareTo(p2.Item2);
        people.Sort(new Comparison<(string,int)>(byAge));
        Console.WriteLine(string.Join(", ", people.Select(p => $"{p.Name}:{p.Age}"))); // B:20, A:30, C:40

        // 5) 고급: 함수 합성/커링 유틸
        Func<int, int> square = x => x * x;
        Func<int, int> inc = x => x + 1;
        var squareThenInc = Compose(square, inc); // inc(square(x))
        Console.WriteLine(squareThenInc(4)); // 17

        // 6) 이벤트에 Action 사용(간단 데모) — 실제로는 EventHandler 패턴 권장
        var evt = new SimpleEvent();
        evt.OnChanged += () => Console.WriteLine("Changed!");
        evt.Raise();
    }

    // f: A->B, g: B->C => g∘f: A->C
    static Func<TA, TC> Compose<TA, TB, TC>(Func<TA, TB> f, Func<TB, TC> g)
        => a => g(f(a));
}

class SimpleEvent
{
    public event Action? OnChanged;
    public void Raise() => OnChanged?.Invoke();
}
````

실무 팁
- Action은 반환값이 없으므로 부작용(side effect) 용도. 반환이 필요하면 Func 사용.
- async void(Action과 결합)은 테스트/예외 전파가 어려움. 비동기 콜백은 Func<Task> 패턴을 권장.
- 라이브러리 API 설계 시 입력만 쓰는 제네릭은 in(반공변), 출력만 쓰는 제네릭은 out(공변)으로 설계하면 유연성이 좋습니다.

### 추가 자료
- [System.Func 문서](https://learn.microsoft.com/dotnet/api/system.func-1)
- [System.Action 문서](https://learn.microsoft.com/dotnet/api/system.action)
- [대리자와 람다 식](https://learn.microsoft.com/dotnet/csharp/programming-guide/delegates/)
- [LINQ 개요와 사용법](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/linq/)