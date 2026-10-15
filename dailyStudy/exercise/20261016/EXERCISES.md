# 실습 문제 — HMAC-SHA256 결제 웹훅 수신 API (Beginner → Pro)

각 Level은 이전 Level의 이해를 바탕으로 합니다. 먼저 기준선을 실행하고, 실패를 하나씩 만들고, 왜 실패했는지 자신의 말로 설명하세요. 정답 코드를 그대로 외우는 것보다 **어느 경계가 어떤 공격·실수를 막는지** 연결하는 것이 목표입니다.

---

## 공통 준비

PowerShell에서 오늘 폴더로 이동합니다.

```powershell
Set-Location D:\workspace\csharp_study\dailyStudy\exercise\20261016
dotnet --version
dotnet restore .\src\WebhookReceiverApi\WebhookReceiverApi.csproj
dotnet build .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release --no-restore
dotnet run --project .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release --no-build -- --self-test
pwsh -NoProfile -File .\verify-http.ps1
```

기준선이 통과하지 않으면 과제 코드를 추가하기 전에 원인을 해결합니다. 빌드 산출물인 `bin/`, `obj/`, 로그, secret은 커밋하지 않습니다.

---

## Level 1 — Beginner: HTTP 요청을 신뢰 경계로 나누기

### 문제

다음 입력을 “아직 신뢰할 수 없는 외부 입력”과 “검증 뒤 생기는 내부 값”으로 분류하세요.

- `Content-Type`
- `X-Webhook-Timestamp`
- `X-Webhook-Id`
- `X-Webhook-Signature`
- raw body bytes
- `PaymentWebhookDraft`
- `PaymentWebhookEvent`
- `WebhookReceipt`

그 뒤 [`WebhookEndpoints.cs`](./src/WebhookReceiverApi/Presentation/WebhookEndpoints.cs)에서 시작해 각 값이 [`WebhookReceiverApplicationService.cs`](./src/WebhookReceiverApi/Application/WebhookReceiverApplicationService.cs)와 Domain으로 이동하는 경로를 종이에 화살표로 그리세요.

### 실행

```powershell
rg -n "X-Webhook|RawBody|PaymentWebhook|WebhookReceipt" .\src\WebhookReceiverApi
```

### 완료 기준

- [ ] header와 body가 처음에는 모두 공격자가 조작할 수 있는 값임을 설명한다.
- [ ] Draft와 검증된 Domain event의 차이를 말한다.
- [ ] `WebhookReceipt`가 만들어졌다는 사실과 downstream 결제 처리가 끝났다는 사실을 구분한다.

---

## Level 2 — Beginner+: canonical bytes를 직접 만들기

### 문제

아래 PowerShell을 실행해 signature를 계산합니다. body 문자열의 공백 하나, timestamp 한 자리, event ID 한 글자를 각각 바꾸어 signature가 어떻게 달라지는지 확인하세요.

```powershell
$secretBytes = [Text.Encoding]::UTF8.GetBytes('local-learning-secret-change-me-20261016')
$timestamp = '1792112400'
$eventId = 'evt_exercise_0001'
$bodyText = '{"type":"payment.succeeded","data":{"paymentId":"pay_1001","amount":39000.00,"currency":"KRW"}}'
$rawBody = [Text.Encoding]::UTF8.GetBytes($bodyText)
$prefix = [Text.Encoding]::UTF8.GetBytes("$timestamp.$eventId.")
$canonical = [byte[]]::new($prefix.Length + $rawBody.Length)
[Array]::Copy($prefix, 0, $canonical, 0, $prefix.Length)
[Array]::Copy($rawBody, 0, $canonical, $prefix.Length, $rawBody.Length)
$mac = [Security.Cryptography.HMACSHA256]::HashData($secretBytes, $canonical)
$signature = 'v1=' + [Convert]::ToHexString($mac).ToLowerInvariant()
$signature
```

그 다음 다음 세 변형을 한 번에 하나씩 적용하고 다시 계산합니다.

1. body의 `:` 뒤에 공백 한 칸 추가
2. timestamp 마지막 숫자 변경
3. event ID를 `evt_exercise_0002`로 변경

### 완료 기준

