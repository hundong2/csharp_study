# 2026-09-10 이해도 체크포인트

코드를 보지 않고 먼저 답하세요. 답과 표를 작성한 뒤 접힌 해설을 열어 비교합니다.

## 질문

1. lost update는 어떤 순서에서 발생하나요?
2. 낙관적 동시성은 읽을 때 잠그지 않는데 무엇을 근거로 안전하게 저장하나요?
3. `ArticleRevision.ExpectedVersion`은 누가 언제 본 값인가요?
4. `ArticleRevisionService`의 조회 직후 버전 검사가 최종 안전장치가 아닌 이유는 무엇인가요?
5. `ConcurrentDictionary.TryUpdate(key, candidate, current)`의 세 인수는 각각 무엇을 뜻하나요?
6. 불변 `KnowledgeArticle` record와 `with`가 동시 편집을 이해하고 테스트하는 데 어떤 도움을 주나요?
7. 빈 제목, 짧은 발행 본문, 버전 충돌을 예외가 아닌 Result로 돌려주는 이유는 무엇인가요?
8. Strategy 누락·중복은 왜 Result가 아니라 시작 시 예외인가요?
9. 취소를 `article.version_conflict` 같은 실패 Result로 바꾸면 어떤 문제가 생기나요?
10. 충돌한 사람의 제목·본문을 최신 버전에 맹목적으로 자동 재시도하면 왜 위험한가요?
11. `EditContentPolicy`와 `PublishArticlePolicy`를 Strategy로 나눈 이유는 무엇인가요?
12. process-local `ConcurrentDictionary`가 여러 서버의 운영 동시성 제어를 대신할 수 없는 이유는 무엇인가요?
13. EF Core에서는 어떤 설정과 예외가 오늘의 Version/CAS 계약에 대응하나요?
14. 동시성 테스트가 `Thread.Sleep` 대신 `TaskCompletionSource` gate를 사용하는 이유는 무엇인가요?
15. “Published 본문은 40자 이상” 규칙을 `PublishArticlePolicy`가 아니라 `KnowledgeArticle` aggregate에 둔 이유는 무엇인가요?
16. 마지막 CAS 전 검사에서 관측된 취소, 검사 직후 바뀐 신호, CAS 성공 뒤 도착한 취소는 각각 어떻게 해석해야 하나요?

## 손으로 실행 추적

초기 문서는 `KB-001`, v1, `Draft`, 제목 “배포 절차”입니다. 데모의 네 요청을 차례로 적용해 빈칸을 채우세요.

| 시점 | 요청 expectedVersion | 서비스 결과 | Repository 현재 버전 | 현재 상태 | 현재 제목 |
| --- | ---: | --- | ---: | --- | --- |
| 시작 | - | - | 1 | Draft | 배포 절차 |
| Alice Edit 뒤 | 1 |  |  |  |  |
| Bob stale Publish 뒤 | 1 |  |  |  |  |
| Bob retry Publish 뒤 | 2 |  |  |  |  |
| Guest 공백 제목 뒤 | 3 |  |  |  |  |

같은 초기 v1을 실제로 동시에 읽은 Alice와 Bob의 CAS 경쟁도 채우세요.

| 단계 | Alice | Bob | Repository |
| --- | --- | --- | --- |
| 둘 다 조회 |  |  | v1 |
| 후보 계산 |  |  | v1 |
| 첫 CAS |  |  |  |
| 두 번째 CAS |  |  |  |

<details>
<summary>정답과 해설 보기</summary>

## 질문 정답

