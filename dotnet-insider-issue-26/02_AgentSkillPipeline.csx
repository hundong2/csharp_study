// 실행: dotnet script 02_AgentSkillPipeline.csx
// 목적: 스킬의 광고→필터→로드→승인→실행과 tenant별 캐시를 안전하게 모형화한다.

// nullable 참조 형식 주석(string?)을 활성화해 null 계약을 컴파일러가 검사하게 한다.
#nullable enable

// 01. 기본 형식과 Console을 가져온다.
using System;
// 02. List와 Dictionary 컬렉션을 가져온다.
using System.Collections.Generic;
// 03. Where, OrderBy 같은 LINQ 확장 메서드를 가져온다.
using System.Linq;
// 04. Task 기반 비동기 실행을 가져온다.
using System.Threading.Tasks;

// 05. record는 값 중심 불변 데이터 모델에 편리하다. positional 매개변수는 속성이 된다.
record SkillCard(string Name, string Description, string Tenant, bool CanRunScript, string Instructions);
// 06. 승인 요청과 결과도 명시적인 값 객체로 표현한다.
record Approval(string Operation, string SkillName, bool Granted);

// 07. 기사 속 스킬 registry를 메모리 목록으로 대신한다.
List<SkillCard> registry = new()
{
    // 08. tenant-a 전용 업그레이드 절차다. 실제 코드 대신 교육용 문자열을 가진다.
    new("upgrade-dotnet", "Assess, plan, build and verify a .NET upgrade", "tenant-a", true,
        "1) assess target framework; 2) plan; 3) build; 4) test"),
    // 09. 다른 tenant의 비밀 스킬은 필터에서 보이지 않아야 한다.
    new("payroll-admin", "Run payroll administration", "tenant-b", true,
        "private tenant-b instructions"),
    // 10. 공용 읽기 전용 스킬은 스크립트를 실행하지 않는다.
    new("clr-glossary", "Explain CLR and JIT terms", "public", false,
        "Explain SDK, runtime, CLR, IL, JIT and GC in beginner language")
};

// 11. 캐시 키에 tenant를 포함해 서로 다른 고객의 해석 결과가 섞이지 않게 한다.
Dictionary<string, string> instructionCache = new(StringComparer.Ordinal);

// 12. static 함수는 사용자가 볼 수 있는 스킬 카드만 반환한다.
static IEnumerable<SkillCard> Advertise(IEnumerable<SkillCard> source, string tenant)
{
    // 13. public 또는 정확히 일치하는 tenant만 통과시킨다. 이것이 노출 필터 모형이다.
    return source.Where(skill => skill.Tenant == "public" || skill.Tenant == tenant)
                 // 14. 결과 순서를 고정하면 테스트가 결정적이다.
                 .OrderBy(skill => skill.Name);
}

// 15. 비동기 로더는 실제 환경의 파일/registry I/O를 흉내 낸다.
static async Task<string> LoadInstructionsAsync(
    SkillCard skill,
    string tenant,
    IDictionary<string, string> cache)
{
    // 16. 문자열 보간으로 tenant와 스킬 이름을 분리한 캐시 키를 만든다.
    string cacheKey = $"{tenant}:{skill.Name}";
    // 17. TryGetValue는 키 존재 여부와 값을 한 번에 얻는다.
    if (cache.TryGetValue(cacheKey, out string? cached))
    {
        // 18. cache hit은 원본을 다시 읽지 않고 이미 검토된 문자열을 돌려준다.
        Console.WriteLine($"cache hit: {cacheKey}");
        return cached;
    }

    // 19. 실제 I/O라면 여기서 파일이나 registry를 읽는다. Delay는 비동기 경계를 만든다.
    await Task.Delay(10);
    // 20. 읽은 지침을 tenant별 캐시에 저장한다.
    cache[cacheKey] = skill.Instructions;
    // 21. 첫 로드임을 관찰할 수 있게 출력한다.
    Console.WriteLine($"loaded: {cacheKey}");
    // 22. 지침 문자열을 호출자에게 반환한다.
    return skill.Instructions;
}

// 23. runner는 승인 객체를 반드시 받고 실제 코드 대신 허용 목록 작업만 수행한다.
static async Task<string> RunApprovedAsync(SkillCard skill, Approval approval)
{
    // 24. 스킬이 스크립트를 허용하지 않거나 승인이 없으면 즉시 거부한다.
    if (!skill.CanRunScript || !approval.Granted || approval.SkillName != skill.Name)
    {
        return "denied";
    }

    // 25. 실제 subprocess 대신 안전한 짧은 비동기 작업을 실행한다.
    await Task.Delay(10);
    // 26. 실행 결과를 구조화할 수 있지만 여기서는 간단한 문자열로 돌려준다.
    return "assessment -> plan -> build -> test";
}

// 27. 현재 요청 tenant를 명시한다. 실제 시스템에서는 인증된 claim에서 가져와야 한다.
string currentTenant = "tenant-a";
// 28. 먼저 이름과 설명만 공개한다. 지침 전체는 아직 읽지 않는다.
List<SkillCard> visible = Advertise(registry, currentTenant).ToList();
// 29. payroll-admin이 출력되지 않아야 필터가 동작한 것이다.
Console.WriteLine($"visible = {string.Join(',', visible.Select(s => s.Name))}");

// 30. 설명이 목표와 맞는 스킬을 선택한다. 실제 에이전트는 모델이 선택할 수 있다.
SkillCard selected = visible.Single(s => s.Name == "upgrade-dotnet");
// 31. 선택된 뒤에만 전체 지침을 로드한다. 이것이 progressive disclosure다.
string firstLoad = await LoadInstructionsAsync(selected, currentTenant, instructionCache);
// 32. 같은 tenant에서 다시 읽으면 캐시를 사용한다.
string secondLoad = await LoadInstructionsAsync(selected, currentTenant, instructionCache);
// 33. 두 결과가 같은지 검사해 캐시가 의미를 바꾸지 않았음을 확인한다.
Console.WriteLine($"same instructions = {firstLoad == secondLoad}");

// 34. 사람 승인을 받은 상황을 명시적인 객체로 만든다. 승인은 실행 직전에 재확인해야 한다.
Approval approval = new("run_skill_script", selected.Name, Granted: true);
// 35. runner에 선택된 스킬과 승인을 함께 전달한다.
string result = await RunApprovedAsync(selected, approval);
// 36. 결과에는 현대화의 네 단계가 순서대로 보여야 한다.
Console.WriteLine($"approved result = {result}");

// CLR 관찰 메모
// - record와 List/Dictionary는 관리 힙 객체이며 GC가 도달 가능성을 추적한다.
// - async 함수는 상태 머신으로 변환되고 Delay 중에는 작업 스레드를 붙잡지 않는다.
// - in-process 스킬은 호스트 CLR/GC/스레드 풀을 공유하므로 보안 경계가 아니다.
// - 별도 프로세스 runner는 시작·직렬화 비용을 내는 대신 실패·권한 격리에 유리하다.
