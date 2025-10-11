# C#에서 불변(Immutable)이란?

객체가 한 번 생성되면 내부 상태가 더 이상 바뀌지 않는 성질을 말합니다. C# 9의 init 전용 설정자와 record가 이를 쉽게 구현하도록 도와줍니다.
  
## 예시 파일

[Records와 init-only 샘플(Program.cs)](https://github.com/dotnet/samples/blob/main/snippets/csharp/records/record-types/Program.cs)

## 답변

불변의 의미와 이유. 
- 정의: 생성 시점 이후 상태가 변경되지 않는 객체. 재할당은 가능하지만, 같은 인스턴스의 내부 값은 바뀌지 않음.
- 사용하는 이유
  - 스레드 안전: 변경이 없으니 동기화 부담이 줄어듭니다.
  - 예측 가능성: 공유/전달해도 “누가 몰래 바꿨나?”를 걱정할 필요가 없습니다.
  - 값 의미 모델링: 주소(값이 같으면 같은 것) 같은 도메인에 적합.
  - 디버깅·테스트 용이: 상태 전이가 줄어 버그 추적이 쉬움.

C#에서 불변을 만드는 방법. 
- init 전용 설정자(C# 9): 생성 이후에는 set 불가. 생성자/객체 이니셜라이저에서만 값 설정.
- record: 값 기반 동등성(Equals/GetHashCode/== 자동) + with 식으로 복사-수정 패턴 제공.
- 읽기 전용 필드/타입: readonly 필드, readonly struct로 변경을 제한.
- 불변 컬렉션: System.Collections.Immutable의 ImmutableArray<T>, ImmutableDictionary<K,V> 등으로 내부 컬렉션도 불변화.

예제 1) init 전용 설정자

````csharp
// 불변 DTO: 생성/초기화 시에만 설정 가능
public class Customer
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
}

// 사용
var c1 = new Customer { Name = "Alice", Age = 30 };
// c1.Age = 31; // 컴파일 오류: init은 생성 이후 설정 불가
````

예제 2) record와 with 식

````csharp
public record Point(int X, int Y);

var p1 = new Point(1, 2);
// 기존 인스턴스는 유지되고, 일부만 바뀐 복사본 생성
var p2 = p1 with { Y = 3 };
// 값 동등성
bool same = p1 == new Point(1, 2); // true
````

예제 3) 내부 컬렉션까지 불변 유지하기

````csharp
using System.Collections.Immutable;

public record Order
{
    public int Id { get; init; }
    public ImmutableArray<string> Items { get; init; } = ImmutableArray<string>.Empty;
}

var o1 = new Order { Id = 1, Items = ImmutableArray.Create("A", "B") };
// o1.Items[0] = "X"; // 불가(불변)
var o2 = o1 with { Items = o1.Items.Add("C") }; // 새 인스턴스
````

주의: “얕은 불변성” 함정. 
- 속성 타입이 가변(List<T>, StringBuilder 등)이면, 외부에서 그 컬렉션을 바꿔 내부 상태가 변할 수 있습니다.
- 해결: 불변 컬렉션 사용, 복사 방어(AsReadOnly, ToArray) 또는 읽기 전용 래퍼 사용.

언제 불변이 유리한가. 
- 동시성(멀티스레드), 캐시 키/사전 키, 설정/환경 값, 도메인 모델(값 객체), 이벤트/메시지 페이로드.
- 대용량/고빈도 업데이트가 필요한 경우엔 비용(복사)이 커질 수 있어 구조적 공유(record + 불변 컬렉션)로 완화.

왜 생겨났나(배경). 
- 변경 가능한(shared mutable) 상태에서 발생하는 경쟁 조건, 방어적 복사 남발, 예측 어려움 문제를 줄이기 위해.
- 함수형 프로그래밍의 장점을 C#에서도 손쉽게 활용하도록 도와주기 위해(C# 9 record/init).

요약. 
- 불변 = 생성 뒤 수정 불가. C# 9의 init, record, 불변 컬렉션으로 간단하게 구현.
- 동시성과 안정성 면에서 실무 이점이 크며, 값 의미 모델링에 적합.

### 추가 자료

- [C# 9 record 소개](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record)
- [init 전용 설정자](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/init)
- [불변 컬렉션 가이드](https://learn.microsoft.com/dotnet/standard/collections/immutable)
- [with 식](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/with-expression)