1. 두 작성자가 같은 버전을 읽고 첫 작성자가 저장한 뒤, 두 번째 작성자가 오래된 내용으로 무조건 저장하면 첫 변경이 조용히 사라집니다.
2. 작성자가 읽은 concurrency token인 `ExpectedVersion`과 저장 순간의 현재 `Version`이 같은지 원자적으로 비교합니다.
3. 편집 화면이나 API client가 문서를 읽었을 때의 Version입니다. 수정 요청을 제출할 때 함께 보냅니다.
4. 조회 검사 뒤 조건부 쓰기 전에도 다른 요청이 저장할 수 있습니다. 조회와 저장은 하나의 원자 연산이 아니므로 Repository CAS가 필요합니다.
5. `key`는 대상 문서 ID, `candidate`는 새 v2 스냅샷, `current`는 방금 읽어 비교 기준으로 삼은 기존 스냅샷입니다. key의 값이 여전히 current일 때만 candidate로 교체합니다.
6. 원본 v1을 바꾸지 않고 v2 후보를 별도로 만들므로 두 요청의 입력과 결과를 신뢰할 수 있습니다. 공유 객체의 제자리 변경 경쟁도 줄어듭니다.
7. 호출자가 입력을 고치거나, 더 긴 본문을 작성하거나, 최신 문서를 다시 읽는 식으로 정상 대응할 수 있는 예상 가능한 결과이기 때문입니다.
8. 모든 Action을 정확히 한 Strategy가 처리해야 한다는 애플리케이션 구성 불변식이 깨진 상태입니다. 사용자 요청으로 고칠 문제가 아니라 코드·배포 구성을 고쳐야 합니다.
9. 사용자가 요청한 중단과 실제 업무 충돌을 구분할 수 없어 잘못된 재시도, 오류 응답, 실패율 metric이 생깁니다.
10. 최신 작성자가 고친 문장, 삭제한 정보, 상태 결정을 오래된 초안이 다시 덮어쓸 수 있습니다. 최신 내용을 보여 주고 사용자가 diff와 병합 결과를 결정해야 합니다.
11. Edit는 현재 상태를 유지하고 Publish는 다음 상태로 Published를 선택하는 서로 다른 변경 이유가 있습니다. Published 본문 길이는 특정 Strategy가 아니라 aggregate가 공통 검사합니다. 이 분리는 Application Service의 분기 확산을 막고 구현별 독립 테스트와 확장을 돕습니다.
12. 프로세스마다 별도 Dictionary를 가지며 재시작 시 데이터가 사라집니다. 여러 서버가 공유하는 원자 조건은 공용 DB나 저장 시스템에서 실행해야 합니다.
13. `Version` 또는 Provider의 `rowversion`을 concurrency token으로 구성합니다. EF Core 조건부 UPDATE가 0행이면 `DbUpdateConcurrencyException`이 발생하며, Adapter가 최신 DB 값을 읽어 Conflict 계약으로 매핑합니다.
14. 실제 시간 길이는 머신과 부하에 따라 달라 flaky test가 됩니다. gate는 두 요청이 정말 같은 스냅샷을 읽은 시점을 신호로 맞추어 경쟁 조건을 결정적으로 재현합니다.
15. 이미 Published인 문서는 `EditContentPolicy`를 거쳐도 Published 상태를 유지합니다. 규칙이 Publish Strategy에만 있으면 Edit로 본문을 40자 미만으로 줄여 우회할 수 있으므로, 모든 다음 스냅샷을 만드는 aggregate가 상태 불변식을 공통 적용해야 합니다.
16. 저장소의 마지막 CAS 전 검사에서 취소가 관측되면 `OperationCanceledException`을 전파하고 버전과 내용을 그대로 둡니다. 취소는 협력적이므로 그 검사 직후 신호가 바뀌면 CAS가 먼저 성공할 수 있고, CAS 성공 뒤의 늦은 취소도 이미 생긴 저장을 되돌리지 못합니다. 두 경우 모두 실제 커밋이 성공했다면 `Saved`와 성공 영수증을 유지해야 합니다.

## 데모 실행 추적 정답

| 시점 | 요청 expectedVersion | 서비스 결과 | Repository 현재 버전 | 현재 상태 | 현재 제목 |
| --- | ---: | --- | ---: | --- | --- |
| 시작 | - | - | 1 | Draft | 배포 절차 |
| Alice Edit 뒤 | 1 | 적용 v1→v2 | 2 | Draft | 안전한 배포 전 점검 |
| Bob stale Publish 뒤 | 1 | `article.version_conflict`, actual v2 | 2 | Draft | 안전한 배포 전 점검 |
| Bob retry Publish 뒤 | 2 | 적용 v2→v3 | 3 | Published | 운영 배포 체크리스트 |
| Guest 공백 제목 뒤 | 3 | `article.title_required` | 3 | Published | 운영 배포 체크리스트 |

## 실제 CAS 경쟁 정답

Alice와 Bob 중 누가 먼저 CAS에 도달하는지는 보장되지 않으므로 이름은 서로 바뀔 수 있습니다.

| 단계 | Alice | Bob | Repository |
| --- | --- | --- | --- |
| 둘 다 조회 | v1 snapshot | v1 snapshot | v1 |
| 후보 계산 | Alice 내용의 v2 | Bob 내용의 v2 | v1 |
| 첫 CAS | 둘 중 먼저 도달한 요청이 Saved | 다른 요청은 아직 대기/실행 | 승자의 v2 |
| 두 번째 CAS | 승자 유지 | `Conflict(current: v2)` 또는 반대 순서 | 승자의 v2, 추가 증가 없음 |

preflight 검사는 두 요청 모두 통과하지만 CAS 비교 기준 `current`가 더 이상 저장소의 현재 값과 같지 않아 두 번째 교체가 실패합니다. 따라서 한 작성자의 내용을 조용히 덮어쓰지 않습니다.

</details>

## 최종 자기 설명

- [ ] lost update의 시간 순서를 그림으로 설명한다.
- [ ] ExpectedVersion과 현재 Version의 차이를 말한다.
- [ ] preflight 검사와 원자 CAS의 역할을 구분한다.
- [ ] 불변 record/`with`가 만드는 이전·다음 스냅샷을 설명한다.
- [ ] 모든 Strategy에 공통인 Published 본문 불변식이 aggregate에 있어야 하는 이유를 말한다.
- [ ] 입력·정책·NotFound·충돌 Result와 구성 예외를 구분한다.
- [ ] 호출자 취소를 일반 실패로 삼키지 않는다.
- [ ] 마지막 CAS 전 검사에서 관측된 취소와, 검사 직후 또는 성공한 CAS 뒤 늦은 취소의 반환 계약을 구분한다.
- [ ] 충돌 뒤 재조회·diff·사용자 병합·명시적 재적용 순서를 말한다.
- [ ] Repository, Strategy, Application Service, DI, Composition Root의 책임을 파일과 연결한다.
- [ ] process-local CAS의 한계와 EF Core concurrency token 대응을 설명한다.
- [ ] 동시 테스트가 실제 지연이 아니라 gate로 경쟁을 증명하는 이유를 말한다.
