# 2026-09-16 연습문제 — .NET 관측 가능성

## 진행 원칙

각 과제는 아래 짧은 반복으로 진행합니다.

1. 바꾸기 전에 예상 결과를 한 문장으로 적습니다.
2. 수정 과제라면 관련 파일만 작게 수정합니다.
3. Release build를 실행합니다.
4. `--self-test`를 실행합니다.
5. 일반 데모에서 log·trace·metric을 직접 확인합니다.
6. 민감정보나 높은 카디널리티 태그가 새로 생기지 않았는지 감사합니다.

기본 검증 명령:

```powershell
dotnet build .\dailyStudy\exercise\20260916\src\DocumentObservabilityExercise\DocumentObservabilityExercise.csproj -c Release
dotnet run --project .\dailyStudy\exercise\20260916\src\DocumentObservabilityExercise\DocumentObservabilityExercise.csproj -c Release --no-build -- --self-test
dotnet run --project .\dailyStudy\exercise\20260916\src\DocumentObservabilityExercise\DocumentObservabilityExercise.csproj -c Release --no-build
```

---

## Beginner 1 — signal 세 종류 찾기

코드를 수정하지 않고 [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs)에서 다음 위치를 찾습니다.

- 시작 log가 만들어지는 곳
- `document.convert` Activity가 만들어지는 곳
- attempts counter가 증가하는 곳
- completion counter와 duration histogram이 기록되는 곳

각 위치 옆에 아래 질문의 답을 메모합니다.

1. 이 signal은 개별 작업을 찾는가, 전체 추세를 보는가?
2. 어떤 필드가 들어가는가?
3. 원문 `SourceText`가 들어가지 않는가?

### 통과 기준

- log, trace, metric 생산 위치를 각각 말할 수 있습니다.
- `RecordAttempt`, `StartConversion`, `RecordCompletion`, `RecordDuration`을 서로 독립된 경계로 호출하는 이유를 설명합니다.
- 코드를 바꾸지 않아도 build와 자체 테스트가 그대로 통과합니다.

---

## Beginner 2 — 예상 거절 관찰하기

[`Program.cs`](./src/DocumentObservabilityExercise/Program.cs)의 데모 원문에 `[[unsupported]]`를 넣습니다.

실행 직후 PowerShell에서 `$LASTEXITCODE`를 입력하면 방금 끝난 프로세스의 종료 코드를 확인할 수 있습니다.

실행 전에 다음 결과를 예상하세요.

- 프로세스 종료 코드는 0인가, 1인가?
- Event ID는 1002와 1003 중 무엇인가?
- Activity status와 `conversion.outcome`은 무엇인가?
- completion counter와 duration histogram은 기록되는가?
- Repository의 저장 건수는 몇 개인가?

### 통과 기준

- 예상 거절이 예외 stack trace가 아니라 실패 Result로 돌아오는 것을 확인합니다.
- Warning 로그, `rejected` metric, Error Activity를 확인합니다.
- 원래 데모 입력으로 되돌린 뒤 자체 테스트 7/7을 다시 확인합니다.

---

## Intermediate 1 — `lower` 출력 형식 추가하기

`plain`, `upper`에 소문자 변환인 `lower`를 추가합니다.

수정할 위치:

1. [`Domain.cs`](./src/DocumentObservabilityExercise/Domain.cs)의 허용 target pattern과 오류 설명
2. [`Infrastructure.cs`](./src/DocumentObservabilityExercise/Infrastructure.cs)의 switch 식
3. [`Telemetry.cs`](./src/DocumentObservabilityExercise/Telemetry.cs)의 metric 차원 허용 목록
4. [`SelfTests.cs`](./src/DocumentObservabilityExercise/SelfTests.cs)의 입력 정규화·변환 테스트
5. [`README.md`](./README.md)의 허용 형식과 cardinality 설명

### 생각할 점

- `lower`는 값 종류가 제한되므로 metric tag로 사용해도 안전한가요?
- 허용 형식을 문자열 여러 곳에 중복하면 어떤 유지보수 위험이 생기나요?
- enum을 도입하면 외부 문자열 파싱 경계는 어디에 두어야 하나요?

### 통과 기준

- `LOWER` 입력이 `lower`로 정규화됩니다.
- `plain`, `upper`, `lower` 모두 build와 자체 테스트를 통과합니다.
- metric tag 조합 상한을 새로 계산해 문서에 적습니다.

---

## Intermediate 2 — 결과 크기 Histogram 추가하기

성공한 변환 결과의 문자 수 분포를 보기 위한 Histogram을 추가합니다.

권장 이름:

```text
csharpstudy.document_conversion.output_size
```

권장 단위:

```text
{character}
```

### 설계 제약

- 성공 경로에서만 기록합니다.
- 태그는 `conversion.target_format`만 사용합니다.
- `jobId`, 실제 본문, 정확한 파일 이름은 태그로 넣지 않습니다.
- 자체 테스트에서 값이 `ConversionReceipt.OutputLength`와 같은지 검증합니다.

### 통과 기준

- 성공 한 건에는 새 Histogram 측정이 하나 생깁니다.
- 거절·취소·장애에는 결과 크기가 기록되지 않습니다.
- 기존 completion과 duration은 각 한 번만 기록됩니다.

---

## Advanced 1 — Converter 전용 child Activity Decorator

전체 `document.convert` Activity 아래에 `document.converter.execute` child Activity를 만드는 `ObservedDocumentConverter`를 설계합니다.

조건:

