# 2026-10-22 Beginner to Pro 연습문제

## 시작 전 규칙

각 문제는 다음 순서로 풉니다.

1. 코드를 바꾸기 전에 결과를 먼저 적습니다.
2. 한 번에 한 가지 규칙만 수정합니다.
3. 아래 공통 검증을 매번 실행합니다.
4. 실패하면 오류 문구보다 “어떤 불변식이 깨졌는가?”를 먼저 설명합니다.

```powershell
cd dailyStudy/exercise/20261022
dotnet build .\GlobalMeeting.slnx -c Release
dotnet run --project .\src\GlobalMeetingApi\GlobalMeetingApi.csproj -c Release --no-build -- --self-test
```

완료 기준은 Release build 경고 0개·오류 0개와 `[SELF-TEST] 17/17 checks passed`입니다. 일부 과제는 새 검사를 추가하므로 분모도 함께 늘리세요.

---

## Beginner 1 — 시간 형식 카드 만들기

다음 값을 어느 C# 형식으로 표현할지 먼저 적으세요.

| 값 | 선택할 형식 | 이유 |
| --- | --- | --- |
| 생일 `2030-01-15` |  |  |
| 매일 알람 `09:00` |  |  |
| API 처리 시각 `2030-01-15T00:00:00Z` |  |  |
| 뉴욕이라는 시간대 규칙 |  |  |
| `UTC-04:00` 차이 |  |  |

힌트: `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeZoneInfo`, `TimeSpan`을 한 번씩 씁니다.

### 실행 확인

[`SelfTestRunner.cs`](./src/GlobalMeetingApi/SelfTesting/SelfTestRunner.cs)의 서울 검사를 찾고 `09:00 → 00:00Z`를 손으로 계산한 뒤 실행 결과와 비교합니다.

### 완료 기준

- 각 형식이 담지 **않는** 정보도 한 가지씩 말할 수 있습니다.
- `DateTimeOffset`이 IANA 시간대 ID를 보존하지 않는다는 점을 설명합니다.

---

## Beginner 2 — duration 경계값 예측하기

[`MeetingSlot.Create`](./src/GlobalMeetingApi/Domain/MeetingSlot.cs)를 읽고 다음 표를 실행 전에 채우세요.

| `durationMinutes` | 성공/실패 | 이유 |
| ---: | --- | --- |
| 0 |  |  |
| 14 |  |  |
| 15 |  |  |
| 20 |  |  |
| 480 |  |  |
| 495 |  |  |

`SelfTestRunner`에 표의 여섯 입력을 반복하는 검사를 추가합니다. 단, 단순히 여섯 메서드를 복사하지 말고 배열과 `foreach`로 입력·기대값을 순회하세요.

### 완료 기준

- 최소·최대·단위 경계를 모두 자동 검사합니다.
- 실패 결과의 `code`가 `duration.invalid`인지 확인합니다.
- 분모를 추가한 검사 수만큼 늘립니다.

---

## Beginner 3 — offset parser 표 기반 검사

[`UtcOffsetParser.Parse`](./src/GlobalMeetingApi/Presentation/MeetingContracts.cs)에 다음 입력을 넣는 표 기반 검사를 작성하세요.

| 입력 | 예상 |
| --- | --- |
| `null` | 성공, 값 없음 |
| `+09:00` | 성공, 9시간 |
| `-04:30` | 성공, -4시간 30분 |
| `09:00` | 형식 실패 |
| `+14:00` | 성공 |
| `+14:01` | 범위 실패 |
| `-15:00` | 범위 실패 |
| `+09:60` | 범위 실패 |

### 생각할 질문

`TimeSpan.TryParse` 하나에 맡기지 않고 API 계약을 정확히 `+HH:mm`으로 제한한 이유는 무엇인가요? 문화권, 생략된 부호, 일(day) 형식 관점에서 답하세요.

### 완료 기준

- 정상 값과 `Error.Code`를 모두 확인합니다.
- parser가 예외를 던지지 않고 예상 오류를 Result로 반환합니다.

---

## Beginner+ 4 — 실제 HTTP로 DST 공백 확인

