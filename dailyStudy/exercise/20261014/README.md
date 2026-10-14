# ASP.NET Core Data Protection으로 배우는 안전한 일회용 초대 토큰

## 👣 코드 읽는 순서 (Reading order)

처음부터 암호화 API 세부사항을 모두 외우려 하지 마세요. 아래 순서대로 **HTTP 계약 → 유효한 Domain 상태 → 유스케이스 → 보안 Adapter → 원자적 저장 → 검증**을 따라가면 됩니다.

1. 이 README의 [오늘의 목표](#-오늘의-목표)와 [HTTP 계약](#-실행할-http-계약)을 읽어 완성 모습을 먼저 봅니다.
2. [<code>InvitationTokenApi.csproj</code>](./src/InvitationTokenApi/InvitationTokenApi.csproj)에서 <code>net10.0</code>, C# 14, nullable, warning-as-error 설정을 확인합니다.
3. [<code>InvitationContracts.cs</code>](./src/InvitationTokenApi/Presentation/InvitationContracts.cs)에서 발급·조회·사용 JSON 모양을 봅니다.
4. [<code>ProjectInvitation.cs</code>](./src/InvitationTokenApi/Domain/ProjectInvitation.cs)와 [<code>Result.cs</code>](./src/InvitationTokenApi/Domain/Result.cs)에서 검증, 정규화, 불변 상태, 예상 실패 표현을 읽습니다.
5. [<code>RoleBasedInvitationExpiryPolicy.cs</code>](./src/InvitationTokenApi/Application/RoleBasedInvitationExpiryPolicy.cs)에서 Member 24시간, Admin 15분 만료 Strategy를 확인합니다.
6. [<code>InvitationApplicationService.cs</code>](./src/InvitationTokenApi/Application/InvitationApplicationService.cs)에서 발급·조회·사용 순서와 Result/예외/취소 경계를 따라갑니다.
7. <code>Application/Ports</code>의 세 interface와 [<code>DataProtectionInvitationTokenProtector.cs</code>](./src/InvitationTokenApi/Security/DataProtectionInvitationTokenProtector.cs), [<code>InMemoryInvitationRepository.cs</code>](./src/InvitationTokenApi/Infrastructure/InMemoryInvitationRepository.cs)를 비교합니다.
8. [<code>InvitationEndpoints.cs</code>](./src/InvitationTokenApi/Presentation/InvitationEndpoints.cs)와 [<code>Program.cs</code>](./src/InvitationTokenApi/Program.cs)에서 HTTP 상태, Problem Details, DI, Composition Root를 확인합니다.
9. [<code>SelfTestRunner.cs</code>](./src/InvitationTokenApi/SelfTesting/SelfTestRunner.cs)의 38개 assertion과 [<code>verify-http.ps1</code>](./verify-http.ps1)의 128개 black-box assertion을 실행합니다.
10. [<code>EXERCISES.md</code>](./EXERCISES.md)를 Beginner부터 Pro+까지 진행하고 [<code>CHECKPOINT.md</code>](./CHECKPOINT.md)로 이해를 검증합니다.

> 막히면 “Data Protection은 토큰을 보호하고, Repository는 토큰의 한 번뿐인 사용을 보장한다”라는 문장으로 돌아오세요.

---

## 📌 빠른 탐색

| 찾는 내용 | 바로가기 |
| --- | --- |
| 오늘 만들 결과 | [오늘의 목표](#-오늘의-목표) |
| 요청·응답 상태 | [실행할 HTTP 계약](#-실행할-http-계약) |
| C# 기본 문법 | [Syntax와 Grammar](#-syntax와-grammar) |
| 토큰 보안 핵심 | [Data Protection을 이해하는 7단계](#-data-protection을-이해하는-7단계) |
| 구조와 실행 흐름 | [구조도](#-구조도) |
| 설계 이유 | [패턴과 설계 의도 Why](#-패턴과-설계-의도-why) |
| 파일 찾기 | [파일 내비게이션 맵](#-파일-내비게이션-맵) |
| 직접 실행 | [빌드와 실행](#-빌드와-실행) |
| 이해도 확인 | [초보자 이해도 검증 단계](#-초보자-이해도-검증-단계-validation-stage) |
| 버전과 출처 | [버전과 공식 출처](#-버전과-공식-출처) |

---

## 🎯 오늘의 목표

프로젝트 초대 Minimal API를 실행하면서 다음을 배웁니다.

- nullable 외부 문자열을 검증·정규화해 불변 <code>ProjectInvitation</code>으로 만듭니다.
- <code>record</code>, <code>enum</code>, generic <code>Result&lt;T&gt;</code>, pattern matching, switch expression, LINQ, <code>with</code>, <code>async/await</code>를 실제 흐름에서 읽습니다.
- ASP.NET Core Data Protection으로 payload의 기밀성과 무결성을 보호합니다.
- 고유한 purpose 문자열로 초대 토큰과 다른 기능의 토큰을 암호학적으로 격리합니다.
- 변조, 잘못된 purpose, 알 수 없는 형식, 만료를 하나의 안전한 외부 오류로 합칩니다.
- <code>TimeProvider</code>와 Strategy로 Member 24시간, Admin 15분 규칙을 결정적으로 테스트합니다.
- 암호화만으로 막을 수 없는 replay를 Repository의 원자적 상태 전이로 차단합니다.
- Domain Model, Result, Application Service, Repository, Strategy, Port/Adapter, DI, Composition Root를 분리합니다.
- 201·200·400·404·409·415와 Problem Details 계약을 실제 Production Kestrel에서 검증합니다.

오늘의 핵심 문장은 이것입니다.

> 보호 토큰은 “서버가 발급했고 변조되지 않았다”를 증명하지만, “아직 사용하지 않았다”까지 증명하지는 않는다.

### 보안 책임 표

| 책임 | 담당 | 이 예제의 구현 |
| --- | --- | --- |
| payload 기밀성·무결성 | Data Protection Adapter | <code>IDataProtector.Protect/Unprotect</code> |
| 토큰 종류 격리 | purpose | <code>InvitationTokenApi.ProjectInvitation.v1</code> |
| 토큰 만료 | 보호된 만료 claim + Strategy | <code>ExpiresAtUtc</code>, <code>IInvitationExpiryPolicy</code> |
| 현재 초대 상태 | Repository | 발급 기록과 token payload 재대조 |
| 정확히 한 번 사용 | Repository 원자 연산 | <code>lock</code> 안의 확인 + 불변 snapshot 교체 |
| HTTP 공개 오류 | Presentation | Problem Details + 안정적인 <code>code</code> |

학습 API이므로 인증·인가, 실제 메일 발송, 운영 DB, 감사 로그는 생략했습니다. 특히 생성·조회 endpoint에는 사용자 인증과 프로젝트별 인가를, 사용 endpoint에는 초대 대상 사용자와 현재 사용자의 신원 일치를 확인하는 절차를 추가하기 전까지 인터넷에 공개하면 안 됩니다.

---

## 🌐 실행할 HTTP 계약

| Method / Path | 성공 | 예상 오류 | 설명 |
| --- | --- | --- | --- |
| <code>POST /invitations</code> | <code>201</code> + <code>Location</code> + 발급 응답 | <code>400</code>, <code>409</code>, <code>415</code> | 초대를 저장하고 토큰을 한 번 전달 |
| <code>GET /invitations/{invitationId}</code> | <code>200</code> + 상태 응답 | <code>404</code> | 토큰을 제외한 현재 초대 조회 |
| <code>POST /invitations/redeem</code> | <code>200</code> + 최소 사용 응답 | <code>400</code>, <code>409</code>, <code>415</code> | 토큰을 검증하고 원자적으로 한 번 사용 |
| <code>GET /</code> | <code>200</code> 준비 상태 | — | 검증 스크립트의 readiness endpoint |

### 초대 발급

~~~json
{
  "invitationId": " invite-1001 ",
  "projectId": " csharp-study ",
  "email": " LEARNER@example.com ",
  "role": " member "
}
~~~

성공 응답은 ID를 대문자, 이메일을 소문자로 정규화하고 bearer secret인 토큰을 처음 한 번 전달합니다.

~~~json
{
  "invitationId": "INVITE-1001",
  "projectId": "CSHARP-STUDY",
  "email": "learner@example.com",
  "role": "Member",
  "token": "CfDJ8...",
  "expiresAtUtc": "2026-10-15T01:00:00+00:00"
}
~~~

<code>Location</code>은 <code>/invitations/INVITE-1001</code>입니다. 실제 토큰과 만료 시각은 실행 때마다 달라집니다.

### 조회와 사용

조회 응답은 <code>status</code>, <code>expiresAtUtc</code>, nullable <code>redeemedAtUtc</code>를 포함하지만 토큰을 다시 노출하지 않습니다. 사용 요청은 다음 한 필드만 보냅니다.

~~~json
{
  "token": "CfDJ8..."
}
~~~

첫 사용은 <code>200</code>이며 <code>invitationId</code>, <code>projectId</code>, <code>role</code>, <code>redeemedAtUtc</code>만 반환합니다. 같은 토큰을 다시 보내면 <code>409 invitation.already_redeemed</code>입니다.

### 안정 오류 코드

| HTTP | <code>code</code> | <code>field</code> | 원인 |
| ---: | --- | --- | --- |
| 400 | <code>invitation.id.invalid</code> | <code>invitationId</code> | 초대 ID 누락, 길이, 문자 규칙 위반 |
| 400 | <code>invitation.project_id.invalid</code> | <code>projectId</code> | 프로젝트 ID 규칙 위반 |
| 400 | <code>invitation.email.invalid</code> | <code>email</code> | 학습용 이메일 형식 위반 |
| 400 | <code>invitation.role.invalid</code> | <code>role</code> | Member/Admin 이외 역할 |
| 400 | <code>invitation.token.invalid_or_expired</code> | <code>token</code> | 빈 값, 형식, 변조, purpose, payload, 미존재, 만료 실패 |
| 404 | <code>invitation.not_found</code> | <code>invitationId</code> | 공개 계약상 조회할 초대 없음 |
| 409 | <code>invitation.duplicate</code> | <code>invitationId</code> | 같은 초대 ID가 이미 존재 |
| 409 | <code>invitation.already_redeemed</code> | <code>token</code> | 유효했던 초대가 이미 사용됨 |
| 400 | <code>http.bad_request</code> | <code>request</code> | 깨진 JSON 등 framework binding 실패 |
| 415 | <code>http.unsupported_media_type</code> | <code>request</code> | <code>application/json</code>이 아닌 body |

오류 본문은 <code>application/problem+json</code>이며 <code>type</code>, <code>title</code>, <code>status</code>, <code>detail</code>, <code>instance</code>, <code>code</code>, <code>field</code>, <code>traceId</code>를 가집니다. 암호 예외 원문은 공개하지 않습니다.

---

## 🔤 Syntax와 Grammar

### 1. <code>var</code>와 정적 형식

~~~csharp
var issuedAtUtc = _timeProvider.GetUtcNow();
~~~

<code>var</code>는 runtime의 “아무 형식”이 아닙니다. 오른쪽 식을 보고 compile time 형식을 <code>DateTimeOffset</code>으로 결정합니다.

### 2. nullable <code>?</code>와 null-forgiving <code>!</code>

요청 DTO와 command의 <code>string?</code>는 네트워크 입력이 누락되거나 null일 수 있음을 표현합니다. Domain factory를 통과하면 내부는 non-null 문자열만 가집니다.

~~~text
외부 string? → 검증·정규화 Result → Domain string
~~~

<code>result.Value!</code>의 <code>!</code>는 앞선 성공 검사로 null이 아님을 증명했다고 nullable 분석기에 알립니다. runtime null 검사를 대신하지 않습니다.

### 3. <code>enum</code>, <code>record</code>, 불변 snapshot

- <code>ProjectRole</code>은 Member/Admin 허용 목록을 만듭니다.
- <code>record</code>는 오류, 명령, payload, Domain snapshot 같은 값 중심 자료에 적합합니다.
- <code>RedeemedAtUtc</code>는 <code>private init</code>이므로 외부가 임의 상태를 만들 수 없습니다.
- Domain의 <code>Redeem</code>만 <code>with</code>로 사용 시각이 다른 새 snapshot을 만듭니다.

불변성만으로 동시성은 해결되지 않습니다. 여러 요청 중 어느 snapshot을 저장할지는 Repository가 원자적으로 결정합니다.

### 4. generic <code>Result&lt;T&gt;</code>

<code>T</code>는 성공 값 형식을 나중에 채우는 자리입니다. 검증·중복·재사용처럼 호출자가 예상하고 분기할 실패는 Result 값으로 전달합니다.

- Result: 사용자가 고칠 수 있거나 업무상 예상 가능한 결과
- 예외: 잘못된 내부 구성, 처리하지 않은 상태, 예상하지 못한 장애
- 취소: 요청자가 더 기다리지 않는 별도 제어 흐름

### 5. pattern matching, switch expression, LINQ

<code>parsed is ProjectRole.Member or ProjectRole.Admin</code>은 enum 파싱 뒤 명시적인 허용 목록을 확인합니다. 역할별 수명과 Repository 결과는 switch expression으로 대응합니다.

<code>All</code>은 모든 ID 문자가 규칙을 만족하는지 확인하고, <code>Count</code>는 동시 결과의 성공/재사용 수를 셉니다. 상태 변경과 원자성은 LINQ에 숨기지 않습니다.

### 6. lambda와 <code>async/await</code>

<code>options =&gt; ...</code>는 설정 객체를 받아 값을 지정하는 lambda입니다. Application Service는 Repository Port를 비동기로 기다리고 HTTP 취소 신호를 끝까지 전달합니다. 취소를 업무 오류로 바꾸지 않습니다.

---

## 🛡 Data Protection을 이해하는 7단계

### 1. 암호화, 인증, replay 방지는 다르다

Data Protection은 payload를 쉽게 읽지 못하게 하고 발급 뒤 바뀌지 않았는지 확인합니다. 그러나 정상 토큰을 복사해 두 번 제출하는 replay는 암호학적으로 정상입니다. 서버 Repository가 사용 상태를 따로 기록해야 합니다.

### 2. purpose는 기능별 암호학적 격리 경계다

<code>IDataProtectionProvider.CreateProtector(purpose)</code>는 purpose별 subkey를 유도합니다. 같은 root key를 쓰더라도 초대용 토큰을 비밀번호 재설정용 protector가 열 수 없어야 합니다.

purpose는 비밀일 필요는 없지만 고유하고 안정적이어야 합니다. 이 예제는 <code>InvitationTokenApi.ProjectInvitation.v1</code>을 한곳에 고정합니다.

### 3. 최소 payload만 보호한다

payload에는 version, invitationId, projectId, role, expiresAtUtc만 넣습니다.

~~~text
InvitationTokenPayload
  → System.Text.Json UTF-8 bytes
  → IDataProtector.Protect
  → Base64Url token
~~~

Base64Url은 전송 가능한 문자열 표현이지 암호화가 아닙니다.

### 4. 만료 claim도 보호한다

이 예제는 <code>ExpiresAtUtc</code>를 보호된 payload와 Repository 양쪽에 기록하고 <code>IInvitationExpiryPolicy</code>로 검사합니다. 만료 시각을 바꾸면 무결성 검증이 실패합니다.

공식 API에는 절대 시각이나 상대 수명을 넣는 <code>ITimeLimitedDataProtector</code>도 있습니다. 이 샘플은 Application, Adapter, Repository가 같은 <code>TimeProvider</code> 기준과 오류 정책을 사용하도록 명시적 만료 claim을 선택했습니다. 어느 방식을 써도 replay 방지 Repository는 필요합니다.

### 5. 외부 실패 원인을 합친다

빈 값, 형식 오류, 한 글자 변조, 다른 purpose/key ring, JSON 구조·version 오류, 만료, 저장 기록 불일치는 모두 <code>invitation.token.invalid_or_expired</code>로 응답합니다. 운영 metric은 내부 원인을 구분할 수 있지만 HTTP detail에는 암호 예외나 key 정보를 넣지 않습니다.

### 6. key 수명과 토큰 수명을 구분한다

Data Protection key의 기본 90일 수명은 **키 생성·회전 주기**입니다. Member 24시간/Admin 15분이라는 **개별 토큰 유효기간**과 다릅니다. key를 삭제하면 그 key로 보호한 payload는 영구적으로 복구할 수 없습니다.

### 7. 다중 인스턴스는 두 공유 자원이 필요하다

여러 서버가 서로 발급한 토큰을 처리하려면 같은 key ring, application name, purpose/payload 계약과 공유 Repository가 모두 필요합니다. 저장 위치를 직접 지정하면 자동 at-rest 보호가 꺼질 수 있으므로 <code>ProtectKeysWith*</code>, 저장소 권한, rotation, backup을 함께 설계합니다.

---

## 🗺 구조도

### 계층과 의존성 방향

~~~mermaid
flowchart LR
    Client["API Client"] -->|"HTTP JSON"| Endpoint["Presentation<br/>InvitationEndpoints"]
    Endpoint -->|"Command / Result"| Service["Application<br/>InvitationApplicationService"]
    Service --> Domain["Domain<br/>ProjectInvitation · Result"]
    Service --> TokenPort["Security Port<br/>IInvitationTokenProtector"]
    Service --> RepositoryPort["Repository Port<br/>IInvitationRepository"]
    Service --> ExpiryPort["Strategy Port<br/>IInvitationExpiryPolicy"]

    TokenAdapter["Security Adapter<br/>DataProtectionInvitationTokenProtector"] -. implements .-> TokenPort
    RepositoryAdapter["Infrastructure Adapter<br/>InMemoryInvitationRepository"] -. implements .-> RepositoryPort
    ExpiryStrategy["Application Strategy<br/>RoleBasedInvitationExpiryPolicy"] -. implements .-> ExpiryPort
    TokenAdapter -->|"purpose별 파생 key"| KeyRing["Data Protection key ring"]

    Program["Composition Root<br/>Program.cs"] --> Service
    Program --> TokenAdapter
    Program --> RepositoryAdapter
    Program --> ExpiryStrategy

    classDef core fill:#dbeafe,stroke:#2563eb,color:#172554
    classDef boundary fill:#fef3c7,stroke:#d97706,color:#451a03
    classDef adapter fill:#dcfce7,stroke:#16a34a,color:#052e16
    classDef security fill:#ffe4e6,stroke:#e11d48,color:#4c0519
    class Domain,Service,TokenPort,RepositoryPort,ExpiryPort core
    class Client,Endpoint,Program boundary
    class RepositoryAdapter,ExpiryStrategy adapter
    class TokenAdapter,KeyRing security
~~~

Domain과 Application은 ASP.NET Core Data Protection 구현을 직접 참조하지 않습니다. Composition Root만 concrete Adapter와 lifetime을 고릅니다.

### 토큰 발급과 첫 사용

~~~mermaid
sequenceDiagram
    autonumber
    actor Client
    participant EP as InvitationEndpoints
    participant AS as InvitationApplicationService
    participant D as ProjectInvitation
    participant XP as Expiry Strategy
    participant TP as Token Protector
    participant R as Repository

    Client->>EP: POST /invitations + JSON
    EP->>AS: CreateAsync(command, token)
    AS->>XP: CalculateExpiration(role, now)
    XP-->>AS: expiresAtUtc
    AS->>D: Create(nullable inputs, times)
    D-->>AS: Result
    AS->>R: AddAsync(invitation)
    R-->>AS: Added / Duplicate
    AS->>TP: Protect(payload)
    TP-->>AS: opaque token
    EP-->>Client: 201 + Location + token

    Client->>EP: POST /invitations/redeem
    EP->>AS: RedeemAsync(token)
    AS->>TP: Unprotect(token, now)
    TP-->>AS: payload / safe failure
    AS->>R: GetByIdAsync(id)
    R-->>AS: current snapshot
    AS->>R: RedeemAsync(id, now)
    R-->>AS: Redeemed / AlreadyRedeemed / Expired
    EP-->>Client: 200 또는 Problem Details
~~~

### 초대 상태

~~~mermaid
stateDiagram-v2
    [*] --> Issued: 201 Created
    Issued --> Redeemed: 첫 유효 redeem
    Issued --> Expired: now >= expiresAtUtc
    Redeemed --> ReplayRejected: 같은 token 재사용
    InvalidToken --> Rejected: 형식·변조·purpose 실패
    Expired --> Rejected: invalid_or_expired
    ReplayRejected --> [*]: 409 Conflict
    Rejected --> [*]: 400 Bad Request
    Redeemed --> [*]: 200 OK
~~~

브라우저에서 검색·확대·light/dark 전환이 가능한 별도 구조도도 제공합니다.

- [독립형 아키텍처 뷰어](./architecture.html)
- [Archify 원본 명세](./architecture.json)

Archify 명세는 showcase artifact 검사 9/9, 구성 오류 0, 경고 0을 통과했습니다. 고정 Viewer UI는 영어지만 노드와 설명은 한국어입니다.

---

## 🧩 패턴과 설계 의도 (Why)

### Nullable 안전성과 불변성

- HTTP 경계는 <code>string?</code>로 누락 가능성을 숨기지 않습니다.
- Domain factory가 공백, 길이, 문자, 이메일, 역할을 검증합니다.
- private 생성자와 <code>private init</code>이 검증 우회를 막습니다.
- 상태는 <code>RedeemedAtUtc</code>에서 파생해 status와 timestamp가 어긋나지 않게 합니다.

이메일 검증은 학습용 최소 규칙입니다. 실제 서비스에서는 전달 가능성, 국제화, 확인 절차를 별도 정책으로 다룹니다.

### Application Service와 Result

~~~text
발급: 역할 파싱 → now 1회 읽기 → 만료 계산 → Domain 생성 → 저장 → 토큰 보호
사용: now 1회 읽기 → 토큰 검증 → 현재 기록 대조 → Repository 원자 전이
~~~

Application Service는 암호 format, HTTP status, dictionary lock을 모릅니다. 입력 오류·중복·replay는 Result, 잘못된 구성·미처리 상태는 예외, 요청 종료는 취소로 구분합니다.

### Port/Adapter, SOLID, DI

- **SRP**: HTTP, 유스케이스, Domain, 만료, 암호화, 저장을 나눕니다.
- **DIP**: Application은 concrete class가 아니라 Port interface를 봅니다.
- **OCP**: 운영 DB, 다른 만료 정책, 다른 token Adapter로 교체할 수 있습니다.
- **Composition Root**: <code>Program.cs</code>만 구현, lifetime, purpose, 수명을 조립합니다.

### 원자성과 테스트 용이성

- 메모리 Repository의 <code>lock</code>은 확인과 snapshot 교체를 한 임계 구역으로 묶습니다.
- 32개 전용 <code>LongRunning</code> 작업을 <code>Barrier</code>에 모은 뒤 동시에 출발시켜 실제 경쟁을 만들고 정확히 1개 성공, 31개 replay를 검증합니다.
- 실제 DB에서는 조건부 UPDATE와 영향 행 수 확인이 필요합니다.
- <code>TimeProvider</code>로 sleep 없는 만료 테스트를 만듭니다.
- 테스트는 <code>EphemeralDataProtectionProvider</code>를 사용해 실제 codec을 검증하면서 key를 남기지 않습니다.

### 운영 전 보완

- endpoint 인증·프로젝트별 인가·감사
- durable DB, unique constraint, 조건부 UPDATE, transaction
- 공유·보호된 key ring, application name, backup과 rotation
- token redaction과 개인정보 보존 정책
- HTTP 요청 본문 제한과 Adapter의 4KB 토큰 길이 상한 조정
- rate limit과 abuse 탐지
- 발급 저장 뒤 보호 실패 시 transaction 또는 보상
- purpose/payload version migration

---

## 🧭 파일 내비게이션 맵

> <code>generate-readme-map</code> 규칙에 따라 역할별로 분류하고 실제 존재하는 상대 경로만 연결했습니다.

### 🚪 진입점과 빌드

| 문서 / 파일 | 설명 |
| --- | --- |
| [<code>InvitationTokenApi.csproj</code>](./src/InvitationTokenApi/InvitationTokenApi.csproj) | <code>net10.0</code>, C# 14, nullable, warning-as-error |
| [<code>Program.cs</code>](./src/InvitationTokenApi/Program.cs) | Composition Root, Data Protection, DI, Problem Details |
| [<code>.gitignore</code>](./.gitignore) | 오늘 프로젝트의 <code>bin/</code>, <code>obj/</code> 제외 |

### 🧠 Domain과 Application

| 문서 / 파일 | 설명 |
| --- | --- |
| [<code>Error.cs</code>](./src/InvitationTokenApi/Domain/Error.cs) | 안정 오류 코드와 관련 필드 |
| [<code>Result.cs</code>](./src/InvitationTokenApi/Domain/Result.cs) | 성공 값 또는 예상 오류 generic |
| [<code>ProjectInvitation.cs</code>](./src/InvitationTokenApi/Domain/ProjectInvitation.cs) | enum, factory, 정규화, 불변 전이 |
| [<code>InvitationCommands.cs</code>](./src/InvitationTokenApi/Application/InvitationCommands.cs) | use case 입력과 보호 payload |
| [<code>InvitationApplicationService.cs</code>](./src/InvitationTokenApi/Application/InvitationApplicationService.cs) | 발급·조회·사용 유스케이스 |
| [<code>RoleBasedInvitationExpiryPolicy.cs</code>](./src/InvitationTokenApi/Application/RoleBasedInvitationExpiryPolicy.cs) | 역할별 만료 Strategy |

### 🔌 Port와 Adapter

| 문서 / 파일 | 설명 |
| --- | --- |
| [<code>IInvitationRepository.cs</code>](./src/InvitationTokenApi/Application/Ports/IInvitationRepository.cs) | 저장·조회·원자 사용 Port |
| [<code>IInvitationTokenProtector.cs</code>](./src/InvitationTokenApi/Application/Ports/IInvitationTokenProtector.cs) | 토큰 보호·검증 Port |
| [<code>IInvitationExpiryPolicy.cs</code>](./src/InvitationTokenApi/Application/Ports/IInvitationExpiryPolicy.cs) | 수명 Strategy Port |
| [<code>DataProtectionInvitationTokenProtector.cs</code>](./src/InvitationTokenApi/Security/DataProtectionInvitationTokenProtector.cs) | purpose, JSON, Base64Url, Data Protection |
| [<code>InMemoryInvitationRepository.cs</code>](./src/InvitationTokenApi/Infrastructure/InMemoryInvitationRepository.cs) | <code>lock</code> 기반 저장 Adapter |

### 🌐 HTTP와 검증

| 문서 / 파일 | 설명 |
| --- | --- |
| [<code>InvitationContracts.cs</code>](./src/InvitationTokenApi/Presentation/InvitationContracts.cs) | 발급·조회·사용 DTO |
| [<code>InvitationEndpoints.cs</code>](./src/InvitationTokenApi/Presentation/InvitationEndpoints.cs) | HTTP 상태와 Problem Details |
| [<code>AdjustableTimeProvider.cs</code>](./src/InvitationTokenApi/SelfTesting/AdjustableTimeProvider.cs) | 결정적 테스트 시계 |
| [<code>SelfTestRunner.cs</code>](./src/InvitationTokenApi/SelfTesting/SelfTestRunner.cs) | Domain·codec·Application·동시성 38개 assertion |
| [<code>verify-http.ps1</code>](./verify-http.ps1) | Production Kestrel 128개 assertion |
| [<code>EXERCISES.md</code>](./EXERCISES.md) | Level 0~9 실습 |
| [<code>CHECKPOINT.md</code>](./CHECKPOINT.md) | 26개 문답과 복습 체크 |
| [<code>architecture.html</code>](./architecture.html) | 대화형 구조도 |
| [<code>architecture.json</code>](./architecture.json) | Archify 원본 명세 |

---

## ▶ 빌드와 실행

모든 명령은 <code>dailyStudy/exercise/20261014</code>에서 실행합니다.

### 1. 복원과 Release 빌드

~~~powershell
dotnet restore .\src\InvitationTokenApi\InvitationTokenApi.csproj
dotnet build .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-restore
~~~

기대 결과: 경고 0개, 오류 0개. 외부 NuGet package 없이 .NET 10 shared framework만 사용합니다.

### 2. 자체 테스트

~~~powershell
dotnet run --project .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-build -- --self-test
~~~

~~~text
SELF-TEST PASSED: 38 assertions
~~~

### 3. 별도 Production HTTP 검증

~~~powershell
.\verify-http.ps1
~~~

스크립트는 Release assembly 신선도를 확인하고 Kestrel port <code>0</code>으로 실제 HTTP를 검증한 뒤 자신이 시작한 정확한 PID와 임시 로그만 정리합니다.

~~~text
HTTP VERIFY PASSED: 128 assertions
~~~

### 4. 직접 서버 실행

~~~powershell
dotnet run --project .\src\InvitationTokenApi\InvitationTokenApi.csproj -c Release --no-build -- --urls http://127.0.0.1:51414 --environment Development
~~~

다른 terminal에서 발급합니다.

~~~powershell
$body = @{ invitationId = 'manual-1'; projectId = 'csharp-study'; email = 'learner@example.com'; role = 'Member' } | ConvertTo-Json
$created = Invoke-RestMethod -Method Post -Uri http://127.0.0.1:51414/invitations -ContentType 'application/json' -Body $body
$created
~~~

토큰을 shell history나 공유 로그에 붙여 넣지 마세요.

### 5. format 검증

~~~powershell
dotnet format .\src\InvitationTokenApi\InvitationTokenApi.csproj --verify-no-changes --no-restore
~~~

---

## ✅ 초보자 이해도 검증 단계 (Validation stage)

### 1단계 — 실행 전 예측

1. 토큰 한 글자를 바꾸면 어느 계층에서 실패할까요?
2. 정상 토큰을 두 번 보내면 Data Protection은 두 번째 요청도 해제할 수 있을까요?
3. 두 번째 성공을 막는 최종 책임은 어디에 있을까요?
4. Admin 토큰 수명을 정하는 파일은 무엇일까요?
5. key 기본 90일 수명과 Admin 토큰 15분 수명은 같은 값일까요?
6. 다른 서버가 토큰을 열려면 DB만 공유하면 충분할까요?

### 2단계 — 코드 위치 찾기

- nullable 입력을 non-null Domain으로 바꾸는 factory
- role enum 허용 목록
- purpose 문자열 생성 위치
- 암호 예외를 Result로 바꾸는 Adapter 경계
- payload와 Repository를 대조하는 Application 흐름
- 확인과 상태 교체를 같은 <code>lock</code>에 둔 원자 연산
- 모든 경쟁 작업이 준비된 뒤 시작시키는 test gate

### 3단계 — 자동 검증

자체 테스트 38개와 HTTP 검증 128개를 실행하고 assertion을 Domain / Strategy / Security Adapter / Application / Repository / HTTP로 분류하세요.

### 4단계 — 작은 변경

purpose 끝의 <code>v1</code>을 <code>v2</code>로 잠시 바꿔 기존 토큰이 왜 열리지 않는지 관찰하세요. 원래 값으로 되돌린 뒤 모든 검증을 다시 통과시키세요.

### 5단계 — 말로 설명

> “Data Protection은 payload의 ___과 ___을 제공하지만 replay를 막지는 않는다. 토큰 종류는 ___로 격리하고 시간은 ___로 주입한다. 정확히 한 번의 사용은 ___의 원자적 상태 전이가 보장한다. 여러 서버에서는 DB뿐 아니라 ___도 공유해야 한다.”

정답과 해설은 [<code>CHECKPOINT.md</code>](./CHECKPOINT.md)에 있습니다.

---

## 🔁 간결한 복습 체크리스트

- [ ] Data Protection의 기밀성·무결성과 replay 방지를 구분한다.
- [ ] 불투명 토큰도 bearer secret이라는 점을 안다.
- [ ] purpose가 같은 key ring 안의 소비자를 격리하는 이유를 설명한다.
- [ ] Base64Url을 암호화와 혼동하지 않는다.
- [ ] 보호된 만료 claim과 key rotation 수명을 구분한다.
- [ ] nullable 경계와 non-null Domain을 설명한다.
- [ ] <code>record</code>, <code>with</code>, pattern matching, switch expression을 찾는다.
- [ ] Result, 예외, 취소의 기준을 설명한다.
- [ ] TimeProvider를 주입한 이유를 안다.
- [ ] 세 Port의 책임을 구분한다.
- [ ] 원자적 1회 사용이 Repository 책임임을 안다.
- [ ] 한 프로세스 <code>lock</code>과 DB 조건부 UPDATE의 범위를 구분한다.
- [ ] 다중 인스턴스에서 key ring과 Repository를 각각 공유해야 함을 안다.
- [ ] 201·200·400·404·409·415 계약을 설명한다.
- [ ] Preview 기능을 stable 실행 코드와 분리한다.

---

## 📚 버전과 공식 출처

### 2026-10-14 확인 결과

| 구분 | 현재 공식 정보 | 이 예제의 선택 |
| --- | --- | --- |
| Stable .NET | .NET 10 LTS, servicing release 10.0.13 | <code>net10.0</code> |
| Stable SDK/runtime | SDK 10.0.402, 병렬 1xx band 10.0.113, Runtime/ASP.NET Core 10.0.13 | 공식 최신 상태 문서화 |
| Stable C# | C# 14 | <code>&lt;LangVersion&gt;14.0&lt;/LangVersion&gt;</code> |
| 설치된 Stable SDK/runtime | SDK 10.0.301, runtime/ASP.NET Core 10.0.9 | 실제 빌드·실행 검증 |
| Pre-release .NET | .NET 11 RC2, SDK 11.0.100-rc.2.26504.105, runtime 11.0.0-rc.2.26504.105 | 설명만 제공 |
| Preview C# | C# 15 Preview | 설명만 제공 |

최신 안정 조합은 .NET 10 LTS와 C# 14입니다. .NET 11 RC2는 go-live 지원 RC이지만 GA 안정판이 아닙니다. 실행 코드는 설치된 Stable SDK 10.0.301로 컴파일하며 C# 15 Preview나 .NET 11 API에 의존하지 않습니다.

### 버전과 언어

> 🔗 [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

> 🔗 [.NET 10 공식 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)

> 🔗 [.NET 2026년 10월 servicing update](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-october-2026-servicing-updates/)

> 🔗 [C# 14의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)

> 🔗 [C# 언어 버전과 target framework 대응](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning)

> 🔗 [.NET 11 RC2 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-rc-2/)

> 🔗 [.NET 11 공식 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json)

> 🔗 [C# 15의 새로운 기능 — Preview](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

### Data Protection, 시간, DI

> 🔗 [ASP.NET Core Data Protection 개요](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0)

> 🔗 [Data Protection 소비자 API](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/overview?view=aspnetcore-10.0)

> 🔗 [Purpose 문자열과 소비자 격리](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings?view=aspnetcore-10.0)

> 🔗 [시간 제한 payload와 ITimeLimitedDataProtector](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/limited-lifetime-payloads?view=aspnetcore-10.0)

> 🔗 [Data Protection key 관리와 기본 90일 수명](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0)

> 🔗 [Key 저장소, application name, key at rest 보호](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)

> 🔗 [TimeProvider 개요](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview)

> 🔗 [ASP.NET Core 의존성 주입](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0)

Stable 실행 코드와 Preview 설명은 분리되어 있으며 Data Protection key나 실제 발급 토큰은 저장소에 포함하지 않습니다.
