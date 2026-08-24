# 자주까먹는 

- [dictionary 사용](./dictionary.csx). 
- [span](./span.csx). 
    - span example
    - hybrid span using heap rent (`ArrayPool`)
- [sorted list](./sortedlist.csx). 
- [string](./string.csx). 
    - string repeat
    - stringbuilder repeat
- [PriorityQueue](./PriorityQueue.csx)  
    - [Comparer Explaination](#c의-comparer-가이드-및-상세-분석)  
    - [Comparer With CompareTo](#compareto). 

# C#의 Comparer<T> 가이드 및 상세 분석

C#에서 객체를 정렬하거나 우선순위를 정의할 때 필수적으로 사용되는 `Comparer<T>` 클래스는 두 인스턴스를 비교하여 순서 관계를 정량화하는 유틸리티입니다.

## 예시 파일
- PriorityQueue.csx

## 답변

`Comparer<T>`는 `System.Collections.Generic` 네임스페이스에 규정된 **추상 기본 클래스(Abstract Base Class)**로, 두 개의 동종 객체를 비교할 수 있도록 `IComparer<T>` 인터페이스의 구현체를 제공합니다. 

이 클래스의 핵심적인 특징과 동작 원리, 활용 형태를 세밀히 분석하여 알려드립니다.

---

### 1. 기본 약속: 비교 결과값의 정량화 의미

`Comparer<T>.Compare(T? x, T? y)` 메서드가 반환하는 정수값은 다음과 같은 일관된 대소 관계 가이드라인을 지닙니다.

| 반환값 (int) | 수학적 의미 | 설명 |
| :--- | :--- | :--- |
| **음수** (Typically $-1$) | $x < y$ | `Processor`나 정렬 알고리즘은 오름차순 기준으로 `x`를 `y`보다 앞에 둠. |
| **$0$** | $x = y$ | 두 개체의 정렬 기준 상의 우선 관계 순위가 동일함. |
| **양수** (Typically $1$) | $x > y$ | 오름차순 기준으로 `x`를 `y`보다 뒤에 둠. |

---

### 2. Comparer<T> 사용의 주요 축 3가지

#### ⓵ 기본 정렬 기준 조회: `Comparer<T>.Default`
C# 자체 내장 타입(예: `int`, `string`, `double`)이나 구현하는 클래스가 `IComparable<T>` 인터페이스를 지원하고 있다면 기본 비교기를 인스턴스화 없이 사용할 수 있습니다.
```csharp
Comparer<int> defaultComparer = Comparer<int>.Default;
int result = defaultComparer.Compare(10, 20); // y가 크므로 음수 (-1) 반환
```

#### ⓶ 팩토리 메서드를 이용한 인라인 구현: `Comparer<T>.Create`
별도의 비교 클래스를 구조화하여 정의할 필요 없이, 즉석에서 정렬 전략을 람다식 표현으로 기입할 수 있습니다. (전달 예시 파일 PriorityQueue.csx에서 최대 힙을 구현하는 데 활용된 방식입니다.)
```csharp
// 역순(내림차순, Descending) 비교기 생성
var descComparer = Comparer<int>.Create((x, y) => y.CompareTo(x));
```

#### ⓷ 인터페이스 직접 구현 상속 (`IComparer<T>`)
도메인 모델의 크기가 커서 복잡한 복합 정렬 속성 로직이 필요하거나 재사용이 필요할 때 직접 클래스로 정렬 우선순위 규칙을 정의합니다.
```csharp
public class ProductPriceComparer : Comparer<Product>
{
    public override int Compare(Product? x, Product? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        
        // 1차 기준: 가격 오름차순
        int priceCompare = x.Price.CompareTo(y.Price);
        if (priceCompare != 0) return priceCompare;
        
        // 2차 기준: 이름 오름차순
        return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
    }
}
```

---

### 3. Comparer<T>와 IEquatable<T>/EqualityComparer<T>의 차이

초보자 분들이 자주 혼동하는 부분으로 용도가 완전히 다릅니다.

- **`Comparer<T>` / `IComparer<T>`**: 대소 관계(`x가 y보다 큰가 작은가?`)를 정량 화하여 **정렬, 최대/최소값 탐색, 힙(PriorityQueue) 인덱싱**에 기여합니다.
- **`EqualityComparer<T>` / `IEqualityComparer<T>`**: 대소 관계 없이 **동일성(`Equals`) 여부와 고유 지문값(`GetHashCode`)**만을 확인하며 `Dictionary<K, V>`의 키 매핑 및 중복 제거에 기여합니다.

### 추가 자료
- [Microsoft Learn: Comparer<T> 클래스 API 가이드](https://learn.microsoft.com/ko-kr/dotnet/api/system.collections.generic.comparer-1?view=net-8.0)
- [Microsoft Learn: IComparable<T>와 IComparer<T> 구현의 상세 차이점](https://learn.microsoft.com/ko-kr/dotnet/api/system.icomparable-1?view=net-8.0)

## CompareTo

-  A.CompareTo(B) = A - B 
- `Compare 에서 양수이면 바꾼다.`
    - `x.CompareTo(y)`: 1, 2, 3 순서에서 1.CompareTo(3) 일때 1 - 3은 `음수` 이므로 `그대로 둔다`. 
        - `오름 차순`, `Ascending`, `Min-Heap`
    - `y.CompareTo(x)`: 1, 2, 3 순서에서 3.CompareTo(1) 일때 3 - 1은 `양수`이므로 `바꾼다`.
        - `내림 차순`, `Descending`, `Max-Heap`