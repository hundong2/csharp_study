# 초보자 이해도 체크포인트 — HMAC-SHA256 결제 웹훅 수신 API

먼저 `<details>`를 열지 말고 자신의 말로 답해 보세요. 정답 문장을 외우기보다 [`README.md`](./README.md)의 구조도와 실제 코드에서 근거를 찾는 것이 중요합니다.

---

## 1. 결제 웹훅에서 HMAC-SHA256 서명은 무엇을 증명하나요?

<details>
<summary>정답과 해설</summary>

수신자가 같은 비밀키로 계산한 HMAC과 요청의 서명이 같다면, **비밀키를 아는 발신자가 서명한 바이트가 전송 중 바뀌지 않았다**고 판단할 수 있습니다. 즉 발신자 인증과 메시지 무결성을 제공합니다.

다만 HMAC만으로 요청의 기밀성, 재전송 방지, 발신 서버의 보안, 업무 처리의 정확히 한 번 실행까지 보장하지는 않습니다. 전송 내용 보호에는 HTTPS가 필요하고, 재전송에는 timestamp와 이벤트 ID 저장소가 추가로 필요합니다.

</details>

---

## 2. 단순 SHA-256 해시가 아니라 HMAC-SHA256을 써야 하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

단순 해시는 누구나 body를 바꾼 뒤 새 해시를 계산할 수 있으므로 발신자를 인증하지 못합니다. HMAC은 발신자와 수신자만 아는 비밀키를 계산에 포함하므로 키를 모르는 공격자는 바뀐 요청의 올바른 서명을 만들 수 없습니다.

`SHA256(secret + message)`처럼 임의로 조립한 방식 대신 검증된 HMAC 알고리즘을 사용해야 길이 확장 공격 같은 설계 실수를 피할 수 있습니다.

</details>

---

## 3. 왜 JSON을 객체로 역직렬화했다가 다시 직렬화한 값이 아니라 raw UTF-8 body 바이트를 서명하나요?

<details>
<summary>정답과 해설</summary>

JSON은 의미가 같아도 공백, 줄바꿈, 속성 순서, 숫자 표기와 escaping이 달라질 수 있습니다. 역직렬화 후 다시 만든 JSON은 발신자가 실제로 서명한 바이트와 다를 수 있어 정상 요청도 검증에 실패합니다.

따라서 HTTP body에서 읽은 **원본 UTF-8 바이트**를 그대로 보존해 서명을 검증합니다. 문자열 trim, 줄바꿈 변경, Unicode 정규화, JSON 재직렬화를 서명 전에 수행하면 안 됩니다.

</details>

---

## 4. 이 예제의 canonical 입력은 정확히 어떻게 만드나요?

<details>
<summary>정답과 해설</summary>

정확한 계약은 다음과 같습니다.

```text
UTF8(timestamp + "." + eventId + ".") || rawBody
```

먼저 timestamp, 점(`.`), 이벤트 ID, 점(`.`)을 **정해진 순서 그대로** 이어 붙여 UTF-8 바이트로 만들고, 그 뒤에 raw body 바이트를 붙입니다. 여기서 `||`는 C#의 논리 OR 연산자가 아니라 **바이트 배열 연결**을 뜻합니다.

구분점은 `123` + `45`와 `12` + `345`처럼 경계가 모호해지는 것을 막습니다. 공백을 추가하거나 body 뒤에 newline을 붙이는 등 한 바이트라도 계약과 달라지면 HMAC 결과가 달라집니다.

</details>

---

## 5. event ID를 body와 별도로 서명 범위에 넣는 가장 중요한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

event ID가 서명되지 않으면 공격자가 정상 요청의 body와 서명을 복사한 뒤 `X-Webhook-Id`만 새 값으로 바꿀 수 있습니다. 저장소는 새 ID라고 생각해 replay 차단을 우회할 수 있습니다.

`eventId`를 canonical 입력에 포함하면 ID 변경도 서명을 깨뜨립니다. 따라서 저장소가 중복 판단에 쓰는 **바로 그 ID 값**과 서명에 사용한 값이 같아야 하며, 서명 후 별도의 trim이나 대소문자 정규화를 해서는 안 됩니다.

