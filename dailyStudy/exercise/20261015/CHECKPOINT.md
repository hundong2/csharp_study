# 초보자 이해도 체크포인트 — SSE, async stream, replay와 backpressure

먼저 `<details>`를 열지 말고 자신의 말로 답하세요. 정답 문장을 외우는 것보다 [`README.md`](./README.md)의 구조도와 실제 코드에서 근거를 찾는 것이 중요합니다.

---

## 1. SSE는 어떤 통신 방식인가요?

<details>
<summary>정답과 해설</summary>

Server-Sent Events는 하나의 HTTP 응답 연결을 오래 유지하면서 서버가 클라이언트 방향으로 여러 이벤트를 차례로 보내는 표준입니다. 응답의 content type은 `text/event-stream`이며 각 이벤트는 `id`, `event`, `data` 같은 필드와 빈 줄로 구분됩니다.

기본 방향은 서버 → 클라이언트입니다. 클라이언트가 진행 상태를 등록하는 명령은 이 예제의 POST처럼 별도 HTTP 요청을 사용합니다.

</details>

---

## 2. 이 예제에서 일반 JSON 응답과 SSE 응답은 어떻게 다른가요?

<details>
<summary>정답과 해설</summary>

POST는 이벤트 하나를 만들고 완성된 JSON 응답 하나와 함께 요청을 끝냅니다. GET SSE는 `200 OK` 뒤 연결을 유지하면서 과거 이벤트를 replay하고 새 이벤트가 생길 때마다 계속 보냅니다.

SSE `data` 안의 payload가 JSON이어도 전체 HTTP body는 JSON 배열 하나가 아닙니다. SSE framing 안에 여러 JSON payload가 시간에 따라 이어집니다.

</details>

---

## 3. `.NET 10`의 `TypedResults.ServerSentEvents`를 쓰는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`IAsyncEnumerable<SseItem<T>>`를 ASP.NET Core의 typed SSE 응답으로 바꿔 줍니다. framework가 content type, SSE 형식, JSON 직렬화와 응답 쓰기를 담당하므로 endpoint가 `data:` 문자열과 newline을 직접 조립하지 않아도 됩니다.

구체적인 result 형식을 반환하므로 코드 가독성, 테스트, response metadata에도 도움이 됩니다.

</details>

---

## 4. `SseItem<T>`의 `EventId`, `EventType`, `Data`는 각각 무엇인가요?

<details>
<summary>정답과 해설</summary>

- `EventId`: 재연결 시 마지막 처리 위치를 나타내는 안정적인 ID
- `EventType`: 클라이언트가 이벤트 종류별 handler를 선택할 수 있는 이름
- `Data`: 실제 진행 상태 payload

event ID는 화면 표시용 임의 번호가 아니라 replay 계약의 일부입니다. 같은 deployment stream 안에서 순서가 안정적이어야 합니다.

</details>

---

## 5. `Task<List<T>>`와 `IAsyncEnumerable<T>`의 차이는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`Task<List<T>>`는 보통 모든 항목을 모은 뒤 목록 하나를 반환합니다. `IAsyncEnumerable<T>`는 다음 항목이 준비될 때마다 하나씩 전달할 수 있어 끝나는 시각을 미리 알 수 없는 live stream에 자연스럽습니다.

비동기 스트림은 소비자가 열거를 중단할 수 있고 전체 결과를 메모리에 모을 필요도 없습니다. 다만 replay history 자체의 보존 크기는 별도로 관리해야 합니다.

</details>

---

## 6. `yield return`은 일반 `return`과 어떻게 다른가요?

<details>
<summary>정답과 해설</summary>

`return`은 메서드 실행을 끝내고 결과를 반환합니다. iterator의 `yield return`은 현재 항목 하나를 소비자에게 전달한 뒤 실행 위치를 보존하며, 소비자가 다음 항목을 요청하면 그 다음부터 계속합니다.

그래서 과거 이벤트를 하나씩 replay한 뒤 같은 메서드에서 live channel을 계속 읽을 수 있습니다.

</details>

---

## 7. `await foreach`는 무엇을 기다리나요?

<details>
<summary>정답과 해설</summary>

