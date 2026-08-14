# 5. Visual Studio July Update: Copilot SDK 기반 Agent

원문: [Visual Studio July update: Meet the new agent powered by Copilot SDK](https://devblogs.microsoft.com/visualstudio/visual-studio-july-update-meet-the-new-agent-powered-by-copilot-sdk/)

## 5.1 Agent (Preview)

Copilot Chat의 agent picker에 GitHub Copilot SDK 기반 새 Agent (Preview)가 추가되었습니다. GitHub Copilot CLI와 같은 SDK 계열을 사용해 CLI, GitHub app, VS Code, Visual Studio 사이의 경험을 일관되게 하고, 응답은 짧게 만들어 실제 변경 검토에 집중하게 합니다.

Preview는 자동으로 신뢰해도 된다는 뜻이 아닙니다. feature, bug fix, refactor에서 다음을 확인하세요.

1. 목표와 완료 조건을 먼저 적습니다.
2. 에이전트가 읽고 바꾼 파일 범위를 확인합니다.
3. 생성된 명령의 외부 효과와 권한을 검토합니다.
4. diff, build, test, 정적 분석, 실행 결과를 직접 확인합니다.
5. 되돌릴 수 있는 작은 커밋으로 보존합니다.

## 5.2 내장 .NET/Azure Skills

.NET/Azure workload가 설치되면 해당 팀 전문가가 만든 built-in skill이 tool picker의 Built-in 범주에 나타납니다. 기본값은 꺼짐이며 description/path/전체 내용을 검토한 뒤 현재 작업에 필요한 것만 켭니다.

이 기능은 [Agent Skills](./02-agent-skills-modernization.md)의 progressive disclosure와 같은 원리입니다. “내장”은 무조건 안전·정답이라는 뜻이 아니라 출처와 기본 제공 경로를 나타냅니다.

## 5.3 조직 수준 custom instructions

GitHub organization owner가 저장소 전체 구성원의 Copilot 응답 선호를 설정할 수 있습니다. 조직 저장소에서 자동 적용되고 상호작용 reference 목록에서 내용을 확인할 수 있으며 옵션에서 끌 수 있습니다.

중요한 경계: 기사는 이를 **preference 설정**으로 설명하며 **policy enforcement**로 쓰지 말라고 합니다. 보안 정책은 branch protection, required review, CI gate, 권한, secret scanning, 서버 측 검증으로 집행해야 합니다. 사용자 지침과 충돌하면 적용 순서·reference를 확인합니다.

## 5.4 브랜치를 Chat 문맥에 첨부

Git Repository 창에서 branch를 우클릭해 `Add to Chat`하면 checkout 전에 요약·비교를 요청할 수 있습니다. branch 문맥은 commit, changes, pull request 문맥을 보완합니다.

- 첨부된 branch가 최신 remote 상태인지 확인합니다.
- 요약은 diff 검토를 대체하지 않습니다.
- 비밀, generated file, 큰 binary가 문맥에 포함되는지 주의합니다.
- “이 branch 설명”과 “이 branch 코드 실행/merge”는 전혀 다른 권한입니다.

## 5.5 MSVC Build Tools 교차 설치 탐색

C++ 팀이 `VCToolsVersion`을 고정했는데 현재 Visual Studio 설치가 아닌 다른 IDE/Build Tools 설치에 그 버전이 있을 수 있습니다. 새 opt-in 탐색은 정확한 pinned version을 모든 설치에서 찾습니다.

```xml
<PropertyGroup>
  <EnableVCToolsVersionDiscovery>true</EnableVCToolsVersionDiscovery>
  <VCToolsVersion>14.43.34604</VCToolsVersion>
</PropertyGroup>
```

- `PropertyGroup`: MSBuild 속성 묶음입니다.
- 첫 속성: 다른 설치까지 정확한 toolset 탐색을 허용합니다.
- 둘째 속성: 요구하는 compiler toolset 버전을 고정합니다.

과거에는 현재 설치에 대상 platform toolset(예: v143)이 하나라도 있으면 다른 설치 탐색을 멈춰, 정확히 고정한 버전이 다른 곳에 있어도 찾지 못할 수 있었습니다. 새 동작은 정확한 버전까지 계속 찾으므로 팀 빌드 재현성이 좋아집니다. 다만 SDK, workload, NuGet lock, 환경 변수, OS까지 자동으로 고정하는 기능은 아닙니다.

## 5.6 Agent가 CLR/JIT를 바꾸는가

에이전트가 생성한 C#도 일반 C#과 똑같이 Roslyn→IL→CLR→JIT를 거칩니다. 모델이 작성했다는 출처는 런타임 최적화에 표시되지 않습니다. 성능은 생성된 코드의 할당, virtual dispatch, async, exception, vectorization 가능성 등에 의해 결정됩니다.

에이전트가 빌드/테스트를 실행하면 별도 `dotnet`, compiler server, testhost 프로세스가 만들어질 수 있습니다. 프로세스별 CLR과 GC 힙을 가지므로 IDE 메모리와 testhost 메모리를 구분해 진단하세요.

## 실습

[06_OperationsCapstone.csx](./06_OperationsCapstone.csx)는 조직 지침을 선호로만 적용하고, 필수 정책 gate와 현대화 단계·build 결과를 분리하는 작은 파이프라인입니다.

```powershell
dotnet script .\06_OperationsCapstone.csx
```

## 다음 단계

- 이전: [MSSQL 도구와 검색 질의 분해](./04-sql-query-decomposition.md)
- 다음: [링크 커버리지](./06-link-coverage.md), [연습문제와 해설](./07-exercises.md)
