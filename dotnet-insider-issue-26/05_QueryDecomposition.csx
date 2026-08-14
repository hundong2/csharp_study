// 실행: dotnet script 05_QueryDecomposition.csx
// 목적: 자연어에서 엄격한 metadata와 semantic query를 분리한 뒤 후보를 필터링한다.

// 01. Console과 StringComparer 같은 기본 형식을 가져온다.
using System;
// 02. List 컬렉션을 가져온다.
using System.Collections.Generic;
// 03. LINQ로 필터와 정렬을 표현한다.
using System.Linq;
// 04. Regex로 episode/scene/shot 패턴을 찾는다.
using System.Text.RegularExpressions;

// 05. 분해 결과를 불변 값 record로 표현한다. int?는 숫자가 없을 수 있다는 뜻이다.
record ParsedQuery(int? Episode, int? Scene, int? Shot, string SemanticText);
// 06. 검색 후보에는 구조화 metadata와 설명, 미리 계산됐다고 가정한 점수가 있다.
record Asset(int Episode, int Scene, int Shot, string Caption, double VectorScore);

// 07. timeout은 악의적/병적인 backtracking으로 오래 멈추는 위험을 제한한다.
TimeSpan regexTimeout = TimeSpan.FromMilliseconds(100);
// 08. IgnoreCase는 Episode/episode를 모두 허용하고 CultureInvariant는 문화권 차이를 줄인다.
Regex metadataPattern = new(
    @"\bepisode\s*(?<episode>\d+)\s+scene\s*(?<scene>\d+)\s+shot\s*(?<shot>\d+)\b",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
    regexTimeout);

// 09. Parser 함수는 Regex 인스턴스를 인수로 받아 테스트에서 교체할 수 있다.
static ParsedQuery Parse(string raw, Regex pattern)
{
    // 10. Match는 처음 일치한 구간과 이름 붙인 group을 반환한다.
    Match match = pattern.Match(raw);
    // 11. 일치가 없으면 metadata는 null이고 원문 전체를 semantic query로 보존한다.
    if (!match.Success)
    {
        return new(null, null, null, raw.Trim());
    }

    // 12. int.Parse는 숫자 group을 32비트 정수로 바꾼다. 실무에서는 범위 검증도 한다.
    int episode = int.Parse(match.Groups["episode"].Value);
    // 13. scene group도 같은 방식으로 변환한다.
    int scene = int.Parse(match.Groups["scene"].Value);
    // 14. shot group도 같은 방식으로 변환한다.
    int shot = int.Parse(match.Groups["shot"].Value);
    // 15. metadata 구간을 제거해 embedding에 넣을 의미 설명만 남긴다.
    string semantic = pattern.Replace(raw, string.Empty).Trim(' ', ',', '-', ':');
    // 16. 구조화 필드와 rewrite를 한 객체로 돌려준다.
    return new(episode, scene, shot, semantic);
}

// 17. 사용자의 noisy query에는 정확 필드와 시각 설명이 섞여 있다.
string query = "episode 3 scene 12 shot 7, hero opens a red door";
// 18. 파서를 한 번 호출해 두 종류의 의도를 분리한다.
ParsedQuery parsed = Parse(query, metadataPattern);
// 19. 추출 결과를 사람이 검토할 수 있게 출력한다.
Console.WriteLine($"episode={parsed.Episode}, scene={parsed.Scene}, shot={parsed.Shot}");
// 20. semantic query에는 metadata 문구가 없어야 한다.
Console.WriteLine($"semantic={parsed.SemanticText}");

// 21. 작은 후보 목록이 검색 index를 대신한다.
List<Asset> assets = new()
{
    // 22. metadata가 정확하고 의미 점수도 높은 정답 후보다.
    new(3, 12, 7, "hero opens the red door", 0.91),
    // 23. 의미는 비슷하지만 다른 episode이므로 strict filter에서 제거되어야 한다.
    new(8, 12, 7, "hero opens a red door", 0.99),
    // 24. metadata는 맞지만 의미 점수가 낮은 후보다.
    new(3, 12, 7, "empty corridor", 0.22)
};

// 25. Where는 exact metadata를 retrieval의 전제 조건으로 적용한다.
List<Asset> filtered = assets.Where(asset =>
        (!parsed.Episode.HasValue || asset.Episode == parsed.Episode.Value) &&
        (!parsed.Scene.HasValue || asset.Scene == parsed.Scene.Value) &&
        (!parsed.Shot.HasValue || asset.Shot == parsed.Shot.Value))
    // 26. strict filter를 통과한 후보만 vector score 내림차순으로 정렬한다.
    .OrderByDescending(asset => asset.VectorScore)
    // 27. 지연 LINQ를 지금 실행하고 결과를 목록으로 고정한다.
    .ToList();

// 28. SQL parameter 모양으로 필터를 출력한다. 실제 쿼리는 문자열 연결 대신 parameter를 쓴다.
Console.WriteLine($"strict filter: Episode=@episode({parsed.Episode}) AND Scene=@scene({parsed.Scene}) AND Shot=@shot({parsed.Shot})");
// 29. 필터 결과를 순서대로 출력한다.
foreach (Asset asset in filtered)
{
    // 30. F2는 점수를 소수 둘째 자리까지 표시한다.
    Console.WriteLine($"candidate score={asset.VectorScore:F2}: {asset.Caption}");
}

// 31. 다른 표현은 현재 정규식이 놓치며 fallback/LLM/패턴 확장이 필요함을 보여 준다.
ParsedQuery brittleCase = Parse("ep 3 sc 12: hero opens a red door", metadataPattern);
// 32. null이면 exact filter를 만들지 못했다는 뜻이지 episode가 없다고 단정하는 것은 아니다.
Console.WriteLine($"abbreviation parsed = {brittleCase.Episode.HasValue}");

// CLR/JIT 관찰 메모
// - Regex는 Match/Group 객체와 문자열을 할당할 수 있어 대량 검색에서 allocation도 측정해야 한다.
// - RegexOptions.Compiled는 동적 코드를 JIT하는 준비 비용과 반복 실행 이득을 비교해야 한다.
// - source-generated regex는 빌드 시 코드를 만들어 NativeAOT/trimming과 시작 성능에 유리할 수 있다.
// - LINQ의 Where/OrderBy는 열거 시 실행되며 ToList에서 결과 배열/목록이 할당된다.