비동기 enumerator가 다음 항목을 준비하는 `MoveNextAsync`를 기다립니다. 다음 live 이벤트가 아직 없다면 thread를 계속 붙잡고 반복하는 대신 비동기로 멈춥니다.

열거가 끝나거나 예외가 나면 enumerator의 비동기 dispose도 수행하므로 Repository의 `finally` 정리와 연결됩니다.

</details>

---

## 8. `[EnumeratorCancellation]`은 왜 필요한가요?

<details>
<summary>정답과 해설</summary>

소비자가 `GetAsyncEnumerator(token)` 또는 `WithCancellation(token)`으로 전달한 취소 token을 async iterator 본문에서 사용할 수 있게 compiler에 알려 줍니다.

이 예제에서는 HTTP 연결 종료 신호가 channel read까지 도달하고, iterator의 `finally`가 subscriber를 제거하도록 연결합니다. 단지 method parameter에 token 이름만 써 두는 것으로는 소비 측 열거 token이 자동 결합되지 않는 경우가 있습니다.

</details>

---

## 9. `Last-Event-ID: 7`이면 서버는 ID 7을 다시 보내나요?

<details>
<summary>정답과 해설</summary>

정상 계약에서는 ID 7보다 **큰** 이벤트만 보냅니다. 이 header는 “마지막으로 처리한 이벤트”를 뜻하므로 replay 조건은 `eventId > 7`입니다.

다만 연결이 끊긴 정확한 시점 때문에 client가 같은 이벤트를 다시 볼 가능성을 완전히 제거할 수는 없습니다. client도 event ID 기준으로 idempotent하게 중복을 무시해야 합니다.

현재 마지막 ID보다 큰 cursor도 형식상 유효하므로 거절하지 않습니다. replay와 live 양쪽에서 그 cursor보다 큰 ID만 전달하며, terminal이 먼저 오면 아무 항목 없이 stream이 끝날 수 있습니다.

</details>

---

## 10. reconnect가 exactly-once 전달을 보장하나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 서버가 bytes를 보냈지만 client가 처리 완료 전에 끊길 수 있고, client가 처리했지만 마지막 ID를 저장하기 전에 끊길 수도 있습니다. 따라서 재전송 또는 중복 가능성이 있습니다.

서버는 순서와 replay를 제공하고, client는 ID로 중복 처리를 막는 at-least-once 친화적인 설계가 현실적입니다. exactly-once는 HTTP 재연결 header 하나로 자동 생기지 않습니다.

</details>

---

## 11. replay 뒤 subscriber를 등록하면 어떤 race가 생기나요?

<details>
<summary>정답과 해설</summary>

history snapshot을 복사한 뒤 live channel을 등록하기 전에 새 이벤트가 append될 수 있습니다. 그 이벤트는 snapshot에도 없고 아직 subscriber도 없으므로 영원히 누락됩니다.

snapshot/cutover 결정과 subscriber 등록을 한 일관된 동기화 경계에서 처리해야 합니다. 정확한 lock 구현보다 “경계 사이 이벤트가 빠지지 않는다”는 invariant가 우선입니다.

</details>

---

## 12. replay에서 중복과 누락 중 무엇이 더 위험한가요?

<details>
<summary>정답과 해설</summary>

둘 다 다뤄야 하지만 일반적으로 누락이 복구하기 더 어렵습니다. 중복은 안정적인 event ID를 기억해 무시할 수 있지만, client가 존재 자체를 모르는 누락 이벤트는 다시 요청할 단서가 없습니다.

그래서 replay/live cutover는 누락을 막고, end-to-end client 처리는 중복에 안전하게 설계합니다.

</details>

---

## 13. `Channel<T>`는 이 예제에서 어떤 역할인가요?

<details>
<summary>정답과 해설</summary>

producer인 POST/Repository append와 consumer인 SSE 연결 사이에서 새 live 이벤트를 비동기적으로 전달하는 thread-safe producer/consumer queue입니다.

과거 이벤트의 영구 원본은 아닙니다. 이 예제에서도 replay는 저장 history가 담당하고 channel은 구독 뒤 도착한 live 알림을 전달합니다.

</details>

---

