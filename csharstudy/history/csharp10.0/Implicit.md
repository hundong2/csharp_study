# C#에서 implicit란?

주로 “암시적 형변환 연산자”를 선언하는 키워드이며, 캐스트 없이 자연스럽게 타입을 변환하도록 합니다. 더 넓게는 “암시적으로 일어나는 일들(숫자형 암시적 변환, var, implicit using 등)”을 의미하기도 합니다.

## 예시 파일

[BigInteger.cs (실제 implicit 변환 구현 예)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Runtime.Numerics/src/System/Numerics/BigInteger.cs)

## 답변

핵심 개념
- implicit 키워드(사용자 정의 암시적 변환)
  - 선언: static implicit operator Target(Source value)
  - 사용: 캐스트 없이 Target t = source; 처럼 자동 변환.
  - 규칙: 정적 메서드, 선언 타입이 Source 또는 Target 중 하나여야 함.
  - 권장: “안전하고 손실/예외가 거의 없는 변환”에만 사용. 손실 가능/비용 큰 변환은 explicit 권장.
- explicit와의 차이
  - explicit은 강제 캐스트 필요(Target t = (Target)source;). 위험/손실 가능 시 사용.
- ‘암시적’ 개념들(키워드와는 별개)
  - 숫자형 암시적 변환: int → long, float → double 등은 캐스트 없이 OK.
  - 암시적 지역 변수(var): 컴파일러가 타입 추론. var 자체가 ‘implicit’ 키워드가 아님.
  - 암시적 인터페이스 구현: 시그니처 일치로 자동 구현(명시적 구현과 대비).
  - C# 10 implicit using: 공통 using을 자동 주입.

예제: 안전한 implicit과 위험 시 explicit
````csharp
using System;
using System.Globalization;

public readonly struct Meter
{
    public double Value { get; }
    public Meter(double value) => Value = value;

    // 안전한 방향(손실 거의 없음): Meter -> double (암시적)
    public static implicit operator double(Meter m) => m.Value;

    // 다소 위험(의미 상 애매하거나 스케일 손실 가능): double -> Meter (명시적 권장)
    public static explicit operator Meter(double v) => new Meter(v);

    public override string ToString() => $"{Value.ToString(CultureInfo.InvariantCulture)} m";
}

public static class ImplicitExamples
{
    public static void Run()
    {
        var m = new Meter(1.5);

        // 암시적: Meter -> double
        double d = m;                // OK (캐스트 없이)
        Console.WriteLine(d);        // 1.5

        // 명시적: double -> Meter
        var m2 = (Meter)2.0;         // 캐스트 필요
        Console.WriteLine(m2);       // 2 m
    }
}
````

실무 팁
- implicit는 놀람 최소화 원칙: 손실/예외/큰 비용이 없고 직관적일 때만.
- 순환/모호성 주의: A→B, B→A를 모두 implicit로 두면 오버로드 해석이 꼬일 수 있음.
- 컬렉션/복잡 객체는 explicit 권장: 깊은 복사·리소스 소유 전환 등은 암시적이면 위험.
- 숫자 변환은 내장 규칙 재사용: 내장 암시적 변환 표를 따르고, 사용자 정의에서 일관성 유지.

자주 묻는 질문
- 왜 정적이어야 하나? 인스턴스 상태에 의존하지 않는 “형 변환 규칙”이기 때문.
- 예외를 던져도 되나? 가능하지만 권장되지 않음(implicit는 안전해야 함). 예외 가능성 있으면 explicit로.
- var와 implicit 차이? var는 “암시적 타입 추론” 키워드(형 변환 아님). implicit는 “형 변환 연산자” 선언 키워드.

### 추가 자료

- [User-defined conversions(implicit/explicit)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/user-defined-conversions)
- [Numeric conversions(암시적 숫자 변환 표)](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/numeric-conversions)
- [var(암시적 지역 변수)](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/var)
- [Implicit using directives(C# 10)](https://learn.microsoft.com/dotnet/core/project-sdk/overview#implicit-using-directives)