// 실행: dotnet script 02_RemoteSkillGuardrails.csx
// 목적: 원격 MCP archive skill을 다운로드하기 전 resource limit과 script 금지를 검사한다.

// 01. Console과 기본 형식을 가져온다.
using System;
// 02. skill manifest의 file 목록을 List로 만든다.
using System.Collections.Generic;
// 03. All/Any/Sum LINQ 연산을 사용한다.
using System.Linq;

// 04. 원격 archive를 실제 byte가 아닌 검증용 metadata로 표현한다.
record ArchiveSkill(string Name, long DownloadBytes, long UncompressedBytes, IReadOnlyList<string> Files);
// 05. 각 제한을 하나의 불변 policy 값으로 묶는다.
record ArchivePolicy(long MaxDownloadBytes, long MaxUncompressedBytes, int MaxFileCount);
// 06. 검증 결과는 허용 여부와 사람이 읽을 이유를 가진다.
record Validation(bool Allowed, string Reason);

// 07. Windows와 Unix separator를 모두 정규화해 path traversal을 검사한다.
static bool IsSafeRelativePath(string path)
{
    // 08. 역슬래시를 slash로 바꿔 platform별 우회를 줄인다.
    string normalized = path.Replace('\\', '/');
    // 09. root path, drive separator, 빈 segment, 부모 이동을 거부한다.
    return !normalized.StartsWith('/') && !normalized.Contains(':') &&
           normalized.Split('/').All(part => part is not "" and not "." and not "..");
}

// 10. archive를 실제 extraction하기 전에 metadata limit을 모두 검사한다.
static Validation Validate(ArchiveSkill skill, ArchivePolicy policy)
{
    // 11. wire payload가 너무 크면 download/memory 사용을 막는다.
    if (skill.DownloadBytes > policy.MaxDownloadBytes) return new(false, "archive download too large");
    // 12. 압축 해제 합계가 크면 decompression bomb를 막는다.
    if (skill.UncompressedBytes > policy.MaxUncompressedBytes) return new(false, "uncompressed data too large");
    // 13. 작은 파일 수만 개로 disk metadata를 고갈시키는 공격을 막는다.
    if (skill.Files.Count > policy.MaxFileCount) return new(false, "too many files");
    // 14. extraction root 바깥으로 나가는 path를 거부한다.
    if (skill.Files.Any(file => !IsSafeRelativePath(file))) return new(false, "unsafe archive path");
    // 15. 원격 archive script는 extension과 관계없이 실행하지 않지만 명백한 실행 파일도 보고한다.
    if (skill.Files.Any(file => file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
        return new(false, "remote executable rejected");
    // 16. 모든 검사 통과 뒤 instructions/resources만 읽을 수 있다.
    return new(true, "instructions and resources only; scripts disabled");
}

// 17. article 예시와 같은 2MB/4MB/50개 제한을 만든다.
ArchivePolicy policy = new(2 * 1024 * 1024, 4 * 1024 * 1024, 50);
// 18. 정상 skill archive metadata다.
ArchiveSkill safe = new("expense-policy", 100_000, 300_000,
    new[] { "SKILL.md", "references/limits.md", "scripts/check.ps1" });
// 19. 부모 path로 host 파일을 덮으려는 archive 모형이다.
ArchiveSkill traversal = new("hostile", 10_000, 20_000,
    new[] { "SKILL.md", "../../startup.exe" });
// 20. 압축 크기는 작지만 해제하면 매우 큰 decompression bomb 모형이다.
ArchiveSkill bomb = new("bomb", 50_000, 2L * 1024 * 1024 * 1024,
    new[] { "SKILL.md" });

// 21. 각 결과를 출력해 어떤 guard가 작동했는지 확인한다.
foreach (ArchiveSkill skill in new[] { safe, traversal, bomb })
{
    // 22. validator를 한 번 호출한다.
    Validation result = Validate(skill, policy);
    // 23. 이름, 허용 여부, 이유를 감사 log 모양으로 출력한다.
    Console.WriteLine($"{skill.Name}: allowed={result.Allowed}, reason={result.Reason}");
}

// CLR/JIT 관찰 메모
// - LINQ Any/All은 short-circuit하지만 Split/Replace는 새 string/array를 할당한다.
// - 실제 archive는 entry를 streaming하며 누적 크기를 checked arithmetic으로 제한해야 한다.
// - path validation만으로 sandbox가 되지 않는다. OS 권한·별도 process·network 제한도 필요하다.
// - remote script를 읽을 수 있음과 실행 권한을 주는 것은 별개다.