## 14. 왜 unbounded channel이 아니라 bounded channel을 사용하나요?

<details>
<summary>정답과 해설</summary>

느린 client보다 producer가 계속 빠르면 읽지 못한 항목이 queue에 무제한 쌓여 메모리를 고갈시킬 수 있습니다. bounded channel은 subscriber 하나가 보유할 대기 항목 수에 상한을 둡니다.

상한에 도달했을 때 기다릴지 버릴지는 `BoundedChannelFullMode`로 정하며, 이는 단순 구현 옵션이 아니라 지연·손실·가용성의 제품 계약입니다.

</details>

---

## 15. `BoundedChannelFullMode.Wait`와 drop mode의 trade-off는 무엇인가요?

<details>
<summary>정답과 해설</summary>

일반적으로 `WriteAsync`와 `Wait`를 조합하면 공간이 날 때까지 writer가 기다려 event 손실을 피하지만 느린 subscriber가 producer의 응답 시간을 늘릴 수 있습니다. drop mode는 producer 진행을 유지하는 대신 이벤트를 버립니다.

현재 Adapter는 `FullMode.Wait` channel에 lock 안에서 `TryWrite`를 호출합니다. buffer가 가득 차 `false`가 나오면 기다리지 않고 그 느린 subscriber를 제거하고 오류로 channel을 완료합니다. 빠른 subscriber와 producer는 계속 진행하고, 끊긴 client는 `Last-Event-ID`로 history replay를 요청합니다.

</details>

---

## 16. 여러 SSE client가 채널 하나를 함께 읽으면 왜 안 되나요?

<details>
<summary>정답과 해설</summary>

하나의 `ChannelReader<T>`를 여러 reader가 소비하면 보통 각 항목을 경쟁해 나눠 가집니다. 이는 작업자들이 job을 분담하는 work queue에는 맞지만 모든 client가 같은 진행 이벤트를 받아야 하는 broadcast에는 맞지 않습니다.

따라서 subscriber마다 별도 channel을 만들고 append 때 각 channel에 fan-out해야 합니다.

</details>

---

## 17. client가 연결을 끊으면 어떤 정리가 필요하나요?

<details>
<summary>정답과 해설</summary>

`RequestAborted` 취소가 Presentation → Application → Repository/channel read로 전달되어 열거를 멈춰야 합니다. async iterator의 `finally`에서 subscriber를 collection에서 제거하고 필요하면 writer를 complete합니다.

정리하지 않으면 사라진 client의 channel과 buffer가 남고 모든 append 때 불필요한 fan-out을 계속해 memory leak과 성능 저하가 생깁니다.

</details>

---

## 18. terminal 이벤트를 처리하는 올바른 순서는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`Completed` 또는 `Failed`에 event ID를 부여하고 history에 저장한 뒤, 현재 subscriber에 terminal 이벤트를 전달하고, 그 다음 channel writer를 complete합니다.

먼저 complete하면 마지막 상태를 놓칩니다. terminal이 이미 저장된 stream을 나중에 구독하면 요청한 ID 뒤 이벤트를 replay한 후 live 대기 없이 정상 종료합니다.

브라우저 `EventSource`는 EOF 뒤에도 자동 재연결하므로 client는 `isTerminal: true`를 처리한 뒤 `eventSource.close()`를 호출해야 합니다.

</details>

---

## 19. 불변 `record`를 쓰면 Repository가 자동으로 thread-safe한가요?

<details>
<summary>정답과 해설</summary>

아닙니다. record 불변성은 개별 이벤트 객체가 나중에 바뀌는 위험을 줄입니다. 그러나 다음 ID 계산, history append, subscriber 등록/제거, fan-out 순서 같은 여러 공유 상태 연산은 여전히 동기화가 필요합니다.

불변 값과 원자적인 상태 전이는 서로 보완하지만 같은 개념이 아닙니다.

</details>

---

