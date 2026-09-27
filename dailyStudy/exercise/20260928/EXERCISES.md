# 실습 문제 — Beginner to Pro

먼저 기본 코드를 변경하지 않은 상태에서 다음을 모두 통과시키세요.

```powershell
$project = 'dailyStudy/exercise/20260928/src/ReportTimeoutApi/ReportTimeoutApi.csproj'
dotnet build $project -c Release
dotnet run --project $project -c Release --no-build -- --self-test
```

각 문제는 **실패하는 assertion을 먼저 추가하고**, 구현 후 다시 통과시키는 순서로 진행합니다.

## 1. Beginner — 입력 경계 직접 확인

### 목표

기본 조건문, nullable 기본값, Result를 실행 결과와 연결합니다.

### 수정할 파일

- [`ReportRequest.cs`](./src/ReportTimeoutApi/Domain/ReportRequest.cs)
- [`SelfTestRunner.cs`](./src/ReportTimeoutApi/SelfTesting/SelfTestRunner.cs)

### 할 일

1. `customerId`가 정확히 3자와 20자일 때 성공하는 assertion을 추가합니다.
2. `simulateMs=-1`, `2000`, `2001`의 성공/실패를 예측하고 assertion으로 만듭니다.
3. `format=" JSON "`이 `json`으로 정규화되는지 확인합니다.
4. 각 실패의 `DomainError.Code`가 기대와 같은지 검사합니다.

### 성공 조건

- 경계값마다 왜 성공/실패하는지 한 문장으로 설명합니다.
- Release build 경고/오류가 0입니다.
- 자체 테스트의 assertion 수가 늘고 모두 통과합니다.

## 2. Junior — TXT Strategy 추가

### 목표

Strategy와 개방 폐쇄 원칙을 직접 경험합니다.

### 수정할 파일

- 새 파일 `Infrastructure/TextReportFormatterStrategy.cs`
- [`ReportRequest.cs`](./src/ReportTimeoutApi/Domain/ReportRequest.cs)
- [`Program.cs`](./src/ReportTimeoutApi/Program.cs)
- [`SelfTestRunner.cs`](./src/ReportTimeoutApi/SelfTesting/SelfTestRunner.cs)

### 할 일

1. `IReportFormatterStrategy`를 구현하고 `FormatName`을 `txt`로 둡니다.
2. 한 줄에 `청구번호 | 설명 | 금액`을 쓰는 `text/plain` 문서를 만듭니다.
3. Domain이 `txt`를 허용하도록 검증을 확장합니다.
4. Composition Root에 새 Strategy를 등록합니다.
5. formatter 메서드가 받은 `CancellationToken`을 본문 생성 중에도 확인합니다.
6. `ReportApplicationService`의 `if`/`switch`를 늘리지 않고 HTTP 200을 만듭니다.

### 성공 조건

- `GET /reports/CUST-100?format=txt&simulateMs=0`이 `200 text/plain`입니다.
- 파일 이름은 `.txt`로 끝납니다.
- 기존 CSV/JSON/timeout 검증이 그대로 통과합니다.

## 3. Intermediate — CPU 작업도 취소에 협력시키기

### 목표

비동기 I/O뿐 아니라 긴 동기 반복도 token을 확인해야 함을 배웁니다.

### 수정할 파일

- 새 Port/Strategy 또는 별도 `ReportChecksumCalculator.cs`
- [`ReportApplicationService.cs`](./src/ReportTimeoutApi/Application/ReportApplicationService.cs)
- [`SelfTestRunner.cs`](./src/ReportTimeoutApi/SelfTesting/SelfTestRunner.cs)

### 할 일

1. 보고서 본문의 checksum을 여러 번 계산하는 교육용 CPU 반복을 추가합니다.
2. 메서드가 `CancellationToken`을 받게 합니다.
3. 매 반복이 아니라 적절한 간격으로 `ThrowIfCancellationRequested()`를 호출합니다.
4. 미리 취소된 token으로 시작하면 포맷 결과를 만들지 않는 테스트를 추가합니다.

### 성공 조건

- 취소 없는 계산 결과는 항상 같습니다.
- 취소 시 `OperationCanceledException`이 위로 전파됩니다.
- 취소를 `Result.Failure`나 빈 문서로 바꾸지 않습니다.

## 4. Senior — 쓰기 작업의 안전한 commit 경계 설계

### 목표

timeout과 side effect가 함께 있을 때 “언제까지 취소 가능한가?”를 설계합니다.

### 수정할 파일

- 새 `IReportAuditRepository` Port와 메모리 Adapter
- [`ReportApplicationService.cs`](./src/ReportTimeoutApi/Application/ReportApplicationService.cs)
- [`SelfTestRunner.cs`](./src/ReportTimeoutApi/SelfTesting/SelfTestRunner.cs)

### 할 일

1. 보고서 생성 성공을 audit log에 한 번 기록합니다.
2. 준비 단계는 token으로 취소 가능하게 합니다.
3. 원자적 commit을 시작한 뒤에는 “저장됐는지 모르는 상태”를 만들지 않도록 경계를 문서화합니다.
4. commit 전 취소와 commit 직후 취소를 각각 결정적인 gate로 테스트합니다. `Thread.Sleep` 시간 추측은 사용하지 않습니다.

### 성공 조건

- 취소된 준비 작업은 audit를 남기지 않습니다.
- commit 완료 뒤에는 성공 기록이 정확히 한 번 존재합니다.
- README에 late cancellation 정책을 5문장 이내로 설명합니다.

## 5. Pro — 운영용 timeout budget으로 확장

### 목표

하나의 숫자가 아니라 전체 요청 budget과 하위 의존성 budget을 설계합니다.

### 할 일

1. 전체 request timeout, DB command timeout, 외부 HTTP timeout의 포함 관계를 표로 만듭니다.
2. `fast-report`, `large-report` 정책을 서로 다른 endpoint 또는 route group에 연결합니다.
3. timeout, client disconnect, dependency timeout을 구분하는 낮은 cardinality metric 이름을 설계합니다.
4. 멱등 요청에만 제한적 retry를 허용하고 backoff/jitter 조건을 적습니다.
5. 100개 동시 요청에서 timeout 뒤 `StartedReads - CompletedReads - CanceledReads == 0`이 되는 부하 검증을 작성합니다.

### 성공 조건

- budget 합이 전체 제한을 넘지 않습니다.
- streaming/업로드처럼 동일 정책이 부적절한 endpoint를 명시합니다.
- timeout 뒤에도 작업·포트·프로세스가 남지 않는 증거를 출력합니다.
- 정상 경로의 p95 latency가 회귀하지 않는 기준을 제시합니다.