</details>

---

## 6. timestamp를 서명 범위에 넣고 현재 시각과 ±300초 이내인지 검사하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

timestamp를 서명하면 공격자가 오래된 요청의 시각만 현재로 바꿀 수 없습니다. 수신자는 현재 Unix time과 비교해 과거 300초보다 오래됐거나 미래 300초보다 더 앞선 요청을 거절하여 탈취된 요청이 사용될 수 있는 시간을 제한합니다.

`±300초`는 과거와 미래 양쪽을 검사한다는 뜻입니다. 정확히 경계인 300초를 허용하는지 여부도 계약과 테스트에서 일관되어야 합니다. 서버 시계 동기화가 깨지면 정상 요청도 거절될 수 있으므로 운영에서는 NTP와 clock skew 관측이 필요합니다.

</details>

---

## 7. `X-Webhook-Signature: v1=<64 lowercase hex>` 형식을 엄격히 검사하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

SHA-256 결과는 32바이트이고, 바이트 하나를 두 개의 16진수 문자로 표현하므로 정확히 64자가 됩니다. 이 예제는 `v1=` 뒤에 소문자 `0-9a-f` 64개가 오는 값만 허용합니다.

버전 접두사는 나중에 서명 방식이나 키를 교체할 때 계약을 구분하게 해 줍니다. 길이, 문자 집합, 중복 header를 느슨하게 허용하면 여러 계층이 같은 입력을 다르게 해석하는 parser ambiguity가 생길 수 있으므로 지원하지 않는 형식은 서명 계산 전에 거절합니다.

</details>

---

## 8. 계산한 서명과 받은 서명을 일반 문자열 `==` 대신 고정 시간 비교로 확인하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

일반 비교는 앞부분에서 차이가 나면 일찍 끝날 수 있어, 응답 시간 차이가 어느 위치까지 맞았는지에 대한 단서가 될 수 있습니다. 서명처럼 비밀과 관련된 값은 받은 hex를 32바이트로 해석한 뒤 `CryptographicOperations.FixedTimeEquals` 같은 고정 시간 비교를 사용합니다.

먼저 엄격한 형식과 길이를 검사하고, 같은 길이의 바이트끼리 비교해야 합니다. 고정 시간 비교는 서명 검증의 한 방어선이며 HTTPS, rate limit, 키 보호를 대신하지는 않습니다.

</details>

---

## 9. 웹훅 비밀키는 코드나 요청마다 새로 만드는 값인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. 공급자와 수신자가 안전하게 공유한 충분히 긴 무작위 비밀을 구성이나 secret store에서 읽습니다. 소스 코드, Git 기록, 로그, 오류 응답에 키를 남기면 안 됩니다.

운영에서는 키 식별자와 버전을 두어 회전 기간에 이전 키와 새 키를 제한적으로 함께 검증할 수 있습니다. 같은 키를 여러 환경에 재사용하지 않고, 유출이 의심되면 즉시 폐기·교체할 절차도 필요합니다.

</details>

---

## 10. 요청 검증은 어떤 큰 순서로 진행해야 하나요?

<details>
<summary>정답과 해설</summary>

일반적인 흐름은 다음과 같습니다.

1. JSON content type과 필요한 header가 각각 정확히 하나인지 검사합니다.
2. body를 최대 16 KiB까지만 raw bytes로 읽습니다.
3. event ID·timestamp 문법과 timestamp 허용 범위를 검사합니다.
4. canonical bytes를 만들고 HMAC 서명을 검증합니다.
5. 서명이 맞은 뒤에만 JSON을 역직렬화하고 Domain 규칙을 검사합니다.
6. 검증된 receipt를 event ID와 함께 원자적으로 처음 한 번만 저장합니다.

세부 구현에서 일부 값 검사의 위치는 달라질 수 있지만, **신뢰하지 않은 큰 입력을 제한하고 인증되지 않은 payload를 업무 처리하지 않는다**는 원칙은 유지해야 합니다.

