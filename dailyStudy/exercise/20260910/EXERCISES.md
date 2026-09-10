# 2026-09-10 실습 문제 — Beginner to Pro

먼저 원본 코드에서 Release 빌드와 `--self-test`의 `14/14 통과`를 확인하세요. 각 구현 문제는 **실패 테스트 추가 → 실패 확인 → 최소 구현 → 전체 회귀 테스트** 순서로 풉니다.

## Beginner — 버전 흐름을 손으로 추적하기

### 문제 1. 데모의 네 요청 추적

[`Program.cs`](./src/OptimisticConcurrencyExercise/Program.cs)의 네 요청을 실행하기 전에 다음 표를 손으로 채우세요.

| actor | expectedVersion | 예상 결과 코드/상태 | 저장 뒤 현재 버전 |
| --- | ---: | --- | ---: |
| `alice` | 1 |  |  |
| `bob-stale` | 1 |  |  |
| `bob-retry` | 2 |  |  |
| `guest` | 3 |  |  |

일반 데모를 실행해 비교하고 `bob-stale` 요청이 `bob-retry`로 바뀔 때 사람이 해야 할 일을 두 문장으로 적습니다.

완료 기준: 최종 문서가 v3 `Published`이고 applied=2, conflicts=1, rejected=1인 이유를 코드 순서대로 설명합니다.

### 문제 2. 제목의 줄바꿈 검증 추가

`ArticleRevision.Create`에서 제목에 `\r` 또는 `\n`이 들어오면 `article.title_single_line` 실패를 반환하게 하세요.

1. `SelfTests`에 줄바꿈 제목이 실패하고 Repository 조회·저장 횟수가 모두 0인 테스트를 먼저 추가합니다.
2. 테스트가 실패하는 것을 확인합니다.
3. Domain의 guard clause를 최소 구현합니다.

완료 기준: 새 테스트와 기존 14개가 모두 통과하며, 새 메서드나 문법에는 목적·매개변수·반환값 및 Why 한글 주석이 있습니다.

### 문제 3. stale 버전 출력 예측

`Program.cs`에서 `bob-retry`의 `ExpectedVersion`을 1로 바꿨을 때 출력의 `[APPLIED]`, `[CONFLICT]`, 최종 버전, summary를 실행 전에 적으세요. 실행해 확인한 뒤 원래 값 2로 되돌립니다.

완료 기준: 두 번째 충돌이 preflight에서 발생하는지, Repository CAS에서 발생하는지 `ArticleRevisionService.ReviseAsync`를 따라가며 구분합니다.

## Intermediate — Domain과 Strategy 확장

### 문제 4. Review Strategy 추가

편집 후 검토 요청을 표현하는 `RevisionAction.Review`와 `ArticleStatus.InReview`를 추가하고 `ReviewArticlePolicy`를 구현하세요.

필수 테스트:

- v1 Draft 문서의 Review는 v2 `InReview`를 저장합니다.
- 등록된 Review Strategy가 없으면 서비스 생성 시 구성 예외가 납니다.
- 같은 Action Strategy가 두 개면 구성 예외가 납니다.
- 기존 Edit와 Publish 테스트는 그대로 통과합니다.

Application Service의 `if`/`switch`로 Review를 직접 분기하지 말고 Composition Root의 Strategy 목록에 새 구현을 추가합니다.

Published 상태를 유지하거나 새로 선택하는 모든 Strategy는 `KnowledgeArticle.CreateNextRevision`의 40자 본문 불변식을 공통으로 거쳐야 합니다. 이 규칙을 각 Strategy에 복사하지 말고 aggregate 한곳에 유지하세요.

완료 기준: “새 정책에는 열려 있고 기존 서비스 흐름 수정에는 닫혀 있다”는 OCP 의도를 코드와 테스트로 설명합니다.

### 문제 5. 수정 영수증에 변경 종류 포함

`RevisionReceipt`에 적용된 `RevisionAction`을 추가하세요.

1. Edit/Publish 영수증이 각각 올바른 Action을 반환하는 실패 테스트를 작성합니다.
2. `MapSaveAttempt`에서 검증된 `ArticleRevision.Action`을 매핑합니다.
3. 데모 출력에 Action이 중복되거나 서로 어긋나지 않는지 확인합니다.

완료 기준: 영수증만 보고 이전/현재 버전, 결과 상태, 적용한 Strategy 종류를 알 수 있고 실패 Result에는 영수증이 생기지 않습니다.

## Advanced — 실제 경쟁과 충돌 해결

### 문제 6. 세 작성자의 CAS 경쟁

기존 `ConcurrentEditorsYieldOneConflictAsync`를 참고해 세 서비스 호출이 모두 v1을 읽도록 gate를 만드세요.

1. 서로 다른 제목을 가진 세 Task를 동시에 시작합니다.
2. 실제 시간 지연이나 `Thread.Sleep` 없이 `TaskCompletionSource`로 세 조회를 모두 모읍니다.
3. `Task.WhenAll(...).WaitAsync(...)`로 영원한 대기를 막습니다.
4. 성공 1건, `article.version_conflict` 2건, 최종 버전 v2를 검증합니다.
5. 최종 제목이 세 후보 중 실제 승자 하나인지 확인합니다.

완료 기준: preflight를 모두 통과한 세 요청도 Repository CAS에서 한 건만 성공한다는 사실을 테스트가 증명합니다.

### 문제 7. 사용자가 선택하는 병합 서비스

충돌한 사람의 본문을 자동 재시도하지 말고 명시적 병합 입력을 받는 흐름을 설계하세요. 예를 들어 `MergeRevisionCommand`에 다음 값을 둡니다.

