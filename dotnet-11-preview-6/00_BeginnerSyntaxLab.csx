/*
실행: dotnet script 00_BeginnerSyntaxLab.csx
선행 문서: 00-csharp-primer.md
목표: 변수, 연산자, 조건문, 반복문, 배열, 메서드, 객체, null, 예외를 한 번에 읽습니다.
*/

#nullable enable

// 01. System namespace 안의 Console, Math, FormatException을 짧은 이름으로 사용합니다.
using System;
// 02. List<T>를 사용하기 위해 generic collection namespace를 엽니다.
using System.Collections.Generic;

// 03. class는 데이터와 메서드를 묶는 reference type 설계도입니다.
public sealed class Product
{
    // 04. constructor는 `new Product(...)`에서 실행됩니다.
    public Product(string name, int price)
    {
        // 05. 오른쪽 parameter 값을 현재 객체의 Name property에 저장합니다.
        Name = name;
        // 06. Price에도 전달받은 값을 저장합니다.
        Price = price;
    }

    // 07. get-only property는 생성자 이후 외부 코드가 바꿀 수 없습니다.
    public string Name { get; }
    // 08. C#의 `_`는 숫자 구분자로 사용할 수 있고 값에는 영향을 주지 않습니다.
    public int Price { get; }

    // 09. method는 이름이 붙은 동작이며 count를 입력받아 정수를 반환합니다.
    public int TotalPrice(int count)
    {
        // 10. `*`가 가격과 수량을 곱하고 return이 호출자에게 결과를 보냅니다.
        return Price * count;
    }
}

// 11. int 변수를 선언하고 3을 대입합니다.
int count = 3;
// 12. var를 사용해도 compiler는 오른쪽 문자열에서 형식을 string으로 확정합니다.
var learnerName = "초보 학습자";
// 13. `new`가 Product 객체를 만들고 생성자를 호출합니다.
var book = new Product("C# 입문서", 20_000);
// 14. instance method를 호출하고 반환값을 total에 저장합니다.
int total = book.TotalPrice(count);
// 15. 보간 문자열의 `{...}` 안에서 변수 값을 문자열에 넣습니다.
Console.WriteLine($"{learnerName}: {book.Name} {count}권 = {total:N0}원");

// 16. 비교 연산의 결과는 bool입니다.
bool receivesDiscount = count >= 3;
// 17. if는 조건이 true일 때 첫 block을 실행합니다.
if (receivesDiscount)
{
    // 18. `*=`는 오른쪽 값을 곱한 결과를 같은 변수에 다시 대입합니다.
    total = (int)(total * 0.9);
    // 19. double 계산 결과를 int로 명시 변환하면 소수 부분이 사라집니다.
    Console.WriteLine($"할인 적용: {total:N0}원");
}
else
{
    // 20. 조건이 false일 때만 else block을 실행합니다.
    Console.WriteLine("할인 없음");
}

// 21. 배열은 같은 형식의 고정 개수 원소를 0-based index로 보관합니다.
int[] scores = { 70, 85, 100 };
// 22. sum accumulator를 0으로 시작합니다.
int sum = 0;
// 23. foreach가 배열의 각 int 원소를 차례로 number 변수에 넣습니다.
foreach (int score in scores)
{
    // 24. `+=`는 기존 sum에 score를 더해 다시 sum에 저장합니다.
    sum += score;
}
// 25. int끼리 나누면 정수 나눗셈이므로 double로 먼저 변환합니다.
double average = (double)sum / scores.Length;
// 26. `F1` format은 소수점 한 자리까지 출력합니다.
Console.WriteLine($"평균 = {average:F1}");

// 27. List<string>은 크기를 변경할 수 있는 generic collection입니다.
var steps = new List<string> { "문서 읽기", "코드 예상", "실행", "수정" };
// 28. for의 i는 0에서 시작해 Count보다 작은 동안 1씩 증가합니다.
for (int i = 0; i < steps.Count; i++)
{
    // 29. 사람에게는 1번부터 보이도록 i+1을 출력하되 index 접근은 i를 씁니다.
    Console.WriteLine($"{i + 1}. {steps[i]}");
}

// 30. nullable reference `string?`에는 문자열 또는 null이 들어갈 수 있습니다.
string? nickname = null;
// 31. `?.`는 null이면 Length 접근을 건너뛰고, `??`는 대신 0을 사용합니다.
int nicknameLength = nickname?.Length ?? 0;
// 32. 결과가 0임을 확인합니다.
Console.WriteLine($"별명 길이 = {nicknameLength}");

// 33. TryParse는 잘못된 입력도 예외 없이 bool로 알려 주는 권장 parsing API입니다.
bool parsed = int.TryParse("42", out int answer);
// 34. `&&`는 양쪽 조건이 모두 true일 때만 true입니다.
if (parsed && answer > 0)
{
    // 35. out parameter로 받은 answer를 사용합니다.
    Console.WriteLine($"변환 성공 = {answer}");
}

// 36. try block에는 실패 가능 코드가 들어갑니다.
try
{
    // 37. Parse는 숫자가 아닌 문자열에서 FormatException을 던집니다.
    int.Parse("숫자 아님");
}
// 38. catch는 지정한 예외 형식을 받아 복구 또는 보고합니다.
catch (FormatException exception)
{
    // 39. 예외의 형식 이름을 출력하고 프로그램은 다음 줄로 계속됩니다.
    Console.WriteLine($"예상한 변환 실패: {exception.GetType().Name}");
}

// 40. 모든 실습 문장을 정상적으로 끝까지 실행했음을 표시합니다.
Console.WriteLine("기초 문법 실습 완료");
