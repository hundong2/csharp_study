// 실행: dotnet script 00_BeginnerSyntax.csx
// 목적: Issue 27에서 반복되는 record, nullable, list, LINQ, pattern, async 문법을 익힌다.

// 01. nullable 참조 형식 분석을 켜서 string?와 string의 계약 차이를 검사한다.
#nullable enable
// 02. Console, StringComparer 같은 기본 형식을 가져온다.
using System;
// 03. List<T> 일반 컬렉션을 가져온다.
using System.Collections.Generic;
// 04. Where, Select, Any 같은 LINQ 확장 메서드를 가져온다.
using System.Linq;
// 05. Task와 await에 필요한 형식을 가져온다.
using System.Threading.Tasks;

// 06. positional record의 세 매개변수는 같은 이름의 init 전용 속성이 된다.
record Skill(string Name, string Category, bool Enabled);

// 07. List<Skill>은 Skill 참조를 순서대로 보관하고 크기가 변하는 collection이다.
List<Skill> skills = new()
{
    // 08. target-typed new로 첫 record 객체를 만든다.
    new("mcp", "protocol", true),
    // 09. 두 번째 활성 skill을 만든다.
    new("testing", "quality", true),
    // 10. 비활성 skill은 뒤의 filter에서 제외된다.
    new("legacy", "protocol", false)
};

// 11. Where는 조건을 만족한 항목만 지연 열거하고 Select는 Name만 투영한다.
// 12. ToList가 지금 열거해 새 List<string>을 할당한다.
List<string> visible = skills.Where(skill => skill.Enabled).Select(skill => skill.Name).ToList();
// 13. Join이 이름 사이에 쉼표를 넣어 하나의 string을 만든다.
Console.WriteLine($"visible = {string.Join(',', visible)}");

// 14. 길이가 고정된 string 배열을 만든다.
string[] states = { "working", "completed", "failed" };
// 15. `is ... or ...` pattern은 두 terminal 상태 중 하나면 true다.
string[] terminal = states.Where(state => state is "completed" or "failed").ToArray();
// 16. terminal 결과를 출력한다.
Console.WriteLine($"terminal = {string.Join(',', terminal)}");

// 17. static local function은 바깥 지역 변수를 capture하지 않는다.
static string Explain(string? value)
{
    // 18. switch expression은 첫 matching arm의 값을 반환한다.
    return value switch
    {
        // 19. null pattern은 값이 없을 때 선택된다.
        null => "값 없음",
        // 20. property pattern은 Length가 0인 빈 문자열을 찾는다.
        { Length: 0 } => "빈 문자열",
        // 21. 밑줄 discard는 나머지 모든 문자열을 받는다.
        _ => $"값={value}"
    };
}

// 22. null 인수로 함수를 호출해 nullable 분기를 확인한다.
Console.WriteLine(Explain(null));
// 23. Task.Delay는 thread를 blocking하지 않는 timer Task를 만든다.
await Task.Delay(10);
// 24. 이 줄은 compiler가 만든 async state machine continuation에서 실행될 수 있다.
Console.WriteLine("기초 완료");

// CLR/JIT 관찰 메모
// - record와 List/배열은 managed heap 객체이며 GC가 도달 가능성을 추적한다.
// - LINQ lambda가 바깥 변수를 capture하면 compiler가 closure 객체를 만들 수 있다.
// - local 값은 IL local로 표현되지만 JIT 뒤 register에만 있을 수도 있다.
// - await가 있는 script 본문은 상태와 다음 위치를 가진 state machine으로 변환된다.
