# 3. NuGet 공급망 보안과 Target Framework 정책

원문:

- [Strengthening NuGet Supply Chain Security: Reducing API Key Lifetime](https://devblogs.microsoft.com/dotnet/strengthening-nuget-supply-chain-security-reducing-api-key-lifetime/)
- [Annual .NET target updates for the AWS SDK for .NET](https://aws.amazon.com/blogs/developer/annual-net-target-updates-for-the-aws-sdk-for-net/)

## NuGet API key 수명 변경

- 2026-08-17부터 새 NuGet.org API key는 최대 30일입니다.
- 그 전에 만든 모든 key는 2026-11-01에 만료됩니다.
- 기존 365일 선택은 새 key에서 제거됩니다.

API key는 package publish 권한을 가진 password와 같습니다. repository secret, build config, log에 노출되면 공격자가 악성 version을 배포할 수 있습니다. 수명 단축은 노출 창을 줄일 뿐 key 자체를 안전하게 만들지는 않습니다.

## Trusted Publishing과 OIDC

```text
GitHub/GitLab workflow
  └─ signed short-lived OIDC identity token
       └─ NuGet.org가 owner policy(repository/workflow/environment) 검증
            └─ 이번 publish에만 쓰는 temporary API key 발급
```

장기 reusable secret을 CI에 저장하지 않고 자동 만료하며 workload identity를 검증합니다. token의 `aud`, `iss`, repository, ref/workflow/environment claim을 좁게 묶고 fork PR에 publish permission을 주지 않습니다. OIDC는 package 내용·dependency가 안전하다는 보장이 아니므로 build provenance, review, lockfile, signature/attestation, protected environment도 필요합니다.

API key를 계속 쓴다면 publish workflow를 전수 조사하고 package scope/permission을 최소화하며 rotation·expiration notification·즉시 revoke를 자동화합니다. source/log에 key를 넣지 않습니다.

## AWS SDK for .NET의 연간 TFM 정책

AWS SDK V4의 대상은 `AWSSDK.Core`와 `AWSSDK.<service>` package에 적용됩니다. 2026년 11월 V4.1부터 매년 최신 .NET target을 추가하고, 최대 두 LTS target을 유지하며 새 oldest LTS보다 앞선 STS/오래된 LTS를 제거합니다.

- V4.1: .NET Core 3.1 제거, .NET 8/10/11 추가·유지
- V4.2(2027): .NET 8 제거, .NET 10/11/12
- .NET Framework 4.7.2와 .NET Standard 2.0은 당분간 유지
- product minor version은 바뀌지만 assembly identity version은 `4.0.0`으로 유지해 기존 compiled library binding compatibility를 돕습니다.

### TFM과 assembly version은 다르다

- **TFM**은 compile API surface와 NuGet asset 선택 기준입니다.
- **package version**은 NuGet release 식별자입니다.
- **assembly version**은 CLR assembly identity/binding의 일부입니다.
- **file/informational version**은 build/release 정보입니다.

NuGet은 app target에 가장 가까운 compatible asset을 고릅니다. 최신 minor에서 app의 TFM-specific asset이 제거되면 `netstandard2.0` asset으로 fallback할 수 있지만 NativeAOT 지원과 최신 performance optimization을 잃을 수 있습니다. 이전 minor에 머물면 새 security/bug update를 못 받습니다. 그래서 SDK support와 .NET runtime support를 별도 표로 관리해야 합니다.

## CLR loader 관점

compile 시 reference assembly가 허용 API를 정하고 runtime에서 implementation assembly가 load됩니다. TFM이 높다고 매 method가 자동으로 빨라지는 것은 아니지만 library가 최신 API, Span, NativeAOT annotation을 활용할 수 있습니다. assembly identity를 안정적으로 유지하면 기존 binary reference가 재compile 없이 bind될 가능성이 높지만 behavioral compatibility와 runtime support는 여전히 검증해야 합니다.

## 실습

```powershell
dotnet script .\04_SupplyChainPolicy.csx
```

long-lived key, 잘못된 OIDC claim, 올바른 Trusted Publishing, TFM fallback을 정책 함수로 평가합니다.

## 다음 단계

- 이전: [테스트·Build Agent](./02-testing-build-agents.md)
- 다음: [데이터 접근과 성능](./04-data-access-performance.md)
- 공식 자료: [NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), [.NET target frameworks](https://learn.microsoft.com/dotnet/standard/frameworks)
