# 6. Issue 26 링크 커버리지

기준 원문: [The .NET Insider - Issue 26](https://dotnet.news/p/the-net-insider-issue-26)

뉴스레터 본문의 **기술 콘텐츠 카드 10개**를 전부 확인한 표입니다. 헤더·푸터의 홈, 작성자, 구독, 로그인, 소셜 공유, 개인정보 링크는 기술 학습 범위에서 제외했습니다.

## 편집 링크 10개

| # | 원문 링크 | 포함한 핵심 개념 | 학습 위치 | 실행 실습 |
|---:|---|---|---|---|
| 1 | [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/) | Runtime/SDK/Libraries, C# 15 extension indexer·union, STJ union, async validation, OpenAPI 3.2, CSRF, SignalR, 테스트, NativeAOT, MAUI/EF/F#/컨테이너 | [런타임 문서 §1.5](./01-dotnet11-runtime-servicing.md), [전용 완전 가이드](../dotnet-11-preview-6/README.md), [세부 62링크 표](../dotnet-11-preview-6/06-link-coverage.md) | [CLR/JIT](./01_ClrJitRuntime.csx) 및 [기존 9개 CSX](../dotnet-11-preview-6/README.md#-실행-가능한-csx-실습) |
| 2 | [Agent Skills for .NET is now released](https://devblogs.microsoft.com/agent-framework/agent-skills-for-net-is-now-released/) | stable API, progressive disclosure, 파일/클래스/코드 스킬, 승인, 필터, 캐시, source pipeline, runner·sandbox | [Agent Skills §2.1~2.5](./02-agent-skills-modernization.md) | [02_AgentSkillPipeline.csx](./02_AgentSkillPipeline.csx) |
| 3 | [CoreCLR progress and Mono timeline for .NET MAUI](https://devblogs.microsoft.com/dotnet/coreclr-progress-and-mono-timeline-dotnet-maui/) | Android/iOS/Mac Catalyst CoreCLR 통합, 진단, Hot Reload, NativeAOT 기반, Blazor WASM Mono 유지, migration 검증 | [런타임 문서 §1.6](./01-dotnet11-runtime-servicing.md) | [01_ClrJitRuntime.csx](./01_ClrJitRuntime.csx) |
| 4 | [.NET and .NET Framework July 2026 servicing updates](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/) | 10.0.10/9.0.18/8.0.29, 보안·신뢰성 패치, CVE 범위, 릴리스 노트·컨테이너·rollout 검증 | [런타임 문서 §1.7](./01-dotnet11-runtime-servicing.md) | [06_OperationsCapstone.csx](./06_OperationsCapstone.csx) |
| 5 | [Modernize .NET in the GitHub Copilot app](https://devblogs.microsoft.com/dotnet/modernize-dotnet-in-github-copilot-app/) | 평가→계획→작업→실행→피드백→검증, upgrade canvas, VS/VS Code/CLI surface | [현대화 §2.6](./02-agent-skills-modernization.md) | [06_OperationsCapstone.csx](./06_OperationsCapstone.csx) |
| 6 | [Building long-running MCP tools with Azure Functions](https://devblogs.microsoft.com/azure-sdk/long-running-mcp-tools-azure-functions/) | 요청/작업 수명, MCP Tasks capability·상태·get/update/cancel, Durable checkpoint/recovery, start/get 도구, polling·ID | [MCP §3.1~3.4](./03-durable-mcp-devproxy.md) | [03_DurableMcpWorkflow.csx](./03_DurableMcpWorkflow.csx) |
| 7 | [How to test agent skills without hitting real APIs](https://developer.microsoft.com/blog/how-to-test-agent-skills-without-hitting-real-apis/) | 실제 API 평가의 비용·부작용·비결정성, URL 보존 interception, JSON data/action, 실행별 reset | [Dev Proxy §3.5~3.6](./03-durable-mcp-devproxy.md) | [04_DevProxyEvaluation.csx](./04_DevProxyEvaluation.csx) |
| 8 | [MSSQL extension for VS Code v1.44](https://devblogs.microsoft.com/azure-sql/vscode-mssql-july2026/) | Shortcuts Configuration Preview, Quick Queries, query/result shortcuts, beta results grid, freeze/hide/show·state·성능 | [SQL 도구 §4.1~4.2](./04-sql-query-decomposition.md) | [05_QueryDecomposition.csx](./05_QueryDecomposition.csx)의 parameter/filter 모형 |
| 9 | [From noisy queries to precise frames](https://devblogs.microsoft.com/ise/from_noisy_queries_to_precise_frames/) | metadata 추출, semantic rewrite, strict pre-filter, vector/hybrid ranking, regex/LLM/fine-tune, exact match·ROUGE-L·BLEU·Recall@5 | [질의 분해 §4.3~4.7](./04-sql-query-decomposition.md) | [05_QueryDecomposition.csx](./05_QueryDecomposition.csx) |
| 10 | [Visual Studio July update](https://devblogs.microsoft.com/visualstudio/visual-studio-july-update-meet-the-new-agent-powered-by-copilot-sdk/) | Copilot SDK Agent Preview, built-in .NET/Azure skills, 조직 지침, branch context, cross-install MSVC toolset discovery | [Visual Studio 문서](./05-visual-studio-agent.md) | [06_OperationsCapstone.csx](./06_OperationsCapstone.csx) |

## 원문 내부 개념의 후속 공식 링크

아래는 제품 사용이나 기반 개념을 더 깊게 확인할 때 사용할 공식 자료입니다. 기사 시점 이후 UI/API가 바뀔 수 있으므로 실제 적용 직전에 확인하세요.

| 주제 | 공식 후속 자료 | 이 과정에서 먼저 읽을 곳 |
|---|---|---|
| C#과 CLR | [CLR 개요](https://learn.microsoft.com/dotnet/standard/clr), [JIT 설정](https://learn.microsoft.com/dotnet/core/runtime-config/compilation), [GC 기본](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals) | [01 문서](./01-dotnet11-runtime-servicing.md) |
| .NET 11 Preview 6 | [.NET 11 다운로드](https://dotnet.microsoft.com/download/dotnet/11.0), [Preview 6 release notes](https://github.com/dotnet/core/tree/main/release-notes/11.0/preview/preview6) | [기존 전용 가이드](../dotnet-11-preview-6/README.md) |
| Agent Framework/Skills | [Agent Framework](https://learn.microsoft.com/agent-framework/), [Agent Skills specification](https://agentskills.io/) | [02 문서](./02-agent-skills-modernization.md) |
| .NET 업그레이드 | [Upgrade Assistant overview](https://learn.microsoft.com/dotnet/core/porting/upgrade-assistant-overview) | [02 문서 §2.6](./02-agent-skills-modernization.md) |
| MCP | [MCP Tasks](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/tasks), [MCP tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools) | [03 문서 §3.1~3.4](./03-durable-mcp-devproxy.md) |
| Durable Functions | [개요](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview), [orchestrator constraints](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-code-constraints) | [03 문서 §3.3](./03-durable-mcp-devproxy.md) |
| Dev Proxy | [개요](https://learn.microsoft.com/microsoft-cloud/dev/dev-proxy/overview), [MockResponsePlugin](https://learn.microsoft.com/microsoft-cloud/dev/dev-proxy/technical-reference/mockresponseplugin) | [03 문서 §3.5~3.6](./03-durable-mcp-devproxy.md) |
| MSSQL VS Code | [확장 개요](https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/mssql/mssql-extension-visual-studio-code) | [04 문서 §4.1~4.2](./04-sql-query-decomposition.md) |
| 검색 | [Azure AI Search vector search](https://learn.microsoft.com/azure/search/vector-search-overview), [hybrid search](https://learn.microsoft.com/azure/search/hybrid-search-overview) | [04 문서 §4.3~4.7](./04-sql-query-decomposition.md) |
| Visual Studio Copilot | [GitHub Copilot in Visual Studio](https://learn.microsoft.com/visualstudio/ide/visual-studio-github-copilot-install-and-states) | [05 문서](./05-visual-studio-agent.md) |

## 범위 검증 체크리스트

- [ ] 위 10개 원문 링크가 모두 열린다.
- [ ] 각 행에 최소 한 개의 로컬 설명 문서가 있다.
- [ ] 제품 UI만 있는 기능도 안전성·검증 방법이 설명되어 있다.
- [ ] 코드로 재현 가능한 개념에는 실행 가능한 CSX가 연결되어 있다.
- [ ] .NET 11 원문의 세부 링크는 기존 62링크 커버리지와 연결되어 있다.

## 다음 단계

[연습문제와 해설](./07-exercises.md)로 이동해 링크별로 이해했는지 확인하세요.
