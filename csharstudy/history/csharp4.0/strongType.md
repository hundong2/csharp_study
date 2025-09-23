# 강한 형식(strong typing)과 인터페이스/상속의 메모리 동작
C#은 “강한 형식·정적 타입 언어”입니다. 컴파일 시 타입이 엄격히 검사되고, 런타임에도 잘못된 형식 변환은 예외를 일으킵니다. 인터페이스와 상속은 이 강한 형식 위에서 다형성을 제공하며, 메모리에서는 “참조(주소)”와 “객체(힙)”가 분리되어 동작합니다.

## 예시 파일
[Reference vs Value Types 샘플 코드](https://github.com/dotnet/samples/blob/main/snippets/csharp/fundamentals/types/ReferenceAndValueTypes.cs)

## 답변
요점 정리
- 강한 형식(strong typing): 형식 규칙을 엄격히 적용. 잘못된 연산/변환은 컴파일 오류 또는 런타임 예외.
- 정적 타이핑(static typing): 변수의 타입이 컴파일 시점에 결정. C#은 정적·강한 형식 언어.
- 인터페이스/상속과의 연관: 다형성 사용은 “타입 호환성” 규칙을 따른다. 인터페이스/기반형으로 참조하는 것은 “참조의 형식”이 바뀌는 것이지, “객체 메모리 레이아웃”이 바뀌는 게 아니다.

1) 강한 형식과 정적 타이핑
- 잘못된 연산/대입은 컴파일 오류 또는 명시적 캐스팅 필요.
- dynamic만 예외적으로 “컴파일 시 확인을 미루고” 런타임에 바인딩(오류 시 런타임 예외).

````csharp
// 강한 형식: 명시적 변환 필요
object o = 123;
// string s = o;           // 컴파일 오류
string s = o.ToString();   // OK
// 또는
// string s = (string)o;   // 런타임 InvalidCastException (o가 실제 string이 아니므로)

// dynamic은 런타임에 검사
dynamic d = "hi";
int len = d.Length;        // OK (string.Length)
int bad = d + 1;           // 런타임에 결합, 상황 따라 예외
````

2) 참조와 객체(스택 vs 힙), 상속의 메모리 레이아웃
- 참조형 변수에는 “힙 객체의 주소(참조)”가 들어간다. 변수 자체는 스택/필드/배열 슬롯 등 “어디에 있든” 참조만 든다.
- 상속 시 파생 객체의 메모리에는 “기반 클래스 필드 → 파생 클래스 필드” 순서로 배치된다(논리적 관점). 참조가 가리키는 대상은 한 덩어리 객체.
- 가상(virtual) 호출은 타입의 메서드 테이블(방법표)을 통해 실제 재정의된 메서드로 분기된다.

````csharp
class Animal { public string Name = "A"; public virtual string Speak() => "..." ; }
class Dog : Animal { public int Age = 1; public override string Speak() => "Woof"; }

Animal a = new Dog();   // 업캐스트(참조 형식만 바뀜)
Console.WriteLine(a.Speak()); // "Woof" (가상 호출: 실제 타입 Dog 기준)
Console.WriteLine(((Dog)a).Age); // 다운캐스트 후 파생 필드 접근
````

3) 인터페이스 다형성과 디스패치
- 어떤 객체가 인터페이스를 구현하면, 인터페이스 형식으로 참조해도 런타임은 “해당 인터페이스의 구현 메서드”로 연결한다(인터페이스 디스패치).
- 객체 메모리 레이아웃은 동일(“Dog 객체” 그대로). 달라지는 건 “참조의 정적 타입”과 “호출 디스패치 경로”.

````csharp
interface IFly { void Fly(); }
class Duck : IFly { public void Fly() => Console.WriteLine("Duck"); }

IFly f = new Duck(); // 인터페이스 참조
f.Fly();             // Duck.Fly() 실행(인터페이스 디스패치)
````