## 20. 외부 DTO에는 nullable을 허용하고 Domain 안쪽은 non-null로 만드는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`RespectRequiredConstructorParameters = true`이므로 요청 positional field를 JSON에서 누락하면 framework가 Domain 호출 전에 `400`으로 거절합니다. 그러나 `stage: null`이나 `message: null`을 명시해 보낼 수는 있으므로 DTO와 Draft의 해당 값은 `string?`로 표현하고 Strategy가 안정적인 Domain 오류로 바꿉니다. `percent: null`은 non-nullable `int` binding 오류입니다.

`Last-Event-ID` header는 처음 연결에서 없을 수 있어 별도로 optional입니다. Application은 없거나 공백인 값을 cursor `0`으로 해석합니다. 검증·정규화가 성공한 뒤 Domain에는 유효한 값만 전달하면 핵심 로직의 모든 줄에서 null 검사를 반복하지 않아도 됩니다.

null-forgiving `!`는 검사 기능이 아니므로 검증 경계를 대신할 수 없습니다.

</details>

---

## 21. Result, 예외, 취소는 어떻게 구분하나요?

<details>
<summary>정답과 해설</summary>

- `Result<T>`: 잘못된 입력, stage-percent 불일치, terminal 뒤 append처럼 호출자가 예상하고 분기할 수 있는 결과
- 예외: 깨진 내부 invariant, 잘못된 구성, 예기치 못한 저장소 장애
- 취소: client가 연결을 끊어 더 이상 결과를 기다리지 않는 제어 흐름

사용자가 고칠 수 있는 오류를 모두 예외로 만들거나, 서버 결함을 400 Result로 숨기거나, 취소를 500으로 기록하면 의미와 관측성이 흐려집니다.

</details>

---

## 22. LINQ는 어디에 적합하고 어디에 부적합한가요?

<details>
<summary>정답과 해설</summary>

확보한 history snapshot에서 기준 ID보다 큰 항목을 고르고 정렬하는 조회에는 `Where`, `OrderBy` 같은 LINQ가 의도를 잘 드러냅니다.

다음 ID 부여, append, fan-out, terminal completion처럼 순서와 원자성이 중요한 상태 변경을 긴 LINQ chain에 숨기면 동시성 경계를 이해하기 어렵습니다. LINQ 자체는 thread safety를 제공하지 않습니다.

</details>

---

## 23. Strategy를 별도 interface로 둔 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`IDeploymentProgressTransitionStrategy`가 최초 stage, 허용 전이, stage별 percent, 단조 증가와 terminal 판단을 한곳에 모읍니다. 같은 `Building`/`Deploying` 단계의 동일 percent는 message-only 상세 갱신을 위해 허용하지만 감소는 거절합니다. Application Service는 구체 범위를 직접 반복하지 않고 “Strategy에 다음 Event 생성을 요청한다”는 유스케이스 흐름에 집중합니다.

정책을 작은 단위로 테스트하고, 새 단계나 환경별 규칙으로 교체할 수 있습니다. interface를 만들었다는 사실보다 변경 이유가 다른 책임을 분리했다는 점이 중요합니다.

</details>

---

## 24. Repository Port와 Adapter 분리는 무엇을 얻나요?

<details>
<summary>정답과 해설</summary>

Application은 append와 stream이라는 필요한 동작만 Port로 알고, history collection·lock·`Channel<T>` 같은 구현은 In-memory Adapter가 담당합니다. Application 테스트는 작은 fake를 사용할 수 있고 나중에 DB/broker Adapter로 바꿀 수 있습니다.

Port에 `HttpContext`나 channel 구현 형식을 그대로 노출하면 의존성 역전의 이점이 줄어듭니다. interface는 업무 의미의 경계를 표현해야 합니다.

</details>

---

## 25. In-memory Repository의 DI lifetime이 왜 중요한가요?

<details>
<summary>정답과 해설</summary>

요청마다 새 instance를 만들면 POST가 저장한 history와 subscriber를 다음 GET/POST가 볼 수 없습니다. 학습용 메모리 저장소는 요청 사이 상태를 공유하는 lifetime이 필요합니다.

공유 mutable instance를 사용하면 그 내부 thread safety가 반드시 필요합니다. 또한 singleton이 요청 scoped service를 붙잡지 않도록 의존성 lifetime도 함께 확인해야 합니다.

</details>

---

## 26. 서버를 두 대로 늘리면 현재 예제가 왜 깨지나요?

