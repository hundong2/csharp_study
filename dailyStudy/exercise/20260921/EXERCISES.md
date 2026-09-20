# 2026-09-21 연습문제 — 파티션별 동시성 제한

아래 과제는 위에서 아래로 갈수록 설계 판단이 더 많이 필요합니다. 한 번에 전부 풀기보다 **한 과제 → 빌드 → 자체 검증 → 직접 HTTP 호출** 순서로 진행하세요.

## Beginner 1 — 입력 경계 바꾸기

보고서 제목의 최대 길이를 현재 값보다 10자 늘리세요.

- 먼저 `ReportRequest.Create`의 검증 위치를 찾습니다.
- 경계값 바로 아래, 경계값, 경계값 바로 위를 자체 테스트에 추가합니다.
- 엔드포인트나 저장소에서 같은 길이 검사를 중복하지 않습니다.

**완료 기준:** 유효한 제목은 성공하고, 너무 긴 제목은 저장되거나 렌더링되지 않으며, 전체 자체 테스트가 통과합니다.

## Beginner 2 — 새 렌더링 Strategy 추가

`Markdown` 보고서 형식을 추가하세요.

- Domain의 허용 형식에 `Markdown`을 추가합니다.
- `IReportRenderer`를 구현하는 `MarkdownReportRenderer`를 만듭니다.
- 기존 `PlainTextReportRenderer`와 새 구현을 Composition Root에서 모두 등록합니다.
- Application Service의 `if`/`switch`를 늘리지 않고 올바른 Strategy가 선택되게 합니다.

**완료 기준:** `format: "markdown"` 요청이 성공하고, 기존 plain text 동작과 테스트가 그대로 유지됩니다.

## Intermediate 3 — 대기열을 없애고 차이를 관찰하기

`QueueLimit`을 `1`에서 `0`으로 바꾼 뒤 동일 클라이언트의 동시 요청 세 개를 보내 보세요.

1. 변경 전에는 한 요청이 실행되고 한 요청이 대기하며 나머지가 429가 되는지 기록합니다.
2. 변경 후에는 첫 요청만 실행되고 나머지가 즉시 429가 되는지 기록합니다.
3. 어느 정책이 사용자 경험과 서버 보호에 더 적합한지 한 문단으로 설명합니다.

**완료 기준:** “큐는 처리량을 늘리는 장치가 아니며 대기 메모리와 지연을 늘릴 수 있다”는 점을 결과와 연결해 설명합니다.

## Intermediate 4 — 제한 대상 경계 확인하기

`/health`에는 정책이 적용되지 않고 `POST /reports`에만 적용되는 이유를 확인하세요.

- `.RequireRateLimiting(...)` 호출 위치를 찾습니다.
- 제한된 요청이 몰리는 동안 `/health`를 호출합니다.
- 모든 엔드포인트에 전역 제한을 걸었을 때 운영 상태 확인에 어떤 문제가 생길지 적습니다.

**완료 기준:** endpoint-specific policy와 global limiter의 차이를 코드 위치와 HTTP 결과로 설명합니다.

## Advanced 5 — 시간 기반 제한과 동시성 제한 연결하기

동일 파티션에 짧은 시간 창 제한과 동시성 제한을 함께 적용하는 정책을 설계하세요.

- fixed window 또는 token bucket이 “일정 시간 동안 몇 건”을 제한한다는 점을 먼저 적습니다.
- concurrency limiter가 “같은 순간 몇 건”을 제한한다는 점과 비교합니다.
- `RateLimiter.CreateChained`를 사용할 때 앞쪽 limiter에서 이미 소비한 시간 기반 permit은 뒤쪽 거절로 자동 복구되지 않는다는 점을 고려해 순서를 선택합니다.

**완료 기준:** 정상, 시간 한도 초과, 동시 실행 초과를 각각 재현하는 테스트가 있고 정책 순서의 이유를 설명합니다.

## Advanced 6 — 신뢰할 수 있는 파티션 키로 교체하기

학습용 `X-Demo-Client` 헤더 대신 인증된 사용자 또는 API client ID를 사용하도록 설계를 바꾸세요.

- 인증되지 않은 임의 문자열을 그대로 파티션 키로 쓰지 않습니다.
- 키 종류가 끝없이 늘어날 때 메모리와 DoS 위험이 생기는 이유를 설명합니다.
- anonymous 사용자의 공유 정책, 키 폐기 또는 캐시 수명, 프록시 뒤 IP 신뢰 경계를 문서화합니다.

**완료 기준:** 클라이언트가 헤더 하나만 바꿔 제한을 우회할 수 없고, 파티션 cardinality가 어떻게 제한되는지 설명할 수 있습니다.

## Pro 7 — 부하 검증과 관측성 계획 세우기

운영 배포 전 검증 계획을 작성하고 가능하면 로컬 부하 테스트로 확인하세요.

- 허용 수, 대기 수, 거절 수, 대기 시간, 처리 시간의 p50/p95/p99를 측정합니다.
- 클라이언트 ID 원문처럼 cardinality가 큰 값이나 개인정보를 metric label에 넣지 않습니다.
- 429 비율이 높을 때 단순히 limit을 올리지 말고 느린 downstream, 큐 길이, 인스턴스 수, 사용자 재시도 폭주를 함께 확인합니다.
- 여러 서버 인스턴스에서 process-local limiter가 전역 한도를 보장하지 못한다는 점을 검증 계획에 포함합니다.

**완료 기준:** 목표 SLO, 예상 트래픽, 실패 조건, rollback 기준이 있는 짧은 테스트 보고서를 작성합니다.

## 마무리 검증 명령

저장소 루트에서 실행합니다.

```powershell
dotnet build ./dailyStudy/exercise/20260921/src/RequestAdmissionApi/RequestAdmissionApi.csproj -c Release
dotnet run --project ./dailyStudy/exercise/20260921/src/RequestAdmissionApi/RequestAdmissionApi.csproj -c Release -- --self-test
```
