# 7. 연습문제와 해설

먼저 답을 가리고 자신의 말로 적은 뒤 펼쳐 보세요. 단순 용어 암기보다 “왜 그렇게 설계하는가”를 설명하는 것이 목표입니다.

## A. C#·CLR·JIT

### 문제 1

SDK와 Runtime의 차이, C# 한 메서드가 CPU에서 실행되기까지를 설명하세요.

<details><summary>해설</summary>

SDK는 compiler/build/test 같은 제작 도구를 포함하고 Runtime은 빌드된 앱을 실행합니다. Roslyn이 C#을 IL과 메타데이터로 만들고, 호스트가 Runtime을 선택하고, CLR loader가 어셈블리/형식을 해석하고, JIT가 호출된 IL을 현재 CPU 기계어로 변환합니다.

</details>

### 문제 2

첫 실행보다 두 번째 실행이 빨랐다는 사실만으로 Tier 1/Dynamic PGO가 원인이라고 단정할 수 없는 이유는 무엇인가요?

<details><summary>해설</summary>

JIT 외에도 OS scheduler, CPU frequency, filesystem/page cache, GC, 다른 프로세스가 영향을 줍니다. 여러 iteration, 별도 process, warmup과 통계 처리를 제공하는 BenchmarkDotNet 같은 도구와 JIT 진단 정보가 필요합니다.

</details>

### 문제 3

MAUI가 CoreCLR로 통합되어도 Blazor WebAssembly가 Mono를 유지할 수 있는 이유는 무엇인가요?

<details><summary>해설</summary>

런타임 선택은 플랫폼 제약과 최적화 목표에 따라 다릅니다. 브라우저 WebAssembly sandbox와 배포 모델에는 Mono의 WebAssembly 구현이 맞고, MAUI 모바일/데스크톱 통합이 모든 환경에서 Mono 제거를 뜻하지 않습니다.

</details>

## B. 보안 패치와 현대화

### 문제 4

코드 변경이 없는데 Runtime patch 뒤에 왜 테스트해야 하나요?

<details><summary>해설</summary>

JIT, GC, TLS, networking, parser, ASP.NET Core 같은 기반 구현이 바뀝니다. 앱 소스가 같아도 성능, timing, 호환성, 오류 처리가 달라질 수 있어 빌드·단위·통합·성능·운영 지표를 검증해야 합니다.

</details>

### 문제 5

업그레이드 에이전트에게 “net8에서 net11로 바꿔” 한 문장만 주고 결과를 바로 merge하면 안 되는 이유를 세 가지 쓰세요.

<details><summary>해설</summary>

전이 dependency와 breaking API, 프로젝트 간 순서, 데이터/인증 계약, 빌드 실패 피드백을 평가해야 합니다. 평가→계획→작은 작업→실행→테스트→사람 검토로 나누고 되돌릴 수 있어야 합니다.

</details>

## C. Agent Skills

### 문제 6

progressive disclosure가 토큰 절약 외에 주는 이점은 무엇인가요?

<details><summary>해설</summary>

관련 없는 지침의 충돌과 prompt surface를 줄이고, 선택한 리소스/스크립트만 별도 승인·감사할 수 있습니다. 다만 필터와 승인은 실행 격리를 대체하지 않습니다.

</details>

### 문제 7

`AssemblyLoadContext`에 신뢰하지 않는 plugin을 로드하면 안전한 sandbox가 되나요?

<details><summary>해설</summary>

아닙니다. 로딩/버전/언로드 경계에는 유용하지만 OS 보안 경계가 아닙니다. 같은 프로세스 권한과 자원을 공유합니다. 불신 코드는 별도 저권한 프로세스/컨테이너, 자원·네트워크 제한, 감사가 필요합니다.

</details>

## D. MCP와 평가

### 문제 8

클라이언트 요청이 30초에 timeout됐지만 서버 작업이 5분 뒤 완료됩니다. 이를 오류 없이 표현하는 계약을 설계하세요.

<details><summary>해설</summary>

작업을 먼저 영속 시작하고 opaque task/workflow ID와 `working`, 권장 poll 시간을 빠르게 반환합니다. 별도 get/cancel 또는 MCP Tasks로 terminal 상태와 결과를 조회합니다. ID 소유권, idempotency, 재시도, 취소 의미를 정의합니다.

