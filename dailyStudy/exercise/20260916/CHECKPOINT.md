# 2026-09-16 이해도 체크 — .NET 관측 가능성

먼저 답을 보지 말고 한 문장으로 설명해 보세요. 막히면 각 답 아래의 “다시 볼 파일”만 따라가면 됩니다.

---

## 1. Log, trace, metric은 각각 어떤 질문에 답하나요?

<details>
<summary>답 보기</summary>

- log는 “한 작업에서 어떤 사건과 오류가 일어났는가?”를 자세히 봅니다.
- trace는 “한 요청이 어떤 구성 요소를 어떤 순서로 지나며 어디서 시간이 걸렸는가?”를 연결합니다.
- metric은 “전체 성공률, 처리량, 지연 분포가 시간에 따라 어떻게 변하는가?”를 집계합니다.

하나로 다른 둘을 완전히 대체하려 하면 개별 원인, 경로, 전체 추세 중 일부를 잃습니다.

다시 볼 파일: [`README.md`의 세 기둥](./README.md#관측-가능성의-세-기둥)

</details>

## 2. `ActivitySource.StartActivity`의 반환값이 왜 nullable인가요?

<details>
<summary>답 보기</summary>

구독 listener가 없거나 sampling 정책이 Activity 생성을 원하지 않으면 실제 `Activity` 객체를 만들 필요가 없습니다. 이때 null을 반환해 관측을 끈 환경의 비용을 줄입니다. 그래서 생산 코드는 `activity?.SetTag(...)`처럼 null을 정상 상태로 처리해야 합니다.

다시 볼 파일: [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 3. 왜 `jobId`를 trace에는 쓰면서 metric 태그에는 넣지 않나요?

<details>
<summary>답 보기</summary>

trace는 개별 작업을 찾아가는 용도라 승인된 불투명 job ID가 correlation에 도움이 됩니다. metric backend는 태그 값 조합마다 시계열을 만들 수 있으므로 고유 job ID를 넣으면 작업 수만큼 조합이 늘어 비용과 메모리가 폭증합니다. metric에는 `target_format`, `outcome`처럼 값 종류가 제한된 필드를 사용합니다.

job ID도 개인정보라면 trace·log에서도 쓰면 안 되며 tokenization이나 접근제어가 필요합니다.

다시 볼 파일: [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs), [`README.md`의 카디널리티](./README.md#metric과-카디널리티)

</details>

## 4. Decorator를 사용한 핵심 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

`ObservedConversionWorkflow`가 안쪽 Service와 같은 `IConversionWorkflow`를 구현하면 호출자는 같은 계약만 사용합니다. 핵심 Service를 수정하지 않고 log·trace·metric이라는 횡단 관심사를 추가할 수 있고, 업무 테스트는 관측 도구 없이, 관측 테스트는 `StubWorkflow`로 분리할 수 있습니다.

다시 볼 파일: [`Application.cs`](./src/DocumentObservabilityExercise/Application.cs), [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 5. 예상 거절, 기술 장애, 호출자 취소는 왜 같은 실패 Result가 아닌가요?

<details>
<summary>답 보기</summary>

- 예상 거절은 호출자가 내용을 고쳐 다시 요청할 수 있으므로 실패 Result가 적합합니다.
- 기술 장애는 정상 업무 결과를 만들 수 없고 상위 retry·경보 정책이 필요하므로 예외를 유지합니다.
- 호출자 취소는 장애가 아니라 중단 의도이므로, 전달 토큰이 취소되고 예외 토큰도 그 토큰과 같을 때 `OperationCanceledException`을 재전파해 별도로 셉니다. 다른 토큰의 취소 예외는 timeout 등 기술 장애일 수 있어 faulted로 남깁니다.

셋을 같은 값으로 바꾸면 재시도, 상태 코드, 로그 수준, 장애율이 잘못 결정될 수 있습니다.

다시 볼 파일: [`ObservedConversionWorkflow.ExecuteAsync`](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 6. completion counter와 duration을 `finally`에서 기록하는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

`finally`는 성공 return, 실패 Result, 취소, 예외 어느 경로에서도 실행됩니다. 각 catch와 return 직전에 따로 기록하면 한 경로를 빠뜨리거나 두 번 기록하기 쉽습니다. 먼저 `outcome`을 정확히 분류하고 `finally`에서 completion과 duration을 각각 한 번 시도합니다. 두 호출도 분리하므로 한 수집기 callback의 실패가 다른 기록 시도를 막지 않습니다.

다시 볼 파일: [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 7. Counter와 Histogram은 무엇이 다른가요?

<details>
<summary>답 보기</summary>

Counter는 시도·완료 건수처럼 누적해서 증가하는 값을 기록하며 수집기는 총합이나 증가율을 계산합니다. Histogram은 각 처리 시간을 개별 기록하고 수집기가 분포와 p50/p95/p99 같은 백분위수를 계산하게 합니다.

다시 볼 파일: [`ConversionTelemetry` 생성자](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 8. `LoggerMessage` source generation은 무엇을 얻나요?

<details>
<summary>답 보기</summary>

Event ID, level, message template, 구조화 속성 이름을 선언해 두면 컴파일 시 효율적인 구현이 생성됩니다. 매번 템플릿을 다시 해석하는 비용을 줄이고 필드 이름을 일관되게 유지합니다. `partial` 메서드의 구현이 빌드 과정에서 채워집니다.

다시 볼 파일: [`Telemetry.cs`의 LogConversion... 메서드](./src/DocumentObservabilityExercise/Telemetry.cs)

</details>

## 9. 왜 예외 객체나 `exception.Message`를 그대로 기록하지 않았나요?

<details>
<summary>답 보기</summary>

예외 Message와 stack에는 파일 경로, 외부 응답, 사용자 데이터, 연결 정보가 들어갈 수 있습니다. 오늘은 분류에 필요한 예외 형식만 기록합니다. 운영에서는 중앙 redaction과 허용 필드 정책을 적용하고, 진단 가치와 정보 노출 위험을 함께 평가해야 합니다.

다시 볼 파일: [`Telemetry.cs`의 catch (Exception)](./src/DocumentObservabilityExercise/Telemetry.cs), [`SelfTests.cs`](./src/DocumentObservabilityExercise/SelfTests.cs)

</details>

## 10. `ConversionRequest.ToString()`을 왜 재정의했나요?

<details>
<summary>답 보기</summary>

요청은 민감할 수 있는 `SourceText`를 포함합니다. 디버거, 문자열 보간, 우발적 객체 로그에서 원문이 자동으로 노출될 위험을 줄이기 위해 작업 ID, 형식, 길이만 반환합니다. 다만 이것은 한 겹의 방어일 뿐이며 logger 호출에서 객체 전체를 넘기지 않는 규칙과 테스트도 함께 필요합니다.

다시 볼 파일: [`Domain.cs`](./src/DocumentObservabilityExercise/Domain.cs)

</details>

## 11. Application Service가 telemetry API를 직접 참조하지 않는 장점은 무엇인가요?

<details>
<summary>답 보기</summary>

업무 순서와 관측 정책이 독립적으로 바뀔 수 있습니다. Service 단위 테스트가 listener나 logger 설정 없이 빨라지고, 다른 UI나 batch에서도 같은 Service를 재사용할 수 있습니다. OpenTelemetry나 logging provider 교체도 Decorator와 Composition Root 쪽에 머뭅니다.

다시 볼 파일: [`Application.cs`](./src/DocumentObservabilityExercise/Application.cs), [`Program.cs`](./src/DocumentObservabilityExercise/Program.cs)

</details>

## 12. 데모의 `ActivityListener`와 `MeterListener`가 운영 collector인가요?

<details>
<summary>답 보기</summary>

아닙니다. 학습 화면과 자체 테스트에서 .NET API가 만든 signal을 즉시 관찰하는 작은 in-process listener입니다. 운영에서는 보통 OpenTelemetry SDK가 signal을 batch로 수집하고 OTLP 또는 공급자 exporter를 통해 별도 backend로 보냅니다. 인증, buffering, retry, sampling, 종료 flush도 필요합니다.

다시 볼 파일: [`Program.cs`](./src/DocumentObservabilityExercise/Program.cs), [`README.md`의 생산자와 수집기](./README.md#생산자와-수집기를-구분하기)

</details>

---

## 말로 설명하는 최종 점검

아래 문장을 코드 없이 완성해 보세요.

1. “로그는 ___을, trace는 ___을, metric은 ___을 보기 좋다.”
2. “Decorator를 쓴 이유는 핵심 Service가 ___을 모르도록 하기 위해서다.”
3. “metric tag에는 ___처럼 값 종류가 제한된 항목을 쓰고, ___처럼 고유한 값은 피한다.”
4. “예상 거절은 ___, 기술 장애는 ___, 호출자 중단은 ___로 표현한다.”
5. “원문과 예외 Message를 signal에 넣지 않는 이유는 ___이다.”

다섯 문장을 막힘없이 설명하고 자체 테스트 7/7을 재현하면 오늘의 핵심을 이해한 것입니다.
