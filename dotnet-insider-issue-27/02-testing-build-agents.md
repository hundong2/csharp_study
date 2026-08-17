# 2. 테스트 에이전트, MSBuild Binlog, Visual Studio Agent

원문:

- [From generated code to trusted code with a unit-test agent](https://devblogs.microsoft.com/dotnet/polyglot-unit-testing-agent/)
- [Analyze MSBuild Binary Logs with Copilot in VS Code](https://devblogs.microsoft.com/dotnet/msbuild-binlog-analyzer-vscode/)
- [Visual Studio July Update — Copilot SDK Agent](https://devblogs.microsoft.com/visualstudio/visual-studio-july-update-meet-the-new-agent-powered-by-copilot-sdk/)

## 테스트는 작성보다 신뢰 루프가 중요하다

open-source `code-testing-generator`는 .NET, Python, TypeScript/JavaScript, Java, Go, Ruby, Rust, Swift, Kotlin, PowerShell, C++ repository를 조사해 unit test를 생성합니다. integration/E2E/browser/performance test는 현재 범위 밖입니다.

```text
요청 → repository 조사 → language/framework/convention/test command 발견
     → 규모 선택(direct / single pass / iterative)
     → behavior별 test 계획 → 작성 → compile/run
     → assertion·scenario·discovery·full workspace 검증
```

새 test project 하나가 혼자 pass해도 solution/CI command에 포함되지 않으면 보호 효과가 없습니다. agent는 기존 test를 삭제하거나 production code를 바꾸지 않고, 외부 URL·port·정확한 timing에 의존하는 unit test를 피하며 repository의 정상 test command에서 새 test가 발견되는지 확인합니다.

### 유용한 assertion

- `NotNull` 하나만 확인하면 default object를 항상 반환하는 결함을 놓칠 수 있습니다.
- arrange 입력과 business behavior에 맞는 exact/structural result를 확인합니다.
- boundary, error path, side effect, 호출하지 않아야 할 dependency를 검사합니다.
- lightweight mutation 사고법으로 연산자/조건을 틀리게 했을 때 test가 fail하는지 묻습니다.

기사 benchmark에서 specialized agent는 152개 중 140개(92.1%), 같은 model의 stock Copilot은 120개(78.9%)를 완료했습니다. vague prompt 89개에서는 79 대 59, 상세 prompt에서는 61 대 61이었습니다. benchmark는 특정 dataset/model/tool의 관찰이지 모든 repository의 보장이 아닙니다. coverage와 test 수가 비슷해도 reliability가 달라질 수 있다는 점이 핵심입니다.

## `.binlog`는 build의 증거다

MSBuild binary log에는 project, target, task, property, item, diagnostic, timing이 들어갑니다.

```powershell
dotnet build -bl:build.binlog
```

Preview MSBuild Binlog Analyzer for VS Code는 `Microsoft.AITools.BinlogMcp` global-tool MCP server를 자동 설치하고 Copilot의 답을 실제 log에 grounding합니다. VS Code 1.99+, GitHub Copilot, .NET SDK가 필요합니다.

- 실패 원인, slow target/task, critical path 분석
- `/errors`, `/perf`, `/timeline`, `/compare`, `/summary`, `/incremental`, `/buildcheck`
- baseline 대비 timing/diagnostic/property/NuGet version regression
- Build Timeline, project graph, incremental rebuild reason, analyzer timing
- GitHub Actions/Azure DevOps binlog 가져오기
- before/after log로 fix·optimization 검증

`Fix all issues`도 diff review와 clean rebuild를 대체하지 않습니다. binlog에는 property와 command line 등 비밀/경로가 포함될 수 있으므로 공유·업로드 전 redaction과 접근 제어가 필요합니다.

### MSBuild와 CLR

MSBuild는 target dependency graph를 만들고 task를 실행합니다. C# compilation target은 Roslyn compiler server를 사용할 수 있고 test는 별도 `testhost` process/CLR에서 실행될 수 있습니다. build critical path는 단순히 duration이 가장 긴 task 하나가 아니라 dependency상 전체 완료를 지연시키는 경로입니다. incremental build는 input/output timestamp와 item/property 변화로 target skip 여부를 결정하므로 nondeterministic generator나 잘못 선언한 output이 cache를 깨뜨립니다.

## Visual Studio July Agent

Issue 26과 같은 원문이 다시 포함되었습니다. 새 Agent (Preview)는 GitHub Copilot CLI와 같은 Copilot SDK 계열이고, built-in .NET/Azure skills, organization custom instructions, branch attachment, cross-install MSVC toolset discovery를 제공합니다.

- built-in skill도 기본 off이며 내용을 검토하고 필요한 것만 켭니다.
- 조직 custom instruction은 preference이지 강제 security policy가 아닙니다.
- branch 요약은 checkout/실행/merge 권한이 아닙니다.
- `EnableVCToolsVersionDiscovery=true`와 `VCToolsVersion` pin은 다른 설치에서 정확한 MSVC version을 찾지만 OS/SDK/NuGet 전체 재현성을 자동 보장하지 않습니다.

자세한 내용은 [Issue 26 Visual Studio 문서](../dotnet-insider-issue-26/05-visual-studio-agent.md)를 읽으세요.

## 실습

```powershell
dotnet script .\03_TestTrustLoop.csx
```

실습은 pass 여부, mutation 감지, test discovery, full workspace build를 별도 gate로 모델링합니다.

## 다음 단계

- 이전: [MCP C# SDK v2와 원격 Skills](./01-mcp-v2-agent-skills.md)
- 다음: [NuGet 보안과 Target Framework](./03-supply-chain-targeting.md)
- 공식 자료: [dotnet/skills](https://github.com/dotnet/skills), [MSBuild binary log](https://learn.microsoft.com/visualstudio/msbuild/obtaining-build-logs-with-msbuild)
