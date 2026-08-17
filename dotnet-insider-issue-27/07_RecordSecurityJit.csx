// 실행: dotnet script 07_RecordSecurityJit.csx
// 목적: record shallow copy, Fetch Metadata policy, boxing과 SIMD를 한 파일에서 관찰한다.

// 01. 기본 형식과 Console을 가져온다.
using System;
// 02. List<T>로 mutable nested state를 만든다.
using System.Collections.Generic;
// 03. SIMD Vector128 intrinsics를 가져온다.
using System.Runtime.Intrinsics;

// 04. record class는 Name 값과 mutable List 참조를 가진다.
record class Team(string Name, List<string> Members);
// 05. HTTP request provenance의 최소 field를 record로 표현한다.
record FetchMetadata(string Site, string Mode, string Destination, bool UserActivated);

// 06. 일반 함수로 record built-in with copy semantics를 이름 있는 API처럼 모형화한다.
static Team ShallowClone(Team source) => source with { };
// 07. deep copy 모형은 nested List도 새로 만든다.
static Team DeepClone(Team source) => source with { Members = new List<string>(source.Members) };

// 08. state-changing endpoint용 Resource Isolation Policy를 pure function으로 만든다.
static bool AllowStateChange(FetchMetadata metadata)
{
    // 09. same-origin request는 정상 app flow로 허용한다.
    if (metadata.Site == "same-origin") return true;
    // 10. address bar/bookmark 같은 user top-level navigation은 GET 전용이어야 하므로 여기서는 거부한다.
    if (metadata.Site == "none" && metadata.Mode == "navigate" && metadata.UserActivated) return false;
    // 11. same-site도 subdomain compromise 위험이 있어 이 strict policy에서는 거부한다.
    return false;
}

// 12. generic constraint가 enum value type만 받게 한다.
static bool GenericEnumEquals<T>(T left, T right) where T : struct, Enum
{
    // 13. 최신 JIT는 이 호출의 boxing을 제거하도록 special-case할 수 있다.
    return left.Equals(right);
}

// 14. 원본 record와 nested list를 만든다.
Team original = new("runtime", new List<string> { "JIT" });
// 15. shallow clone은 새 Team이지만 같은 Members 참조를 가진다.
Team shallow = ShallowClone(original);
// 16. deep clone 모형은 새 Members list를 가진다.
Team deep = DeepClone(original);
// 17. shallow list에 값을 추가하면 original에서도 보인다.
shallow.Members.Add("GC");
// 18. deep list에 값을 추가해도 original에는 보이지 않는다.
deep.Members.Add("AOT");
// 19. 참조 공유 여부와 count를 출력한다.
Console.WriteLine($"shallow shares list = {ReferenceEquals(original.Members, shallow.Members)}, original count = {original.Members.Count}");
// 20. deep clone의 list가 분리됐는지 출력한다.
Console.WriteLine($"deep shares list = {ReferenceEquals(original.Members, deep.Members)}, deep count = {deep.Members.Count}");

// 21. same-origin fetch request를 만든다.
FetchMetadata sameOrigin = new("same-origin", "same-origin", "empty", false);
// 22. malicious cross-site form POST 모형을 만든다.
FetchMetadata crossSite = new("cross-site", "navigate", "document", false);
// 23. strict state-change policy 결과를 출력한다.
Console.WriteLine($"fetch allowed same/cross = {AllowStateChange(sameOrigin)}/{AllowStateChange(crossSite)}");

// 24. enum 비교 전 현재 thread가 할당한 byte 수를 읽는다.
long before = GC.GetAllocatedBytesForCurrentThread();
// 25. result를 사용해 호출이 dead-code로 제거되지 않게 한다.
bool enumEqual = false;
// 26. 반복 호출로 allocation 차이를 관찰한다.
for (int i = 0; i < 10_000; i++)
{
    // 27. DayOfWeek enum 두 값을 generic method로 비교한다.
    enumEqual ^= GenericEnumEquals(DayOfWeek.Monday, DayOfWeek.Tuesday);
}
// 28. 반복 뒤 allocation count를 읽는다.
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
// 29. 환경별 JIT 차이를 결론내리지 않고 관찰값을 출력한다.
Console.WriteLine($"generic enum result={enumEqual}, allocated={allocated} bytes");

// 30. 네 int lane을 가진 Vector128을 만든다.
Vector128<int> vector = Vector128.Create(4, 9, 2, 7);
// 31. half를 바꿔 원본과 lane별 Max를 취한다.
vector = Vector128.Max(vector, Vector128.Shuffle(vector, Vector128.Create(2, 3, 0, 1)));
// 32. neighbor를 바꿔 다시 Max를 취하면 모든 lane에 global max가 전파된다.
vector = Vector128.Max(vector, Vector128.Shuffle(vector, Vector128.Create(1, 0, 3, 2)));
// 33. 첫 scalar lane에서 최댓값 9를 꺼낸다.
Console.WriteLine($"SIMD horizontal max = {vector.ToScalar()}");

// CLR/JIT 관찰 메모
// - `with`는 compiler-generated clone 뒤 initializer를 적용하고 nested reference는 기본 공유한다.
// - enum generic Equals boxing 제거는 runtime/JIT version에 따라 달라 allocation을 직접 측정해야 한다.
// - Vector128 intrinsic은 지원 ISA에서 SIMD instruction으로 lower되고 fallback/architecture가 다를 수 있다.
// - Fetch Metadata 판단은 managed code가 빠른지보다 올바른 endpoint 계약·defense-in-depth가 중요하다.
