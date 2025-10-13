# C#의 in 변경자란?
메서드 매개변수를 “읽기 전용 참조(ref readonly)”로 전달하는 키워드입니다. 값 형식(특히 큰 struct)의 복사를 피하면서도 호출 측 값을 변경하지 못하도록 보장합니다. 또한 제네릭 형식 매개변수의 반공변성(contravariance)을 표시하는 in도 있습니다.

## 예시 파일
[in parameter modifier (공식 문서, 코드 포함)](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/in-parameter-modifier)

## 답변
핵심 정리
- in 매개변수(modifier)
  - 의미: ref처럼 참조로 전달하되, 메서드 내부에서 수정은 금지(ref readonly).
  - 장점: 큰 struct 복사 비용 절감 + 불변성 보장.
  - 호출: 대부분 M(arg)로 호출(호출 시 in 생략 가능). 오버로드 구분이 필요하면 M(in arg)로 명시.
  - 제약: 메서드 내에서 파라미터 또는 그 필드에 할당 불가. readonly가 아닌 인스턴스 메서드 호출 시 “방어적 복사”가 발생할 수 있음(성능 주의).
- in 형식 매개변수(제네릭 반공변성)
  - 의미: interface IComparer<in T>처럼 T를 “입력 위치”에서만 사용하겠다는 약속. Dog는 Animal의 하위형이지만 IComparer<Animal>은 IComparer<Dog>의 상위형이 됨(반공변).
- 주의: foreach (var x in collection)의 in은 “키워드”일 뿐 변경자가 아닙니다.

예제 코드(읽기 전용 참조와 반공변성)
````csharp
using System;

readonly struct BigStruct // 필드 많을수록 복사 비용↑
{
    public readonly double A, B, C, D, E, F, G, H;
    public BigStruct(double v) => (A,B,C,D,E,F,G,H) = (v,v,v,v,v,v,v,v);

    // readonly가 아닌 인스턴스 메서드를 in 인자로 호출하면 방어적 복사 발생 가능
    public double Sum() => A + B + C + D + E + F + G + H;

    public readonly double FastSum() => A + B + C + D + E + F + G + H; // readonly로 방어적 복사 방지
}

static class InModifierDemo
{
    // 1) 값 전달: 전체 복사 발생
    static double SumByValue(BigStruct s) => s.Sum();

    // 2) in 전달: 참조 전달 + 읽기 전용. 구조체 복사 없음(대신 읽기 전용 보장)
    static double SumByIn(in BigStruct s) => s.FastSum(); // readonly 멤버 호출이 이상적

    // 3) 잘못된 시도들(컴파일 오류 예)
    // static void Mutate(in BigStruct s) => s = default;              // 파라미터 재할당 불가
    // static void MutateField(in MutableStruct s) => s.X = 10;        // 필드 수정 불가

    public static void Run()
    {
        var bs = new BigStruct(1);

        // 호출 시 in 생략 가능
        Console.WriteLine(SumByIn(bs));     // OK: 읽기 전용 참조 전달
        Console.WriteLine(SumByIn(in bs));  // 명시 호출도 가능

        Console.WriteLine(SumByValue(bs));  // 값 복사 발생
    }
}

// 4) 반공변성(in T): “입력 전용” 제네릭 매개변수
interface IConsumer<in T> { void Consume(T item); }
class Animal { }
class Dog : Animal { }
class AnyAnimalConsumer : IConsumer<Animal> { public void Consume(Animal a) { } }

static class VarianceDemo
{
    public static void Run()
    {
        IConsumer<Animal> ca = new AnyAnimalConsumer();
        IConsumer<Dog> cd = ca; // 반공변: Animal 소비자는 Dog 소비자 자리에 배치 가능
        cd.Consume(new Dog());
    }
}
````

실무 팁
- 큰 struct 인자: in 사용 + 해당 struct의 멤버를 readonly로 표시해 방어적 복사 방지.
- ReadOnlySpan<T>/Span<T>: 이미 ref-like 타입이라 in이 필요하지 않습니다.
- API 설계: 변경 금지 의도를 명확히 하고 복사 비용을 줄일 때 in를 고려. 작은 struct나 참조형에는 이점이 적습니다.

### 추가 자료
- [in 매개변수(키워드)](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/in-parameter-modifier)
- [readonly 인스턴스 멤버(방어적 복사 방지)](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-8.0/readonly-instance-members)
- [제네릭의 공변/반공변(out/in)](https://learn.microsoft.com/dotnet/standard/generics/covariance-and-contravariance)
- [매개변수 전달 방식(ref, in, out)](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/method-parameters)