서버를 실행합니다.

```powershell
dotnet run --project .\src\GlobalMeetingApi\GlobalMeetingApi.csproj -c Release -- --urls http://127.0.0.1:5198
```

다른 창에서 뉴욕의 2030년 DST 시작 공백을 보냅니다.

```powershell
$body = @{
    meetingId = 'MEET-SPRING-001'
    organizerId = 'USER-100'
    title = '존재하지 않는 시각 검사'
    localDate = '2030-03-10'
    localStartTime = '02:30:00'
    timeZoneId = 'America/New_York'
    durationMinutes = 30
} | ConvertTo-Json

try {
    Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5198/meetings' -ContentType 'application/json' -Body $body
} catch {
    $_.ErrorDetails.Message
}
```

### 완료 기준

- 상태 `400`, code `local_time.invalid`, field `localStartTime`을 확인합니다.
- 서버가 `03:30`으로 자동 보정하지 않는 이유를 한 문장으로 적습니다.

---

## Intermediate 5 — 중복 시각의 두 순간 비교

2030-11-03 뉴욕 `01:30`을 다음 두 offset으로 각각 예약하세요.

- `preferredUtcOffset: "-04:00"`
- `preferredUtcOffset: "-05:00"`

회의 ID와 주최자 ID는 서로 다르게 하여 충돌을 피합니다.

### 실행 전 예측

| offset | 예상 `startsAtUtc` |
| --- | --- |
| `-04:00` |  |
| `-05:00` |  |

### 완료 기준

- 두 응답의 지역 날짜·시각은 같고 UTC는 정확히 한 시간 차이입니다.
- offset을 빼면 `allowedValues`가 `-04:00`, `-05:00` 두 개임을 확인합니다.
- 배열 순서에 의존하지 않고 값 집합으로 검사합니다.

---

## Intermediate 6 — 새로운 모호성 Strategy

`PreferStandardOffsetPolicy`를 새로 작성해 중복 시각에서 더 작은 offset(뉴욕 예제에서는 `-05:00`)을 자동 선택하게 합니다.

1. [`IAmbiguousTimePolicy`](./src/GlobalMeetingApi/Application/Ports/IAmbiguousTimePolicy.cs)를 구현합니다.
2. 사용자가 offset을 명시했다면 가능한 후보인지 먼저 검증합니다.
3. 생략했다면 후보 중 더 작은 값을 선택합니다.
4. [`Program.cs`](./src/GlobalMeetingApi/Program.cs)의 DI 등록 한 줄만 교체합니다.
5. `-05:00 → 06:30Z` 검사를 추가합니다.

### 설계 질문

- 이 정책이 기술적으로 가능해도 제품 요구사항으로 위험할 수 있는 이유는 무엇인가요?
- API 응답에 “서버가 자동 선택함”을 어떻게 드러낼 것인가요?
- 사용자의 명시적 선택과 기본 정책 중 무엇을 우선할 것인가요?

### 완료 기준

- `ScheduleMeetingService`와 `TimeZoneInfoLocalTimeResolver`를 수정하지 않습니다.
- Composition Root의 구현 교체만으로 동작이 바뀝니다.
- 정책의 장점과 사용자 오해 위험을 README 메모로 남깁니다.

끝나면 원래 `RequireExplicitOffsetPolicy` 등록으로 되돌립니다.

---

## Intermediate+ 7 — 주최자 일정 조회 추가

한 주최자의 UTC 범위 안 회의를 시작 시각 순으로 조회하는 기능을 추가합니다.

```text
GET /organizers/{organizerId}/meetings?fromUtc=...&toUtc=...
```

### 요구사항

1. `IMeetingRepository`에 범위 조회 Port를 추가합니다.
2. In-memory Adapter는 `lock` 안에서 snapshot 배열을 만든 뒤 반환합니다.
3. Application Service는 `fromUtc < toUtc`, 최대 31일 범위를 검증합니다.
4. Presentation은 빈 목록을 정상 `200 []`로 반환합니다.
5. UTC 입력만 받고 출력에는 각 예약의 원래 지역 문맥도 포함합니다.

