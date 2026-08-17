// 실행: dotnet script 04_SupplyChainPolicy.csx
// 목적: publish credential 정책과 NuGet TFM asset 선택의 핵심 판단을 모형화한다.

// 01. 기본 형식과 Console을 가져온다.
using System;
// 02. target 목록을 다루기 위해 collection을 가져온다.
using System.Collections.Generic;
// 03. Contains/OrderByDescending을 사용한다.
using System.Linq;

// 04. credential 종류와 만료·claim을 한 값으로 묶는다.
record PublishCredential(string Kind, DateTimeOffset ExpiresAt, string Repository, string Workflow);
// 05. owner policy는 허용 repository와 workflow를 명시한다.
record TrustPolicy(string Repository, string Workflow, TimeSpan MaxLifetime);

// 06. credential을 현재 시각과 owner policy에 대해 검증한다.
static bool CanPublish(PublishCredential credential, TrustPolicy policy, DateTimeOffset now)
{
    // 07. 만료됐거나 허용 최대 수명보다 오래 남은 credential을 거부한다.
    if (credential.ExpiresAt <= now || credential.ExpiresAt - now > policy.MaxLifetime) return false;
    // 08. Trusted OIDC 종류만 허용한다. 단순 API key 문자열은 이 정책에서 거부한다.
    if (credential.Kind != "oidc") return false;
    // 09. repository와 workflow claim이 owner policy와 정확히 같아야 한다.
    return credential.Repository == policy.Repository && credential.Workflow == policy.Workflow;
}

// 10. NuGet의 복잡한 compatibility graph를 교육용 exact→netstandard fallback으로 단순화한다.
static string SelectAsset(string appTfm, IReadOnlyCollection<string> packageTfms)
{
    // 11. exact target asset이 있으면 최신 API/optimization을 위해 우선 선택한다.
    if (packageTfms.Contains(appTfm)) return appTfm;
    // 12. exact가 없고 netstandard2.0이 있으면 compatible fallback 모형을 선택한다.
    if (packageTfms.Contains("netstandard2.0")) return "netstandard2.0";
    // 13. compatible asset이 없음을 명시한다.
    return "incompatible";
}

// 14. 평가 시각을 고정해 test를 결정적으로 만든다.
DateTimeOffset now = new(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);
// 15. publish policy는 10분 token과 특정 workflow만 허용한다.
TrustPolicy policy = new("org/repo", ".github/workflows/publish.yml", TimeSpan.FromMinutes(10));
// 16. 30일 API key는 기사 새 최대치 안이어도 이 stricter OIDC-only 정책에서는 거부한다.
PublishCredential apiKey = new("api-key", now.AddDays(30), "org/repo", policy.Workflow);
// 17. 다른 repository claim을 가진 OIDC token은 거부해야 한다.
PublishCredential wrongRepo = new("oidc", now.AddMinutes(5), "attacker/fork", policy.Workflow);
// 18. 올바른 short-lived workload identity다.
PublishCredential trusted = new("oidc", now.AddMinutes(5), policy.Repository, policy.Workflow);

// 19. 세 credential의 정책 결과를 출력한다.
Console.WriteLine($"api-key allowed = {CanPublish(apiKey, policy, now)}");
// 20. fork claim이 거부되는지 출력한다.
Console.WriteLine($"wrong repo allowed = {CanPublish(wrongRepo, policy, now)}");
// 21. trusted workflow만 허용되는지 출력한다.
Console.WriteLine($"trusted publishing allowed = {CanPublish(trusted, policy, now)}");

// 22. V4.2 모형 target 목록에는 net8.0이 없고 netstandard2.0 fallback이 있다.
string[] v42Targets = { "net472", "netstandard2.0", "net10.0", "net11.0", "net12.0" };
// 23. net8 app이 선택하는 asset을 출력한다.
Console.WriteLine($"net8.0 selects = {SelectAsset("net8.0", v42Targets)}");
// 24. net10 app은 exact asset을 선택한다.
Console.WriteLine($"net10.0 selects = {SelectAsset("net10.0", v42Targets)}");

// CLR/JIT 관찰 메모
// - DateTimeOffset은 value type이고 string/record/array는 managed allocation을 만든다.
// - 실제 NuGet compatibility는 nearest framework reducer가 수행하므로 이 함수보다 복잡하다.
// - OIDC 검증은 signature, issuer, audience, nonce/expiry와 claim을 server에서 검증해야 한다.
// - TFM asset 선택은 compile/reference와 runtime implementation에 영향을 주며 JIT 자체의 trust 판단은 아니다.
