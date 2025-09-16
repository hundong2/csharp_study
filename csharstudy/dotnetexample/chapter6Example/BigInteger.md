# C#의 BigInteger란?
64비트 한계를 넘어 임의 정밀도 정수를 표현/연산할 수 있는 타입(System.Numerics.BigInteger)입니다. 값이 매우 커져도 오버플로 없이 정확한 정수 계산이 가능합니다.

## 예시 파일
[BigInteger 샘플 Program.cs (dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/VS_Snippets_CLR_System/system.numerics.biginteger/cs/program.cs)

## 답변
핵심 개념
- 위치: System.Numerics 네임스페이스. .NET 6+는 기본 포함(옛 .NET Framework에선 System.Numerics.dll 참조 필요).
- 임의 정밀도: 메모리가 허용하는 한 자릿수 제한 없음. 불변(immutable) 구조체.
- 기본 연산: +, -, *, /, %, 비교, 비트 연산(&, |, ^, <<, >>), 수학 유틸리티(Pow, ModPow, Abs, GreatestCommonDivisor 등).
- 변환: int/long 등에서 암시적/명시적 변환 지원. 반대로 축소 변환 시 오버플로 주의(checked 사용).
- 리터럴 주의: 매우 큰 정수 리터럴을 직접 대입할 수 없습니다. BigInteger.Parse 또는 바이트 배열 생성 사용.

자주 쓰는 패턴과 예제
````csharp
using System;
using System.Numerics;

class Demo
{
    static void Main()
    {
        // 1) 생성
        var a = BigInteger.Parse("123456789012345678901234567890");
        var b = BigInteger.Parse("987654321098765432109876543210");

        // 리터럴 직접 대입(X) → Parse 또는 FromBytes 사용
        // var c = 123456789012345678901234567890; // 컴파일 오류
        var c = BigInteger.Pow(10, 50) + 12345; // 수학 연산으로도 생성 가능

        // 2) 기본 연산
        BigInteger sum = a + b;
        BigInteger diff = b - a;
        BigInteger prod = a * 12345;
        BigInteger quot = b / 10;
        BigInteger rem  = b % 10;

        // 3) 수학 유틸리티
        BigInteger pow = BigInteger.Pow(2, 200); // 2^200
        BigInteger modPow = BigInteger.ModPow(7, 560, 561); // 모듈러 거듭제곱(암호/수론)
        BigInteger gcd = BigInteger.GreatestCommonDivisor(a, b);

        // 4) 비트 연산/쉬프트
        BigInteger bits = (BigInteger)1 << 200;   // 2^200
        BigInteger masked = bits & ((1 << 8) - 1); // 하위 8비트 마스크

        // 5) 타입 변환(축소 변환은 checked 권장)
        long maybeLong;
        if (a >= long.MinValue && a <= long.MaxValue)
        {
            maybeLong = (long)a;
        }

        // 6) 바이트 배열 ↔ BigInteger
        byte[] bytes = a.ToByteArray();              // little-endian, 부호 포함
        BigInteger fromBytes = new BigInteger(bytes);

        Console.WriteLine($"sum={sum}");
        Console.WriteLine($"pow={pow}");
        Console.WriteLine($"gcd={gcd}");
    }
}
````

성능과 주의사항
- 불변: 모든 연산은 새 인스턴스를 생성합니다. 대규모 반복 연산은 할당 비용이 큽니다(필요 시 알고리즘/캐시 최적화).
- 문자열 파싱 비용: Parse 반복 호출을 피하고, 필요 시 중간 표현(바이트 배열, 누적 계산 등)을 활용.
- 서식화: ToString("X") 같은 표준/사용자 지정 서식으로 16진수 등 출력 가능.

사용 사례
- 암호/수론(모듈러 연산, 큰 소수 관련 계산), 과학 계산, 금융/정밀 계산(정수 영역), 임의 정밀 카운팅/조합론.

### 추가 자료
- [BigInteger API 문서](https://learn.microsoft.com/dotnet/api/system.numerics.biginteger)
- [수학 메서드(ModPow/GCD 등)](https://learn.microsoft.com/dotnet/api/system.numerics.biginteger#methods)
- [.NET Numerics 개요](https://learn.microsoft.com/dotnet/standard/numerics)