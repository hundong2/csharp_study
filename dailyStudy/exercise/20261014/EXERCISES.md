# Beginner to Pro 연습문제 — 안전한 일회용 초대 토큰

이 연습은 단순히 “암호화된 문자열을 만든다”에서 끝나지 않습니다. 외부 입력 검증, 토큰의 기밀성과 무결성, purpose 격리, 만료, 저장소의 원자적 1회 사용, HTTP 오류 계약을 차례대로 연결합니다.

각 단계가 끝날 때마다 아래 세 명령으로 회귀를 확인하세요.

~~~powershell
dotnet build .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release
dotnet run --project .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
~~~

<code>verify-http.ps1</code>은 Release 실행 파일을 별도 Production Kestrel 프로세스로 시작하고, 운영체제가 고른 동적 loopback 포트에서 실제 HTTP를 검증합니다. 마지막에는 자신이 시작한 정확한 PID만 종료합니다.

> 완료 기준은 “빌드된다”가 아닙니다. Domain 규칙, Application 흐름, 보호 토큰, 저장 상태, HTTP 응답, 설명이 같은 계약을 말해야 합니다.

---

## Level 0 — Beginner: 완성된 흐름을 실행하기

먼저 코드를 바꾸지 말고 전체 자동 검증을 실행하세요.

~~~powershell
dotnet build .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release
dotnet run --project .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
~~~

그다음 두 터미널로 사람이 직접 호출합니다.

터미널 A:

~~~powershell
dotnet run --project .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-build -- --urls http://127.0.0.1:51414 --environment Development
~~~

터미널 B:

~~~powershell
$baseUrl = 'http://127.0.0.1:51414'
$issueBody = @{
    invitationId = ' exercise-100 '
    projectId = ' csharp-study '
    email = ' LEARNER@example.com '
    role = ' member '
} | ConvertTo-Json

$created = Invoke-RestMethod -Method Post -Uri "$baseUrl/invitations" -ContentType 'application/json' -Body $issueBody
$created

$read = Invoke-RestMethod -Uri "$baseUrl/invitations/$($created.invitationId)"
$read

$redeemBody = @{ token = $created.token } | ConvertTo-Json
$redeemed = Invoke-RestMethod -Method Post -Uri "$baseUrl/invitations/redeem" -ContentType 'application/json' -Body $redeemBody
$redeemed
~~~

터미널 A는 확인 후 <kbd>Ctrl</kbd>+<kbd>C</kbd>로 종료하세요.

관찰할 것:

1. ID는 공백이 제거되고 대문자로 정규화됩니다.
2. 이메일은 공백이 제거되고 소문자가 됩니다.
3. 역할 문자열은 <code>ProjectRole.Member</code>로 바뀝니다.
4. 발급 응답에는 토큰이 있지만 GET과 교환 성공 응답에는 토큰이 없습니다.
5. 교환 전 상태는 <code>Issued</code>, 교환 뒤 상태는 <code>Redeemed</code>입니다.

완료 기준:

- 발급 → 조회 → 교환 흐름을 자신의 말로 설명합니다.
- “토큰이 유효하다”와 “아직 사용하지 않았다”가 서로 다른 검사임을 말합니다.
- 토큰을 로그나 화면 캡처에 남기지 않습니다.

---

## Level 1 — Beginner: C# 문법을 실제 값 흐름으로 읽기

다음 순서로 파일을 읽으세요.

1. <code>Domain/ProjectInvitation.cs</code>
2. <code>Domain/Result.cs</code>
3. <code>Application/InvitationCommands.cs</code>
4. <code>Application/RoleBasedInvitationExpiryPolicy.cs</code>
5. <code>Application/InvitationApplicationService.cs</code>

코드에서 각각의 근거 줄을 찾아 아래 표를 채우세요.