- [ ] canonical 식을 정확히 `UTF8(timestamp + "." + eventId + ".") || rawBody`로 적는다.
- [ ] `||`가 byte sequence 결합임을 설명한다.
- [ ] JSON 의미가 같아도 bytes가 다르면 HMAC이 달라짐을 관찰한다.
- [ ] event ID가 서명되지 않을 때 replay ID 바꿔치기가 가능한 이유를 말한다.

---

## Level 3 — Basic: signature 형식을 엄격하게 검증하기

### 문제

[`HmacSha256WebhookSignatureVerifier.cs`](./src/WebhookReceiverApi/Infrastructure/HmacSha256WebhookSignatureVerifier.cs)의 파싱 규칙을 읽고 아래 후보를 표로 판정하세요.

| 후보 | 허용/거절 | 이유 |
| --- | --- | --- |
| `v1=` + lowercase hex 64자 |  |  |
| `v1=` + uppercase hex 64자 |  |  |
| `v1=` + hex 62자 |  |  |
| `v2=` + lowercase hex 64자 |  |  |
| `v1=... , v1=...` |  |  |
| Base64 44자 |  |  |

### 구현 과제

[`SelfTestRunner.cs`](./src/WebhookReceiverApi/SelfTesting/SelfTestRunner.cs)에 uppercase hex와 잘못된 version이 거절되는 테스트를 하나씩 추가하세요. 새 테스트 메서드 위에는 목적·파라미터·반환값을 설명하는 한글 주석을 작성합니다.

### 검증

```powershell
dotnet build .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release
dotnet run --project .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release --no-build -- --self-test
```

### 완료 기준

- [ ] SHA-256 HMAC 결과 32 bytes가 hex 64자라는 관계를 설명한다.
- [ ] parser가 모호한 여러 signature와 알 수 없는 version을 조용히 받아들이지 않는다.
- [ ] 유효 signature 비교에 `CryptographicOperations.FixedTimeEquals`를 사용한다.

---

## Level 4 — Basic+: raw body 16 KiB 경계 검증하기

### 문제

16 KiB는 `16 * 1024 = 16,384` bytes입니다. 정확히 16,384 bytes와 16,385 bytes인 body를 만들었을 때 어떤 계층이 어떻게 반응해야 하는지 예상한 뒤 테스트하세요.

[`WebhookEndpoints.cs`](./src/WebhookReceiverApi/Presentation/WebhookEndpoints.cs)의 body reader를 확인하며 다음 질문에 답합니다.

1. `Content-Length`가 16,385이면 body를 읽기 전에 거절할 수 있나요?
2. `Content-Length`가 없으면 어떻게 실제 stream을 제한하나요?
3. 16,384자의 한글 문자열이 왜 16 KiB가 아닐 수 있나요?
4. 제한보다 한 byte 더 읽어 보는 이유는 무엇인가요?

### 실행

```powershell
pwsh -NoProfile -File .\verify-http.ps1
```

스크립트의 oversized-body 검증을 찾아 경계가 16,384와 16,385 중 어디에서 바뀌는지 확인합니다.

### 완료 기준

- [ ] byte와 char의 차이를 설명한다.
- [ ] 선언된 `Content-Length`와 실제 stream 양쪽을 방어해야 함을 안다.
- [ ] 큰 요청을 전부 메모리에 읽은 뒤 크기를 재는 방식이 왜 늦은지 설명한다.

---

## Level 5 — Intermediate: ±300초 timestamp 경계표 만들기

### 문제

서버의 고정 현재 시각을 `12:00:00 UTC`라고 가정하고 아래 요청을 허용/거절로 분류하세요. 절댓값 기준임을 잊지 마세요.

| 서명 시각 | 차이 | 예상 |
| --- | ---: | --- |
| `11:55:00` | -300초 |  |
| `11:54:59` | -301초 |  |
| `12:05:00` | +300초 |  |
| `12:05:01` | +301초 |  |

### 구현 과제

[`ManualTimeProvider.cs`](./src/WebhookReceiverApi/SelfTesting/ManualTimeProvider.cs)를 이용해 정확히 `-300`, `-301`, `+300`, `+301`초를 모두 검사하는 self-test를 추가하세요. 테스트에서 실제 `UtcNow`나 `Task.Delay`를 사용하지 않습니다.

### 검증

```powershell
dotnet run --project .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release -- --self-test
```

### 완료 기준