- `IDocumentConverter`를 그대로 구현하고 안쪽 Converter를 감쌉니다.
- `DocumentConversionService`는 수정하지 않습니다.
- 변환 엔진 이름은 `simple`처럼 제한된 안전 값만 태그로 사용합니다.
- 원문, 변환 본문, 원시 예외 Message는 태그에 넣지 않습니다.
- `Program`의 Composition Root에서 `SimpleDocumentConverter`를 Decorator로 감쌉니다.

### 자체 테스트 아이디어

1. 부모 Activity와 child Activity의 `TraceId`가 같습니다.
2. child의 `ParentSpanId`가 부모의 `SpanId`와 같습니다.
3. Converter 거절 시 child Activity가 Error 상태입니다.
4. listener가 없어도 변환 결과는 같습니다.

### 통과 기준

- trace가 부모 1개 + 자식 1개로 보입니다.
- 핵심 Application Service는 `ActivitySource`를 여전히 참조하지 않습니다.
- child Activity가 생겨도 metric 측정 횟수는 바뀌지 않습니다.

---

## Advanced 2 — 취소와 timeout 분리하기

현재 코드는 전달받은 `CancellationToken`이 취소되었고 예외의 토큰도 그 토큰과 같을 때만 `canceled`로 분류합니다. 다른 토큰의 `OperationCanceledException`은 우선 `faulted`입니다. 여기에 변환 엔진 timeout을 별도 outcome으로 표현하는 정책을 설계하세요.

먼저 아래 결정을 문장으로 적습니다.

1. timeout은 실패 Result인가, 예외인가?
2. 호출자 취소와 timeout을 어떤 오류 형식 또는 토큰으로 구분할 것인가?
3. log level과 Event ID는 무엇인가?
4. metric outcome은 `timeout`을 새로 추가할 것인가?
5. Activity status와 `error.type`은 무엇인가?
6. timeout을 실패 Result로 정했다면 `conversion.timeout` 같은 고정 코드를 `OperationError.IsKnownCode` vocabulary에 어떻게 추가할 것인가?

### 주의

`OperationCanceledException`이라는 형식만 보고 모두 호출자 취소로 분류하면 timeout이 성공적인 사용자 중단처럼 숨을 수 있습니다. 반대로 모든 취소를 Error 경보로 보내면 정상 중단이 장애율을 왜곡합니다.

### 통과 기준

- 호출자 취소와 timeout 자체 테스트가 각각 있습니다.
- 두 경로가 다른 log/metric 분류를 가집니다.
- 원래 예외 또는 Result 계약을 상위 계층이 구분할 수 있습니다.
- 새 오류 코드나 metric outcome을 택했다면 Domain·Telemetry의 고정 허용 목록과 카디널리티 예산도 함께 갱신합니다.

---

## Pro 1 — telemetry 허용 목록 감사 테스트

현재 테스트는 특정 비밀 문자열이 signal에 없는지 검사합니다. 이를 “허용된 필드만 존재한다”는 allowlist 방식으로 강화하세요.

예시 정책:

| Signal | 허용 key |
| --- | --- |
| Log | `JobId`, `TargetFormat`, `TraceId`, `OutputLength`, `ErrorCode`, `ExceptionType`, `{OriginalFormat}` |
| Trace | `conversion.job_id`, `conversion.target_format`, `conversion.outcome`, `error.type` |
| Metric | `conversion.target_format`, `conversion.outcome` |

### 구현 힌트

- 실제 key 집합에서 허용 집합을 뺀 결과가 비어 있는지 검사합니다.
- log Event ID마다 허용 key가 다를 수 있으므로 Event ID별 정책을 둘 수 있습니다.
- 새 signal을 추가하면 테스트와 정책 문서를 같은 변경에서 업데이트합니다.

### 통과 기준

- 예상하지 못한 key 하나를 임시로 추가하면 테스트가 실패합니다.
- key를 제거하면 다시 통과합니다.
- 값 검사와 key allowlist 검사를 둘 다 유지합니다.

---

## Pro 2 — 운영 OpenTelemetry 연결 설계서

코드를 바로 바꾸기 전에 한 페이지 설계서를 작성합니다.

필수 항목:

1. `ActivitySource`와 `Meter` 이름
2. Generic Host에서 `IMeterFactory`와 수명을 연결하는 위치
3. trace sampling 기준과 예상 일일 span 수
4. OTLP endpoint 인증과 비밀 보관 방법
5. exporter 장애 시 업무 요청을 실패시킬지 여부
6. metric tag 조합 예산
7. 로그·trace·metric 보존 기간
8. 개인정보 redaction과 접근권한
9. 성공률·지연 SLO와 경보 조건
10. collector/backend 장애를 검증할 통합 테스트

### 통과 기준

- “OpenTelemetry를 쓴다”가 아니라 운영 실패 경계와 비용을 구체적으로 적습니다.
- 업무 처리와 telemetry export 실패를 결합할지 분리할지 근거를 제시합니다.
- PII, secret, high-cardinality 값의 금지 목록이 있습니다.

---

## 마무리 회귀 검사

모든 과제 뒤 다음을 확인합니다.

- [ ] Release build가 경고 0개·오류 0개다.
- [ ] 자체 테스트가 모두 통과한다.
- [ ] 일반 데모의 Result와 Repository 저장 건수가 의도대로다.
- [ ] 성공·거절·취소·장애가 서로 다른 outcome으로 보인다.
- [ ] 정상 listener에서 completion과 duration이 실행 한 건당 한 번씩 기록된다.
- [ ] 원문·변환 본문·원시 예외 Message가 signal에 없다.
- [ ] metric tag에 고유 ID나 무제한 문자열이 없다.
- [ ] README의 구조도와 실제 의존성 방향이 같다.
