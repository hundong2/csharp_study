# 2026-10-21 Beginner to Pro 연습문제

먼저 [`README.md`](./README.md)의 Reading order대로 코드를 한 번 실행하세요. 각 단계는 **실패하는 test 작성 → 최소 구현 → 중복 정리** 순서로 진행합니다.

---

## Beginner — 테스트 한 줄씩 읽기

### 1. 목적 길이 경계 DataRow 추가

목표: 같은 테스트 메서드로 4자, 5자, 120자, 121자 목적을 검증합니다.

1. `RentalReservationTests.cs`를 새로 만듭니다.
2. `[TestMethod]`와 `[DataRow]`를 사용합니다.
3. 길이만 달라지고 나머지 입력은 `TestData`에서 가져옵니다.
4. 실패할 때 `rental.invalid_purpose`와 `field == purpose`를 모두 확인합니다.

검증 질문:

- 5자와 120자는 성공해야 하는가?
- 공백을 제거하기 전 길이와 후 길이 중 무엇을 업무 규칙으로 쓰는가?
- 한 테스트에서 여러 독립 규칙을 모두 확인하면 실패 원인을 읽기 어려워지지 않는가?

### 2. AAA 표시 없이도 세 구역 찾기

`RentalWindowTests.Create_WithOffsets_NormalizesToUtc`의 Arrange, Act, Assert 주석을 지운 복사본을 종이에 적습니다. 각 문장이 어느 구역인지 구분하고 Act가 정확히 한 행동인지 확인하세요.

### 3. 실패 메시지를 의도적으로 보기

UTC 기대값을 한 시간 틀리게 바꾸어 test를 실행합니다.

```powershell
dotnet test --project .\tests\EquipmentRentalApi.Tests\EquipmentRentalApi.Tests.csproj -c Release
```

실패 로그에서 다음을 찾은 뒤 코드를 되돌립니다.

- test class와 method 이름
- expected와 actual
- source file과 line

---

## Intermediate — test-first로 새 Strategy 추가

### 4. 태블릿 대여 정책

요구사항:

- `category = tablet`
- 최소 준비 시간 1시간
- 최대 대여 기간 3일

권장 순서:

1. `TabletRentalPolicyTests`에 59분 시작 실패, 정확히 1시간 성공을 먼저 작성합니다.
2. 3일 성공, 3일+1tick 실패를 추가합니다.
3. test가 compile 또는 assertion에서 실패하는 것을 확인합니다.
4. `TabletRentalPolicy : IRentalPolicy`를 구현합니다.
5. `Program`의 DI 등록과 `GET /`의 공개 분류 metadata를 수정합니다. Domain의 분류 형식 검증은 `tablet`을 이미 허용합니다.
6. HTTP 통합 테스트 하나를 추가해 Composition Root 등록 누락까지 잡습니다.

왜 unit test만으로 충분하지 않은가:

- class 자체는 맞아도 DI에 등록하지 않으면 실제 API는 `policy_unavailable`이 됩니다.
- 따라서 정책 경계는 unit test, 조립 경계는 integration test가 각각 필요합니다.

### 5. 같은 ID 재시도의 멱등 의미 정하기

현재는 같은 `reservationId`를 다시 보내면 모두 `409`입니다. 다음 중 하나를 선택하고 test부터 작성하세요.

- 같은 payload면 기존 예약을 `200`으로 반환, 다른 payload면 `409`
- 같은 ID는 payload와 무관하게 항상 `409`

비교 시 주의할 값:

- 사용자가 보낸 원래 offset과 UTC 정규화 값
- 목적 문자열의 앞뒤 공백
- 대소문자가 다른 식별자

### 6. Test Double 직접 만들기

`IEquipmentRentalRepository`를 구현하는 `RecordingRepository`를 test project에 만듭니다.

- 전달받은 예약을 `LastAttempt`에 기록
- 지정한 `ReservationWriteStatus` 반환
- 호출 횟수 기록

다음 계약을 검증합니다.

- Domain 실패면 Repository 호출 0회
- 정책 실패면 Repository 호출 0회
- 성공이면 정확히 1회

