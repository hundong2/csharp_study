# 2026-09-03 단계별 실행 실습

> 각 단계는 [`src/SensorStreamingExercise/`](./src/SensorStreamingExercise/) 안의 코드를 직접 고친 뒤 아래 명령으로 확인합니다. 새 메서드를 만들 때도 목적·매개변수·반환값 한글 주석을 꼭 작성하세요.

```powershell
dotnet build ./src/SensorStreamingExercise/SensorStreamingExercise.csproj
dotnet run --project ./src/SensorStreamingExercise -- --self-test
```

## 1단계 — `foreach`와 `yield return` 연결하기

`DemoReadings`에 `ForSensor(IEnumerable<SensorReading> source, string sensorId)` 반복자 메서드를 추가하세요. `foreach`로 입력을 읽고 ID가 맞는 항목만 `yield return` 하며, `Program.cs`에서 `SENSOR-A`만 흘려보내 보세요.

검증 기준: 기본 실행의 처리 건수는 2건, 새 경고는 1건입니다. 필터 호출 전에는 데이터가 만들어지지 않는 지연 실행 이유를 한 문장으로 설명하세요.

## 2단계 — Strategy 경계값 확장하기

`TemperatureHumidityRule`에 저온 주의 임계값을 추가해 `5°C` 이하도 `Warning`으로 분류하세요. `RuleCreatesLowTemperatureAlertAsync` self-test를 추가하고 테스트 배열에 등록하세요.

검증 기준: `5.0`과 `4.9`가 주의, `5.1`이 정상인지 경계값 세 개를 확인합니다.

## 3단계 — 비동기 스트림의 점진 처리 관찰하기

`InMemorySensorStream`의 원소별 지연을 `500ms`로 바꾸고 실행하세요. 모든 입력을 먼저 기다린 뒤 출력하는지, 아니면 경고가 준비되는 순서대로 보이는지 관찰해 기록하세요.

검증 기준: `await foreach`가 한 건씩 소비한다는 증거와 `Task<List<T>>`로 전체 목록을 반환할 때의 메모리 차이를 설명합니다.

## 4단계 — 취소 전파 직접 실행하기

`Program.cs`에 `CancellationTokenSource(TimeSpan.FromMilliseconds(1500))`를 만들고 서비스에 토큰을 전달하세요. 원소별 지연은 `700ms`로 설정하고 `OperationCanceledException`을 프로그램의 가장 바깥 경계에서만 처리하세요.

검증 기준: 두 번째 항목의 주의 경고까지 보인 뒤 1.5초 안팎에 종료되고, `ReadAllAsync`의 `Task.Delay`까지 같은 토큰이 전달됩니다.

## 5단계 — Repository 충돌 경로 검증하기

같은 `AlertId`지만 `Severity`가 다른 두 경고를 차례로 저장하는 테스트를 추가하세요.

검증 기준: 첫 저장은 성공 `true`, 두 번째는 `IsSuccess == false`이며 오류 문자열에 “다른 내용”이 포함됩니다. 같은 경고 재시도와 ID 충돌의 차이를 설명하세요.

## 6단계 — Pro 확장 설계하기

생산자가 소비자보다 빠른 운영 상황을 가정해 아래 중 하나를 설계 메모와 테스트로 추가하세요.

- 용량이 제한된 `Channel<SensorReading>`과 가득 찼을 때 대기하는 backpressure
- 메시지 브로커 offset/checkpoint와 재시작 후 재처리
- DB 경고 저장과 외부 알림 발행을 함께 보장하는 Transactional Outbox

검증 기준: 무제한 메모리 증가, 유실, 중복 중 무엇을 막는 설계인지와 남는 trade-off를 명시합니다.