- [ ] 과거뿐 아니라 지나치게 먼 미래 timestamp도 거절한다.
- [ ] 경계의 포함 여부를 테스트 이름과 assertion으로 명시한다.
- [ ] `TimeProvider`가 테스트를 빠르고 결정적으로 만드는 이유를 설명한다.

---

## Level 6 — Intermediate+: Domain validation과 Result 구분하기

### 문제

[`PaymentWebhookEvent.cs`](./src/WebhookReceiverApi/Domain/PaymentWebhookEvent.cs)의 `Create`에 다음 입력을 하나씩 넣는 self-test를 작성하세요.

1. 알 수 없는 `type`
2. 공백 `paymentId`
3. 0원
4. 소수 셋째 자리 금액
5. 소문자 통화 코드 `krw`
6. 유효한 `payment.failed`

각 경우에 “성공 값”, “예상 `Result` 실패”, “예외” 중 무엇이 맞는지 먼저 적습니다.

### 추가 질문

- `PaymentWebhookDraft`의 문자열은 왜 nullable인가요?
- 검증된 `PaymentWebhookEvent`의 property는 왜 non-null인가요?
- switch expression의 `_` arm은 무엇을 잡나요?
- 잘못된 금액을 예외로 던지지 않는 이유는 무엇인가요?

### 검증

```powershell
dotnet build .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release
dotnet run --project .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release --no-build -- --self-test
```

### 완료 기준

- [ ] 외부 입력 오류는 안정적인 code가 있는 `Result`로 확인한다.
- [ ] 내부 invariant 파괴나 구성 오류는 예외로 남겨야 하는 이유를 설명한다.
- [ ] 불변 `record`가 검증 뒤 값의 우연한 변경을 줄이는 이유를 안다.

---

## Level 7 — Advanced: replay 경쟁 조건 재현하기

### 문제

같은 유효한 event ID와 같은 서명으로 32개 작업을 동시에 실행해 Repository claim을 경쟁시킵니다. 기대 결과는 **정확히 1개 성공, 31개 replay 실패**입니다.

먼저 잘못된 구현을 말로 설명하세요.

```text
if (!repository.Contains(id))
{
    repository.Add(receipt);
}
```

두 요청이 `Contains`를 모두 통과할 수 있는 시간선을 그립니다. 그런 다음 [`InMemoryWebhookReceiptRepository.cs`](./src/WebhookReceiverApi/Infrastructure/InMemoryWebhookReceiptRepository.cs)의 원자적 연산을 찾아봅니다.

### 구현 과제

[`SelfTestRunner.cs`](./src/WebhookReceiverApi/SelfTesting/SelfTestRunner.cs)에 동시 요청 32개를 만드는 테스트가 없다면 추가하고, 있다면 동시 요청 수를 바꾸어 두 번 이상 실행합니다.

### 검증

```powershell
1..5 | ForEach-Object {
    dotnet run --project .\src\WebhookReceiverApi\WebhookReceiverApi.csproj -c Release --no-build -- --self-test
}
```

### 완료 기준

- [ ] 실행을 반복해도 성공 수가 항상 1임을 확인한다.
- [ ] 불변 `record`와 collection의 thread safety가 서로 다른 문제임을 설명한다.
- [ ] 운영 DB에서는 unique constraint와 transaction이 이 계약을 어떻게 지킬지 설명한다.

---

## Level 8 — Advanced+: 인증된 ID와 payload의 결합 검증하기

### 문제

정상 요청 하나를 기준으로 다음 변조를 각각 수행합니다. **signature는 원래 값 그대로** 둡니다.

1. body의 `amount`만 변경
2. `X-Webhook-Timestamp`만 변경
3. `X-Webhook-Id`만 변경

세 요청이 모두 signature 검증에서 실패해야 합니다. 특히 3번이 통과한다면 event ID가 canonical bytes에서 빠진 심각한 replay 우회입니다.

### 실행

```powershell
pwsh -NoProfile -File .\verify-http.ps1
```

검증 스크립트에 ID-only 변조 case가 없다면 추가합니다. request마다 새 signature를 다시 계산하지 않도록 주의하세요. “변조 후에도 원래 signature를 재사용한다”가 이 테스트의 핵심입니다.

### 완료 기준

- [ ] 세 필드가 모두 HMAC으로 결합되어 있음을 black-box HTTP 테스트로 증명한다.
- [ ] 같은 body와 signature에 새 event ID만 붙여 replay 저장소를 우회할 수 없음을 확인한다.
- [ ] 응답과 로그가 expected signature나 secret을 노출하지 않는지 확인한다.

