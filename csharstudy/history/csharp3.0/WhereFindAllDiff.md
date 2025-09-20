# FindAll vs Where 내부 구현 차이
List<T>.FindAll은 즉시 물질화(Eager)하는 메서드이고, LINQ의 Where는 지연 실행(Deferred) 이터레이터를 반환합니다. 또한 Where는 체이닝 시 predicate를 결합해 호출 비용을 줄이는 최적화가 있습니다.

## 예시 파일
[System.Linq Where 구현(WhereEnumerableIterator 등)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Linq/src/System/Linq/Where.cs)

## 답변
핵심 차이
- 반환 형식
  - FindAll: 새 List<T>를 즉시 만들어 반환(즉시 필터링, 메모리 할당).
  - Where: IEnumerable<T> 이터러블을 반환(지연 실행, foreach 시점에 필터링).
- 델리게이트
  - FindAll: Predicate<T>
  - Where: Func<T, bool>
- 최적화
  - Where는 소스 유형별 특화 이터레이터(배열/리스트 등)와 Where 체이닝 결합(CombinePredicates)로 호출 수를 최소화.
  - FindAll은 한 번 훑으며 결과 리스트를 채우는 단순 루프.

비교 데모 1) 지연 vs 즉시
````csharp
using System;
using System.Collections.Generic;
using System.Linq;

var nums = new List<int> { 1, 2, 3 };

// 즉시 물질화: 결과가 리스트로 고정됨
var eager = nums.FindAll(n => n > 1);

// 지연 실행: 아직 평가되지 않음
var deferred = nums.Where(n => n > 1);

// 원본 변경
nums.Add(100);

// 결과 비교
Console.WriteLine(string.Join(",", eager));    // 2,3  (추가된 100 없음)
Console.WriteLine(string.Join(",", deferred)); // 2,3,100 (열거 시점에 반영)
````

비교 데모 2) Where 체이닝 최적화(결합) vs 단순 체이닝
````csharp
using System;
using System.Collections.Generic;
using System.Linq;

var data = new List<int> { 1, 2, 3, 4, 5 };

int calls = 0;
var q = data
    .Where(n => { calls++; return n % 2 == 0; }) // 짝수
    .Where(n => n > 2);                           // 2보다 큼

// .NET LINQ는 두 predicate를 결합해 1패스에서 1회 호출로 처리
_ = q.ToList();
Console.WriteLine($"LINQ Where predicate 호출 수 = {calls}"); // 5 (요소당 1회)
````

내부 구조를 이해하기 위한 “단순화된” 재구현 예
- FindAll: 즉시 리스트 생성
- Where: yield 기반 지연 실행(여기선 결합 최적화 없음 → 체이닝하면 predicate가 두 번 호출됨)
````csharp
using System;
using System.Collections.Generic;

public static class Mini
{
    // FindAll: 즉시 물질화
    public static List<T> MyFindAll<T>(List<T> source, Predicate<T> match)
    {
        var result = new List<T>();
        foreach (var item in source)
            if (match(item)) result.Add(item);
        return result;
    }

    // Where: 지연 실행(yield). 체이닝 결합 최적화는 생략(단순 참조 구현)
    public static IEnumerable<T> MyWhere<T>(IEnumerable<T> source, Func<T, bool> pred)
    {
        foreach (var item in source)
            if (pred(item)) yield return item;
    }
}

// 사용 예(체이닝 시 호출 수 비교)
var arr = new[] { 1, 2, 3, 4, 5 };
int myCalls = 0;
var q2 = Mini.MyWhere(arr, n => { myCalls++; return n % 2 == 0; });
q2 = Mini.MyWhere(q2, n => n > 2);
_ = new List<int>(q2); // 열거 실행
Console.WriteLine($"Mini.MyWhere predicate 호출 수 = {myCalls}"); // 10 (요소당 2회)
````

정리
- FindAll: 즉시 실행, List<T> 반환, 단발성 필터 후 추가 가공에 편리.
- Where: 지연 실행, IEnumerable<T> 반환, 파이프라인 조합/스트리밍 처리에 유리, 내부적으로 predicate 결합 최적화와 소스별 특화 이터레이터를 사용.

### 추가 자료
- [List<T>.FindAll 소스(List.cs)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs)
- [Enumerable.Where 문서](https://learn.microsoft.com/dotnet/api/system.linq.enumerable.where)
- [LINQ 지연 실행 설명](https://learn.microsoft.com/dotnet/csharp/linq/deferred-execution)