4) 값 형식과 박싱(특히 인터페이스 사용 시)
- 값 형식(struct)을 object/인터페이스로 취급하면 “박싱” 발생: 힙에 임시 객체를 만들어 값 복사.
- 박싱은 할당/GC 비용이 있으니 주의. 제네릭의 “제약된 호출(constrained call)”로 회피 가능.

````csharp
struct S : IFly { public void Fly() => Console.WriteLine("S"); }

S s = new S();

// 인터페이스로 캐스팅 → 박싱 발생 (S → IFly → 힙에 박스)
IFly boxed = s;
boxed.Fly(); // 박스 안의 메서드 호출

// 제네릭 + 인터페이스 제약으로 박싱 회피(컴파일러가 constrained callvirt 사용)
static void CallFly<T>(T x) where T : IFly => x.Fly();
CallFly(s); // 박싱 없이 직접 호출
````

5) 메모리 관점 요약
- 참조형(class): 객체는 힙에, 변수엔 참조(주소). 상속 시 “기반 → 파생” 필드가 한 객체에 연속 배치(논리적 관점). 가상 메서드는 메서드 테이블을 통해 실제 구현으로 호출.
- 값 형식(struct): 변수에 “값 자체”가 저장. 인터페이스/object로 다룰 때 박싱. 제네릭 제약을 사용하면 박싱 없이 호출 가능.
- 인터페이스: 객체는 그대로, 참조의 정적 타입만 인터페이스로 바뀜. 호출은 인터페이스 디스패치 경로를 통해 실제 구현으로 간다.

6) 실무 팁
- 설계: 불필요한 박싱을 피하려면 자주 호출되는 핫패스에 제네릭+제약을 고려.
- 성능: sealed 클래스/메서드는 JIT 최적화(가상 호출 제거)에 유리.
- 안전성: 다운캐스트는 as/패턴 매칭으로 안전하게 처리.

작은 종합 예제: 인터페이스/상속/박싱 비교
````csharp
using System;

interface INoise { string Speak(); }

class Animal : INoise
{
    public virtual string Speak() => "...";
}

sealed class Dog : Animal // sealed: 더 상속 불가 → 가상 호출 최적화에 유리
{
    public override string Speak() => "Woof";
}

struct Cat : INoise
{
    public string Speak() => "Meow";
}

class Demo
{
    static void Main()
    {
        // 참조형 + 상속 + 가상 호출
        Animal a = new Dog();
        Console.WriteLine(a.Speak());    // Woof

        // 인터페이스 디스패치(객체는 여전히 Dog)
        INoise n = a;
        Console.WriteLine(n.Speak());    // Woof

        // 값 형식 + 인터페이스: 박싱 발생
        Cat c = new Cat();
        INoise boxed = c;                // boxing
        Console.WriteLine(boxed.Speak());// Meow

        // 제네릭 제약으로 박싱 회피
        Console.WriteLine(CallSpeak(c)); // Meow (boxing 없음)
    }

    static string CallSpeak<T>(T x) where T : INoise => x.Speak();
}
````

핵심 체크리스트
- C#은 강한 형식·정적 타입: 타입 규칙 엄격, 컴파일 시 검증.
- 다형성은 “참조의 정적 타입”과 “런타임 실제 타입”의 분리로 동작. 메모리의 객체 레이아웃은 동일.
- 값 형식의 인터페이스 사용은 박싱 유발. 제네릭 제약으로 회피 가능.
- sealed/readonly 등 키워드는 성능·의도에 도움이 된다.

### 추가 자료
- [C# 형식 시스템 개요](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/)
- [상속과 다형성](https://learn.microsoft.com/dotnet/csharp/fundamentals/object-oriented/inheritance)
- [인터페이스](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/interfaces)
- [박싱/언박싱](https://learn.microsoft.com/dotnet/csharp/programming-guide/types/boxing-and-unboxing)
- [virtual/override/sealed](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/virtual)