</details>

---

## 11. 16 KiB body 제한에서 KiB는 몇 바이트이며, 왜 문자 수가 아니라 바이트 수를 세나요?

<details>
<summary>정답과 해설</summary>

16 KiB는 `16 * 1024 = 16,384`바이트입니다. HTTP body와 HMAC 입력은 바이트이므로 제한도 실제로 읽은 바이트 수에 적용해야 합니다. 한글처럼 UTF-8에서 여러 바이트를 쓰는 문자는 문자열 길이와 전송 크기가 다릅니다.

제한은 과도한 메모리 사용, 긴 HMAC 계산, JSON parser 부담을 줄이는 보안 경계입니다. 정확히 16,384바이트는 허용하고 그보다 한 바이트라도 많으면 거절한다는 경계를 테스트해야 합니다.

</details>

---

## 12. `Content-Length`가 16 KiB 이하이면 실제 body 길이 검사를 생략해도 되나요?

<details>
<summary>정답과 해설</summary>

안 됩니다. `Content-Length`가 없을 수 있고 chunked 전송을 사용할 수도 있으며, 프록시와 서버 설정에 따라 신뢰 경계가 다릅니다. 큰 `Content-Length`는 일찍 거절하는 빠른 검사로 쓸 수 있지만 실제 stream 읽기에도 독립적인 상한이 있어야 합니다.

보통 최대 크기보다 한 바이트 더 읽어 초과 여부를 구분하거나, 제한 stream을 사용합니다. 무제한 `ReadToEndAsync` 뒤 길이를 검사하면 이미 큰 입력을 메모리에 올린 뒤라 방어가 늦습니다.

</details>

---

## 13. JSON parsing보다 서명 검증을 먼저 하는 이유는 무엇이며, raw body는 언제 문자열이 되나요?

<details>
<summary>정답과 해설</summary>

서명 검증 전 JSON parsing을 하면 인증되지 않은 입력에 CPU와 메모리를 더 쓰고, parser 동작에 따라 원본 바이트를 잃을 수 있습니다. raw bytes로 HMAC을 확인한 뒤에만 UTF-8 JSON으로 역직렬화합니다.

서명이 맞아도 UTF-8이나 JSON 형식이 잘못될 수 있으므로 parsing 실패는 안전한 `400` 계열 결과로 바꿉니다. 반대로 parsing 오류 세부 정보, 원문 결제 payload, stack trace를 응답이나 로그에 그대로 노출하지 않습니다.

</details>

---

## 14. timestamp 5분 제한만 있으면 replay Repository는 필요 없나요?

<details>
<summary>정답과 해설</summary>

필요합니다. 공격자나 공급자의 retry가 같은 정상 요청을 5분 안에 여러 번 보낼 수 있고, 각 요청의 HMAC과 timestamp는 모두 유효합니다. timestamp는 replay 가능 시간을 줄일 뿐 한 번만 처리되게 만들지 않습니다.

Repository는 검증된 event ID를 기억하여 같은 ID의 두 번째 요청을 중복으로 판단합니다. 보존 기간은 적어도 공급자가 재전송할 수 있는 최대 기간보다 길어야 하며, 무한히 저장하지 않도록 만료·정리 정책도 정해야 합니다.

</details>

---

## 15. 같은 event ID의 요청 두 개가 동시에 오면 `Contains` 후 `Add`로 막을 수 있나요?

<details>
<summary>정답과 해설</summary>

두 연산 사이에 다른 요청이 들어올 수 있어 막을 수 없습니다. 두 요청 모두 `Contains == false`를 보고 둘 다 처리하는 race condition이 생깁니다.

“ID가 없으면 하나만 예약하고, 이미 있으면 중복을 반환한다”는 동작을 lock, `ConcurrentDictionary.TryAdd`, 데이터베이스 unique constraint/transaction 같은 **하나의 원자적 연산**으로 만들어야 합니다. 동시 요청 테스트에서 성공이 정확히 하나인지 확인해야 합니다.

</details>

