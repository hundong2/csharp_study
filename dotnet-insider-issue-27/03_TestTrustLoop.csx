// 실행: dotnet script 03_TestTrustLoop.csx
// 목적: 단순 pass가 아니라 assertion·mutation·discovery·full build gate를 모두 통과시킨다.

// 01. Console과 기본 형식을 가져온다.
using System;
// 02. List<T>로 test 결과를 모은다.
using System.Collections.Generic;
// 03. All/Count LINQ 연산을 사용한다.
using System.Linq;

// 04. production behavior를 외부 dependency 없는 pure function으로 만든다.
static decimal ApplyDiscount(decimal price, bool premium)
{
    // 05. premium이면 10%, 아니면 원가를 반환하는 conditional expression이다.
    return premium ? price * 0.90m : price;
}

// 06. 의도적으로 잘못된 mutant는 premium 할인을 5%로 바꾼다.
static decimal MutantApplyDiscount(decimal price, bool premium)
{
    // 07. 좋은 test suite라면 이 변경을 잡아야 한다.
    return premium ? price * 0.95m : price;
}

// 08. 하나의 test case를 이름·입력·기대값으로 표현한다.
record TestCase(string Name, decimal Price, bool Premium, decimal Expected);
// 09. 실행 결과는 원본 pass와 mutant kill 여부를 따로 기록한다.
record TestResult(string Name, bool OriginalPassed, bool MutantKilled);

// 10. normal, premium, zero boundary scenario를 명시한다.
List<TestCase> cases = new()
{
    // 11. 일반 고객은 가격이 바뀌지 않는다.
    new("regular price unchanged", 100m, false, 100m),
    // 12. premium은 정확히 10% 할인되어야 한다.
    new("premium gets ten percent", 100m, true, 90m),
    // 13. 0 boundary에서도 0이어야 한다.
    new("zero remains zero", 0m, true, 0m)
};

// 14. 모든 test를 실행할 결과 목록이다.
List<TestResult> results = new();
// 15. 각 scenario의 실제값을 계산한다.
foreach (TestCase test in cases)
{
    // 16. production 결과가 expected와 정확히 같은지 검사한다.
    bool passed = ApplyDiscount(test.Price, test.Premium) == test.Expected;
    // 17. mutant 결과가 expected와 다르면 이 test가 mutation을 죽인다.
    bool killed = MutantApplyDiscount(test.Price, test.Premium) != test.Expected;
    // 18. 두 품질 signal을 저장한다.
    results.Add(new(test.Name, passed, killed));
    // 19. test별 결과를 출력한다.
    Console.WriteLine($"{test.Name}: pass={passed}, mutantKilled={killed}");
}

// 20. repository test command가 3개를 모두 발견했다고 모형화한다.
int discoveredByRepositoryCommand = results.Count;
// 21. original test가 모두 pass해야 한다.
bool allPassed = results.All(result => result.OriginalPassed);
// 22. 적어도 business behavior를 바꾼 mutant는 하나 이상의 test가 잡아야 한다.
bool mutationDetected = results.Any(result => result.MutantKilled);
// 23. full workspace build 결과를 독립 gate로 둔다.
bool fullWorkspaceBuilt = true;
// 24. 기존 test를 지우지 않았다는 review 결과를 독립 gate로 둔다.
bool existingTestsPreserved = true;
// 25. 모든 조건이 맞아야 trusted result다.
bool trusted = allPassed && mutationDetected && discoveredByRepositoryCommand == cases.Count &&
               fullWorkspaceBuilt && existingTestsPreserved;
// 26. 최종 gate와 discovery 수를 출력한다.
Console.WriteLine($"trusted={trusted}, discovered={discoveredByRepositoryCommand}");

// CLR/JIT 관찰 메모
// - decimal은 128-bit value type이며 binary floating point와 다른 base-10 scale semantics를 가진다.
// - static pure function은 side effect가 없어 JIT가 inline하기 쉽고 unit test도 결정적이다.
// - List/record result는 heap에 할당되며 작은 test 자체보다 testhost/runner 시작 비용이 클 수 있다.
// - coverage는 실행된 줄을 말할 뿐 assertion이 결함을 잡는다는 보장은 아니다.
