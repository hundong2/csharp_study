# C#에서 접두사 @와 $의 의미
C# 문자열/식별자 앞의 @, $는 각각 “그대로(Verbatim) 해석”과 “보간(Interpolated) 문자열”을 뜻합니다. 둘을 함께 써서 $@"..."처럼도 사용할 수 있으며, @는 식별자 앞에 붙여 키워드를 변수명으로 쓰는 용도도 있습니다.

## 예시 파일
- [Interpolated strings (dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/language-reference/tokens/interpolated/Program.cs)
- [Verbatim strings (dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/language-reference/tokens/verbatim/Program.cs)

## 답변
요약
- $: 보간 문자열. "..." 안에서 {표현식}을 계산해 문자열에 삽입합니다. 형식 지정과 정렬도 가능({x:N2}, {name,-10}).
- @: Verbatim(있는 그대로) 문자열. 이스케이프 시퀀스(\n, \t 등)를 해석하지 않고, 줄바꿈과 백슬래시를 그대로 보존합니다. 내부 큰따옴표는 ""로 이스케이프.
- 결합: $@"..." 또는 @$"..."로 “보간 + 그대로”를 함께 사용. 윈도 경로 + 변수 삽입 같은 경우 유용.
- @ 식별자: @class 처럼 키워드도 변수/필드명으로 허용하게 하는 접두사(컴파일러에 “이건 식별자”라고 알림).

자주 쓰는 패턴과 주의점
- 경로/정규식/여러 줄 텍스트: @"C:\Windows\System32" 처럼 @ 문자열이 깔끔.
- 변수 삽입 + 경로: $@"C:\logs\{DateTime.Today:yyyyMMdd}.log"
- 중괄호 이스케이프(보간): "{{" 또는 "}}"로 리터럴 중괄호 출력.
- 큰따옴표 이스케이프(Verbatim): @ 문자열에서는 "" 사용. 일반 문자열에서는 \".
- 문화권 제어: 보간은 기본적으로 현재 문화권. 불변 문화가 필요하면 FormattableString.Invariant($"...") 사용.

예제 코드
````csharp
using System;
using System.Globalization;

public static class SymbolPrefixesDemo
{
    public static void Main()
    {
        // 1) $ 보간 문자열
        int n = 12345;
        Console.WriteLine($"n={n}, n:N2={n:N2}");            // n=12345, n:N2=12,345.00 (문화권 의존)

        // 2) @ Verbatim 문자열(이스케이프 미해석, 줄바꿈/백슬래시 보존)
        string path = @"C:\Windows\System32\drivers\etc\hosts";
        Console.WriteLine(path);

        // 3) 결합: 보간 + Verbatim
        string log = $@"C:\logs\app-{DateTime.Today:yyyyMMdd}.log";
        Console.WriteLine(log);

        // 4) 보간에서 중괄호 출력
        Console.WriteLine($"Print braces: {{ and }}");

        // 5) Verbatim에서 큰따옴표 출력
        string quote = @"She said, ""Hello""";
        Console.WriteLine(quote);

        // 6) 보간의 문화권 고정(예: Invariant)
        double pi = Math.PI;
        string inv = FormattableString.Invariant($"pi={pi:F3}");
        Console.WriteLine(inv);                              // pi=3.142 (항상 . 사용)

        // 7) @ 식별자: 키워드를 변수명으로
        int @class = 10; // 'class'는 키워드지만 @를 붙이면 식별자로 사용 가능
        Console.WriteLine(@class);
    }
}
````

추가로 알아두면 좋은 점
- C# 11의 “Raw string literal”("""...""")은 많은 따옴표/백슬래시 이스케이프를 더 줄여줍니다. 필요 시 $ 또는 @$와도 결합 가능합니다.
- 보간 문자열은 컴파일 시 형식화 코드로 변환됩니다. 고빈도 경로에서는 StringBuilder/Span 기반 API 또는 InterpolatedStringHandler가 최적화에 유리할 수 있습니다.

### 추가 자료
- [문자열 보간 ($) 문법](https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated)
- [Verbatim 문자열 (@) 문법](https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim)
- [FormattableString.Invariant](https://learn.microsoft.com/dotnet/api/system.formattablestring.invariant)
- [Raw string literals (C# 11)](https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string)