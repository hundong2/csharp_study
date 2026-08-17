# 8. 연습문제와 해설

답을 펼치기 전에 자신의 말로 설명하세요.

## MCP와 Skills

### 1. stateless MCP server가 장바구니 상태를 가질 수 있나요?

<details><summary>해설</summary>
가능합니다. protocol transport가 숨은 session을 요구하지 않는다는 뜻이지 application state 금지가 아닙니다. 명시적 basket ID를 argument로 전달하고 durable store에서 인증·tenant와 함께 조회합니다.
</details>

### 2. MRTR에서 `requestState`를 그대로 돌려받았으니 신뢰해도 되나요?

<details><summary>해설</summary>
아닙니다. opaque는 client가 해석하지 않는다는 뜻입니다. server는 서명/만료/tenant binding 또는 server-side handle lookup으로 위조와 replay를 검증해야 합니다.
</details>

### 3. 50KB ZIP이므로 안전하다고 할 수 없는 이유는?

<details><summary>해설</summary>
압축 해제 뒤 수 GB가 되거나 파일 수가 과도하고 `../` path가 있을 수 있습니다. download size, uncompressed size, file count, canonical extraction path를 모두 제한하고 remote script를 실행하지 않습니다.
</details>

## 테스트와 빌드

### 4. 새 test project가 혼자 pass했지만 CI에서 발견되지 않습니다. 완료인가요?

<details><summary>해설</summary>
아닙니다. solution/repository의 정상 test command와 full workspace build가 새 test를 발견해야 합니다. test discovery도 품질 gate입니다.
</details>

### 5. line coverage 100%면 assertion이 강하다고 할 수 있나요?

<details><summary>해설</summary>
아닙니다. 코드를 실행했다는 신호일 뿐 결과 의미를 검사했는지는 모릅니다. 작은 mutation이 test를 실패시키는지, error/boundary behavior를 확인하는지 봅니다.
</details>

### 6. binlog 기반 답이 일반 chat 추측보다 나은 이유는?

<details><summary>해설</summary>
실제 target/task/property/diagnostic/timing에 grounding되므로 원인과 critical path를 증거로 설명할 수 있습니다. 그래도 자동 fix 뒤 before/after build와 diff 검토가 필요합니다.
</details>

## 공급망과 target

### 7. API key를 30일로 줄이면 Trusted Publishing과 같나요?

<details><summary>해설</summary>
아닙니다. 노출 창만 줄입니다. Trusted Publishing은 repository/workflow identity를 OIDC로 검증하고 publish마다 짧은 credential을 발급해 reusable secret 저장을 없앱니다.
</details>

### 8. package에 `net8.0` asset이 없어도 `netstandard2.0`으로 실행되면 아무 손실이 없나요?

<details><summary>해설</summary>
기능상 compatible할 수 있지만 최신 TFM 전용 API, NativeAOT support, performance optimization을 잃을 수 있습니다. 이전 package minor 고정은 새 update를 못 받는 tradeoff가 있습니다.
</details>

## 데이터

### 9. EF Core에서 generic repository를 모은 class가 반드시 UoW인가요?

<details><summary>해설</summary>
아닙니다. 핵심은 관련 변경이 같은 transaction에서 한 번 commit되는 경계입니다. DbContext가 이미 tracking과 SaveChanges UoW를 제공합니다.
</details>

### 10. `WHERE YEAR(order_date)=2026`이 느릴 수 있는 이유는?

<details><summary>해설</summary>
column마다 함수를 계산해 raw column B-tree index를 바로 seek하지 못할 수 있습니다. `>= 2026-01-01 AND < 2027-01-01` 범위로 바꾸고 plan을 확인합니다.
</details>

### 11. ESR index 뒤 `SORT`와 `FETCH`가 사라졌다는 의미는?

<details><summary>해설</summary>
index order가 sort를 만족하고 projection까지 index field만 사용한 covered query라는 뜻일 수 있습니다. 실제 `totalKeysExamined/nReturned`와 write 비용도 확인합니다.
</details>

### 12. `HasDefaultSchema(tenant)`만 동적으로 부르면 왜 첫 tenant가 재사용될 수 있나요?

<details><summary>해설</summary>
EF Core model이 DbContext type 기준으로 cache되기 때문입니다. `IModelCacheKeyFactory` key에 schema와 designTime을 포함해야 tenant마다 model이 분리됩니다.
</details>

## AI·C#·보안·JIT

### 13. audio callback에서 `AppendAsync`를 fire-and-forget하면 어떤 문제가 생기나요?

<details><summary>해설</summary>
consumer보다 capture가 빠르면 Task/chunk가 무한히 쌓여 memory/latency가 증가하고 exception·순서·cleanup을 잃습니다. bounded Channel과 명시적 full mode로 backpressure를 처리합니다.
</details>

### 14. `record with { }`가 deep copy가 아닌 예를 드세요.

<details><summary>해설</summary>
record 속성이 `List<string>`이면 새 record도 같은 List를 가리킵니다. 한 clone에서 Add하면 원본에도 보입니다. nested collection도 새로 복사해야 합니다.
</details>

### 15. `Sec-Fetch-Site: same-site`는 `same-origin`과 같은가요?

<details><summary>해설</summary>
아닙니다. same-origin은 scheme/host/port가 모두 같고 same-site는 subdomain/port 차이를 허용할 수 있습니다. 민감 endpoint는 same-site subdomain compromise까지 고려합니다.
</details>

### 16. .NET 11 benchmark에서 한 method가 50% 빨랐으면 내 서비스도 50% 빨라지나요?

<details><summary>해설</summary>
아닙니다. 해당 method가 전체 workload에서 차지하는 비율, OS/CPU/architecture, input, cache, GC가 다릅니다. 같은 workload를 BenchmarkDotNet과 production trace/SLO로 측정합니다.
</details>

## 코드 변형 실험

1. [01_StatelessMcpMrtr.csx](./01_StatelessMcpMrtr.csx)의 state 한 글자를 바꿔 거부되는지 확인합니다.
2. [02_RemoteSkillGuardrails.csx](./02_RemoteSkillGuardrails.csx)의 file count를 51로 만들어 이유를 확인합니다.
3. [03_TestTrustLoop.csx](./03_TestTrustLoop.csx)에서 premium case를 지우고 mutation signal이 왜 약해지는지 설명합니다.
4. [04_SupplyChainPolicy.csx](./04_SupplyChainPolicy.csx)의 expiry를 20분으로 늘려 policy가 거부하는지 봅니다.
5. [05_DataTransactionAndIndex.csx](./05_DataTransactionAndIndex.csx)의 tenant schema를 같게 만들어 cache key 결과를 비교합니다.
6. [06_BoundedAudioPipeline.csx](./06_BoundedAudioPipeline.csx)의 capacity를 1로 바꾸고 출력 interleaving을 관찰합니다.
7. [07_RecordSecurityJit.csx](./07_RecordSecurityJit.csx)에서 deep list를 수정해 original count가 유지되는지 확인합니다.

## 최종 과제

다음을 5분 안에 설명하세요.

> Issue 27은 “숨은 상태와 장기 비밀을 줄이고, 명시적인 경계·증거·backpressure·실측으로 신뢰를 만든다”는 공통 원리를 MCP, 테스트, 공급망, DB, AI, 웹 보안, JIT에 적용한다.
