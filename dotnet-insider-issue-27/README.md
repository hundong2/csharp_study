# .NET Insider Issue 27 완전 학습 가이드

[The .NET Insider - Issue 27](https://dotnet.news/p/the-net-insider-issue-27)(2026-08-17)의 **기술 콘텐츠 카드 15개**를 C# 초보자도 순서대로 익힐 수 있게 재구성했습니다. MCP C# SDK v2.0, Agent Skills, 테스트·빌드 에이전트, NuGet 공급망, 데이터베이스 성능, Unit of Work, 로컬 음성 AI, C# record, Fetch Metadata, 멀티테넌시와 .NET 11 JIT 성능을 다룹니다.

> “모든 링크”는 뉴스레터 본문의 편집 기술 카드 15개를 뜻합니다. 로그인·구독·작성자·소셜·개인정보·사이트 탐색 링크는 제외했습니다. 빠진 항목이 없는지는 [15개 링크 커버리지 표](./07-link-coverage.md)에서 확인할 수 있습니다.

## 📌 빠른 탐색

| 단계 | 문서 | 실행 실습 | 학습 결과 |
|---:|---|---|---|
| 0 | [처음 시작하기](./00-start-here.md) | [00_BeginnerSyntax.csx](./00_BeginnerSyntax.csx) | C# 값·record·컬렉션·비동기를 읽는다 |
| 1 | [MCP C# SDK v2와 원격 Skills](./01-mcp-v2-agent-skills.md) | [01_StatelessMcpMrtr.csx](./01_StatelessMcpMrtr.csx), [02_RemoteSkillGuardrails.csx](./02_RemoteSkillGuardrails.csx) | stateless HTTP, MRTR, 원격 스킬 경계를 설명한다 |
| 2 | [테스트·Binlog·Visual Studio Agent](./02-testing-build-agents.md) | [03_TestTrustLoop.csx](./03_TestTrustLoop.csx) | 테스트 신뢰 루프와 실제 빌드 증거를 구분한다 |
| 3 | [NuGet 보안과 Target Framework](./03-supply-chain-targeting.md) | [04_SupplyChainPolicy.csx](./04_SupplyChainPolicy.csx) | OIDC, 짧은 자격 증명, TFM fallback을 판단한다 |
| 4 | [SQL·DocumentDB·UoW·멀티테넌시](./04-data-access-performance.md) | [05_DataTransactionAndIndex.csx](./05_DataTransactionAndIndex.csx) | 트랜잭션 경계와 index 설계를 연결한다 |
| 5 | [Foundry Local 실시간 음성](./05-local-speech-ai.md) | [06_BoundedAudioPipeline.csx](./06_BoundedAudioPipeline.csx) | bounded channel, backpressure, async stream을 이해한다 |
| 6 | [record·Fetch Metadata·.NET 11 성능](./06-csharp-security-performance.md) | [07_RecordSecurityJit.csx](./07_RecordSecurityJit.csx) | 얕은 복사, 요청 출처, boxing/SIMD/JIT를 설명한다 |
| 7 | [링크 커버리지](./07-link-coverage.md), [연습문제](./08-exercises.md) | 전체 재실행 | 15개 원문을 하나의 시스템으로 연결한다 |

## 20분 빠른 실행

```powershell
cd dotnet-insider-issue-27
dotnet script 00_BeginnerSyntax.csx
dotnet script 01_StatelessMcpMrtr.csx
dotnet script 02_RemoteSkillGuardrails.csx
dotnet script 03_TestTrustLoop.csx
dotnet script 04_SupplyChainPolicy.csx
dotnet script 05_DataTransactionAndIndex.csx
dotnet script 06_BoundedAudioPipeline.csx
dotnet script 07_RecordSecurityJit.csx
```

모든 CSX는 외부 API, DB, 마이크, NuGet.org를 변경하지 않는 로컬 모형이며 .NET 10에서도 실행됩니다. 기사 속 Preview API는 문서의 별도 코드 블록으로 구분했습니다. 설치가 필요하면 [.NET/CSX 시작 안내](../dotnet-11-preview-6/00-getting-started.md)를 먼저 읽으세요.

## 🧭 전체 학습 지도

```text
요청 ──> stateless MCP ──> tool ──> input_required ──> 재요청 ──> 결과
                │                     │
                ├─ 원격 Skill 발견    └─ 사용자 승인·sampling·roots
                └─ HTTP routing/WAF/관찰성

코드 변경 ──> 저장소 조사 ──> 테스트 계획 ──> assertion ──> 전체 빌드/binlog 검증

패키지 ──OIDC──> 짧은 자격 증명 ──> NuGet 게시
데이터 ──transaction boundary / index / tenant schema──> 일관성과 성능
오디오 ──bounded Channel──> 로컬 모델 ──async stream──> interim/final text
C# ──Roslyn──> IL ──CoreCLR/RyuJIT──> boxing 제거·범위 검사 제거·SIMD
```

## ✅ 완료 체크리스트

- [ ] stateless protocol과 stateless application이 다른 이유를 설명한다.
- [ ] MRTR의 `input_required`, `requestState`, `inputResponses` 흐름을 안다.
- [ ] 원격 archive skill에 크기·압축 해제·파일 수 제한이 필요한 이유를 안다.
- [ ] “통과하는 테스트”와 “결함을 잡는 테스트”를 구분한다.
- [ ] `.binlog`의 target/task/property/diagnostic과 critical path를 설명한다.
- [ ] NuGet Trusted Publishing이 장기 API key보다 안전한 이유를 설명한다.
- [ ] TFM과 assembly version, NuGet asset fallback을 구분한다.
- [ ] Unit of Work가 repository 모음이 아니라 transaction boundary임을 안다.
- [ ] sargable query, execution plan, ESR compound index, covered query를 설명한다.
- [ ] EF Core schema-per-tenant에서 model cache key에 schema가 필요한 이유를 안다.
- [ ] bounded channel이 실시간 오디오의 backpressure를 만드는 방식을 안다.
- [ ] record의 `with`가 기본적으로 shallow copy임을 안다.
- [ ] Fetch Metadata 4개 header와 CSRF 방어의 관계를 설명한다.
- [ ] .NET 11 성능 수치를 자신의 workload에 그대로 적용하면 안 되는 이유를 안다.
- [ ] [커버리지 표](./07-link-coverage.md)의 15개 항목을 모두 확인했다.

## 🔁 초보자 관점 3회 검토 기록

| 회차 | 발견한 이해 장벽 | 반영한 개선 |
|---:|---|---|
| 1 | protocol/session, TFM/OIDC, transaction/index 같은 선행 용어가 많음 | [용어 사전](./00-start-here.md)과 최소 문법 실습 추가 |
| 2 | 기사 API는 외부 패키지·서비스가 필요해 그대로 실행하기 어려움 | 제품 코드와 로컬 모형을 구분하고 각 CSX에 기대 출력·변형 실험 추가 |
| 3 | 실행 결과만으로 CLR/JIT·GC·비동기 내부 동작과 보안 경계를 연결하기 어려움 | 모든 의미 있는 코드 문장에 번호 주석, 각 파일 끝에 CLR/JIT 관찰 메모 추가 |

## 🔗 원문과 이전 과정

> 🔗 [The .NET Insider - Issue 27](https://dotnet.news/p/the-net-insider-issue-27)

- Issue 27에도 다시 실린 Visual Studio July Update는 [Issue 26의 상세 문서](../dotnet-insider-issue-26/05-visual-studio-agent.md)와 연결했습니다.
- .NET 11 Preview 6의 전체 기능은 [.NET 11 전용 과정](../dotnet-11-preview-6/README.md)에서 이어서 학습하세요.