### LINQ 힌트

```csharp
meetings
    .Where(meeting => meeting.OrganizerId == normalizedOrganizerId)
    .Where(meeting => meeting.Slot.StartsAtUtc < toUtc
        && fromUtc < meeting.Slot.EndsAtUtc)
    .OrderBy(meeting => meeting.Slot.StartsAtUtc)
    .ToArray();
```

힌트 코드를 그대로 붙이기 전에 왜 “시작이 범위 안”만 보지 않고 **구간 겹침**을 검사하는지 설명하세요.

### 완료 기준

- Repository가 HTTP DTO를 반환하지 않습니다.
- Application이 query 유효성을 검증합니다.
- 시작 전부터 진행 중인 회의도 조회 범위와 겹치면 포함됩니다.

---

## Advanced 8 — 24개 경쟁 요청으로 원자성 확인

같은 주최자와 같은 UTC 구간에 서로 다른 24개 meeting ID를 동시에 저장합니다.

```csharp
const int requestCount = 24;
var readyCount = 0;

// 두 신호는 모든 작업이 출발선에 모인 뒤 한꺼번에 시작하게 합니다.
var allReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

var tasks = Enumerable.Range(1, requestCount)
    .Select(index => Task.Run(async () =>
    {
        // Interlocked를 써야 여러 thread의 준비 횟수가 사라지지 않습니다.
        if (Interlocked.Increment(ref readyCount) == requestCount)
        {
            allReady.TrySetResult(true);
        }

        await startSignal.Task;
        return await service.ScheduleAsync(
            CreateCompetingCommand(index),
            CancellationToken.None);
    }))
    .ToArray();

try
{
    await allReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
}
finally
{
    // 준비 확인이 실패해도 대기 중인 작업은 반드시 풀어 줍니다.
    startSignal.TrySetResult(true);
}

var results = await Task.WhenAll(tasks);
```

기준 구현은 [`SelfTestRunner.cs`](./src/GlobalMeetingApi/SelfTesting/SelfTestRunner.cs)의 시작 관문을 참고하세요. `ValueTask`가 즉시 완료되는 현재 메모리 저장소에서는 단순히 24개를 만든 뒤 `WhenAll`만 호출하면 실제 경쟁이 생기지 않을 수 있습니다.

### 확인할 불변식

- 성공은 정확히 1개입니다.
- `organizer.time_conflict`는 정확히 23개입니다.
- 예상하지 않은 오류는 없습니다.
- 저장된 성공 ID는 24개 후보 중 하나입니다.

### 고장 실험

Repository에서 충돌 조회와 저장 사이의 한 `lock`을 의도적으로 두 개로 나눠 경쟁 검사를 여러 번 실행해 보세요. 실패를 관찰한 뒤 즉시 원래 원자 구현으로 되돌립니다.

### 완료 기준

- 왜 단순 “먼저 조회하고 나중에 저장”이 안전하지 않은지 TOCTOU라는 말 없이도 설명할 수 있습니다.
- test retry로 경쟁 실패를 숨기지 않습니다.

---

## Advanced+ 9 — 영속 DB 불변식 설계

코드를 작성하기 전에 운영 DB 설계 메모를 만드세요.

### 반드시 결정할 항목

- `meeting_id` unique constraint
- `organizer_id`, `starts_at_utc`, `ends_at_utc` index
- 같은 주최자의 겹침을 막는 DB 기능 또는 transaction 전략
- 여러 API 인스턴스에서의 원자성
- deadlock/serialization failure의 제한된 retry
- commit 결과를 잃은 unknown outcome과 idempotency
- 원래 `local_date`, `local_start_time`, `time_zone_id`, `utc_offset` 보존
- 시간대 규칙 버전 또는 재계산 이력

### 비교표

