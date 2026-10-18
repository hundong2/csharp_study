# 연습문제 — Minimal API 검증 경계를 직접 바꿔 보기

각 과제는 원본에서 한 단계씩 진행하세요. 저장소 루트에서 오늘 폴더로 이동한 뒤 baseline을 통과시키고, 변경 뒤 같은 명령을 다시 실행합니다.

```powershell
cd .\dailyStudy\exercise\20261019
dotnet build .\src\InterviewValidationApi\InterviewValidationApi.csproj -c Release
dotnet run --project .\src\InterviewValidationApi\InterviewValidationApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
```

---

## Beginner 1 — Attribute 하나 추가하기

요구 사항:

1. `candidateName`에 숫자를 허용하지 않는 사용자 정의 `CandidateNameAttribute`를 만드세요.
2. null과 빈 문자열은 `[Required]`가 담당하게 하세요.
3. `홍길동`, `Jane Doe`, `김 민수`는 허용하고 `Candidate01`은 400으로 거절하세요.
4. `verify-http.ps1`에 성공·실패 assertion을 각각 추가하세요.

관찰할 점: 한 필드만 필요한 규칙은 `ValidationAttribute`가 자연스럽습니다.

힌트: `ValidationAttribute.IsValid(object?, ValidationContext)`를 override하고 오류 멤버 이름을 넣으세요.

---

## Beginner 2 — nullable과 Required 확인하기

`CreateInterviewRequest.CandidateEmail`에서 `[Required]`를 잠시 제거하고 JSON에서 필드를 생략해 보세요.

확인할 질문:

- compiler의 `string?`와 runtime validation은 같은 역할인가요?
- handler까지 들어갔을 때 Domain은 어떤 오류를 반환하나요?
- 다시 `[Required]`를 붙였을 때 어느 계층에서 먼저 멈추나요?

실험 뒤 원래 계약으로 복구하세요.

---

## Beginner 3 — onsite 요청 실행하기

직접 서버를 실행하고 다음 세 요청을 보내세요.

1. `mode=onsite`, `room=B-201`, `meetingUrl=null` → 201
2. `mode=onsite`, `room=null` → 400
3. `mode=onsite`, `room=B-201`, `meetingUrl=https://...` → 400

각 응답에서 `status`, `errors`, `code`, `traceId`를 표로 기록하세요.

---

## Intermediate 1 — 종료 시각을 입력으로 받지 않는 이유

현재 API는 `startsAtUtc + durationMinutes`로 `endsAtUtc`를 계산합니다.

1. 만약 클라이언트가 `endsAtUtc`도 보낸다면 어떤 모순이 가능한지 세 가지 적으세요.
2. 실험용 branch에서 `endsAtUtc`를 추가하고 “끝 > 시작”과 “duration 일치” 교차 검증을 구현하세요.
3. 어느 계약이 더 단순한지 비교한 뒤, 원본 설계로 돌아오세요.

목표: 파생 가능한 값을 입력으로 받지 않는 것이 검증 상태 공간을 줄인다는 점을 이해합니다.

---

## Intermediate 2 — 새 Phone mode Strategy

요구 사항:

1. `InterviewMode.Phone`을 추가하세요.
2. 요청 `mode=phone`은 `meetingUrl`과 `room`을 모두 받지 않게 하세요.
3. 응답 location은 `phone`이라는 공개 문자열을 사용하세요. 전화번호는 개인정보이므로 이 학습 예제에서 입력받지 않습니다.
4. `PhoneInterviewBufferPolicy`는 앞 2분, 뒤 3분을 반환하세요.
5. DI 등록을 빠뜨렸을 때 시작 또는 자체 테스트가 명확히 실패하는지 확인하세요.

검증: phone 성공, 불필요한 location 필드 실패, buffer 충돌 경계를 self-test에 추가합니다.

---

## Intermediate 3 — 다국어 validation 메시지

`AddLocalization`과 validation localization 문서를 참고해 영어/한국어 메시지를 분리하세요.

완료 조건:

- 같은 안정적인 `code`는 언어와 무관합니다.
- `Accept-Language: ko`와 `en`에서 사람이 읽는 메시지만 달라집니다.
- 잘못된 언어 header는 안전한 기본 언어를 사용합니다.

공식 참고: <https://learn.microsoft.com/en-us/aspnet/core/validation/overview?view=aspnetcore-10.0>

---

## Advanced 1 — 충돌 결과에 안전한 정보만 추가하기

409 응답에 아래 값을 추가하세요.

- `conflictKind`: `interviewer_time_overlap`
- `retryable`: `false`

다른 지원자의 이름, 이메일, meeting URL, 정확한 기존 일정은 노출하지 마세요.

왜 “충돌이 있다”는 사실과 기존 일정 상세 공개가 다른 보안 결정인지 한 문장으로 설명하세요.

