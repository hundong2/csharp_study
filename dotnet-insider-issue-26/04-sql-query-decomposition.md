# 4. MSSQL 개발 도구와 검색 질의 분해

- [MSSQL extension for VS Code July 2026](https://devblogs.microsoft.com/azure-sql/vscode-mssql-july2026/)
- [From noisy queries to precise frames](https://devblogs.microsoft.com/ise/from_noisy_queries_to_precise_frames/)

하나는 SQL 개발자의 입력·결과 확인 경험, 다른 하나는 자연어 검색을 구조화하는 검색 파이프라인입니다. 공통점은 **의도를 구조화하고 중요한 데이터만 보이게 한다**는 점입니다.

## 4.1 MSSQL extension v1.44

기사의 두 Public Preview 기능:

### Shortcuts Configuration

- MSSQL toolbar의 `Open Shortcuts Configuration`에서 통합 관리
- **Quick Queries**: 자주 쓰는 SQL을 저장하고 키보드 단축키 지정
- 바로 실행하거나 편집기에 열어 검토하는 동작 선택
- **Extension Shortcuts**: Query Editor의 실행·연결·기타, Result View의 탐색·결과 명령을 그룹별 설정

바로 실행은 빠르지만 parameter 없는 `DELETE`, `UPDATE`, 운영 연결 같은 위험이 있습니다. 변경 쿼리는 기본적으로 편집기에 열어 데이터베이스·트랜잭션·영향 행을 검토하는 편이 안전합니다. 비밀과 개인 데이터가 포함된 쿼리를 팀 설정이나 저장소에 그대로 커밋하지 마세요.

### Enhanced Results Grid

설정 `mssql.preview.betaResultsGrid`를 `true`로 켜는 Preview 기능입니다.

- 결과 그리드 성능과 UI 개선
- 결과 상태 관리 개선
- 중요한 열 고정(freeze)
- 열 숨기기/다시 표시

열을 숨기는 것은 데이터 권한 제거가 아닙니다. 결과는 이미 클라이언트에 전달되었을 수 있습니다. 민감 데이터 보호는 SQL projection, row-level security, masking, 최소 권한 같은 서버/쿼리 계층에서 수행해야 합니다.

## 4.2 SQL 한 줄 읽기

```sql
SELECT Episode, Scene, Shot, Caption
FROM MediaAssets
WHERE Episode = @episode AND Scene = @scene
ORDER BY Shot;
```

- `SELECT`: 돌려받을 열을 지정합니다.
- `FROM`: 읽을 테이블/뷰입니다.
- `WHERE`: 서버가 처리할 행을 제한합니다.
- `@episode`: 문자열 연결 대신 사용하는 parameter입니다. 형식·실행 계획 재사용·injection 방어에 중요합니다.
- `ORDER BY`: 결과 순서를 명시합니다. 없으면 순서를 보장하지 않습니다.

검색 질의에서 추출한 값도 SQL 문자열에 이어 붙이지 말고 parameter로 전달하고, 숫자 범위와 사용자 접근 권한을 검증합니다.

## 4.3 noisy query 문제

예를 들어 사용자가 “episode 3 scene 12 shot 7에서 주인공이 붉은 문을 여는 장면”을 검색합니다.

- `episode=3`, `scene=12`, `shot=7`: 정확히 일치해야 하는 구조화 메타데이터
- `주인공이 붉은 문을 여는 장면`: 의미적으로 비슷한 자산을 찾을 semantic query

전체 문장을 그대로 embedding하면 에피소드 숫자와 구조어가 벡터 의미를 희석하고 다른 episode/scene/shot이 상위에 나올 수 있습니다.

## 4.4 query decomposition 파이프라인

```text
raw query
  ├─ parser ──> episode / scene / shot / characters
  └─ rewrite ─> 정리된 시각·의미 설명
                    │
metadata pre-filter ├─> vector/semantic retrieval ─> hybrid rank ─> frames
```

엄격한 메타데이터 필터는 검색 후 점수 조정이 아니라 가능하면 retrieval의 **전제 조건**으로 적용합니다. 그 뒤 남은 후보에서 벡터 유사도·키워드 점수 등을 결합합니다.

## 4.5 파서 선택

| 방식 | 장점 | 비용/위험 |
|---|---|---|
| Regex | 거의 0인 지연·호출 비용, 결정적 | 표현 변형에 취약, 패턴 유지보수 |
| LLM | 자연어 변형에 강하고 필드 확장 쉬움 | 기사 실험에서 약 4~5초, 토큰 비용, 출력 검증 필요 |
| Fine-tuned model | 배포 후 매우 낮은 지연 가능 | 학습 데이터·배포·모니터링 lifecycle 복잡성 |

기사의 약 500개 query 평가는 exact field match, ROUGE-L, BLEU, Recall@5를 사용했습니다. 데이터셋에서는 regex도 메타데이터 정확도가 높았지만 rewrite 품질은 모델 방식보다 낮을 수 있었습니다. 데이터 분포가 바뀌면 결과도 바뀌므로 자신의 query log를 개인정보 제거 후 평가해야 합니다.

## 4.6 Regex와 CLR/JIT

[05_QueryDecomposition.csx](./05_QueryDecomposition.csx)는 `Regex`로 메타데이터를 추출합니다.

- 기본 Regex는 패턴을 내부 명령으로 해석합니다.
- `RegexOptions.Compiled`는 동적 메서드 IL/네이티브 코드 생성 비용을 먼저 내고 반복 실행을 빠르게 할 수 있습니다. 짧게 한 번 쓰면 오히려 손해일 수 있습니다.
- source-generated regex(`[GeneratedRegex]`)는 빌드 시 코드를 생성해 시작·trimming·NativeAOT 예측성을 높일 수 있습니다. CSX에는 source generator 환경이 없어 일반 Regex를 사용합니다.
- backtracking 패턴은 입력에 따라 폭발할 수 있으므로 timeout, 단순한 패턴, 필요하면 `NonBacktracking`을 검토합니다.

## 4.7 평가 방법

1. parser의 episode/scene/shot exact match를 각각 측정합니다.
2. rewrite는 사람이 만든 reference와 ROUGE-L/BLEU를 보되 의미 품질을 완전히 대표하지 않음을 기록합니다.
3. 최종 시스템은 Recall@5, nDCG, 사용자 성공률과 지연을 함께 봅니다.
4. parser만 좋고 retrieval이 나쁠 수 있으므로 단계별·end-to-end 실패를 둘 다 저장합니다.
5. 잘못된 metadata를 확신 있게 필터링하면 정답을 완전히 제거하므로 confidence와 fallback 정책을 둡니다.

## 실습

```powershell
dotnet script .\05_QueryDecomposition.csx
```

`episode=3`, `scene=12`, `shot=7`, 의미 query와 strict filter를 확인하세요. 이어서 `ep 3` 또는 한국어 표기처럼 패턴이 놓치는 입력을 넣어 brittle한 지점을 직접 찾으세요.

## 다음 단계

- 이전: [장기 실행 MCP와 Dev Proxy](./03-durable-mcp-devproxy.md)
- 다음: [Visual Studio 에이전트 업데이트](./05-visual-studio-agent.md)
- 공식 후속 자료: [MSSQL VS Code 확장](https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/mssql/mssql-extension-visual-studio-code), [벡터 검색 개념](https://learn.microsoft.com/azure/search/vector-search-overview)
