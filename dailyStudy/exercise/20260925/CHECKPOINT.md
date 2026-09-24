# 이해도 체크포인트

먼저 답을 말하거나 적은 뒤 해설을 펼치세요. 처음 1~6번은 입문자가 반드시 확인할 핵심이고, 뒤 질문은 실무 설계 경계를 점검합니다.

## 1. Output Cache는 무엇을 저장하나요?

<details>
<summary>해설 보기</summary>

server가 만든 HTTP 응답의 status, header, body를 cache entry로 저장하고 같은 key의 다음 요청에 재사용합니다. Repository 객체 자체나 Domain 데이터를 저장하는 기능은 아닙니다.

</details>

## 2. 별도 정책으로 허용 범위를 넓히지 않았을 때 어떤 응답이 기본적으로 캐시되나요?

<details>
<summary>해설 보기</summary>

기본 정책은 GET 또는 HEAD 요청의 200 응답을 대상으로 합니다. 인증된 요청과 `Set-Cookie`가 있는 응답은 저장하지 않습니다. 단순히 임의의 cookie header가 요청에 있다는 사실만으로 같은 의미라고 단정하지 말고, 실제 인증 여부와 응답의 `Set-Cookie`를 구분해야 합니다. POST 결과나 400·404·500도 저절로 캐시되지 않습니다.

</details>

## 3. Output Cache, Response Cache, ETag는 어떻게 다른가요?

<details>
<summary>해설 보기</summary>

Output Cache는 server가 정책에 따라 완성된 응답을 보관하고 재사용합니다. Response Caching은 `Cache-Control` 같은 HTTP cache 규칙과 client/proxy header를 따릅니다. ETag는 표현의 버전을 나타내는 validator라서 `If-None-Match` 재검증으로 304를 보낼 수 있지만, 그 자체가 응답 body를 저장하는 cache는 아닙니다. 함께 사용할 수 있지만 같은 기능은 아닙니다.

</details>

## 4. `category` query와 `Accept-Language` header를 왜 cache key에 포함해야 하나요?

<details>
<summary>해설 보기</summary>

둘 다 응답 body를 바꾸기 때문입니다. 빠지면 먼저 저장된 category나 언어의 응답이 다른 요청에 재사용됩니다. 반대로 응답과 무관한 추적 header까지 모두 포함하면 hit율이 낮아지고 항목 수가 폭증합니다.

</details>

## 5. 같은 URL을 두 번 호출했는데 두 번째 `OriginReadNumber`가 그대로인 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

첫 요청의 완성된 응답에 들어 있던 숫자까지 함께 캐시되었기 때문입니다. 두 번째 요청은 endpoint와 Repository를 다시 실행해 숫자를 만든 것이 아니라 저장된 body를 반환합니다.

</details>

## 6. tag는 cache key와 무엇이 다른가요?

<details>
<summary>해설 보기</summary>

key는 어떤 요청이 특정 entry를 다시 사용할지 결정합니다. tag는 category·언어처럼 key가 서로 다른 여러 entry를 한 그룹으로 묶어 무효화하는 이름입니다. tag eviction은 Repository의 공지를 삭제하지 않습니다.

</details>

## 7. POST를 받자마자 tag를 지우지 않고, 저장 성공 뒤에만 지우는 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

입력 검증이나 저장이 실패하면 원본 데이터는 변하지 않았으므로 기존 cache도 여전히 유효합니다. 먼저 지우면 불필요한 miss가 발생합니다. 또한 저장 전에 지운 뒤 다른 GET이 옛 데이터를 다시 채우면, POST 성공 후에도 오래된 응답이 남을 수 있습니다. 오늘 예제의 순서는 “업무 저장 성공 → generation 증가 → server-owned token으로 관련 tag 정리”입니다.

</details>

## 8. tag를 지우면 이미 처리 중인 요청의 오래된 응답 생성도 자동으로 중단되나요?

<details>
<summary>해설 보기</summary>

항상 그렇지는 않습니다. eviction과 동시에 진행 중이던 miss가 이전 snapshot으로 cache를 다시 채우는 race를 별도로 고려해야 합니다. 오늘 예제는 성공한 쓰기마다 process-local generation을 올려 새 GET이 새 key만 사용하게 하고, `TaskCompletionSource` gate로 이 경합을 재현합니다. 다중 node에서는 distributed revision 또는 durable invalidation이 추가로 필요합니다.

</details>

## 9. resource locking은 cache stampede를 어떻게 줄이나요?

<details>
<summary>해설 보기</summary>

같은 cold cache key의 요청 여러 개 중 하나가 응답을 만들 동안 나머지가 기다리게 하여, 같은 비싼 원본 작업이 동시에 반복되는 것을 줄입니다. 서로 다른 key, 여러 process의 업무 상태, 인증 권한, DB 동시성까지 해결하는 전역 lock은 아닙니다.

</details>

## 10. resource locking 검사를 `Thread.Sleep`으로 만들면 왜 약한 테스트인가요?

<details>
<summary>해설 보기</summary>

