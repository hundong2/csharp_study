# 2026-09-04 단계별 실행 과제

> 각 단계는 [`src/ExchangeRateClientExercise/`](./src/ExchangeRateClientExercise/)의 코드를 직접 고친 뒤 빌드와 자체 테스트로 확인합니다. 새 메서드에는 목적·매개변수·반환값을 설명하는 한글 주석을 쓰고, 낯선 문법의 첫 사용에는 “무엇이며 왜 쓰는지”를 덧붙이세요.

```powershell
dotnet build ./src/ExchangeRateClientExercise/ExchangeRateClientExercise.csproj
dotnet run --project ./src/ExchangeRateClientExercise
dotnet run --project ./src/ExchangeRateClientExercise -- --self-test
```

## 1단계 — fake JSON을 DTO와 Domain으로 연결하기

[`Infrastructure.cs`](./src/ExchangeRateClientExercise/Infrastructure.cs)의 fake JSON `rates` 객체에 유효한 통화-환율 항목 하나를 추가하고, 역직렬화 DTO의 어떤 속성이 [`Domain.cs`](./src/ExchangeRateClientExercise/Domain.cs)의 어떤 값으로 옮겨지는지 표로 적으세요. 추가 항목의 통화 코드는 세 글자로 만들고 환율은 양수로 둡니다.

검증 기준:

- 기본 실행이 외부망 없이 성공합니다.
- 추가한 통화-환율 항목이 역직렬화되고 Domain 검증을 통과합니다.
- “JSON 모양과 업무 모델을 분리해야 제공자 변경의 영향이 Infrastructure에 머문다”는 이유를 설명합니다.

## 2단계 — 환율 선택 Strategy 교체하기

[`Application.cs`](./src/ExchangeRateClientExercise/Application.cs)의 `IRateSelectionPolicy`를 구현하는 `FeeAdjustedRateSelectionPolicy`를 만드세요. 같은 통화에는 `1`을 유지하고, 다른 통화에는 표의 정확한 환율에 학습용 수수료율을 곱합니다. [`Program.cs`](./src/ExchangeRateClientExercise/Program.cs)의 Composition Root에서 `ExactCurrencyRateSelectionPolicy` 대신 새 구현을 주입합니다.

검증 기준:

- 표에 있는 통화는 수수료가 반영되고, 기준 통화 자신은 여전히 `1`입니다.
- 표에 없는 통화에는 `UnsupportedCurrency` 실패 `Result`가 나옵니다.
- `ConvertCurrencyService`를 수정하지 않고 정책을 교체했음을 확인합니다.

## 3단계 — 취소 토큰을 끝까지 전파하기

fake handler가 응답 전에 잠시 기다리도록 만들고, 이미 취소된 토큰과 실행 중 취소되는 토큰을 각각 전달하는 self-test를 추가하세요. 같은 토큰이 Application Service → Gateway → `HttpClient.SendAsync` → handler의 대기까지 전달되어야 합니다.

검증 기준:

- 테스트가 긴 timeout을 기다리지 않고 빠르게 끝납니다.
- 취소를 실패 문자열로 숨기지 않고 `OperationCanceledException` 계열로 관찰합니다.
- “취소 요청”과 “작업 강제 종료”의 차이를 한 문장으로 설명합니다.

## 4단계 — HTTP와 JSON 실패를 분리하기

fake handler가 HTTP `503 Service Unavailable`을 반환하는 경우와, `200 OK`지만 잘못된 JSON을 반환하는 경우를 각각 검증하세요. 오류 메시지에는 분류 가능한 오류 코드나 상태를 담되 원문 응답 전체와 비밀 값은 넣지 않습니다.

검증 기준:

- `503`과 JSON 오류가 서로 다른 실패 원인으로 구분됩니다.
- 어느 경우에도 `IRateSelectionPolicy.SelectRate`가 호출되지 않습니다.
- 잘못된 JSON, 필수 필드 누락, 유효하지 않은 음수 환율의 차이를 설명합니다.

## 5단계 — 제한된 retry와 관측성 추가하기

첫 두 번은 `503`, 세 번째는 성공하는 handler를 만들고 조회 `GET`에만 최대 세 번의 재시도를 적용하세요. 학습용으로 짧은 지수 backoff를 사용하되, 실제 운영에서는 jitter가 필요한 이유를 주석에 설명합니다. 시도 횟수와 전체 경과 시간을 기록하는 작은 observer 또는 logger 계약도 추가합니다.

검증 기준:

- 일시적 실패 뒤 세 번째 응답에서 성공하고 요청 횟수는 정확히 3입니다.
- `400 Bad Request`와 잘못된 JSON은 재시도하지 않습니다.
- 각 시도에 개별 timeout이 있고 전체 호출에도 상한이 있음을 테스트합니다.
- 로그에 API 키, 전체 응답 본문, 개인 정보가 들어가지 않습니다.

## 6단계 — Pro 운영 설계로 확장하기

다음 요구를 만족하는 운영 구성 메모와 최소 한 개의 테스트를 추가하세요.

- `IHttpClientFactory` typed client 또는 수명이 긴 `HttpClient` + `PooledConnectionLifetime` 중 하나를 선택합니다.
- timeout, retry, circuit breaker의 순서와 대상 오류를 명시합니다.
- 환율의 관측 시각·만료 시각을 저장하고, provider 장애 시 stale cache 사용 여부를 정책으로 정합니다.
- trace ID, 통화 쌍, 공급자, 상태 코드, 시도 횟수, 지연 시간을 구조화 로그/메트릭으로 남깁니다.
- API 키는 비밀 저장소에서 읽고 URL·로그·예외에 노출하지 않습니다.

검증 기준: 정상·일시 오류·영구 오류·timeout·사용자 취소·stale cache 각각에서 최종 결과, 요청 횟수, 로그/메트릭을 표로 작성합니다. 선택하지 않은 대안과 남는 trade-off도 두 문장 이상 기록하세요.
