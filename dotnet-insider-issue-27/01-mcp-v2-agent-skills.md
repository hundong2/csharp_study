# 1. MCP C# SDK v2.0과 원격 Agent Skills

원문:

- [Announcing v2.0 of the official MCP C# SDK](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/)
- [Discover Agent Skills from MCP servers in .NET](https://devblogs.microsoft.com/agent-framework/discover-agent-skills-from-mcp-servers-in-net/)

## v2.0의 가장 큰 변화

MCP C# SDK v2.0은 2026-07-28 MCP specification을 구현합니다. 과거 Streamable HTTP는 `initialize`/`initialized` handshake 뒤 서버가 발급한 `Mcp-Session-Id`를 매 요청에 보내야 했습니다. 특정 instance에 붙는 sticky routing 또는 공유 session store가 필요했습니다.

새 wire format은 기본적으로 다음을 사용합니다.

```text
각 HTTP POST = protocol version + capabilities + JSON-RPC request
                ├─ Mcp-Method: tools/call
                ├─ Mcp-Name: get_order_status
                └─ 선택한 Mcp-Param-* header
```

`HttpServerTransportOptions.Stateless`의 기본값이 `true`이고 handshake와 protocol-level session requirement가 사라졌습니다. 아무 instance나 요청을 처리할 수 있어 round-robin, serverless, edge, container 수평 확장이 쉬워집니다.

**stateless protocol은 stateless application이 아닙니다.** 장바구니나 browser 작업 상태가 필요하면 한 tool이 명시적 `basketId` 같은 handle을 만들고 다음 tool argument로 전달합니다. 숨은 transport state 대신 인증·tenant와 결합된 명시적 application state를 사용합니다.

## HTTP header routing

v2는 method/name과 선택 parameter를 header에 mirror합니다. `[McpHeader("Region")]`은 schema에 `x-mcp-header`를 알리고 client가 `Mcp-Param-Region`을 보냅니다. proxy, WAF, gateway가 JSON body를 parsing하지 않고 routing·rate limit·관찰을 할 수 있습니다.

- JSON-RPC body가 authoritative source입니다.
- header와 body가 다르면 server는 `HeaderMismatch`로 거부합니다.
- non-ASCII 값은 안전한 Base64 sentinel로 encoding합니다.
- header 승격은 민감 값을 log에 노출할 수 있으므로 비밀 parameter에는 사용하지 않습니다.

## Multi Round-Trip Requests(MRTR)

stateless server는 session channel로 client에 elicitation, sampling, roots 요청을 밀어낼 수 없습니다. MRTR은 server가 `InputRequiredResult`를 반환하고 client가 필요한 입력을 모아 같은 `tools/call`을 다시 보내도록 바꿉니다.

```text
tools/call(ticketId=123)
  ← input_required(inputs, opaque requestState)
사용자 확인/LLM sampling/roots 조회
tools/call(ticketId=123, inputResponses, 같은 requestState)
  ← completed result
```

server tool은 `InputRequiredException`과 `InputRequest.ForElicitation`, `ForSampling`, `ForRootsList`를 사용합니다. client `McpClient`는 handler가 있으면 왕복을 자동 해결합니다. 민감한 OAuth/동의는 `UrlElicitationRequiredException`으로 server-hosted URL에서 out-of-band로 처리합니다.

`requestState`는 client가 해석하거나 수정하지 않는 opaque data지만 **서버가 신뢰해도 되는 증명은 아닙니다**. 서명·만료·tenant binding 또는 server-side storage로 위조와 replay를 검증해야 합니다.

## 호환성과 package 구조

- 안정된 v1 API는 v2에서도 compile/run합니다.
- v2 client/server는 이전 handshake로 자동 fallback합니다.
- 경고: `MCP9004` legacy SSE, `MCP9005` Sampling/Roots/Logging, `MCP9006` stateful-only option.
- 예외: v2 Tasks(SEP-2663)는 v1 experimental Tasks와 wire/API 호환되지 않습니다.

| package | 역할 |
|---|---|
| `ModelContextProtocol.Core` | client와 low-level server, analyzer 포함 |
| `ModelContextProtocol` | stdio, hosting/DI, attribute tool discovery |
| `ModelContextProtocol.AspNetCore` | Streamable HTTP server |
| `ModelContextProtocol.Extensions.Tasks` | 장기 작업·polling·pluggable persistence |
| `ModelContextProtocol.Extensions.Apps` | interactive server UI, experimental |

대상은 `net8.0`, `net9.0`, `net10.0`, .NET Framework용 `netstandard2.0`입니다. in-memory task store는 개발용이며 process restart/여러 instance를 견디려면 durable shared `IMcpTaskStore`가 필요합니다.

## MCP에서 Agent Skills 발견

`Microsoft.Agents.AI.Mcp`의 experimental API는 `skill://index.json` discovery document에서 광고를 읽고 authenticated MCP connection으로 내용을 가져옵니다.

| 배포 형식 | 동작 | 보안 경계 |
|---|---|---|
| `skill-md` | `SKILL.md`와 sibling resource를 필요할 때 파일별 조회 | resource path·content 검증 |
| `archive` | ZIP/TAR/tar.gz를 받아 controlled directory에 해제 | download·uncompressed size·file count 제한 |

archive에는 decompression bomb, path traversal, disk/memory/CPU exhaustion 위험이 있습니다. `ArchiveMaxSizeBytes`, `ArchiveMaxUncompressedSizeBytes`, `ArchiveMaxFileCount`, extraction root 검증을 적용합니다. **원격 archive에 포함된 script는 실행되지 않습니다.** 로드·resource 읽기·local script 실행도 기본 human approval과 최소 권한을 유지합니다.

중앙 server는 policy/runbook을 한 번 배포하고 여러 agent가 재배포 없이 다음 discovery부터 업데이트를 받게 합니다. 이는 일관성을 높이지만 server compromise가 전체 agent에 퍼지는 blast radius도 키우므로 TLS/auth, version pin, signature/hash, audit, staged rollout이 필요합니다.

## CLR/JIT 내부 관점

- ASP.NET Core request는 thread pool에서 시작하지만 socket I/O `await` 중 thread를 반납합니다.
- JSON deserialization은 object/array/string을 heap에 할당하고 GC pressure를 만듭니다. streaming과 size limit이 중요합니다.
- stateless는 application object allocation이 0이라는 뜻이 아닙니다. request마다 context와 parsed payload가 생성됩니다.
- reflection 기반 tool discovery는 metadata를 읽으며 trimming/NativeAOT에서 보존 annotation이 필요할 수 있습니다.
- archive extraction을 별도 process로 격리하면 host CLR과 address space를 공유하지 않지만 IPC/시작 비용이 생깁니다.

## 실습

```powershell
dotnet script .\01_StatelessMcpMrtr.csx
dotnet script .\02_RemoteSkillGuardrails.csx
```

첫 실습은 session 없이 `input_required`→재요청을 수행합니다. 둘째는 원격 skill archive의 크기·압축 해제 크기·파일 수·script를 검증합니다.

## 다음 단계

- 이전: [처음 시작하기](./00-start-here.md)
- 다음: [테스트·Binlog·Visual Studio Agent](./02-testing-build-agents.md)
- 공식 후속 자료: [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), [MCP specification](https://modelcontextprotocol.io/specification/)