- 최신 문서 ID와 버전
- 사용자가 비교한 이전 초안
- 사용자가 최종 선택한 제목과 본문
- 적용할 Action

먼저 다음 테스트를 작성합니다.

- stale 요청 자체는 계속 충돌합니다.
- 최신 버전을 다시 읽었어도 사용자의 병합 선택 없이 자동 저장하지 않습니다.
- 사용자가 선택한 병합 결과를 최신 버전에 제출하면 한 번 성공합니다.
- 병합 화면을 보는 사이 새 저장이 생기면 다시 충돌합니다.

완료 기준: 무한 재시도 루프가 없고, 매 저장은 사용자가 실제로 비교한 최신 버전을 `ExpectedVersion`으로 사용합니다.

### 문제 8. 충돌 관측 Decorator

`IArticleRepository`를 감싸는 `ConflictCountingRepository`를 만들어 `Saved`, `NotFound`, `Conflict` 횟수를 thread-safe하게 기록하세요.

1. 병렬 저장 테스트를 먼저 작성합니다.
2. `Interlocked` 또는 적합한 concurrent collection을 사용합니다.
3. 제목·본문 같은 사용자 내용을 metric label이나 로그에 넣지 않습니다.
4. 내부 Repository의 결과와 예외·취소를 바꾸지 않고 그대로 전달합니다.

완료 기준: 호출 결과는 Decorator 전후가 같고, 세 작성자 경쟁 뒤 Saved 1, Conflict 2가 기록됩니다.

추가 경계 테스트로 Repository Decorator가 내부 조건부 저장 진입 전에 token을 취소하여 마지막 CAS 전 검사에서 관측되는 경우에는 예외와 v1 무변경을, CAS 성공 직후 token을 취소하는 경우에는 성공 영수증과 저장된 v2를 검증하세요. 취소는 협력적이므로 마지막 검사 직후 신호가 바뀌면 CAS가 먼저 성공할 수 있으며, 커밋 뒤 취소 검사로 성공을 실패로 뒤집으면 안 됩니다.

## Pro — 운영 저장소와 API 경계

### 문제 9. EF Core optimistic concurrency Adapter 설계

코드를 쓰기 전에 `KnowledgeArticleEntity` 매핑과 Repository 알고리즘을 문서화한 뒤 integration test를 추가하세요.

- 기본 키 `Id`
- concurrency token인 `Version` 또는 Provider가 지원하는 `rowversion`
- 원래 token을 포함하는 조건부 `UPDATE`
- `SaveChangesAsync`의 `DbUpdateConcurrencyException`을 `ArticleSaveAttempt.Conflict`로 매핑
- 충돌 뒤 database/current/original 값을 조회하는 방법
- 조회와 저장 사이 삭제를 `NotFound`와 구분하는 규칙
- 저장 성공과 감사 기록을 같은 트랜잭션에 묶는 범위

integration test는 서로 다른 `DbContext` 두 개가 같은 v1을 읽고 각각 v2를 저장하려 할 때 한 쪽만 성공해야 합니다. 사용하는 Provider가 실제 concurrency 동작을 재현하는지 명시하고, 단순 mock으로 DB 조건부 쓰기를 증명했다고 주장하지 않습니다.

완료 기준: 마지막 쓰기 승리나 exactly-once라고 표현하지 않고, 0-row update/동시성 예외와 사용자 병합 책임을 명시합니다.

### 문제 10. HTTP `ETag` / `If-Match` 경계

Web API를 가정하여 Version을 HTTP 조건부 요청에 연결하세요.

1. GET은 현재 Version을 안전한 `ETag`로 반환합니다.
2. PUT/PATCH는 `If-Match`가 없으면 명시적 사전 조건 오류를 반환합니다.
3. `If-Match`를 `ExpectedVersion`으로 변환해 Application Service에 전달합니다.
4. `If-Match`를 실제로 평가해 조건이 맞지 않은 stale ETag는 HTTP 412 Precondition Failed와 최신 ETag로 매핑합니다. 조건부 헤더가 아닌 별도 Domain 충돌을 409로 쓰는 정책과 섞지 않습니다.
5. 권한 실패, 검증 실패, 문서 부재, 취소, 서버 구성 오류를 서로 다른 경계로 유지합니다.

필수 테스트: 정상 갱신, stale ETag, 누락 ETag, 잘못된 ETag, 요청 취소, 충돌 응답에 사용자 본문이 노출되지 않는 경우를 검증합니다.

완료 기준: [RFC 9110의 precondition 평가 규칙](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.2.2)을 근거로 상태 코드를 설명하고, 클라이언트의 “재조회 → diff → 사용자 병합 → 새 If-Match 제출” 흐름을 API 문서에 적습니다.

## 매 단계 공통 검증 명령

```powershell
dotnet build dailyStudy/exercise/20260910/src/OptimisticConcurrencyExercise/OptimisticConcurrencyExercise.csproj -c Release
dotnet run --project dailyStudy/exercise/20260910/src/OptimisticConcurrencyExercise/OptimisticConcurrencyExercise.csproj -c Release --no-build
dotnet run --project dailyStudy/exercise/20260910/src/OptimisticConcurrencyExercise/OptimisticConcurrencyExercise.csproj -c Release --no-build -- --self-test
```

마지막에는 새로 작성한 모든 메서드 상단에 목적, parameter 의미, 반환값을 설명했는지 확인합니다. 처음 추가한 nullable, `record`, `with`, LINQ, pattern, async/concurrency 문법에는 “무엇이며 왜 사용했는지”를 설명하는 한글 주석을 남깁니다.
