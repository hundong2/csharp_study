# 3. 장기 실행 MCP 도구와 Dev Proxy 평가

- [Building long-running MCP tools with Azure Functions](https://devblogs.microsoft.com/azure-sdk/long-running-mcp-tools-azure-functions/)
- [How to test agent skills without hitting real APIs](https://developer.microsoft.com/blog/how-to-test-agent-skills-without-hitting-real-apis/)

## 3.1 왜 일반적인 도구 호출이 깨지는가

MCP의 보통 `tools/call`은 요청 하나와 응답 하나입니다. 하지만 미디어 처리, 데이터 채굴, 대규모 빌드는 수분 이상 걸릴 수 있고 클라이언트나 프록시는 흔히 30~60초에 요청을 끊습니다.

중요한 구분:

- **요청 연결 수명**: HTTP/MCP 응답을 얼마나 기다리는가
- **작업 수명**: 실제 계산이 완료될 때까지 얼마나 걸리는가

연결이 끊겨도 작업이 계속될 수 있습니다. 반대로 응답을 받았다고 결과가 영속 저장됐다는 뜻도 아닙니다.

## 3.2 MCP Tasks 확장

기사 시점의 MCP Tasks 확장은 2026-07-28 release candidate였고 생태계 지원은 진행 중이었습니다. client가 capability를 광고하고 server가 Tasks 사용 여부를 결정합니다.

대표 연산:

- `tasks/get`: 현재 상태와 결과를 조회
- `tasks/update`: 작업 정보 갱신
- `tasks/cancel`: 협조적 취소 요청

대표 상태:

```text
working ──> input_required ──> working ──> completed
   │                                  ├──> failed
   └──────────────────────────────────> cancelled
```

상태 전이는 서버의 계약입니다. terminal 상태 이후 다시 `working`으로 돌아가지 않게 하고, 취소는 “요청 접수”와 “실제로 멈춤”을 구분해야 합니다.

## 3.3 Durable Functions 중간 패턴

Tasks 지원이 널리 퍼지기 전 기사 예시는 Durable Functions로 두 도구를 제공합니다.

1. `start_mining`: orchestration을 시작하고 짧은 예산만 기다립니다. 빨리 끝나면 결과, 아니면 `workflow_id`를 반환합니다.
2. `get_mining_result`: ID로 `completed`, `running`, `failed`, `not_found`를 조회하며 `poll_after_seconds` 같은 다음 행동을 알려 줍니다.

ID는 추측 가능한 순번보다 난수성이 충분한 opaque handle이어야 하고 인증 주체/tenant에 바인딩해야 합니다. 에이전트가 ID를 잘못 만들거나 다른 대화의 ID를 재사용할 수 있으므로 서버가 소유권과 형식을 검증해야 합니다. Tasks 확장에서는 SDK가 task handle 수명주기를 관리하는 방향입니다.

### Durable orchestration과 replay

Durable 워크플로는 checkpoint와 이벤트 이력으로 프로세스 재시작 뒤에도 진행을 복구합니다. orchestrator는 이력을 replay할 수 있으므로 결정적이어야 합니다. 현재 시간, 난수, 네트워크, 파일 I/O를 orchestrator에서 직접 수행하지 말고 durable API 또는 activity로 격리합니다. activity는 재시도로 두 번 실행될 수 있다고 가정하고 idempotency key를 사용합니다.

## 3.4 폴링 설계

나쁜 폴링은 100ms마다 무한 조회해 서버와 토큰을 낭비합니다. 응답에 다음 권장 시간을 포함하고 지수 backoff와 jitter를 사용하세요.

```text
delay = min(maxDelay, baseDelay × 2^attempt) + jitter
```

요청에는 timeout과 `CancellationToken`을 전파하되, 클라이언트 취소가 작업 전체 취소인지 단순히 기다림 중단인지 API 계약에 명시해야 합니다.

## 3.5 Agent Skill 평가에서 실제 API를 호출하면 안 되는 이유

원문은 대규모 평가에서 다음 문제를 지적합니다.

- 호출 비용과 rate limit
- write/delete가 운영 데이터를 변경하는 부작용
- 동시 실행이 공유 상태를 바꿔 결과가 비결정적
- 전체 mock server는 관리 비용이 크고 URL을 바꾸면 평가하는 스킬 토큰 자체가 달라짐

Dev Proxy는 스킬에 적힌 **실제 base URL을 유지**한 채 로컬에서 트래픽을 가로채 결정적인 응답 파일을 돌려줍니다. `getAll`, `getOne`, `merge`, `delete` 같은 action과 query를 설정할 수 있고 매 실행 데이터 상태를 초기화할 수 있습니다.

## 3.6 결정적인 테스트 데이터

[04_DevProxyEvaluation.csx](./04_DevProxyEvaluation.csx)는 실제 네트워크 대신 in-memory store를 매 시나리오마다 새로 만듭니다.

- 같은 초기 상태 + 같은 입력 → 같은 출력
- `merge` 후 값 변경을 검증
- `delete` 후 조회 실패를 검증
- 테스트 종료 뒤 새 store가 초기 상태인지 검증

실제 Dev Proxy 설정에서는 URL 패턴을 가능한 좁게 제한하고 HTTPS 인증서 설치 범위, 로그의 토큰/개인정보, 프록시를 끈 뒤 운영 호출로 새지 않는지 확인하세요. 가장 안전한 원칙은 **agent eval을 production API에 실행하지 않는 것**입니다.

## 실습

```powershell
dotnet script .\03_DurableMcpWorkflow.csx
dotnet script .\04_DevProxyEvaluation.csx
```

첫 실습은 `workflow_id`를 받은 뒤 `working`에서 `completed`로 이동합니다. 두 번째는 두 번 실행한 시나리오 결과가 같고 운영 호출 수가 0임을 보여 줍니다.

## 다음 단계

- 이전: [Agent Skills와 현대화 워크플로](./02-agent-skills-modernization.md)
- 다음: [MSSQL 도구와 검색 질의 분해](./04-sql-query-decomposition.md)
- 공식 후속 자료: [MCP Tasks](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/tasks), [Durable Functions 개요](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview), [Dev Proxy](https://learn.microsoft.com/microsoft-cloud/dev/dev-proxy/overview)