---

## 16. replay 방지와 업무 부작용의 exactly-once 처리는 같은 문제인가요?

<details>
<summary>정답과 해설</summary>

완전히 같지 않습니다. event ID를 먼저 기록하고 결제 업무 처리에 실패하면 retry가 중복으로 막혀 작업이 유실될 수 있습니다. 반대로 업무 처리 후 ID를 기록하기 전에 장애가 나면 retry 때 부작용이 다시 실행될 수 있습니다.

학습용 in-memory Repository는 원자적 중복 판단을 보여 주지만, 운영의 exactly-once에 가까운 처리는 보통 업무 변경과 inbox 기록을 같은 DB transaction으로 묶고 idempotent consumer/outbox를 사용합니다. 외부 시스템 호출까지 하나의 transaction으로 만들 수는 없으므로 멱등키와 보상 정책도 필요합니다.

</details>

---

## 17. `X-Webhook-Id`도 길이와 허용 문자를 검증해야 하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

event ID는 canonical 입력, Repository key, 로그와 metric에 사용됩니다. 무제한 길이와 제어 문자·줄바꿈을 허용하면 메모리 사용, 로그 삽입, 높은 cardinality, 계층별 정규화 차이 문제가 생깁니다.

이 예제는 빈 값이 아닌 최대 100자의 ASCII 영문자·숫자·밑줄·하이픈만 허용합니다. 중요한 점은 검증 뒤 다른 문자열로 바꾸는 것이 아니라, **서명에 쓴 정확한 값**을 replay key로 쓰는 것입니다.

</details>

---

## 18. 어떤 실패는 `Result<T>`로, 어떤 실패는 예외로 다루나요?

<details>
<summary>정답과 해설</summary>

잘못된 header, 만료된 timestamp, 서명 불일치, 큰 body, 잘못된 JSON/Domain 값, 이미 본 event ID처럼 호출자가 예상하고 고칠 수 있는 실패는 `Result<T>`의 오류 값으로 표현할 수 있습니다. Presentation은 이를 정해진 HTTP 상태와 안전한 응답으로 바꿉니다.

비밀키 구성 누락, 깨진 내부 invariant, 저장소 연결 장애처럼 정상 분기로 간주하면 안 되는 문제는 예외로 남깁니다. 모든 오류를 예외로 만들면 제어 흐름과 서버 장애가 섞이고, 모든 예외를 `400` Result로 숨기면 운영 장애를 놓치게 됩니다.

</details>

---

## 19. `record`와 불변 객체를 요청·Domain 결과에 사용하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`record`는 같은 구성 값을 값 기준으로 비교하기 편하고, 생성 뒤 바뀌지 않는 데이터 묶음을 명확히 표현합니다. 검증된 event ID나 결제 이벤트가 처리 도중 몰래 변경되지 않으므로 “검증한 값과 저장한 값이 같다”는 추론이 쉬워집니다.

하지만 record가 자동으로 thread-safe한 Repository를 만들어 주지는 않습니다. 개별 값의 불변성과 여러 요청이 공유하는 컬렉션의 원자성은 별도로 설계해야 합니다.

</details>

---

## 20. `string?`의 `?`와 null 검사는 어떤 경계를 나타내나요?

<details>
<summary>정답과 해설</summary>

`string?`는 값이 없을 수 있음을 nullable 분석기와 독자에게 알립니다. 외부 JSON과 header는 누락되거나 `null`일 수 있으므로 입력 Draft에서는 nullable로 정직하게 표현하고, 검증을 통과한 Domain 객체 안쪽에서는 가능한 한 non-null 형식으로 좁힙니다.

null-forgiving 연산자 `!`는 runtime 검사를 하지 않습니다. 앞에서 실제 null 검사를 완료했다는 사실을 컴파일러에 알려 줄 뿐이므로, 외부 입력 검증을 대신해서 사용하면 안 됩니다.

</details>

---

## 21. Application Service는 이 웹훅 흐름에서 어떤 책임을 가지나요?

<details>
<summary>정답과 해설</summary>

