// 실행: dotnet script 00_BeginnerSyntax.csx
// 목적: 뒤의 모든 실습에서 반복되는 C# 문법을 가장 작은 예제로 익힌다.

// 01. using은 다른 네임스페이스의 형식 이름을 짧게 쓸 수 있게 컴파일러에 알려 준다.
using System;
// 02. List<T>와 같은 일반 컬렉션 형식이 들어 있는 네임스페이스를 가져온다.
using System.Collections.Generic;
// 03. Where와 Select 같은 LINQ 확장 메서드를 사용할 수 있게 한다.
using System.Linq;
// 04. Task와 비동기 대기 형식을 가져온다.
using System.Threading.Tasks;

// 05. int는 32비트 부호 있는 정수이고, scoreA라는 지역 변수에 80을 대입한다.
int scoreA = 80;
// 06. 같은 형식의 두 번째 값을 만든다. 세미콜론은 문장의 끝이다.
int scoreB = 90;
// 07. double은 실수 형식이다. 2.0을 써서 정수 나눗셈이 되지 않게 한다.
double average = (scoreA + scoreB) / 2.0;
// 08. $ 문자열은 {식}을 평가해 문자열 안에 넣고, :F1은 소수점 한 자리 형식이다.
Console.WriteLine($"평균 = {average:F1}");

// 09. 배열은 길이가 고정된 같은 형식 값의 연속 컬렉션이다.
string[] states = { "working", "completed", "failed" };
// 10. s => ... 는 문자열 하나를 받아 bool을 돌려주는 람다 함수다.
// 11. is ... or ... 패턴은 둘 중 하나와 일치할 때 참이다.
// 12. Where는 조건을 만족하는 값만 지연 열거하고 ToList가 지금 결과를 만든다.
List<string> terminalStates = states.Where(s => s is "completed" or "failed").ToList();
// 13. string.Join은 목록 원소 사이에 쉼표를 넣어 하나의 문자열을 만든다.
Console.WriteLine($"terminal = {string.Join(',', terminalStates)}");

// 14. 반복 횟수를 기억할 정수 변수다.
int retryCount = 0;
// 15. 조건이 참인 동안 블록을 반복한다.
while (retryCount < 3)
{
    // 16. ++는 현재 값에 1을 더해 같은 변수에 다시 저장한다.
    retryCount++;
    // 17. 메서드 호출은 인수를 평가하고 새 스택 프레임을 만들 수 있지만 JIT가 인라인할 수도 있다.
    Console.WriteLine($"retry {retryCount}");
}

// 18. static 지역 함수는 바깥 지역 변수를 캡처하지 않아 숨은 클로저 객체가 필요 없다.
static string Describe(string state)
{
    // 19. switch 식은 첫 번째로 일치하는 패턴 오른쪽 값을 결과로 돌려준다.
    return state switch
    {
        // 20. 문자열 상수 패턴이 일치하면 한국어 설명을 선택한다.
        "working" => "작업 중",
        // 21. or 패턴은 두 종료 상태를 한 분기로 묶는다.
        "completed" or "failed" => "종료됨",
        // 22. 밑줄은 위 패턴 어디에도 맞지 않는 모든 값이다.
        _ => "알 수 없음"
    };
}

// 23. foreach는 배열 열거자의 각 값을 state 변수에 차례대로 넣는다.
foreach (string state in states)
{
    // 24. 사용자 함수의 반환값을 문자열 보간으로 출력한다.
    Console.WriteLine($"{state} -> {Describe(state)}");
}

// 25. Task.Delay는 스레드를 막지 않는 타이머 Task를 만들고 await는 완료 뒤 이어서 실행한다.
await Task.Delay(10);
// 26. 이 줄은 상태 머신의 continuation에서 실행될 수 있다.
Console.WriteLine("기초 실습 완료");

// CLR 관찰 메모
// - 문자열과 List 객체는 관리 힙에 놓이고 GC가 수명을 추적한다.
// - 지역 정수는 IL 지역 슬롯으로 표현되지만 JIT 뒤에는 레지스터에만 있을 수 있다.
// - 람다는 캡처가 없으므로 런타임이 대리자 인스턴스를 재사용할 수 있다.
// - await가 있는 스크립트 본문은 컴파일러가 상태 머신으로 변환한다.