---

## Level 9 — Pro: 새 Signature Strategy 설계하기

### 문제

secret 회전 요구가 들어왔다고 가정합니다. 현재 key와 이전 key를 10분 동안 함께 허용해야 하지만, 공격자가 header로 임의 알고리즘 downgrade를 선택해서는 안 됩니다.

다음을 설계 문서 또는 별도 연습 branch에 구현하세요.

- 서버가 허용하는 key ID 목록
- 현재 key로 먼저 검증하고 이전 key는 제한된 기간만 검증하는 정책
- key ID도 canonical 입력에 넣을지에 대한 결정과 이유
- 알 수 없는 version/key ID의 즉시 거절
- 어느 key가 성공했는지 외부 응답에 노출하지 않는 정책
- key material을 소스가 아닌 secret store에서 가져오는 경계

### 테스트 목록

1. 현재 key 성공
2. 허용 기간 안의 이전 key 성공
3. 기간 뒤 이전 key 실패
4. 알 수 없는 key ID 실패
5. event ID/body/timestamp 변조 실패
6. 두 key 모두 실패해도 timing·응답으로 상세 차이를 과도하게 드러내지 않음

### 완료 기준

- [ ] Application Service의 유스케이스 순서는 바꾸지 않고 Strategy를 교체할 수 있다.
- [ ] Options가 시작 시 key 구성 오류를 검출한다.
- [ ] header가 서버의 허용 알고리즘 정책을 마음대로 약화시키지 못한다.

---

## Level 10 — Pro+: durable replay와 side effect 설계하기

### 문제

서버를 두 대로 늘리고 재시작해도 replay를 막아야 하며, 접수한 결제 이벤트를 메시지 브로커에 전달해야 한다고 가정합니다. 다음 실패 시나리오를 해결하는 구조를 그리세요.

```text
DB에 event ID 저장 성공 → broker publish 직전에 프로세스 종료
```

다음 항목을 반드시 포함합니다.

- 공유 DB의 event ID unique constraint
- receipt와 outbox message를 같은 transaction에 기록
- 별도 worker가 outbox publish 후 상태 갱신
- broker의 at-least-once delivery를 견디는 idempotent consumer
- replay record의 retention/TTL과 감사 보존 정책
- secret rotation과 중앙 secret store
- metric: accepted, replayed, signature_failed, expired, oversized
- body/secret/signature를 제외한 안전한 구조화 로그

### 검증 전략

구현하지 않아도 다음 integration test를 구체적으로 작성하세요.

1. 같은 ID를 서로 다른 두 API instance에 동시에 전송
2. DB commit 뒤 API process 강제 종료
3. outbox worker 재시작
4. broker가 같은 message를 두 번 전달
5. 최종 업무 side effect가 정확히 한 번 관찰되는지 확인

### 완료 기준

- [ ] webhook 접수 idempotency와 downstream exactly-once 효과가 같은 문제가 아님을 설명한다.
- [ ] 단일 프로세스 `ConcurrentDictionary`만으로 다중 instance를 보호할 수 없음을 안다.
- [ ] transaction/outbox/idempotent consumer가 각각 막는 실패 구간을 설명한다.

---

## 최종 제출 체크

- [ ] `dotnet restore`와 Release build가 성공한다.
- [ ] warning 0개, error 0개다.
- [ ] self-test가 통과한다.
- [ ] 실제 Kestrel HTTP 검증이 통과한다.
- [ ] canonical 입력에 timestamp, event ID, raw body가 정확히 들어간다.
- [ ] body·timestamp·event ID 중 하나만 바꾸어도 원래 signature가 실패한다.
- [ ] 정확히 300초는 허용하고 301초는 거절하는 경계를 검증했다.
- [ ] 16,384 bytes와 16,385 bytes 경계를 검증했다.
- [ ] 같은 event ID의 동시 요청 중 정확히 하나만 성공한다.
- [ ] 모든 새 C# 메서드 위에 목적·파라미터·반환값의 쉬운 한글 설명이 있다.
- [ ] 처음 등장한 문법과 보안·아키텍처 선택의 Why를 주석으로 설명했다.
- [ ] secret, signature, body, `bin/`, `obj/`, 로그를 커밋하지 않았다.