Application Service는 “event ID 검증 → timestamp·서명 확인 → payload 해석 → Domain 검증 → receipt 원자적 저장”이라는 유스케이스 순서를 조정합니다. Domain 검증기, 서명 Strategy, Repository Port를 호출하지만 HTTP framework의 세부 사항이나 저장소 자료구조를 직접 알 필요는 없습니다.

이 경계를 두면 endpoint는 header/body를 받아 HTTP 응답으로 바꾸는 일에 집중하고, 핵심 흐름은 실제 웹 서버 없이 단위 테스트할 수 있습니다.

</details>

---

## 22. Repository Port와 in-memory Adapter를 분리하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

Application에는 “검증된 receipt를 event ID로 원자적으로 처음 한 번만 저장한다” 같은 업무 의미의 interface만 보입니다. lock, dictionary, SQL unique constraint 같은 저장 기술은 Adapter가 담당합니다.

따라서 테스트에서는 결정적인 fake를 사용할 수 있고, 운영에서는 Application 코드를 바꾸지 않고 durable DB Adapter로 교체할 수 있습니다. interface가 `HttpContext`나 `ConcurrentDictionary`를 그대로 노출하면 계층 분리의 이점이 줄어듭니다.

</details>

---

## 23. 서명 검증을 Strategy로 분리하면 무엇이 좋아지나요?

<details>
<summary>정답과 해설</summary>

`v1` HMAC-SHA256의 canonicalization, 서명 parsing과 비교 규칙을 한 구현에 모을 수 있습니다. Application Service는 구체 알고리즘보다 “이 요청의 서명이 유효한가?”라는 의도에 의존합니다.

나중에 키 회전이나 새 서명 버전을 지원할 때 새 Strategy를 추가할 수 있고, 테스트에서는 성공·실패를 원하는 대로 반환하는 fake로 유스케이스 분기를 검증할 수 있습니다. 알고리즘이 하나뿐인데 무조건 interface를 늘리는 것이 목적이 아니라, **변경 이유와 보안 규칙을 한 경계에 모으는 것**이 핵심입니다.

</details>

---

## 24. DI와 Composition Root는 각각 어떤 역할을 하나요?

<details>
<summary>정답과 해설</summary>

의존성 주입(DI)은 Application Service가 구체 클래스나 현재 시각을 내부에서 직접 만들지 않고 생성자를 통해 받게 합니다. Composition Root인 `Program.cs`는 어떤 Port에 어떤 Adapter/Strategy를 연결하고 어떤 lifetime을 쓸지 애플리케이션 시작점에서 한 번 결정합니다.

요청 사이 replay 상태를 공유하는 학습용 in-memory Repository는 적절한 공유 lifetime과 thread safety가 필요합니다. 반면 요청별 객체나 scoped DB context를 singleton이 붙잡으면 lifetime 오류가 생길 수 있으므로 의존성 수명도 함께 검토해야 합니다.

</details>

---

## 25. `TimeProvider`, fake Repository와 테스트용 Strategy가 테스트 용이성을 어떻게 높이나요?

<details>
<summary>정답과 해설</summary>

코드 안에서 `DateTimeOffset.UtcNow`를 직접 읽으면 ±300초 경계 테스트가 실행 시각에 따라 흔들립니다. 주입한 `TimeProvider`를 고정하면 299초, 정확히 300초, 301초와 미래 시각을 결정적으로 검사할 수 있습니다.

fake Repository로 첫 요청/중복/저장 실패를 만들고, 테스트용 Strategy로 서명 성공과 실패를 분리하면 외부 시스템 없이 Application 흐름을 빠르게 확인할 수 있습니다. 별도로 실제 Kestrel 검증을 두어 header parsing, raw body, body limit, DI 조립과 HTTP 상태까지 black-box로 확인하면 단위 테스트가 놓친 연결 오류를 찾을 수 있습니다.

</details>

---

## 26. 서버를 여러 대로 늘리거나 재시작하면 in-memory replay 방지는 왜 부족한가요?

<details>
<summary>정답과 해설</summary>

