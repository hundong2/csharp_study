# 2. Agent Skills와 .NET 현대화 워크플로

두 원문은 “에이전트에게 지식을 어떻게 공급하는가”와 “큰 변경을 어떻게 검토 가능한 단계로 나누는가”를 설명합니다.

- [Agent Skills for .NET is now released](https://devblogs.microsoft.com/agent-framework/agent-skills-for-net-is-now-released/)
- [Modernize .NET in the GitHub Copilot app](https://devblogs.microsoft.com/dotnet/modernize-dotnet-in-github-copilot-app/)

## 2.1 Agent, tool, skill의 차이

| 개념 | 역할 | 예 |
|---|---|---|
| Agent | 목표를 해석하고 다음 행동을 선택 | 업그레이드 에이전트 |
| Tool | 호출 가능한 원자 기능과 입력/출력 계약 | 파일 읽기, 빌드 실행 |
| Skill | 설명·지침·리소스·선택적 스크립트를 묶은 재사용 지식 | 회사 배포 절차 |

Skill은 모델을 재학습하는 기능이 아닙니다. Agent Skills 기사에서 API는 안정화되어 `[Experimental]` 특성이 제거되었습니다. 스킬은 이름/설명 같은 메타데이터와 `SKILL.md` 또는 코드 지침, 선택적 스크립트·참조 자료로 구성됩니다.

## 2.2 점진적 공개

모든 스킬 본문을 처음부터 문맥에 넣으면 토큰을 낭비하고 관련 없는 지침끼리 충돌합니다.

```text
1. 스킬 이름·한 줄 설명만 광고
2. 에이전트가 관련 스킬 선택
3. 선택한 스킬 지침 로드
4. 필요한 참조 파일만 읽기
5. 필요하고 승인된 스크립트만 실행
```

이것이 progressive disclosure입니다. [02_AgentSkillPipeline.csx](./02_AgentSkillPipeline.csx)는 설명 기반 선택, tenant 필터, 캐시, 승인, 실행기를 메모리 안의 안전한 모형으로 구현합니다.

## 2.3 세 가지 작성 방식

| 방식 | 적합한 경우 | 실행·배포 특성 |
|---|---|---|
| 파일 기반 | 비개발자 편집, 여러 언어/도구 공유, 저장소 관리 | `SKILL.md`, scripts, references; 스크립트 실행기를 소유자가 제공 |
| 클래스 기반 | C# 패키지와 내부 NuGet으로 강한 형식 재사용 | 클래스/코드가 프로세스 안에서 실행될 수 있음 |
| 코드 정의 | 런타임 생성, 앱 상태를 클로저로 캡처 | 동적이지만 수명·동시성·권한 관리 필요 |

여러 팀은 공유 저장소나 내부 NuGet에서 스킬을 독립적으로 배포·조합할 수 있습니다. 에이전트는 설명을 근거로 선택하므로 설명은 “무엇을 할 수 있고 언제 쓰는가”를 구체적으로 써야 합니다.

## 2.4 운영 안전장치

기사의 생산 API는 다음을 강조합니다.

- `load_skill`, `read_skill_resource`, `run_skill_script`는 기본적으로 사람 승인을 요구합니다.
- agent/tenant별 predicate로 노출 가능한 스킬을 필터링합니다.
- 해석한 스킬을 한 번 캐시하며 필요하면 tenant 키로 격리합니다.
- 공개된 source pipeline을 확장해 자체 registry를 연결할 수 있습니다.
- 클래스/코드 스킬은 in-process일 수 있고 파일 스크립트는 위임된 runner가 실행합니다.

**승인**은 **샌드박스**가 아닙니다. 사용자가 승인해도 스크립트에는 최소 권한, CPU/시간/메모리 제한, 네트워크·파일 격리, 입력 검증, 감사 로그가 필요합니다. 스킬 내용을 코드처럼 리뷰하세요.

### CLR 관점

- in-process C# 스킬은 같은 CLR, GC 힙, 스레드 풀, 환경 변수, 권한을 공유합니다. 무한 루프·메모리 누수·`Environment.Exit` 같은 동작이 호스트 전체에 영향을 줄 수 있습니다.
- 별도 프로세스 runner는 주소 공간과 실패를 더 잘 격리하지만 IPC 직렬화, 시작 비용, OS 권한 설계가 필요합니다.
- `AssemblyLoadContext`는 플러그인 어셈블리 로딩·언로드에 유용하지만 보안 경계가 아닙니다.
- 동적 코드와 reflection은 trimming/NativeAOT에서 별도 메타데이터 보존이 필요할 수 있습니다.

## 2.5 .NET Agent Skills 구성 흐름

기사의 개념적 사용 흐름은 다음과 같습니다. 정확한 패키지/API 버전은 공식 문서를 확인하세요.

```csharp
// 개념 예시: 실제 패키지 버전에 따라 이름과 생성자가 달라질 수 있다.
var provider = new AgentSkillsProvider(skillPath, SubprocessScriptRunner.RunAsync);
var agent = new AIAgent(/* model, tools, AIContextProviders = [provider] */);
var result = await agent.RunAsync("우리 서비스 업그레이드 절차를 찾아줘");
```

Provider가 스킬 목록·지침·리소스·스크립트 도구를 AI 문맥에 공급하고, runner가 파일 기반 스크립트 실행 정책을 구현합니다. 경로를 받았다고 무조건 실행하지 말고 canonical path가 허용된 루트 안인지 확인해야 합니다.

## 2.6 현대화는 한 번의 프롬프트가 아니다

GitHub Copilot upgrade의 순서는 다음과 같습니다.

1. **평가**: 대상 .NET, NuGet 의존성, breaking API, 프로젝트 관계를 수집합니다.
2. **계획**: 독립 업그레이드 가능 여부와 순서를 반영한 구조화 계획을 만듭니다.
3. **작업화**: 검토·실행 가능한 작은 implementation task로 나눕니다.
4. **실행**: 코드 변환, 패키지 업데이트, 빌드를 수행합니다.
5. **피드백**: 빌드 실패와 새 정보를 계획에 되돌립니다.
6. **검증**: 테스트·변경 검토·최종 결과를 확인합니다.

GitHub Copilot 앱의 upgrade canvas는 assessment, plan, tasks, 진행, 코드 변경, 빌드 실패, 결과를 한 화면에 표시합니다. 같은 upgrade workflow는 Visual Studio의 `Modernize`, VS Code upgrade extension, Copilot CLI plugin에서도 제공됩니다.

### 사람이 반드시 확인할 것

- 지원 종료 프레임워크와 목표 버전 선택
- 데이터베이스/인증/암호화/직렬화의 의미 변화
- 간접·전이 NuGet 의존성, package lock, source 신뢰
- nullable/async 변경이 런타임 계약을 바꾸는지
- 빌드 성공과 사용자 동작 성공을 혼동하지 않았는지
- 커밋을 작게 나눴고 되돌릴 수 있는지

## 실습

```powershell
dotnet script .\02_AgentSkillPipeline.csx
```

정상 출력은 tenant A가 `upgrade-dotnet`만 보고, 승인 뒤 스킬을 한 번 로드하고, 두 번째 로드에서 캐시를 사용하는 흐름을 포함합니다.

## 다음 단계

- 이전: [.NET 11, CLR/JIT, MAUI, 서비스 업데이트](./01-dotnet11-runtime-servicing.md)
- 다음: [장기 실행 MCP와 Dev Proxy](./03-durable-mcp-devproxy.md)
- 공식 후속 자료: [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/), [Agent Skills 표준](https://agentskills.io/)
