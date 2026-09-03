# 2026-09-04 초보자 이해도 검증 단계

코드를 보지 않고 먼저 답한 뒤, 실제 파일과 실행 결과를 열어 스스로 채점하세요. 설명할 때는 “무엇이다”에서 끝내지 말고 “이 예제에서 왜 필요하다”까지 말합니다.

## A. 기본 Syntax와 Grammar

- [ ] `using`, `var`, `new`, `if`, `foreach`, `try/catch`, `return`, 문자열 보간을 코드에서 각각 찾고 한 문장으로 설명한다.
- [ ] `class`, `interface`, `record`, 제네릭 `<T>`, nullable `?`의 차이를 예제 형식과 연결한다.
- [ ] `Task<T>`, `async`, `await`가 HTTP 대기 중 스레드를 붙잡지 않는 흐름을 설명한다.
- [ ] `CancellationToken`이 강제 종료가 아니라 협력적인 중단 요청인 이유를 설명한다.
- [ ] 같은 토큰이 `Program`부터 fake handler까지 전달되어야 하는 이유를 말한다.
- [ ] LINQ의 `All`과 `Contains`가 각각 “모두 검사”와 “값 존재 확인” 의도를 어떻게 드러내는지 설명한다.

## B. HTTP와 JSON 경계

- [ ] 요청마다 `HttpClient`를 생성·폐기할 때 연결 풀과 포트에 생길 수 있는 문제를 설명한다.
- [ ] 수명이 긴 `HttpClient`와 `IHttpClientFactory` 방식의 공통 목표와 차이를 말한다.
- [ ] fake `HttpMessageHandler`가 실제 네트워크 없이도 `HttpClient` 경계를 검증하는 원리를 설명한다.
- [ ] 외부 JSON DTO와 Domain record를 바로 합치지 않은 이유를 말한다.
- [ ] 역직렬화 성공 뒤에도 null, 통화 코드, 양수 환율을 다시 검증해야 하는 이유를 설명한다.
- [ ] HTTP `503`, HTTP `400`, 잘못된 JSON, 필수 값 누락을 retry 관점에서 구분한다.

## C. 아키텍처와 설계

- [ ] `Currency`/`Money`, `ConvertCurrencyService`, `HttpExchangeRateGateway`, `ExactCurrencyRateSelectionPolicy`, `IConversionAudit`, Composition Root를 실제 파일과 연결한다.
- [ ] Application이 구체 HTTP Gateway가 아니라 `IExchangeRateGateway`에 의존할 때 얻는 테스트 이점을 설명한다.
- [ ] `IRateSelectionPolicy` 구현을 교체해도 `ConvertCurrencyService`를 수정하지 않는 구조가 OCP와 어떻게 연결되는지 말한다.
- [ ] 예상 가능한 업무 실패에 `Result<T>`, 구성 오류에 예외, 사용자 취소에 `OperationCanceledException`을 사용한 이유를 설명한다.
- [ ] timeout과 사용자 취소가 비슷한 예외로 보일 때 운영에서 어떻게 구분할지 말한다.
- [ ] retry 횟수 제한, 지수 backoff, jitter, circuit breaker가 각각 해결하려는 문제를 설명한다.
- [ ] 로그·메트릭·trace에 남길 값과 절대 남기지 않을 비밀 값을 구분한다.

## D. 직접 실행 검증

```powershell
dotnet --version
dotnet build ./src/ExchangeRateClientExercise/ExchangeRateClientExercise.csproj
dotnet run --project ./src/ExchangeRateClientExercise
dotnet run --project ./src/ExchangeRateClientExercise -- --self-test
```

- [ ] 설치된 Stable SDK로 `net10.0` 빌드가 경고 0개, 오류 0개로 성공한다.
- [ ] 기본 실행이 인터넷이나 API 키 없이 결정적으로 성공한다.
- [ ] 출력에서 `USD → KRW`, `KRW → JPY`, `EUR → EUR` 성공과 `USD → XYZ`, 음수 금액 실패를 확인한다.
- [ ] 유효한 입력 네 건만 Gateway에 도달하여 `HTTP 요청 4건`이 출력되는 이유를 설명한다.
- [ ] 자체 테스트에서 200 매핑·환산과 요청 형식, HTTP 상태 실패, 손상·중복 JSON, 검증·미지원 통화, 취소, 동일 통화 정책이 6/6 통과한다.
- [ ] 자체 테스트를 두 번 실행해 같은 결과가 나오는지 확인한다.
- [ ] 모든 메서드 위에 목적·매개변수·반환값 한글 설명이 있고, 첫 `async/await`, nullable, record, LINQ, DI 사용에 쉬운 이유 주석이 있음을 확인한다.

## E. 마지막 구두 점검

아래 상황을 1분 안에 설명할 수 있으면 오늘 목표를 달성한 것입니다.

> “실제 환율 제공자가 5초 동안 응답하지 않다가 `503`을 반환했다. 이 요청은 어디에서 취소되고, 어떤 실패로 분류되며, 재시도 여부는 누가 정하고, 운영자는 어떤 로그·메트릭·trace로 원인을 찾는가?”

답에는 최소한 `CancellationToken`, Gateway Adapter, 제한된 retry, `Result<T>` 또는 예외 경계, 시도 횟수와 지연 시간, trace ID가 포함되어야 합니다.
