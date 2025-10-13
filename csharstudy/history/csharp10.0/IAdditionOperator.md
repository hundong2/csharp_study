# IAdditionOperators의 self 제약은 왜 필요한가?

TSelf이 “자신을 구현한 타입 자신”임을 컴파일러에 보장하기 위한 자기참조 제약(CRTP)입니다. 이를 통해 제네릭에서 static abstract 연산자(+)를 컴파일 타임에 안전하게 호출할 수 있고, 잘못된 조합을 방지합니다.

## 예시 파일

[IAdditionOperators 원본 코드 (dotnet/runtime)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Runtime/src/System/Numerics/IAdditionOperators.cs)

## 답변

핵심 포인트
- 자기참조 제약(Curiously Recurring Template Pattern, CRTP)
  - 선언: public interface IAdditionOperators<TSelf, TOther, TResult> where TSelf : IAdditionOperators<TSelf, TOther, TResult>?
  - 의미: “TSelf 자체가 IAdditionOperators<TSelf, TOther, TResult>를 구현한 타입이어야 한다”
  - 목적:
    - 정적 멤버 제약 보장: static abstract 연산자(+)가 TSelf에 존재함을 보장해 제네릭 코드에서 a + b를 컴파일 타임에 확인 가능.
    - 일관성(Coherence): 임의의 타입으로 TSelf를 채워 넣는 오용을 방지. “TSelf는 항상 자기 자신”이라는 계약을 강제.
- 왜 꼭 필요하나?
  - .NET 7의 Generic Math는 “인터페이스의 static abstract 멤버”를 이용합니다. 제네릭에서 T에 대해 +를 호출하려면 T가 해당 인터페이스를 “TSelf=자기 자신”으로 구현했음이 보장되어야 합니다.
  - 인터페이스 내부의 기본 구현(예: checked 연산자 기본 구현)이 left + right를 호출하려면, TSelf가 동일한 인터페이스 체인에 있음을 컴파일러가 알아야 합니다.
- 물음표(?)의 의미
  - null 허용 주석(Nullable Reference Types) 표기입니다. TSelf가 값 형식(대부분 수치형은 struct)일 수도, 참조 형식일 수도 있으므로 경고를 피하고 양쪽을 수용하도록 설계된 흔적입니다. 런타임 동작을 바꾸진 않습니다.

사용 예 1) 제네릭 합계 함수에서 + 보장
````csharp
using System;
using System.Numerics;

public static class GenericSum
{
    public static T Sum<T>(ReadOnlySpan<T> values)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var acc = T.AdditiveIdentity; // 0
        foreach (var v in values)
            acc = acc + v;            // static abstract +를 컴파일 타임에 호출
        return acc;
    }

    public static void Demo()
    {
        Console.WriteLine(Sum<int>(stackalloc[] { 1, 2, 3 }));     // 6
        Console.WriteLine(Sum<double>(stackalloc[] { 1.5, 2.5 })); // 4
    }
}
````

사용 예 2) 사용자 정의 수형이 “자기 자신”으로 구현
````csharp
using System;
using System.Globalization;
using System.Numerics;

public readonly struct Meter :
    IAdditionOperators<Meter, Meter, Meter>,
    IAdditiveIdentity<Meter, Meter>,
    IParsable<Meter>, ISpanParsable<Meter>
{
    public double Value { get; }
    public Meter(double v) => Value = v;

    public static Meter operator +(Meter a, Meter b) => new(a.Value + b.Value);
    public static Meter AdditiveIdentity => new(0);

    public static Meter Parse(string s, IFormatProvider? p) => new(double.Parse(s, p));
    public static bool TryParse(string? s, IFormatProvider? p, out Meter r)
    { var ok = double.TryParse(s, NumberStyles.Float, p, out var v); r = ok ? new(v) : default; return ok; }

    public static Meter Parse(ReadOnlySpan<char> s, IFormatProvider? p) => new(double.Parse(s, p));
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out Meter r)
    { var ok = double.TryParse(s, NumberStyles.Float, p, out var v); r = ok ? new(v) : default; return ok; }
}
````

만약 이 제약이 없다면?
- 누군가 IAdditionOperators<X, TOther, TResult>를 “TSelf ≠ X”로 이상하게 닫아 구현하는 일이 가능해져, 제네릭에서 +를 안전하게 호출할 수 없고, 인터페이스 기본 구현도 성립하지 않습니다. CRTP 제약이 이를 원천 차단합니다.

정리
- where TSelf : IAdditionOperators<TSelf, TOther, TResult>?는 “TSelf가 자기 자신으로 이 인터페이스를 구현한다”는 계약을 강제하는 장치입니다.
- 이를 통해 제네릭에서 연산자/Parse 같은 정적 멤버를 타입 안정적으로 호출하고, JIT 인라이닝 등 최적화가 가능해집니다.

### 추가 자료

- [Generic math 소개(.NET 블로그)](https://devblogs.microsoft.com/dotnet/introducing-generic-math/)
- [Static abstract 인터페이스 멤버](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-11.0/static-abstracts-in-interfaces)
- [IAdditionOperators 문서](https://learn.microsoft.com/dotnet/api/system.numerics.iadditionoperators-3)
- [IParsable/ISpanParsable 문서](https://learn.microsoft.com/dotnet/api/system.iparsable-1), [ISpanParsable](https://learn.microsoft.com/dotnet/api/system.ispanparsable-1)