---

## Advanced 2 — 면접관별 근무 시간 Port

`IInterviewerAvailability` Port를 추가해 면접관이 UTC 기준 허용 시간에 근무하는지 비동기로 확인하세요.

제약:

- DataAnnotations 또는 `IValidatableObject`에서 Port를 호출하지 않습니다.
- `ScheduleInterviewService`에서 `CancellationToken`을 전달합니다.
- 한 명이라도 근무 시간이 아니면 `interview.outside_working_hours` Result를 반환합니다.
- Repository는 호출되지 않아야 합니다.
- fake Adapter로 성공·실패·취소를 self-test합니다.

---

## Advanced 3 — 여러 assembly에서 AddValidation

Presentation DTO와 endpoint를 별도 class library로 옮겨 보세요.

1. `dotnet new classlib -n InterviewValidationApi.Presentation -f net10.0`으로 project를 만들고 host에서 project reference를 추가합니다.
2. class library에는 `Microsoft.Extensions.Validation` package `10.0.13`을 추가합니다. Minimal API endpoint 형식도 옮긴다면 아래 `ItemGroup`을 `.csproj`의 `<Project>...</Project>` 안에 추가합니다.
3. host assembly에서만 `AddValidation()`을 호출했을 때 검증 metadata가 발견되는지 확인합니다.
4. DTO/endpoint가 있는 assembly에 `AddApiValidation(this IServiceCollection services)` extension을 만들고, 그 메서드 안에서 `services.AddValidation()`을 호출한 뒤 host에서 extension을 호출합니다.
5. 이동 전후 HTTP 400 계약이 같은지 검증합니다.

package 추가 예시:

```powershell
dotnet add .\InterviewValidationApi.Presentation\InterviewValidationApi.Presentation.csproj package Microsoft.Extensions.Validation --version 10.0.13
```

endpoint 형식을 위한 framework reference 예시:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

`AddValidation()` 호출을 DTO가 있는 assembly의 코드에 두어야 source generator가 그 assembly의 검증 형식을 발견할 수 있습니다. `FrameworkReference`는 endpoint route 형식을 컴파일하기 위한 것이고, `Microsoft.Extensions.Validation` package는 validation API와 generator를 제공하므로 역할이 다릅니다.

주의: .NET 10 validation source generator는 `AddValidation`이 호출된 assembly 경계를 고려합니다. “컴파일된다”와 “자동 검증이 실행된다”를 구분하세요.

---

## Pro 1 — 운영 DB에서 원자적 시간 충돌 보장하기

현재 `lock`을 PostgreSQL 또는 SQL Server 설계로 바꾸는 기술 문서를 작성하세요.

반드시 다룰 항목:

- 여러 API 인스턴스에서는 process-local lock이 실패하는 이유
- transaction isolation 또는 DB constraint 선택
- 면접관이 여러 명일 때 충돌 키/행을 잠그는 순서
- deadlock 회피와 retry 상한
- 정확히 경계가 닿는 반열린 구간 정의
- 저장 성공 응답을 잃었을 때 idempotency 전략

가능하면 integration test에서 20개 병렬 요청 중 정확히 하나만 commit되는지 검증하세요.

---

## Pro 2 — 일정 변경과 낙관적 동시성

`PUT /interviews/{id}` 또는 `PATCH`를 설계하고 version/ETag를 사용하세요.

완료 조건:

- 읽은 version을 `If-Match`로 보냅니다.
- header 누락은 428, 오래된 ETag는 412입니다.
- 일정 변경도 충돌 검사와 version 갱신을 한 transaction에서 처리합니다.
- 응답에서 새 ETag를 돌려줍니다.
- 같은 version으로 동시에 수정한 두 요청 중 하나만 성공합니다.

---

## Pro 3 — 관측성과 개인정보 보호

구조화 로그, metric, trace를 설계하세요.

허용 예:

- 일정 ID
- 결과 code
- mode
- 처리 시간
- 충돌 여부

금지 예:

- 지원자 이메일 원문
- meeting URL
- 회의실의 민감한 출입 정보
- validation body 전체

`validation.failed`의 field 이름별 횟수, `slot_conflict` 비율, 저장 지연을 낮은 카디널리티 metric으로 표현하고 경보 기준을 적으세요.

---

## 완료 정의

- [ ] 변경 뒤 build가 경고 0, 오류 0이다.
- [ ] 새 규칙의 성공·경계·실패 self-test가 있다.
- [ ] 실제 HTTP 성공/Problem Details 계약을 검증한다.
- [ ] validation Attribute 안에 I/O가 없다.
- [ ] Domain 불변식과 Application 업무 규칙의 위치를 설명한다.
- [ ] 병렬 요청에서도 저장 원자성이 유지된다.
- [ ] 개인정보와 내부 예외를 응답·로그에 노출하지 않는다.