| 문법 | 이 예제에서 하는 일 | 왜 사용했는가 |
| --- | --- | --- |
| nullable <code>?</code> | 아직 검증하지 않은 외부 입력 표현 | 네트워크 입력이 누락될 현실을 숨기지 않음 |
| <code>record</code> | 명령, 오류, 결과 payload를 값으로 표현 | 생성 뒤 임의 변경을 줄이고 값 비교를 쉽게 함 |
| <code>var</code> | 오른쪽 식에서 정적 형식 추론 | 긴 형식명을 반복하지 않되 compile-time 형식 유지 |
| <code>is ... or ...</code> | 허용 enum 값 제한 | 정의되지 않은 숫자 enum까지 통과시키지 않음 |
| switch expression | 역할별 수명 또는 저장 결과 매핑 | 가능한 case를 한곳에서 빠짐없이 확인 |
| <code>async/await</code> | 저장소 작업을 비동기로 기다림 | thread를 붙잡지 않고 취소·예외 흐름 보존 |
| <code>with</code> | 기존 초대에서 사용 시각만 바꾼 복사본 생성 | 불변 snapshot 유지 |

실행 확인:

~~~powershell
$body = @{
    invitationId = ' syntax-101 '
    projectId = ' project-a '
    email = ' Beginner@Example.COM '
    role = ' aDmIn '
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri http://127.0.0.1:51414/invitations -ContentType 'application/json' -Body $body
~~~

완료 기준:

- <code>var</code>를 dynamic과 혼동하지 않습니다.
- nullable 표시는 null 검사를 수행하는 코드가 아님을 설명합니다.
- <code>with</code>가 원본 record를 제자리에서 바꾸지 않음을 설명합니다.

---

## Level 2 — Beginner+: 검증 오류와 Problem Details 확인하기

서버가 실행 중일 때 다음 helper를 터미널 B에 준비하세요.

~~~powershell
function Send-IssueRequest {
    param(
        [string]$InvitationId = 'validation-100',
        [string]$ProjectId = 'project-100',
        [string]$Email = 'learner@example.com',
        [string]$Role = 'Member'
    )

    $body = @{
        invitationId = $InvitationId
        projectId = $ProjectId
        email = $Email
        role = $Role
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Method Post -Uri http://127.0.0.1:51414/invitations -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
    [pscustomobject]@{
        Status = $response.StatusCode
        MediaType = $response.Headers.'Content-Type'
        Problem = $response.Content | ConvertFrom-Json
    }
}
~~~

다음 네 요청을 실행하세요.

~~~powershell
Send-IssueRequest -InvitationId 'x'
Send-IssueRequest -InvitationId 'validation-101' -ProjectId 'bad/id'
Send-IssueRequest -InvitationId 'validation-102' -Email 'not-an-email'
Send-IssueRequest -InvitationId 'validation-103' -Role 'Owner'
~~~

기대 계약:

| 잘못된 필드 | HTTP | code | field |
| --- | ---: | --- | --- |
| invitationId | 400 | <code>invitation.id.invalid</code> | <code>invitationId</code> |
| projectId | 400 | <code>invitation.project_id.invalid</code> | <code>projectId</code> |
| email | 400 | <code>invitation.email.invalid</code> | <code>email</code> |
| role | 400 | <code>invitation.role.invalid</code> | <code>role</code> |

추가로 깨진 JSON과 잘못된 media type도 확인하세요.

~~~powershell
Invoke-WebRequest -Method Post -Uri http://127.0.0.1:51414/invitations -ContentType 'application/json' -Body '{' -SkipHttpErrorCheck
Invoke-WebRequest -Method Post -Uri http://127.0.0.1:51414/invitations -ContentType 'text/plain' -Body '{}' -SkipHttpErrorCheck
~~~

완료 기준:

- Application 오류와 framework가 handler보다 먼저 만든 400·415를 구분합니다.
- 모든 오류 응답의 media type이 <code>application/problem+json</code>인지 확인합니다.
- 사람용 <code>detail</code>이 아니라 안정적인 <code>code</code>로 프로그램이 분기해야 하는 이유를 설명합니다.

---

## Level 3 — Intermediate: 세 번째 역할을 끝까지 추가하기

읽기 전용 역할 <code>Reader</code>를 새로 추가하고, Member보다 길고 Admin보다 권한이 낮다는 정책을 문서화하세요. 수명은 예를 들어 48시간으로 정합니다.

변경 순서:

1. <code>ProjectRole</code>에 <code>Reader</code>를 추가합니다.
2. <code>ProjectInvitation.ParseRole</code>의 허용 목록과 안전한 오류 설명을 갱신합니다.
3. <code>RoleBasedInvitationExpiryPolicy</code>가 Reader 수명을 생성자에서 받고 양수인지 검증하게 합니다.
4. switch expression에 Reader case를 추가합니다.
5. <code>Program.cs</code>의 Composition Root에서 세 수명을 명시적으로 조립합니다.
6. Presentation 계약 설명과 자체 테스트에 Reader 발급·교환 시나리오를 추가합니다.
7. README의 역할 표, HTTP 예, 구조 설명을 함께 갱신합니다.

주의:

- <code>Enum.TryParse</code>만 호출하면 enum에 정의되지 않은 숫자 문자열이 들어올 수 있으므로 허용 목록 확인을 유지합니다.
- “Reader는 권한이 낮으니 영원히 유효해도 된다”는 결론은 안전하지 않습니다.
- 기존 Member와 Admin 계약이 깨지지 않아야 합니다.

완료 기준:

- 새 역할을 추가했을 때 Domain, Strategy, Composition Root, HTTP 계약, 테스트가 함께 바뀌었습니다.
- 처리하지 않은 역할을 default 값으로 조용히 바꾸지 않습니다.
- 세 회귀 명령이 모두 통과합니다.

---

## Level 4 — Intermediate+: 변조와 purpose 격리를 검증하기

먼저 발급된 토큰의 가운데 글자 하나를 바꾸어 교환해 보세요.

~~~powershell
$token = $created.token
$index = [int][Math]::Floor($token.Length / 2)
$replacement = if ($token[$index] -eq 'A') { 'B' } else { 'A' }
$tampered = $token.Substring(0, $index) + $replacement + $token.Substring($index + 1)
$body = @{ token = $tampered } | ConvertTo-Json

Invoke-WebRequest -Method Post -Uri http://127.0.0.1:51414/invitations/redeem -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
~~~

그다음 자체 테스트에 “같은 key ring이지만 다른 purpose로 보호한 payload”를 추가하세요.

검증할 세 경우:

1. 원래 토큰을 원래 purpose로 해제하면 성공합니다.
2. 한 글자 변조한 토큰은 실패합니다.
3. 다른 purpose의 protector가 만든 토큰은 실패합니다.

세 실패 원인을 외부에는 모두 다음 하나로 반환하세요.

~~~text
400
code  = invitation.token.invalid_or_expired
field = token
~~~

완료 기준:

- purpose가 단순 설명 문구가 아니라 암호학적 격리 경계임을 설명합니다.
- <code>CryptographicException.Message</code>를 HTTP detail이나 로그의 사용자 필드에 그대로 노출하지 않습니다.
- 변조·purpose 불일치·만료를 외부에서 구별하지 못하게 유지합니다.

---

## Level 5 — Advanced: 만료 경계를 결정적으로 테스트하기

실제 시간을 기다리는 <code>Task.Delay</code> 테스트를 만들지 마세요. 테스트용 <code>TimeProvider</code>를 사용해 다음 세 순간을 검증합니다.

~~~text
expiresAtUtc 직전 1 tick  → 사용 가능
expiresAtUtc 정확히      → 만료
expiresAtUtc 이후 1 tick → 만료
~~~

작업 순서:

1. 고정된 UTC 시각에 Member 초대를 발급합니다.
2. 시계를 만료 직전으로 이동해 교환 성공을 확인합니다.
3. 별도 초대를 만들고 시계를 정확한 만료 경계로 이동합니다.
4. <code>invitation.token.invalid_or_expired</code>를 확인합니다.
5. Production 코드가 <code>DateTimeOffset.UtcNow</code>를 직접 호출하지 않는지 검색합니다.

~~~powershell
rg -n "DateTimeOffset\\.UtcNow|DateTime\\.UtcNow" .\src\InvitationTokenApi --glob '!bin/**' --glob '!obj/**'
~~~

완료 기준:

- 결과가 컴퓨터 속도나 실행 시각에 따라 흔들리지 않습니다.
- 한 요청 안에서는 현재 시각을 한 번만 읽습니다.
- <code>nowUtc >= expiresAtUtc</code>라는 경계를 자신의 말로 설명합니다.

---

## Level 6 — Advanced+: 동시 재사용 경쟁을 재현하기

같은 유효 토큰을 32개 작업이 동시에 교환하도록 자체 테스트를 확장하세요.

~~~text
32 concurrent redeem attempts
  ├─ 정확히 1개: Redeemed
  └─ 정확히 31개: AlreadyRedeemed
~~~

확인할 사항:

1. 저장소가 “조회 후 나중에 저장”을 별도 임계 구역에서 수행하지 않습니다.
2. 아직 사용 전인지 확인하는 일과 <code>RedeemedAtUtc</code>를 기록하는 일이 하나의 원자적 연산입니다.
3. Application Service가 먼저 GET한 결과만 믿지 않고 Repository의 최종 상태 전이 결과를 해석합니다.
4. 테스트는 모든 Task가 끝난 뒤 성공 수와 재사용 수를 각각 셉니다.

실험을 위해 lock을 임시로 두 단계로 나눠 실패를 관찰할 수 있지만, 원래의 원자적 구현으로 복구한 뒤 세 회귀 명령을 반드시 다시 실행하세요.

완료 기준:

- Data Protection만으로 토큰이 한 번만 사용된다고 말하지 않습니다.
- 한 프로세스 lock의 보장 범위와 여러 서버·DB의 보장 범위를 구분합니다.
- 경쟁 테스트가 “최소 한 개 성공”이 아니라 “정확히 한 개 성공”을 검증합니다.

---

## Level 7 — Pro: key ring과 다중 인스턴스 배포 설계하기

현재 데모 key ring을 운영 환경으로 옮길 때 다음 두 실험을 자체 테스트나 별도 작은 실험 코드로 수행하세요.

### 실험 A — 서로 다른 key ring

1. Provider A로 토큰을 발급합니다.
2. 독립된 Provider B로 해제를 시도합니다.
3. 실패가 안전한 공통 토큰 오류로 바뀌는지 확인합니다.

### 실험 B — 공유 key ring

1. 테스트 전용 임시 폴더 하나에 key ring을 저장하는 Provider A와 B를 만듭니다.
2. A가 발급하고 B가 해제합니다.
3. 성공을 확인한 뒤 테스트가 만든 정확한 임시 폴더만 정리합니다.

운영 설계 문서에는 다음을 포함하세요.

- 여러 앱 인스턴스가 같은 key ring과 application name을 공유하는 방법
- key at rest 보호와 저장소 접근 권한
- 키 회전 중 기존 토큰을 수명 동안 해제할 수 있는 조건
- 컨테이너 재시작 때 휘발성 key ring을 잃으면 생기는 현상
- 환경별 purpose/application name 격리
- key 삭제과 revoke가 아직 유효한 토큰에 미치는 영향

완료 기준:

- “암호화 알고리즘이 강하다”만으로 배포가 끝나지 않음을 설명합니다.
- key ring 공유와 초대 Repository 공유를 별개 문제로 다룹니다.
- 테스트용 key를 저장소나 Git에 커밋하지 않습니다.

---

## Level 8 — Pro+: 운영 DB의 원자적 1회 사용 설계하기

메모리 Repository를 실제 DB Adapter로 교체하는 설계와 통합 테스트 계획을 작성하세요. 핵심은 다음과 같은 조건부 갱신입니다.

~~~sql
UPDATE Invitations
SET RedeemedAtUtc = @now
WHERE InvitationId = @id
  AND RedeemedAtUtc IS NULL
  AND ExpiresAtUtc > @now;
~~~

영향받은 행 수가 1이면 성공입니다. 0이면 같은 transaction 안에서 현재 상태를 읽어 <code>AlreadyRedeemed</code>, <code>Expired</code>, <code>NotFound</code> 중 내부 결과를 정합니다. 외부 오류는 보안 계약에 맞게 다시 합칠 수 있습니다.

필수 설계 항목:

- <code>InvitationId</code> unique constraint
- UTC column 형식과 정밀도
- 동일 토큰 100개 동시 요청 통합 테스트
- transaction 격리와 deadlock 재시도 기준
- 취소 시 rollback
- 만료·사용 완료 데이터의 보존 및 삭제 정책
- 토큰 원문을 저장하거나 로그에 남기지 않는 정책
- 성공, invalid-or-expired, replay, 지연 시간 metric
- 이메일 같은 개인정보의 열람 권한과 보존 기간

완료 기준:

- 애플리케이션의 <code>ExistsAsync</code> 뒤 <code>UpdateAsync</code>만으로 원자성을 보장하지 않습니다.
- Repository Port는 유지하고 Adapter와 통합 테스트를 교체하는 방향을 제시합니다.
- 보안 오류를 세밀하게 관측하되 공격자에게 원인을 자세히 응답하지 않습니다.

---

## Level 9 — Pro+: 토큰 형식 버전 마이그레이션

payload의 <code>Version</code>을 실제 마이그레이션 경계로 사용해 보세요.

요구 사항:

1. 새 발급은 version 2 payload만 만듭니다.
2. 배포 전 발급한 version 1 토큰은 원래 만료 시각까지 사용할 수 있습니다.
3. 알 수 없는 version 99는 공통 invalid-or-expired 오류입니다.
4. version별 역직렬화와 Domain 변환을 작은 전용 component로 분리합니다.
5. version 1 지원 제거 날짜와 관측 지표를 문서화합니다.

주의:

- JSON 필드를 무조건 optional로 늘리는 것만으로 모든 호환성이 해결되지는 않습니다.
- purpose 문자열을 즉시 바꾸면 기존 토큰 전체가 무효가 될 수 있습니다.
- payload version, purpose version, API version은 서로 다른 개념입니다.

완료 기준:

- rolling deployment 중 구버전·신버전 인스턴스가 함께 있을 때의 흐름을 그립니다.
- 구버전 토큰 지원 종료 조건을 “감으로” 정하지 않고 최대 수명과 관측 자료로 정합니다.
- 알 수 없는 payload를 예외 본문과 함께 외부로 노출하지 않습니다.

---

## 최종 제출 체크

- [ ] Release build가 warning 없이 성공한다.
- [ ] 자체 테스트가 검증·변조·purpose·만료·재사용·동시성을 확인한다.
- [ ] 별도 Production Kestrel black-box 검증이 통과한다.
- [ ] 모든 새 C# 메서드에 목적·파라미터·반환값을 설명하는 한글 주석이 있다.
- [ ] 처음 나온 문법과 보안·아키텍처 선택의 이유를 한글 주석으로 설명했다.
- [ ] 토큰, key ring, 개인정보, build 산출물을 커밋하지 않았다.
- [ ] README의 계약·구조도·출처·검증 기록을 변경 사항과 함께 갱신했다.
