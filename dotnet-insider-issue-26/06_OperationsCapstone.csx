// 실행: dotnet script 06_OperationsCapstone.csx
// 목적: preference, 강제 policy, 현대화 단계와 build 결과를 서로 다른 상태로 모델링한다.

// 01. Console과 기본 형식을 가져온다.
using System;
// 02. List 컬렉션을 가져온다.
using System.Collections.Generic;
// 03. LINQ를 이용해 통과하지 못한 gate를 찾는다.
using System.Linq;

// 04. 현대화 워크플로의 유한 상태를 enum으로 제한한다.
enum UpgradeStage { Assess, Plan, Implement, Build, Test, Review, Done }
// 05. preference는 응답 형식을 바꾸지만 보안 정책을 집행하지 않는다.
record Preference(string Scope, string Text, bool Enabled);
// 06. policy gate는 이름과 통과 여부, 증거를 명시한다.
record PolicyGate(string Name, bool Passed, string Evidence);
// 07. 실행 기록은 단계와 사람이 검토할 메시지를 묶는다.
record Event(UpgradeStage Stage, string Message);

// 08. 조직 지침은 짧은 응답을 선호하지만 이를 보안 gate로 오해하면 안 된다.
Preference organizationInstruction = new("organization", "Keep summaries short and cite changed files", true);
// 09. workflow event를 순서대로 보존하는 목록을 만든다.
List<Event> events = new();

// 10. 평가 단계에서 현재 target과 의존성을 수집했다고 기록한다.
events.Add(new(UpgradeStage.Assess, "target=net8.0; dependencies inventoried"));
// 11. 계획 단계는 프로젝트 순서와 되돌리기 단위를 만든다.
events.Add(new(UpgradeStage.Plan, "upgrade library before web app; one commit per task"));
// 12. 구현은 실제 코드/패키지 변경이 일어나는 단계다.
events.Add(new(UpgradeStage.Implement, "target framework and packages updated"));
// 13. build 성공은 필요하지만 사용자 동작 검증을 대신하지 않는다.
events.Add(new(UpgradeStage.Build, "build succeeded"));
// 14. 테스트 결과를 별도 event로 기록한다.
events.Add(new(UpgradeStage.Test, "128 passed; 1 skipped with reason"));
// 15. 사람이 diff를 검토해야 완료로 갈 수 있다.
events.Add(new(UpgradeStage.Review, "human reviewed serialization and auth changes"));

// 16. 실제 집행 정책은 CI/서버에서 얻은 독립 gate 목록이다.
List<PolicyGate> gates = new()
{
    // 17. required review를 통과했다는 증거다.
    new("required-review", true, "2 approvals"),
    // 18. 테스트 gate를 통과했다는 증거다.
    new("tests", true, "128 passed"),
    // 19. secret scan을 통과했다는 증거다.
    new("secret-scan", true, "0 findings")
};

// 20. All은 모든 gate가 참일 때만 true다. 빈 목록이면 true가 되므로 gate 구성 자체도 검증해야 한다.
bool canComplete = gates.Count > 0 && gates.All(gate => gate.Passed);
// 21. 정책을 모두 통과한 경우에만 Done event를 추가한다.
if (canComplete)
{
    events.Add(new(UpgradeStage.Done, "all enforced gates passed"));
}

// 22. preference 활성 여부를 출력한다. 결과 요약 형식에만 영향을 준다.
Console.WriteLine($"preference enabled = {organizationInstruction.Enabled}: {organizationInstruction.Text}");
// 23. event를 순서대로 출력해 upgrade canvas의 작은 모형을 만든다.
foreach (Event item in events)
{
    // 24. -12 정렬은 단계 열 너비를 맞춰 사람이 훑기 쉽게 한다.
    Console.WriteLine($"{item.Stage,-12} | {item.Message}");
}

// 25. 모든 gate의 증거를 별도로 출력한다.
foreach (PolicyGate gate in gates)
{
    // 26. preference 문구가 이 결과를 강제로 바꾸지 못한다.
    Console.WriteLine($"gate {gate.Name} = {gate.Passed} ({gate.Evidence})");
}

// 27. 최종 완료 가능 여부를 명시한다.
Console.WriteLine($"complete = {canComplete}");

// CLR/JIT 관찰 메모
// - enum은 기본적으로 int 기반 값이며 잘못된 cast도 가능하므로 외부 입력은 Enum.IsDefined 등으로 검증한다.
// - List<Event>의 객체 참조와 record 인스턴스는 관리 힙에 있으며 짧은 수명 객체는 Gen 0에서 회수되는 경우가 많다.
// - LINQ All의 predicate 호출은 JIT 인라인 여부와 열거자 형태에 따라 비용이 달라질 수 있지만 gate I/O 비용보다 보통 작다.
// - 에이전트가 생성한 C#도 동일한 Roslyn/IL/CLR/JIT 파이프라인을 거치며 출처가 최적화를 바꾸지 않는다.