<details>
<summary>정답과 해설</summary>

각 프로세스의 메모리 history와 subscriber channel은 서로 공유되지 않습니다. POST가 서버 A에 저장되고 SSE GET이 서버 B에 연결되면 B는 이벤트를 모릅니다. 재시작해도 history가 사라집니다.

운영에서는 durable shared event store와 live pub/sub/broker, stream별 순서, retention, gap recovery를 설계해야 합니다. sticky session만으로 내구성과 재시작 문제까지 해결되지는 않습니다.

</details>

---

## 27. self-test와 실제 Kestrel HTTP 검증이 둘 다 필요한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

self-test는 Domain, Strategy, Application, Adapter의 경계와 동시성을 빠르고 결정적으로 확인합니다. 그러나 실제 header parsing, content type, JSON binding, SSE framing/flush, middleware, Production DI 조립은 놓칠 수 있습니다.

`verify-http.ps1`은 실제 Kestrel을 실행해 외부 관찰 가능한 계약을 black-box로 확인합니다. 두 종류는 서로 대체하지 않고 결함을 찾는 범위가 다릅니다.

</details>

---

## 28. 왜 C# 15 preview 기능을 예제 코드에 쓰지 않았나요?

<details>
<summary>정답과 해설</summary>

2026-10-15 기준 stable은 .NET 10.0.13, SDK 10.0.402, C# 14이고 로컬 설치 SDK는 10.0.301입니다. .NET 11 RC 2 / SDK 11.0.100-rc.2와 C# 15는 최신 동향이지만 preview/RC 범위입니다.

학습자가 현재 stable SDK로 그대로 빌드할 수 있도록 예제는 `net10.0`과 C# 14 범위만 사용합니다. preview 기능은 설명과 공식 링크로만 분리합니다.

</details>

---

## 29. 빈칸을 채워 전체 흐름을 완성하세요

> “Presentation은 `__________` header 문자열을 읽고 Application은 이를 0 이상의 cursor로 검증한다. Repository는 저장된 history를 `__________`한 뒤 subscriber별 bounded `__________`로 live 이벤트를 보낸다. Application은 이를 `__________<T>`로 노출하고 Presentation은 `TypedResults.__________`로 SSE 응답을 만든다. 연결 종료는 `__________`으로 전파되며 terminal 이벤트 뒤 channel을 `__________`한다.”

<details>
<summary>정답과 해설</summary>

> “Presentation은 **`Last-Event-ID`** header 문자열을 읽고 Application은 이를 0 이상의 cursor로 검증한다. Repository는 저장된 history를 **replay**한 뒤 subscriber별 bounded **`Channel<T>`**로 live 이벤트를 보낸다. Application은 이를 **`IAsyncEnumerable`**`<T>`로 노출하고 Presentation은 `TypedResults.`**`ServerSentEvents`**로 SSE 응답을 만든다. 연결 종료는 **`CancellationToken`**으로 전파되며 terminal 이벤트 뒤 channel을 **complete**한다.”

</details>

---

## 간결한 복습 체크리스트

- [ ] SSE의 방향, content type, event framing을 설명한다.
- [ ] `SseItem<T>`의 ID/type/data를 구분한다.
- [ ] `IAsyncEnumerable<T>`, `yield return`, `await foreach`를 설명한다.
- [ ] `[EnumeratorCancellation]`과 요청 취소의 연결을 안다.
- [ ] replay와 live 모두 `Last-Event-ID`보다 큰 ID만 전달한다.
- [ ] replay/live cutover 누락 race를 설명한다.
- [ ] subscriber별 bounded channel이 필요한 이유를 안다.
- [ ] `Wait`와 drop mode의 trade-off를 설명한다.
- [ ] terminal 저장·fan-out·complete 순서를 안다.
- [ ] nullable 경계, 불변 record, Result/예외/취소를 구분한다.
- [ ] LINQ, Strategy, Port/Adapter, DI/SOLID의 Why를 설명한다.
- [ ] 단일 프로세스 메모리 Adapter의 운영 한계를 안다.
- [ ] stable .NET 10/C# 14와 Preview .NET 11/C# 15를 구분한다.
