# .NET Insider Issue 26 완전 학습 가이드

[The .NET Insider - Issue 26](https://dotnet.news/p/the-net-insider-issue-26)(2026-08-03)에 실린 **10개 편집 링크**를 초보자도 순서대로 학습할 수 있도록 재구성한 실습 자료입니다. C# 최소 문법에서 시작해 CLR/JIT, .NET 11, MAUI 런타임, 보안 패치, Agent Skills, Durable MCP, Dev Proxy, SQL 도구, 검색 질의 분해, Visual Studio 에이전트까지 이어집니다.

> 이 폴더에서 말하는 “모든 링크”는 뉴스레터 본문의 기술 카드 10개입니다. 로그인·구독·소셜·개인정보·사이트 탐색 링크는 학습 주제가 아니므로 제외했습니다. 10개 링크와 자료의 대응은 [링크 커버리지 표](./06-link-coverage.md)에 있습니다.

## 먼저 알아둘 점

- 기사들은 2026년 Preview 또는 당시 제품 상태를 설명합니다. 운영 환경에서는 현재 공식 문서와 지원 정책을 다시 확인하세요.
- CSX는 개념을 안전하게 재현하는 **로컬 모형**입니다. Azure, GitHub Copilot, SQL Server, 모바일 기기 또는 실제 API를 변경하지 않습니다.
- .NET 11 Preview 6의 62개 세부 링크는 이미 저장소의 [.NET 11 Preview 6 완전 학습 가이드](../dotnet-11-preview-6/README.md)에 문서·실습·커버리지 표로 정리되어 있어 그 자료를 필수 선수 과정으로 연결합니다.

## 15분 빠른 시작

```powershell
cd dotnet-insider-issue-26
dotnet script 00_BeginnerSyntax.csx
dotnet script 01_ClrJitRuntime.csx
dotnet script 02_AgentSkillPipeline.csx
dotnet script 03_DurableMcpWorkflow.csx
dotnet script 04_DevProxyEvaluation.csx
dotnet script 05_QueryDecomposition.csx
dotnet script 06_OperationsCapstone.csx
```

`dotnet script` 명령이 없다면 [.NET 11 자료의 설치 안내](../dotnet-11-preview-6/00-getting-started.md)를 먼저 읽으세요.

## 권장 학습 순서

| 단계 | 읽을 문서 | 실행할 실습 | 도달 목표 |
|---:|---|---|---|
| 0 | [처음 시작하기](./00-start-here.md) | [00_BeginnerSyntax.csx](./00_BeginnerSyntax.csx) | 값·변수·조건·반복·함수·컬렉션·비동기를 읽는다 |
| 1 | [.NET 11, CLR/JIT, MAUI, 서비스 업데이트](./01-dotnet11-runtime-servicing.md) | [01_ClrJitRuntime.csx](./01_ClrJitRuntime.csx) | C#→IL→JIT→CPU와 CoreCLR·Mono·NativeAOT 차이를 설명한다 |
| 2 | [Agent Skills와 현대화 워크플로](./02-agent-skills-modernization.md) | [02_AgentSkillPipeline.csx](./02_AgentSkillPipeline.csx) | 점진적 공개·승인·필터·캐시와 평가→계획→실행을 구현한다 |
| 3 | [장기 실행 MCP와 Dev Proxy](./03-durable-mcp-devproxy.md) | [03_DurableMcpWorkflow.csx](./03_DurableMcpWorkflow.csx), [04_DevProxyEvaluation.csx](./04_DevProxyEvaluation.csx) | 타임아웃과 작업 수명을 분리하고 결정적인 평가를 만든다 |
| 4 | [MSSQL 도구와 검색 질의 분해](./04-sql-query-decomposition.md) | [05_QueryDecomposition.csx](./05_QueryDecomposition.csx) | 메타데이터 필터와 의미 검색을 분리한다 |
| 5 | [Visual Studio 에이전트 업데이트](./05-visual-studio-agent.md) | [06_OperationsCapstone.csx](./06_OperationsCapstone.csx) | 에이전트·스킬·조직 지침·브랜치 문맥·재현 빌드를 구분한다 |
| 6 | [링크 커버리지](./06-link-coverage.md), [연습문제와 해설](./07-exercises.md) | 전체 다시 실행 | 10개 원문을 자신의 말로 연결한다 |

## 전체 그림

```text
C# 소스 ──Roslyn──> IL + 메타데이터 ──CLR 로더──> JIT 기계어 ──> CPU
                                      │
                                      ├─ GC / 예외 / 스레드 풀 / async
                                      ├─ CoreCLR 또는 Mono / NativeAOT
                                      └─ 앱, 에이전트, MCP 도구, SQL 클라이언트

사용자 목표 ──> 평가 ──> 계획 ──> 승인 ──> 실행 ──> 관찰 ──> 검증
                 │                 │        │
                 └─ 현대화         ├─ Skill ├─ Durable workflow
                                   └─ 정책  └─ Dev Proxy / 테스트 데이터
```

## 완료 체크리스트

- [ ] SDK와 Runtime, CLR과 JIT의 차이를 설명할 수 있다.
- [ ] Tier 0/Tier 1, Dynamic PGO, OSR, GC safe point를 설명할 수 있다.
- [ ] MAUI에서 CoreCLR로 통합하는 이유와 Blazor WebAssembly가 Mono를 유지하는 이유를 안다.
- [ ] 서비스 업데이트를 기능 업그레이드와 혼동하지 않고 패치·검증 절차를 세운다.
- [ ] Agent Skill의 세 가지 작성 방식과 승인·필터·캐시·스크립트 격리를 설명한다.
- [ ] 장기 실행 MCP 작업에서 요청 타임아웃과 작업 상태를 분리한다.
- [ ] Dev Proxy로 운영 API를 호출하지 않는 결정적 평가를 구성하는 이유를 안다.
- [ ] SQL 빠른 쿼리, 결과 그리드 상태, 질의 분해의 역할을 설명한다.
- [ ] Visual Studio 에이전트, 조직 지침, 브랜치 문맥, MSVC 버전 탐색을 구분한다.
- [ ] [링크 커버리지 표](./06-link-coverage.md)의 10개 항목을 모두 확인했다.

## 초보자 관점 3회 검토 기록

| 회차 | 처음 읽을 때 막힐 지점 | 반영한 개선 |
|---:|---|---|
| 1 | SDK/Runtime, 프로세스/스레드, API/JSON 같은 선행 용어가 생소함 | [용어 사전과 최소 문법](./00-start-here.md), 첫 CSX를 추가 |
| 2 | 기사 속 제품 기능과 로컬에서 실행되는 코드 모형의 경계가 불분명함 | 각 문서에 “실제 제품/실습 모형” 구분과 정상 출력·변형 실험을 추가 |
| 3 | 코드가 실행되어도 CLR/JIT 내부에서 왜 그렇게 동작하는지 연결하기 어려움 | 각 CSX의 번호 주석, CLR 관찰 메모, 메모리·비동기·정규식 JIT 설명과 복습 질문을 추가 |

## 원문

- [The .NET Insider - Issue 26](https://dotnet.news/p/the-net-insider-issue-26)
- 각 기술 원문과 공식 후속 학습 링크는 [06-link-coverage.md](./06-link-coverage.md)에 모았습니다.
