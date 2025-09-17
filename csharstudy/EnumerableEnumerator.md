# IEnumerable<T> vs IEnumerator<T> 차이

시퀀스 “제공자”와 시퀀스 “커서”의 차이입니다. IEnumerable<T>는 열거를 시작하기 위한 진입점이고, IEnumerator<T>는 현재 위치/이동 상태를 갖는 1회용 커서입니다.

## 예시 파일

[IEnumerable<T> 소스 코드 (dotnet/runtime)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/IEnumerable.cs)

## 답변

핵심 차이

- IEnumerable<T>
  - 역할: “열거 가능한 컬렉션”을 표현. GetEnumerator()로 새 Enumerator를 만들어 줌.
  - 특징: 여러 번 반복 가능(매 호출마다 새 Enumerator 제공), LINQ 확장 메서드의 대상.
  - 사용처: foreach, LINQ, 지연 실행 파이프라인의 시작점.
- IEnumerator<T>
  - 역할: “현재 위치를 가진 커서”. MoveNext()로 전진, Current로 현재 요소 읽기.
  - 특징: 단일 패스(1회용), 보통 IDisposable(리소스 정리 필요). Reset은 거의 사용 안 함.
  - 사용처: foreach가 내부적으로 사용하는 저수준 메커니즘.

foreach 동작
- foreach는 원본 컬렉션의 IEnumerable<T>.GetEnumerator()를 호출해 IEnumerator<T>를 얻고, MoveNext()/Current를 반복하며 마지막에 Dispose를 호출합니다.

간단 예제
````csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// 1) IEnumerable<T> 구현: 여러 번 반복 가능
public sealed class FibSeq : IEnumerable<int>
{
    private readonly int _count;
    public FibSeq(int count) => _count = count;

    // yield return으로 Enumerator를 “자동 생성”
    public IEnumerator<int> GetEnumerator()
    {
        int a = 0, b = 1;
        for (int i = 0; i < _count; i++)
        {
            yield return a;
            (a, b) = (b, a + b);
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

class Program
{
    static void Main()
    {
        IEnumerable<int> seq = new FibSeq(5);

        // A) 고수준: foreach (IEnumerable<T> 대상)
        foreach (var x in seq) Console.Write($"{x} ");      // 0 1 1 2 3
        Console.WriteLine();

        // B) 저수준: IEnumerator<T> 직접 사용(1회용 커서)
        using var e = seq.GetEnumerator();
        while (e.MoveNext())
            Console.Write($"{e.Current} ");                  // 0 1 1 2 3
        Console.WriteLine();

        // C) 여러 번 반복 가능(IEnumerator는 매번 새로 생성됨)
        Console.WriteLine(seq.Sum());                        // 7
        Console.WriteLine(string.Join(",", seq.Take(3)));    // 0,1,1
    }
}
````

실무 팁
- 컬렉션을 제공하려면 IEnumerable<T>만 구현하면 충분(대부분 yield return 사용).
- Enumerator를 직접 구현해야 할 일은 드뭅니다(성능 특수화나 비관리 자원 관리 시).
- LINQ는 IEnumerable<T>를 중심으로 동작하며, 지연 실행으로 필요할 때마다 새 Enumerator를 만들어 소비합니다.

### 추가 자료
- [IEnumerator<T> 소스 코드](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/IEnumerator.cs)
- [IEnumerable<T> 문서](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)
- [foreach와 이터레이터(yield) 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/yield)