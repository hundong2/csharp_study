// 실행: dotnet script 04_DevProxyEvaluation.csx
// 목적: 실제 API 대신 매번 초기화되는 결정적 store로 agent skill 평가를 재현한다.

// nullable 참조 형식 주석(Todo?)을 활성화해 없는 키 경로를 검사한다.
#nullable enable

// 01. Console과 문자열 비교 형식을 가져온다.
using System;
// 02. Dictionary 기반 가짜 API 상태를 가져온다.
using System.Collections.Generic;
// 03. LINQ로 키를 정렬해 출력 순서를 고정한다.
using System.Linq;

// 04. record class는 API가 돌려주는 항목을 값 중심으로 표현한다.
record class Todo(int Id, string Title, bool Done);

// 05. FakeApi는 테스트마다 새 인스턴스를 만들어 상태를 격리한다.
sealed class FakeApi
{
    // 06. readonly는 필드 참조 재대입을 막지만 Dictionary 내용 변경은 허용한다.
    private readonly Dictionary<int, Todo> _items;

    // 07. 생성자는 항상 같은 seed data로 시작한다.
    public FakeApi()
    {
        // 08. collection initializer로 두 항목을 결정적으로 넣는다.
        _items = new()
        {
            [1] = new(1, "read skill", false),
            [2] = new(2, "run evaluation", false)
        };
    }

    // 09. GetAll은 ID 순으로 정렬해 Dictionary 내부 순서에 의존하지 않는다.
    public IReadOnlyList<Todo> GetAll() => _items.Values.OrderBy(item => item.Id).ToList();

    // 10. GetOne은 없는 키에 예외를 던져 404 계약을 간단히 모형화한다.
    public Todo GetOne(int id) => _items.TryGetValue(id, out Todo? item)
        ? item
        : throw new KeyNotFoundException($"todo {id} not found");

    // 11. Merge는 record with 식으로 완료 값만 바꾼 새 객체를 저장한다.
    public Todo Merge(int id, bool done)
    {
        // 12. 먼저 현재 값을 읽어 존재하지 않는 ID를 일관되게 처리한다.
        Todo current = GetOne(id);
        // 13. 같은 ID/Title을 보존하고 Done만 바꾼다.
        Todo updated = current with { Done = done };
        // 14. 새 값을 같은 키에 덮어쓴다.
        _items[id] = updated;
        // 15. HTTP 응답 본문에 해당할 값을 반환한다.
        return updated;
    }

    // 16. Delete는 키가 있었는지를 bool로 돌려준다.
    public bool Delete(int id) => _items.Remove(id);
}

// 17. 한 평가 시나리오를 함수로 만들어 같은 입력으로 반복할 수 있게 한다.
static string RunScenario()
{
    // 18. 새 API 인스턴스는 운영 데이터와 무관한 동일 seed state를 가진다.
    FakeApi api = new();
    // 19. merge action을 흉내 내 첫 항목을 완료한다.
    Todo merged = api.Merge(1, true);
    // 20. delete action을 흉내 내 두 번째 항목을 제거한다.
    bool deleted = api.Delete(2);
    // 21. 남은 상태를 정렬된 문자열로 직렬화해 비교 가능한 결과를 만든다.
    string state = string.Join('|', api.GetAll().Select(x => $"{x.Id}:{x.Title}:{x.Done}"));
    // 22. 입력과 초기 상태가 같으면 이 문자열도 항상 같아야 한다.
    return $"merged={merged.Done};deleted={deleted};state={state}";
}

// 23. 첫 번째 독립 평가를 실행한다.
string first = RunScenario();
// 24. 두 번째 평가는 새 store에서 똑같이 시작한다.
string second = RunScenario();
// 25. 두 결과를 출력해 사람이 확인한다.
Console.WriteLine(first);
// 26. 완전히 같아야 테스트가 결정적이다.
Console.WriteLine($"deterministic = {first == second}");
// 27. 이 실습은 HttpClient를 전혀 만들지 않았으므로 운영 호출 수는 0이다.
Console.WriteLine("production calls = 0");

// CLR 관찰 메모
// - record의 with는 기존 객체를 변경하지 않고 새 객체를 할당한다.
// - 각 시나리오의 FakeApi/Dictionary/Todo는 반환 후 도달 불가가 되어 GC 대상이 된다.
// - 실제 Dev Proxy는 프로세스 밖 네트워크 계층에서 URL을 가로채므로 스킬 본문 URL을 바꾸지 않는다.
// - 결정성은 JIT 최적화 여부가 아니라 입력·시간·난수·외부 상태를 통제해 얻는다.
