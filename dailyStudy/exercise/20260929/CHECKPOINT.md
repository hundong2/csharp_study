# 2026-09-29 이해도 체크 — Health Checks

먼저 답을 가리고 말이나 글로 설명한 뒤 펼쳐 보세요.

## 1. Liveness는 어떤 질문에 답하나요?

<details>
<summary>정답 보기</summary>

“현재 프로세스가 살아 있고 HTTP에 답할 수 있는가?”에 답합니다. DB나 외부 API의 일시 장애를 liveness 실패로 만들면 오케스트레이터가 정상 프로세스를 반복 재시작할 수 있으므로 이 예제는 등록 검사를 하나도 실행하지 않습니다.

</details>

## 2. Startup과 readiness는 어떻게 다른가요?

<details>
<summary>정답 보기</summary>

Startup은 초기 warm-up이 한 번 끝났는지 확인합니다. Readiness는 지금 이 순간 새 트래픽을 받을 수 있는지 계속 확인합니다. startup은 보통 한 번 Healthy가 되면 유지되지만 readiness는 dependency 장애와 복구에 따라 여러 번 바뀔 수 있습니다.

</details>

## 3. 선택 dependency 장애가 왜 HTTP 200인가요?

<details>
<summary>정답 보기</summary>

핵심 기능은 계속 제공할 수 있다는 제품 정책이기 때문입니다. 본문의 `Degraded`가 부가 기능 저하를 알리고, HTTP 200은 오케스트레이터가 replica를 트래픽 대상에 유지하게 합니다. 모든 시스템에서 무조건 200이어야 하는 보편 법칙은 아닙니다.

</details>

## 4. Readiness 503은 프로세스를 재시작하라는 뜻인가요?

<details>
<summary>정답 보기</summary>

아닙니다. 새 트래픽 대상에서 잠시 제외하라는 뜻입니다. 재시작 판단은 liveness가 담당하고, 이미 처리 중인 요청의 종료·drain 정책은 별도로 설계해야 합니다.

</details>

## 5. Tag와 Predicate는 각각 무엇을 하나요?

<details>
<summary>정답 보기</summary>

등록할 때 tag로 검사를 묶고, endpoint의 `Predicate`가 그 tag를 보고 이번 요청에 실행할 등록만 고릅니다. startup check는 `startup`과 `ready` 두 tag를 가져 두 endpoint 모두에 포함됩니다.

</details>

## 6. 왜 Domain이 `HealthStatus`를 직접 사용하지 않나요?

<details>
<summary>정답 보기</summary>

Domain의 관찰·판정 규칙을 ASP.NET Core framework와 분리하기 위해서입니다. `ApplicationReadinessHealthCheck` Adapter가 Domain 단계를 framework 단계로 변환하므로 Domain과 정책을 console, worker, 단위 테스트에서도 재사용할 수 있습니다.

</details>

## 7. `CancellationToken`을 Infrastructure까지 전달해야 하는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

token을 누락하면 registration timeout도 작업을 강제 종료하지 못해 health 응답 자체가 늦어질 수 있고, 클라이언트나 proxy가 먼저 포기해도 실제 DB/API I/O가 계속될 수 있습니다. 이 예제는 framework → HealthCheck Adapter → Application Service → probe별 linked token → Probe Port → `Task.Delay`까지 취소 신호를 전달합니다. 개별 budget만 끝나면 중요도를 보존한 Unavailable 관찰로 바꾸고, 상위 요청 취소는 다시 던집니다.

</details>

## 8. 예상 가능한 dependency 장애를 예외 대신 값으로 둔 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

외부 서비스의 일시 불가용은 운영 중 예상 가능한 상태이며 정책이 필수/선택에 따라 정상적으로 분기해야 하기 때문입니다. 개별 probe timeout도 중요도를 가진 불가용 관찰로 바꿉니다. 중복 DI 이름 같은 프로그래머 오류는 예외로, 상위 registration/호출자 중단은 취소 예외로 별도 처리합니다.

</details>

## 9. 왜 Repository 대신 `IDependencyProbe`를 사용하나요?

<details>
<summary>정답 보기</summary>

Repository는 보통 영속 Domain aggregate를 저장하고 조회합니다. 여기서는 데이터를 저장하는 것이 아니라 현재 외부 시스템을 관찰하므로 Probe/Gateway 의미의 Port가 책임을 더 정확하게 드러냅니다.

</details>

## 10. `Task.WhenAll`의 장점과 주의점은 무엇인가요?

<details>
<summary>정답 보기</summary>

독립 probe를 동시에 실행해 전체 latency가 각 latency의 합으로 커지는 일을 줄입니다. 반면 replica 수와 probe 수가 많으면 외부 dependency에 동시 부하를 만들 수 있으므로 짧고 값싼 검사, 적절한 scrape 주기, timeout, 필요하면 캐시나 jitter가 필요합니다.

</details>

## 11. Health JSON에 exception 원문을 넣으면 왜 위험한가요?

<details>
<summary>정답 보기</summary>

stack trace, host name, 내부 경로, SQL, 연결 문자열 같은 민감정보가 노출될 수 있습니다. 공개 응답은 안정적인 안전 코드만 주고 상세 원인은 접근 통제된 로그·trace에서 찾아야 합니다.

</details>

## 12. `lock` 안에서 `await`하지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

느린 I/O가 끝날 때까지 다른 요청이 공유 상태 잠금을 얻지 못하고, 교착이나 긴 대기 위험이 커지기 때문입니다. 예제는 lock 안에서 상태만 짧게 복사한 뒤 `Task.Delay`를 잠금 밖에서 await합니다.

</details>

## 13. Development 장애 주입 endpoint를 Production에서 숨기는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

누구나 실제 dependency를 장애 상태처럼 보이게 하거나 readiness에서 replica를 제외시킬 수 있는 공격면이 되기 때문입니다. 예제는 Development + 명시적 opt-in에서만 route를 등록하고 loopback 요청만 허용합니다. Production에서는 opt-in을 줘도 route를 등록하지 않으며 자체 테스트로 실제 404를 확인합니다.

</details>

## 14. 이해했는지 확인하는 마지막 설명

다음 문장을 완성해 보세요.

> liveness에 DB 검사를 넣지 않는 이유는 **DB 장애가 프로세스 재시작으로 해결되지 않으며 재시작 폭풍을 만들 수 있기 때문**이고, readiness가 503이라는 뜻은 프로세스를 죽이라는 뜻이 아니라 **새 트래픽 대상에서 잠시 제외하라는 뜻**이다.

## 복습 체크

- [ ] live/startup/ready의 역할을 구분했다.
- [ ] Healthy/Degraded/Unhealthy와 200/503 매핑을 설명했다.
- [ ] tag와 predicate의 실행 선택 흐름을 설명했다.
- [ ] Domain, Application, Adapter의 경계를 찾았다.
- [ ] cancellation이 실제 probe까지 전달되는 경로를 찾았다.
- [ ] 공개 health 응답에서 민감정보를 숨길 이유를 설명했다.