| 선택 | 장점 | 주의점 |
| --- | --- | --- |
| serializable transaction | Application 변경이 비교적 작음 | 충돌·retry·처리량 고려 |
| PostgreSQL range + exclusion constraint | 불변식을 DB가 직접 보호 | DB 종속성과 migration 학습 필요 |
| organizer별 advisory/distributed lock | 명시적 직렬화 | lock 수명·장애 복구·hot key |
| optimistic version + bounded retry | 일반적인 CAS 모델 | 겹침 query 전체를 안전하게 보호하는 설계 필요 |

### 완료 기준

- “먼저 SELECT 후 INSERT”만으로 충분하다고 쓰지 않습니다.
- DB가 제공하는 원자적 보장과 Application retry 경계를 함께 설명합니다.

---

## Pro 10 — 반복 일정은 UTC 반복이 아닙니다

“매주 월요일 오전 9시 뉴욕 회의, 12회”를 모델링하세요. 구현 전에 다음 두 모델을 비교합니다.

### 모델 A — UTC에 7일씩 더하기

첫 순간을 UTC로 만든 뒤 `AddDays(7)`을 반복합니다.

### 모델 B — 지역 달력에서 다음 월요일 09:00 만들기

각 회차의 `DateOnly + TimeOnly + timeZoneId`를 만든 뒤 그 날짜의 시간대 규칙으로 UTC를 다시 계산합니다.

### 질문

1. DST가 바뀐 뒤에도 지역 오전 9시를 지키는 모델은 어느 쪽인가요?
2. 각 회차가 invalid/ambiguous이면 생성 전체를 실패할지, 해당 회차만 재확인할지 결정하세요.
3. 시간대 법률이 예약 생성 뒤 바뀌면 기존 UTC를 유지할지 재계산할지 결정하세요.
4. 이미 알림을 보낸 회차를 재계산할 수 있나요?
5. 사용자가 변경 내용을 승인해야 하나요?

### 구현 선택 과제

- `IRecurrenceExpander` Port를 정의합니다.
- 최대 회차와 최대 미래 기간을 제한합니다.
- 모든 회차를 먼저 검증한 뒤 저장하거나, 부분 성공 계약을 명확히 만듭니다.
- 한 회차의 오류에 날짜, 지역 시각, 가능한 offset을 포함합니다.

### 완료 기준

- 단순 UTC `AddDays(7)`이 지역 시각 반복 요구와 다를 수 있음을 자동 검사로 보여 줍니다.
- DST 전환 전후 두 회차의 지역 시각과 UTC를 함께 출력합니다.

---

## Pro+ 11 — 시간대 규칙 변경 운영 runbook

코드보다 운영 절차를 설계하는 문제입니다.

1. OS/컨테이너의 시간대 데이터 업데이트를 어떻게 배포할지 적습니다.
2. 미래 예약 중 계산 결과가 달라지는 항목을 찾는 reconciliation job을 설계합니다.
3. `oldUtc`, `newUtc`, `timeZoneId`, 규칙 버전, 변경 이유를 감사 로그에 남깁니다.
4. 자동 변경, 사용자 승인, 운영자 승인 기준을 구분합니다.
5. 변경 알림의 idempotency key와 재전송 정책을 적습니다.
6. 실행 임박 예약의 freeze window를 결정합니다.
7. 여러 인스턴스가 같은 예약을 동시에 재계산하지 않게 하는 방법을 적습니다.

### 완료 기준

- 시간대 데이터를 정적 상수처럼 취급하지 않습니다.
- 재계산이 side effect를 중복시키지 않도록 멱등성과 감사 가능성을 포함합니다.

---

## 최종 제출 체크

- [ ] 수정 전에 모든 결과를 예측했다.
- [ ] 새 메서드에도 무엇/파라미터/반환을 설명하는 한글 주석을 달았다.
- [ ] 새 문법이 처음 등장하는 줄에 쉬운 해설을 달았다.
- [ ] Domain/Application이 ASP.NET Core나 구체 DB에 의존하지 않는다.
- [ ] 취소를 Result 실패로 바꾸지 않는다.
- [ ] Release build가 경고 0개·오류 0개다.
- [ ] 자체 검증의 통과 수와 출력 분모가 일치한다.
- [ ] `bin/`, `obj/`, TestResults를 Git에 올리지 않는다.