mock library 없이 만든 작은 fake/spy가 오히려 의도를 더 잘 드러내는 경우를 설명하세요.

---

## Advanced — 통합 경계와 실패 설계

### 7. 실패 Repository를 DI로 교체

`ThrowingRepository`가 `TryAddAsync`에서 내부 예외를 던지게 합니다. `WebApplicationFactory.WithWebHostBuilder` 또는 별도 Factory로 이 Adapter를 주입하고 다음을 검증하세요.

- HTTP 상태 `500`
- `code == http.internal_error`
- `traceId` 존재
- 원래 exception message와 stack trace가 응답에 없음

운영 오류는 Result로 바꾸지 않는 이유도 한 문단으로 적으세요. 예외를 `400`으로 위장하면 alert와 재시도 정책이 잘못될 수 있습니다.

### 8. 테스트 fixture 공유와 격리

현재 integration test는 매 test마다 Factory를 만듭니다. class 단위로 Factory를 공유하도록 바꿔 보고, 예약 상태 때문에 어떤 test가 순서 의존적으로 실패하는지 관찰합니다.

다음 해결책을 비교하세요.

- test마다 새 Factory
- test initialize에서 Repository reset
- 요청마다 고유 ID·장비 사용
- immutable fixture와 mutable state fixture 분리

속도 개선 전후를 측정하되, 속도를 위해 격리를 포기하지 마세요.

### 9. 계약 assertion helper

Problem Details 검증의 중복을 `AssertProblemAsync` helper로 추출합니다.

입력:

- `HttpResponseMessage`
- 기대 HTTP 상태
- 기대 `code`
- 기대 `field`

반환:

- 검증만 하므로 `Task` 또는 필요하면 파싱한 `JsonObject`

helper가 너무 많은 구현 세부사항을 숨기지 않도록 이름과 실패 메시지를 설계하세요.

---

## Pro — 운영 동시성과 CI 증거

### 10. 실제 DB 원자성 설계

PostgreSQL을 예로 다음을 설계 문서로 작성합니다.

- `reservation_id` unique constraint
- 같은 `equipment_id`의 겹치는 시간 구간 차단
- transaction isolation 또는 exclusion constraint
- deadlock/serialization failure의 bounded retry
- 여러 API replica에서의 일관성

“먼저 SELECT, 없으면 INSERT”만으로 부족한 경쟁 실행 순서를 두 요청 A/B로 그리세요.

### 11. 실제 Kestrel black-box test

TestServer 밖에서 OS가 고른 loopback port로 Release 앱을 실행하고 PowerShell `HttpClient`로 검증합니다.

필수 안전 장치:

- 정확한 child PID만 종료
- `finally`에서 process와 stream 정리
- 고정 port 대신 사용 가능한 임시 port
- source보다 오래된 build output 거절
- stdout/stderr를 실패 때만 출력

TestServer와 실제 Kestrel에서 각각 잡을 수 있는 결함을 표로 비교하세요.

### 12. CI 실패 증거 개선

Microsoft.Testing.Platform의 reporting/diagnostic extension을 조사해 다음 계획을 작성합니다.

- TRX 또는 CI-native report
- hang timeout
- crash dump 보존 기간과 민감정보 정책
- flaky test 재시도보다 우선할 root-cause evidence
- test category별 병렬화 정책

retry로 빨간 build를 초록으로 가리는 것과 근본 원인을 수집하는 것의 차이를 설명하세요.

---

## 완료 조건

- [ ] 새 test가 먼저 실패하는 것을 확인했다.
- [ ] production code를 최소한으로 바꿔 test를 통과시켰다.
- [ ] test 이름만 읽어도 조건과 기대가 보인다.
- [ ] 시간·random·공유 상태·network 비결정성을 통제했다.
- [ ] Domain, Application, HTTP 중 가장 낮고 적절한 경계에 test를 뒀다.
- [ ] Release build와 전체 `dotnet test`가 warning/error/failure 없이 통과한다.
- [ ] `dotnet format --verify-no-changes`가 통과한다.
- [ ] `bin/`, `obj/`, test result 임시 파일을 commit하지 않는다.