</details>

### 문제 9

Durable orchestrator 안에서 `DateTime.Now`와 외부 HTTP 호출을 직접 하면 왜 문제가 되나요?

<details><summary>해설</summary>

orchestrator는 이벤트 이력을 replay합니다. replay마다 시간/외부 결과가 달라지면 결정성이 깨집니다. durable context가 제공하는 시간/ID를 쓰고 외부 I/O는 activity로 분리하며 activity는 중복 실행 가능성을 처리합니다.

</details>

### 문제 10

평가용 스킬의 URL을 localhost mock server로 바꾸는 것과 Dev Proxy interception의 차이는 무엇인가요?

<details><summary>해설</summary>

URL을 바꾸면 평가 대상 스킬의 토큰과 동작 조건 자체가 달라집니다. Dev Proxy는 실제 URL을 그대로 둔 채 로컬 네트워크 계층에서 응답을 가로채므로 production side effect 없이 더 충실한 평가가 가능합니다.

</details>

## E. SQL·검색·IDE

### 문제 11

결과 그리드에서 주민번호 열을 숨겼습니다. 보안 조치로 충분한가요?

<details><summary>해설</summary>

아닙니다. 숨김은 표시 상태일 뿐 데이터가 클라이언트에 이미 도착했을 수 있습니다. SELECT projection, 최소 권한, masking, row-level security 등 서버/쿼리 계층에서 제한해야 합니다.

</details>

### 문제 12

`episode 3 scene 12 shot 7, red door`를 그대로 embedding하는 것보다 분해가 나은 이유를 설명하세요.

<details><summary>해설</summary>

episode/scene/shot은 의미 유사도가 아니라 정확히 일치해야 하는 metadata입니다. 이를 strict pre-filter로 쓰고 `red door`만 semantic/vector query로 만들면 잘못된 에피소드의 의미상 비슷한 장면이 상위에 오는 문제를 줄입니다.

</details>

### 문제 13

조직 custom instruction에 “보안 검사를 통과한 코드만 승인하라”고 쓰면 policy gate가 되나요?

<details><summary>해설</summary>

아닙니다. 모델 응답 선호일 뿐 우회 불가능한 집행 장치가 아닙니다. required CI, branch protection, reviewer, 권한, secret scan 같은 서버 측 gate가 필요합니다.

</details>

## F. 코드 변형 실험

1. [02_AgentSkillPipeline.csx](./02_AgentSkillPipeline.csx)의 `currentTenant`를 `tenant-b`로 바꾸고 보이는 스킬을 예상하세요.
2. 승인 `Granted`를 `false`로 바꾸고 runner 결과가 `denied`인지 확인하세요.
3. [03_DurableMcpWorkflow.csx](./03_DurableMcpWorkflow.csx)의 response budget을 300ms로 바꿔 inline 완료 경로를 확인하세요.
4. [04_DevProxyEvaluation.csx](./04_DevProxyEvaluation.csx)에서 seed data에 현재 시간을 넣고 왜 결정성이 깨지는지 설명하세요.
5. [05_QueryDecomposition.csx](./05_QueryDecomposition.csx)에 `EP03-S12-S07` 형식을 지원하는 패턴을 추가하고 기존 입력도 통과하는지 회귀 테스트하세요.
6. [06_OperationsCapstone.csx](./06_OperationsCapstone.csx)의 한 gate를 실패시키고 `Done`이 추가되지 않는지 확인하세요.

## 최종 설명 과제

다음 문장을 5분 안에 비전공자에게 설명해 보세요.

> “Issue 26은 .NET 11의 실행 기반을 개선하는 흐름과, 에이전트가 그 기반 위에서 오래 걸리는 작업·업그레이드·검색·도구 사용을 안전하고 관찰 가능하게 만드는 흐름을 함께 보여 준다.”

설명에 `CLR/JIT`, `CoreCLR/Mono`, `patch`, `skill`, `approval`, `durable task`, `deterministic evaluation`, `metadata filter`, `policy gate`가 모두 들어가면 과정을 완료한 것입니다.