각 프로세스의 메모리는 서로 다릅니다. 첫 요청이 서버 A에 저장되고 같은 ID의 retry가 서버 B로 가면 B는 처음 보는 이벤트라고 판단합니다. 서버가 재시작해도 기억한 ID가 사라집니다.

운영에서는 모든 인스턴스가 공유하는 durable Repository와 unique constraint/transaction이 필요합니다. 공급자 계정마다 비밀키가 다르다면 replay key도 tenant/provider 경계를 포함해야 서로 다른 공급자의 우연히 같은 ID가 충돌하지 않습니다.

</details>

---

## 27. HMAC 검증을 넣은 뒤에도 남는 주요 보안 한계와 필수 테스트는 무엇인가요?

<details>
<summary>정답과 해설</summary>

HMAC은 만능 방화벽이 아닙니다. HTTPS, reverse proxy와 서버의 request-size limit, rate limiting, timeout/cancellation, 동시 연결 제한, secret store와 키 회전, 최소 권한, 민감한 body·서명 비로그, metric cardinality 제한, 운영 경보가 함께 필요합니다. 유효하게 서명된 payload도 Domain 규칙에 어긋날 수 있으므로 금액·통화·이벤트 종류 검증을 생략하면 안 됩니다.

최소한 다음 회귀 사례를 자동화해야 합니다.

- 정상 요청, body 한 바이트 변조, event ID 변조, timestamp 변조
- `v1=` 누락, 대문자 hex, 63/65자리, 여러 signature 값
- 과거·미래 299/300/301초 경계
- body 16,383/16,384/16,385바이트와 `Content-Length` 없는 전송
- 잘못된 UTF-8/JSON과 Domain 값
- 같은 ID의 순차 replay와 여러 동시 요청에서 성공이 정확히 하나인지

</details>

---

## 28. 이 자료의 stable/preview 버전 경계는 무엇이며, 왜 예제에는 stable 기능만 사용하나요?

<details>
<summary>정답과 해설</summary>

2026-10-16 기준 stable은 **.NET 10.0.13 / SDK 10.0.402 / C# 14**이고, preview 동향은 **.NET 11 RC 2 / SDK 11.0.100-rc.2 / C# 15 preview**입니다. 로컬 환경은 SDK 10.0.301과 runtime 10.0.9이며 예제 target은 `net10.0`, 언어 범위는 C# 14입니다.

preview/RC 기능은 출시 전 동작이나 API가 바뀔 수 있고 stable SDK에서 컴파일되지 않을 수 있습니다. 그래서 실행 예제는 현재 설치된 stable SDK로 재현 가능한 기능만 사용하고, preview 정보는 최신 동향 설명과 공식 출처로만 분리합니다.

</details>

---

## 간결한 복습 체크리스트

- [ ] HMAC의 인증·무결성과 HTTPS의 기밀성, replay 방지를 구분한다.
- [ ] `UTF8(timestamp + "." + eventId + ".") || rawBody`를 바이트 단위로 설명한다.
- [ ] raw body를 JSON 재직렬화하거나 trim하면 안 되는 이유를 안다.
- [ ] `X-Webhook-Signature`의 `v1=<64 lowercase hex>` 형식을 설명한다.
- [ ] event ID가 서명 범위에 없을 때 replay 우회가 가능한 이유를 설명한다.
- [ ] timestamp의 과거·미래 ±300초 경계와 `TimeProvider`의 가치를 안다.
- [ ] 16 KiB가 16,384바이트이며 실제 stream에도 제한을 둬야 함을 안다.
- [ ] 서명을 고정 시간으로 비교하고 비밀키를 코드·로그에 남기지 않는다.
- [ ] event ID 예약이 원자적이어야 하고 in-memory 저장소의 운영 한계를 안다.
- [ ] Result와 예외, record와 nullable의 역할을 구분한다.
- [ ] Application Service, Repository, Strategy, DI, Composition Root의 책임을 설명한다.
- [ ] stable .NET 10/C# 14와 preview .NET 11/C# 15를 구분한다.