실행 환경의 속도에 따라 요청이 실제로 겹치지 않을 수 있어 가끔 통과하거나 실패합니다. `TaskCompletionSource`나 barrier로 첫 원본 read를 멈추고 모든 요청이 도착한 뒤 gate를 열어야 순서를 결정적으로 재현할 수 있습니다.

</details>

## 11. 사용자별 응답은 `Authorization` header로 vary하면 캐시해도 안전한가요?

<details>
<summary>해설 보기</summary>

기본 선택은 아니오입니다. 인증 token을 key 재료로 삼으면 민감 정보 취급, 높은 cardinality, token 교체, 잘못된 인증 판정 문제가 생깁니다. ASP.NET Core 기본 정책이 인증 요청과 cookie 응답을 캐시하지 않는 이유입니다. 사용자별 데이터는 캐시하지 않거나, 별도의 권한 모델과 안전한 사용자 식별자를 가진 전용 설계를 해야 합니다.

</details>

## 12. `Result<T>`로 다룰 실패와 예외로 보존할 상황은 어떻게 나누나요?

<details>
<summary>해설 보기</summary>

빈 제목이나 잘못된 category처럼 예상 가능하고 caller가 고칠 수 있는 업무 실패는 `Result<T>`가 적합합니다. Redis 단절, 저장소 버그 같은 예상 밖 인프라 실패는 관측 가능한 예외 경로로 남깁니다. `OperationCanceledException`도 업무 검증 실패가 아니라 작업 중단 신호이므로 일반 실패 Result로 삼키지 않습니다.

</details>

## 13. 요청의 `CancellationToken`을 어디까지 전달해야 하나요?

<details>
<summary>해설 보기</summary>

endpoint에서 Application Service와 Repository의 commit 전 취소 가능한 I/O까지 같은 token을 전달합니다. 업무 저장 뒤에는 request token과 분리한 server-owned timeout으로 tag를 정리하고, process-local generation으로 새 요청의 key를 바꿉니다. 다중 node에서 강한 복구가 필요하면 transactional outbox/worker 같은 durable 경계를 사용합니다.

</details>

## 14. DI, Repository, Strategy, Application Service는 각각 무슨 역할인가요?

<details>
<summary>해설 보기</summary>

`Program.cs`의 DI 구성은 interface와 구현을 조립하는 Composition Root입니다. `IAnnouncementRepository`는 저장 기술을 Application에서 분리합니다. `IAnnouncementLocalizationStrategy`는 언어별 표현 알고리즘을 교체합니다. `AnnouncementApplicationService`는 Domain 규칙과 Port를 엮어 use case 순서를 정합니다. interface는 무조건 늘리는 장식이 아니라 실제 교체점과 I/O 경계에 둡니다.

</details>

## 15. `IAnnouncementLocalizationStrategy`가 Output Cache 정책까지 알아야 하나요?

<details>
<summary>해설 보기</summary>

아닙니다. Strategy는 공지를 특정 언어의 표현으로 바꾸는 일만 담당합니다. 어떤 header로 cache key를 나누고 얼마 동안 저장할지는 HTTP/Presentation의 관심사입니다. 이렇게 나누면 Strategy를 HTTP server 없이 테스트할 수 있습니다.

</details>

## 16. Redis Output Cache를 쓰면 두 node의 `InMemoryAnnouncementRepository`도 자동으로 동기화되나요?

<details>
<summary>해설 보기</summary>

아닙니다. Redis provider가 공유하는 것은 cached HTTP response입니다. node마다 별도 메모리 Repository를 두면 tag eviction 뒤 각 node가 서로 다른 원본으로 응답을 다시 만들 수 있습니다. 다중 node는 같은 DB 같은 source of truth도 공유해야 합니다.

</details>

## 17. Redis tag eviction까지 성공하면 DB와 cache가 항상 원자적으로 일치하나요?

<details>
<summary>해설 보기</summary>

아닙니다. DB commit 후 eviction 전에 process가 종료되는 실패 창이 있습니다. 짧은 TTL로 stale 허용 시간을 제한하거나, DB transaction에 outbox event를 함께 기록하고 worker가 eviction을 재시도하는 등 요구되는 일관성 수준에 맞는 회복 전략이 필요합니다.

</details>

## 18. 다중 node 검증에서 꼭 분리해 확인할 세 가지는 무엇인가요?

<details>
<summary>해설 보기</summary>

첫째, A가 채운 cache를 B가 읽는가. 둘째, 성공한 mutation의 tag eviction이 B에도 보이는가. 셋째, miss 뒤 두 node가 같은 source of truth에서 최신 데이터를 읽는가입니다. 첫 번째만 통과해도 전체 데이터 일관성이 증명된 것은 아닙니다.

</details>

## 짧은 복습

- [ ] 기본 cache 대상: GET/HEAD, 200, 비인증, `Set-Cookie` 응답 아님
- [ ] 다른 표현을 만드는 query/header는 vary key에 포함
- [ ] 성공한 쓰기 뒤 generation 증가와 관련 tag cleanup
- [ ] 동일 cold key의 stampede는 resource locking으로 완화
- [ ] 사용자별 응답은 기본적으로 cache하지 않음
- [ ] 취소는 Result 실패로 숨기지 않음
- [ ] 공유 Redis cache와 공유 업무 저장소를 구분
