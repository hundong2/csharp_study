# 7. Issue 27 링크 커버리지

기준: [The .NET Insider - Issue 27](https://dotnet.news/p/the-net-insider-issue-27)

뉴스레터 본문의 기술 카드 **15개**를 모두 매핑했습니다. 로그인·구독·작성자·소셜·개인정보·사이트 탐색 링크는 기술 학습 범위에서 제외했습니다.

## 기술 카드 15개

| # | 원문 | 포함한 개념 | 학습 문서 | 실행 실습 |
|---:|---|---|---|---|
| 1 | [MCP C# SDK v2.0](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/) | 2026-07-28 spec, stateless default, handshake/session 제거, HTTP header routing, MRTR, v1 호환, Tasks 예외, package/extensions | [01 문서](./01-mcp-v2-agent-skills.md) | [01 CSX](./01_StatelessMcpMrtr.csx) |
| 2 | [Visual Studio July Agent](https://devblogs.microsoft.com/visualstudio/visual-studio-july-update-meet-the-new-agent-powered-by-copilot-sdk/) | Copilot SDK Agent Preview, built-in skills, 조직 지침, branch context, MSVC discovery | [02 문서](./02-testing-build-agents.md), [Issue 26 상세](../dotnet-insider-issue-26/05-visual-studio-agent.md) | [Issue 26 capstone](../dotnet-insider-issue-26/06_OperationsCapstone.csx) |
| 3 | [Polyglot unit-test agent](https://devblogs.microsoft.com/dotnet/polyglot-unit-testing-agent/) | repository 조사, direct/single/iterative, assertion·scenario·discovery, mutation 사고법, benchmark와 한계 | [02 문서](./02-testing-build-agents.md) | [03 CSX](./03_TestTrustLoop.csx) |
| 4 | [Agent Skills from MCP](https://devblogs.microsoft.com/agent-framework/discover-agent-skills-from-mcp-servers-in-net/) | `skill://index.json`, `skill-md`/archive, `UseMcpSkills`, 중앙 배포, extraction limit, remote script 금지 | [01 문서](./01-mcp-v2-agent-skills.md) | [02 CSX](./02_RemoteSkillGuardrails.csx) |
| 5 | [MSBuild Binlog Analyzer](https://devblogs.microsoft.com/dotnet/msbuild-binlog-analyzer-vscode/) | `.binlog`, MCP grounding, errors/perf/timeline/compare/incremental, baseline regression, critical path, CI integration | [02 문서](./02-testing-build-agents.md) | [03 CSX](./03_TestTrustLoop.csx)의 evidence gate |
| 6 | [AWS SDK annual .NET targets](https://aws.amazon.com/blogs/developer/annual-net-target-updates-for-the-aws-sdk-for-net/) | 연간 TFM 추가/제거, 최대 두 LTS, net472/netstandard2.0, minor/package/assembly version, fallback tradeoff | [03 문서](./03-supply-chain-targeting.md) | [04 CSX](./04_SupplyChainPolicy.csx) |
| 7 | [SQL 20 best practices](https://antondevtips.com/blog/how-to-optimize-sql-queries-20-proven-best-practices) | sargability/index/type, 적게 읽기/keyset/watermark, join/subquery, materialized view, batch/transaction, plan/stats/monitoring | [04 문서](./04-data-access-performance.md) | [05 CSX](./05_DataTransactionAndIndex.csx) |
| 8 | [Unit of Work in .NET](https://www.nikolatech.net/blogs/unit-of-work-pattern-in-dotnet) | transaction boundary, EF `DbContext`/`SaveChanges`, Dapper connection/transaction, scoped DI, commit/rollback/dispose | [04 문서](./04-data-access-performance.md) | [05 CSX](./05_DataTransactionAndIndex.csx) |
| 9 | [Azure DocumentDB query tuning](https://devblogs.microsoft.com/documentdb/query-performance-tuning-guide/) | diagnostic log, explain, COLLSCAN/IXSCAN/SORT/FETCH, ESR, index-backed sort, covered query, over-indexing | [04 문서](./04-data-access-performance.md) | [05 CSX](./05_DataTransactionAndIndex.csx) |
| 10 | [NuGet API key lifetime](https://devblogs.microsoft.com/dotnet/strengthening-nuget-supply-chain-security-reducing-api-key-lifetime/) | 30일 key, 2026-11-01 기존 key 만료, OIDC Trusted Publishing, workload identity, rotation·scope·revoke | [03 문서](./03-supply-chain-targeting.md) | [04 CSX](./04_SupplyChainPolicy.csx) |
| 11 | [Foundry Local live Speech-to-Text](https://devblogs.microsoft.com/dotnet/foundry-local-live-speech-to-text-csharp/) | model lifecycle/cache/EP, WinML+NAudio, PCM, bounded channel/backpressure, async stream, interim/final, cleanup/privacy | [05 문서](./05-local-speech-ai.md) | [06 CSX](./06_BoundedAudioPipeline.csx) |
| 12 | [Clone method for C# record](https://www.meziantou.net/adding-a-clone-method-to-a-csharp-record.htm) | compiler clone, `with`, instance `Clone` 제한, extension method, shallow/deep copy | [06 문서](./06-csharp-security-performance.md) | [07 CSX](./07_RecordSecurityJit.csx) |
| 13 | [Fetch Metadata headers](https://andrewlock.net/understanding-the-fetch-metadata-http-headers-sec-fetch-site-and-friends/) | Dest/Site/Mode/User, same-site/origin, CORS/no-cors, Resource Isolation Policy, CSRF defense-in-depth | [06 문서](./06-csharp-security-performance.md) | [07 CSX](./07_RecordSecurityJit.csx) |
| 14 | [Schema-separated multi-tenancy](https://barretblake.dev/posts/development/2026/07/multi-tenant-part-2/) | schema isolation, query filter/stamp 제거, `IModelCacheKeyFactory`, provisioning/migration, schema injection, model memory | [04 문서](./04-data-access-performance.md) | [05 CSX](./05_DataTransactionAndIndex.csx) |
| 15 | [.NET 11 Performance Edition](https://steven-giesel.com/blogPost/86620358-bb91-4295-84fc-a1329b2567ae) | Enum boxing 제거, Span/range check, timezone cache, Guid parsing, LINQ Min/Max SIMD reduction, BenchmarkDotNet 해석 | [06 문서](./06-csharp-security-performance.md) | [07 CSX](./07_RecordSecurityJit.csx) |

## 공식 후속 링크

| 영역 | 공식 자료 |
|---|---|
| MCP | [C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), [Specification](https://modelcontextprotocol.io/specification/) |
| Agent Skills | [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/), [Agent Skills 표준](https://agentskills.io/) |
| Build/Test | [dotnet/skills](https://github.com/dotnet/skills), [MSBuild binary logs](https://learn.microsoft.com/visualstudio/msbuild/obtaining-build-logs-with-msbuild) |
| NuGet/.NET targets | [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), [Target frameworks](https://learn.microsoft.com/dotnet/standard/frameworks) |
| Data | [EF transactions](https://learn.microsoft.com/ef/core/saving/transactions), [Dynamic model](https://learn.microsoft.com/ef/core/modeling/dynamic-model), [PostgreSQL EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html) |
| Local AI | [Foundry Local live transcription](https://learn.microsoft.com/azure/foundry-local/how-to/how-to-live-transcribe-audio), [Channels](https://learn.microsoft.com/dotnet/core/extensions/channels) |
| Web security | [Fetch Metadata specification](https://www.w3.org/TR/fetch-metadata/), [ASP.NET Core antiforgery](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery) |
| CLR/JIT | [CLR 개요](https://learn.microsoft.com/dotnet/standard/clr), [Tiered compilation](https://learn.microsoft.com/dotnet/core/runtime-config/compilation) |

## 검증 체크리스트

- [ ] 표에 1~15번이 중복·누락 없이 있다.
- [ ] 모든 원문에 최소 한 개의 로컬 설명 문서가 연결된다.
- [ ] 코드화 가능한 주제는 실행 CSX에 연결된다.
- [ ] 외부 서비스가 필요한 기능은 local model과 실제 제품을 구분한다.
- [ ] Preview/experimental 기능과 운영 보안 주의가 표시되어 있다.

다음: [연습문제와 해설](./08-exercises.md)
