# 초보자 이해도 체크포인트 — 초대 토큰과 원자적 1회 사용

먼저 답을 가리고 자신의 말로 설명하세요. 정답 문장을 외우는 것보다 코드에서 근거 위치를 찾는 것이 중요합니다.

---

## 1. Data Protection은 이 예제에서 무엇을 보장하나요?

<details>
<summary>정답과 해설</summary>

보호한 payload의 내용을 쉽게 읽지 못하게 하는 기밀성과, 발급 뒤 한 글자라도 바뀌었는지 확인하는 무결성을 제공합니다. 같은 key ring과 purpose를 가진 protector만 정상적으로 해제할 수 있습니다.

하지만 사용자의 신원을 인증하거나, 초대를 자동으로 한 번만 쓰게 하거나, 데이터베이스에 초대를 영구 저장하지는 않습니다. 이 예제에서 1회 사용은 Repository의 원자적 상태 전이가 따로 보장합니다.

</details>

---

## 2. “불투명 토큰”은 무슨 뜻인가요?

<details>
<summary>정답과 해설</summary>

클라이언트가 내부 필드와 규칙에 의존하지 않고 하나의 의미 없는 문자열처럼 보관하고 되돌려 보내야 한다는 뜻입니다. invitationId, projectId, role, 만료 시각이 보호된 payload 안에 있어도 클라이언트가 이를 파싱해 권한을 판단해서는 안 됩니다.

불투명하다고 공개해도 되는 것은 아닙니다. 토큰을 가진 사람이 초대를 사용할 수 있으므로 bearer secret처럼 다루고, 로그·URL query·분석 도구·오류 메시지에 남기지 않아야 합니다.

</details>

---

## 3. purpose 문자열은 왜 필요한가요?

<details>
<summary>정답과 해설</summary>

같은 애플리케이션과 key ring을 사용하더라도 “초대 수락”, “비밀번호 재설정”, “이메일 변경” 토큰을 서로 바꿔 쓰지 못하게 암호학적으로 격리합니다.

초대용 purpose로 보호한 값은 비밀번호 재설정용 protector가 해제할 수 없어야 합니다. 따라서 purpose는 설명용 label이 아니라 보안 계약입니다. purpose를 무계획하게 바꾸면 아직 유효한 기존 토큰도 모두 해제하지 못할 수 있습니다.

</details>

---

## 4. 변조, purpose 불일치, 만료를 왜 같은 오류로 반환하나요?

<details>
<summary>정답과 해설</summary>

외부 응답은 모두 다음 계약으로 합칩니다.

~~~text
HTTP 400
code  = invitation.token.invalid_or_expired
field = token
~~~

공격자가 “암호 형식은 맞지만 만료됨”, “key는 맞지만 purpose가 다름”, “실제 invitationId가 존재하지 않음”을 구별하면 시스템 내부 상태를 탐색하는 단서가 됩니다. 서버 내부 관측에는 원인을 분류할 수 있지만, 응답에는 안전하고 일관된 설명만 보냅니다.

</details>

---

## 5. 토큰을 성공적으로 해제했으면 왜 Repository를 다시 조회하나요?

<details>
<summary>정답과 해설</summary>

보호된 payload는 “서버가 과거에 이 값을 발급했다”는 증거이지 현재 상태의 원본은 아닙니다. 발급 뒤 초대가 취소되거나, 이미 사용되거나, 저장 기록과 정책이 달라질 수 있습니다.

Application Service는 payload의 invitationId로 현재 초대를 찾고, projectId·role·expiresAtUtc가 발급 기록과 같은지 대조합니다. 그 뒤 Repository의 원자적 교환 연산이 최종 상태를 결정합니다.

</details>

---

## 6. 토큰 자체만으로 한 번만 사용할 수 있게 만들 수 있나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 동일한 보호 토큰 문자열은 만료 전까지 여러 번 제출할 수 있습니다. 암호화와 서명은 replay를 자동으로 막지 않습니다.

한 번만 사용하려면 서버가 invitationId별 사용 상태를 보관하고, “아직 사용 전인지 확인”과 “사용 완료 시각 기록”을 하나의 원자적 연산으로 수행해야 합니다.

</details>

---

## 7. 조회한 뒤 저장하는 두 단계 코드가 왜 위험한가요?

<details>
<summary>정답과 해설</summary>

두 요청이 동시에 “아직 사용 전”을 읽은 뒤 각각 성공으로 저장할 수 있기 때문입니다. 이를 check-then-act 경쟁 조건이라고 합니다.

메모리 Adapter는 같은 임계 구역 안에서 검사와 상태 전이를 묶어야 합니다. 실제 DB에서는 조건부 UPDATE, unique constraint, transaction 같은 저장소 수준의 원자성이 최종 보루입니다. Application Service에서 먼저 GET하는 것만으로는 보장되지 않습니다.

</details>

---

## 8. 동시에 같은 토큰을 32번 보내면 어떤 결과가 맞나요?

<details>
<summary>정답과 해설</summary>

정확히 하나만 성공하고 나머지 31개는 <code>invitation.already_redeemed</code>여야 합니다. “하나 이상 성공했다” 또는 “마지막 상태가 Redeemed다”만 확인하면 여러 요청이 동시에 성공한 결함을 놓칠 수 있습니다.

경쟁 테스트는 모든 작업의 결과를 모아 성공 수와 AlreadyRedeemed 수를 각각 확인해야 합니다.

</details>

---

## 9. <code>record</code>와 <code>with</code>를 사용한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

<code>ProjectInvitation</code>은 특정 시점의 상태 snapshot입니다. <code>record</code>는 값 중심 비교와 불변 모델에 적합하고, <code>with</code>는 원본을 제자리에서 바꾸지 않고 <code>RedeemedAtUtc</code>만 다른 새 snapshot을 만듭니다.

불변성만으로 동시성 문제가 자동 해결되지는 않습니다. 두 snapshot 중 어느 것을 저장할지 결정하는 Repository 연산은 여전히 원자적이어야 합니다.

</details>

---

## 10. 외부 입력에는 왜 nullable 문자열을 사용하고, Domain 안쪽에서는 non-null 문자열을 사용하나요?

<details>
<summary>정답과 해설</summary>

JSON 필드는 누락되거나 null일 수 있으므로 요청 DTO와 Application 명령은 <code>string?</code>로 현실을 표현합니다. Domain factory가 누락, 길이, 허용 문자, 이메일 모양을 확인하고 정규화한 뒤에만 non-null 문자열을 가진 <code>ProjectInvitation</code>을 만듭니다.

이렇게 경계에서 불확실성을 처리하면 핵심 로직의 모든 줄에서 null을 반복 검사하지 않아도 됩니다. null-forgiving 연산자 <code>!</code>는 앞선 성공 검사로 안전함을 증명한 뒤 분석기에 알리는 표식이지 runtime null 검사를 대신하지 않습니다.

</details>

---

## 11. <code>Enum.TryParse</code>가 성공하면 role 검증이 끝난 것인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. enum의 기반 숫자에 해당하는 문자열이 이름으로 정의되지 않았어도 파싱될 수 있습니다. 그래서 파싱 성공 뒤 값이 <code>Member</code> 또는 <code>Admin</code>이라는 허용 목록에 실제로 속하는지 pattern matching으로 다시 확인합니다.

보안과 권한 입력에는 “파싱 가능한가”보다 “명시적으로 허용했는가”가 중요합니다.

</details>

---

## 12. 만료 경계에서 <code>nowUtc == expiresAtUtc</code>이면 사용할 수 있나요?

<details>
<summary>정답과 해설</summary>

사용할 수 없습니다. 이 예제의 경계는 <code>nowUtc >= expiresAtUtc</code>이면 만료입니다.

경계 규칙을 코드, 토큰 검증, Repository 상태 전이, 테스트에서 동일하게 사용해야 합니다. 한곳은 초과, 다른 곳은 이상으로 비교하면 정확한 만료 순간에 결과가 달라질 수 있습니다.

</details>

---

## 13. <code>TimeProvider</code>를 주입하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

Production에서는 실제 UTC 시각을 사용하면서 테스트에서는 원하는 시각으로 이동하기 위해서입니다. 실제로 몇 분을 기다리는 만료 테스트는 느리고 불안정하지만, 테스트 시계를 쓰면 만료 직전·정확한 경계·직후를 즉시 재현할 수 있습니다.

한 요청 안에서는 현재 시각을 한 번 읽어 토큰 검사와 상태 전이에 같은 값을 전달합니다. 여러 번 읽으면 만료 경계에서 앞 검사는 성공하고 뒤 저장은 실패하는 등 설명하기 어려운 흔들림이 생길 수 있습니다.

</details>

---

## 14. Result, 예외, 취소는 어떻게 구분하나요?

<details>
<summary>정답과 해설</summary>

- <code>Result&lt;T&gt;</code>: 잘못된 입력, 중복 ID, 없는 초대, 이미 사용한 토큰처럼 호출자가 예상하고 분기할 수 있는 결과
- 예외: 잘못된 Strategy 수명, 처리하지 않은 enum case, 손상된 내부 상태처럼 프로그래머 계약 위반이나 예상하지 못한 장애
- 취소: HTTP 연결 종료처럼 호출자가 결과를 더 기다리지 않는 별도 제어 흐름

사용자가 고칠 수 있는 검증 실패를 모두 예외로 만들거나, 서버 버그를 400 Result로 숨기면 문제의 성격과 관측 가능성이 흐려집니다.

</details>

---

## 15. Token Protector를 Port로 감싼 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

Application Service가 <code>IDataProtector</code>, JSON 직렬화, Base64Url 형식, 암호 예외 같은 framework 세부사항을 몰라도 “payload를 보호한다/검증한다”는 업무 수준 동작만 사용하게 합니다.

Infrastructure 또는 Security Adapter가 ASP.NET Core Data Protection을 구현하고, Composition Root가 이를 연결합니다. 덕분에 Application 테스트는 작은 fake를 사용할 수 있고 보안 구현의 예외를 안전한 Result로 바꾸는 경계도 한곳에 모입니다.

</details>

---

## 16. Strategy와 DI는 이 예제에서 각각 어떤 역할인가요?

<details>
<summary>정답과 해설</summary>

<code>IInvitationExpiryPolicy</code> Strategy는 역할별 토큰 수명과 만료 경계를 정합니다. Application Service는 구체 수치나 조건문을 직접 소유하지 않고 정책을 호출합니다.

DI는 Repository, Token Protector, Expiry Policy, TimeProvider를 Application Service에 전달합니다. <code>Program.cs</code>는 어떤 구현과 lifetime을 사용할지 한곳에서 결정하는 Composition Root입니다. DI container를 쓴다는 사실만으로 책임 분리가 자동으로 생기지는 않습니다.

</details>

---

## 17. 메모리 Repository의 lock은 무엇을 보장하고 무엇을 보장하지 않나요?

<details>
<summary>정답과 해설</summary>

현재 프로세스 안에서 같은 invitationId의 중복 추가와 교환 경쟁을 직렬화해 정확히 한 요청만 상태 전이에 성공하게 합니다.

프로세스 재시작 뒤 내구성, 여러 서버 인스턴스 사이 동시성, 데이터베이스 transaction, 재해 복구는 보장하지 않습니다. 운영 DB에서는 조건부 UPDATE와 transaction을 사용하고, 여러 인스턴스가 같은 저장소를 바라보게 해야 합니다.

</details>

---

## 18. 여러 서버가 같은 Repository를 사용하면 Data Protection 토큰도 자동으로 공유되나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 저장소 상태와 key ring은 별개의 공유 자원입니다.

여러 인스턴스가 서로 발급한 토큰을 검증하려면 같은 key ring, application name, purpose 계약을 사용해야 합니다. 반대로 key ring만 공유하고 Repository가 각 인스턴스 메모리에 따로 있으면 replay 방지와 상태 조회가 일관되지 않습니다.

</details>

---

## 19. key ring을 컨테이너의 임시 파일 시스템에만 두면 어떤 문제가 생기나요?

<details>
<summary>정답과 해설</summary>

컨테이너가 재시작되어 key를 잃으면 재시작 전 발급한 아직 유효한 토큰도 해제하지 못합니다. 사용자는 모두 같은 invalid-or-expired 응답을 받겠지만, 실제 원인은 배포 설정입니다.

운영에서는 여러 인스턴스가 접근할 내구성 있는 key 저장소, key at rest 보호, 접근 권한, 회전과 보존 기간을 설계해야 합니다. key 파일을 소스 저장소에 커밋하면 안 됩니다.

</details>

---

## 20. 왜 발급 응답, GET 응답, 교환 응답의 필드가 서로 다른가요?

<details>
<summary>정답과 해설</summary>

- 발급 응답: 클라이언트에게 처음 전달해야 하므로 token과 expiresAtUtc를 포함합니다.
- GET 응답: 상태 확인에 필요한 email·status·만료·사용 시각을 포함하지만 bearer secret인 token은 다시 노출하지 않습니다.
- 교환 응답: 권한 부여 결과에 필요한 invitationId·projectId·role·redeemedAtUtc만 반환하고 token과 email을 제외합니다.

응답마다 필요한 최소 정보만 공개하면 로그, 브라우저 확장, 중간 계층에 비밀이나 개인정보가 남을 표면을 줄일 수 있습니다. 실제 GET endpoint에는 별도의 인증·인가 정책도 필요합니다.

</details>

---

## 21. GET에서 형식이 잘못된 ID와 존재하지 않는 ID를 모두 404로 다루는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

조회 호출자에게 특정 ID 규칙이나 존재 여부를 필요 이상으로 구별해 주지 않는 정책입니다. 두 경우 모두 <code>invitation.not_found</code>로 응답하면 조회 oracle을 줄이고 계약도 단순해집니다.

반면 POST 발급 입력은 사용자가 바로 고쳐야 하므로 잘못된 invitationId를 400 <code>invitation.id.invalid</code>로 구체적으로 알려 줍니다. 같은 문자열이라도 use case 경계가 다르면 공개할 오류 정책도 달라질 수 있습니다.

</details>

---

## 22. 각 HTTP 상태 코드는 무엇을 뜻하나요?

<details>
<summary>정답과 해설</summary>

| 상태 | 의미 |
| ---: | --- |
| 201 | 새 초대를 만들었고 Location이 조회 URL을 가리킴 |
| 200 | 초대 조회 또는 토큰 교환 성공 |
| 400 | 입력 검증 실패 또는 invalid-or-expired 토큰 |
| 404 | 조회할 초대를 공개 계약상 찾을 수 없음 |
| 409 | 같은 invitationId 중복 또는 이미 사용한 토큰 |
| 415 | 요청 body의 media type을 지원하지 않음 |

409 재사용은 토큰이 암호학적으로 잘못된 것이 아니라 유효했던 초대의 현재 서버 상태와 충돌한다는 뜻입니다.

</details>

---

## 23. 415 응답의 framework 오류 code까지 고정하지 않는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

415는 endpoint의 Application Service가 만든 업무 오류가 아니라 ASP.NET Core가 handler 실행 전에 만들 수 있는 framework 오류입니다. 이 자료의 핵심 외부 계약은 HTTP 415와 <code>application/problem+json</code>입니다.

Application 오류는 <code>code</code>, <code>field</code>, <code>traceId</code>까지 명시적으로 검증하지만, framework가 관리하는 세부 확장 필드까지 불필요하게 고정하면 framework servicing 변화에 취약한 테스트가 됩니다.

</details>

---

## 24. Data Protection을 JWT, 비밀번호 해싱, 일반 장기 암호화 대신 사용해도 되나요?

<details>
<summary>정답과 해설</summary>

용도가 다릅니다.

- Data Protection: 같은 애플리케이션 계열이 발급하고 소비하는 짧은 수명의 보호 payload
- JWT 같은 표준 토큰: 명시적인 issuer, audience, 서명 검증, 다른 시스템과의 상호 운용 계약
- 비밀번호 해싱: 복호화하면 안 되는 비밀번호를 salt와 느린 hash로 검증
- 장기 데이터 암호화: 별도 key 관리, 접근 통제, rotation, 감사 정책이 필요한 저장 데이터

이 예제의 보호 토큰 설계를 다른 문제에 그대로 복사하면 안 됩니다.

</details>

---

## 25. black-box 검증이 왜 별도 Production Kestrel을 실행하나요?

<details>
<summary>정답과 해설</summary>

메서드를 직접 호출하는 자체 테스트만으로는 실제 JSON binding, content type, Location header, middleware, Problem Details, Production 환경 조립을 모두 확인할 수 없습니다.

<code>verify-http.ps1</code>은 운영체제가 선택한 동적 loopback 포트에서 Release 실행 파일을 별도 프로세스로 실행합니다. 종료할 때 프로세스 이름 전체가 아니라 자신이 시작한 PID만 멈춰 사용자의 다른 서버를 보호합니다.

</details>

---

## 26. 코드 읽기 문장의 빈칸을 채워 보세요

> “Data Protection은 payload의 ______과 ______을 제공하지만 replay를 막지는 않는다. 토큰 종류는 ______로 격리하고, 시간은 ______로 주입한다. 정확히 한 번의 사용은 ______의 원자적 상태 전이가 보장한다. 여러 서버에서는 Repository뿐 아니라 ______도 공유해야 한다.”

<details>
<summary>정답과 해설</summary>

> “Data Protection은 payload의 **기밀성**과 **무결성**을 제공하지만 replay를 막지는 않는다. 토큰 종류는 **purpose**로 격리하고, 시간은 **TimeProvider**로 주입한다. 정확히 한 번의 사용은 **Repository**의 원자적 상태 전이가 보장한다. 여러 서버에서는 Repository뿐 아니라 **Data Protection key ring과 application name**도 공유해야 한다.”

</details>

---

## 간결한 복습 체크리스트

- [ ] Data Protection의 보장과 비보장을 구분한다.
- [ ] 토큰을 bearer secret으로 취급하고 로그·URL에 남기지 않는다.
- [ ] purpose가 토큰 use case를 격리하는 이유를 설명한다.
- [ ] 변조·purpose 불일치·만료를 같은 외부 오류로 합치는 이유를 안다.
- [ ] 토큰 payload와 현재 Repository 상태를 다시 대조하는 이유를 안다.
- [ ] 1회 사용은 원자적 저장 상태 전이가 보장함을 안다.
- [ ] nullable 경계와 non-null Domain을 구분한다.
- [ ] record, with, pattern matching, switch expression을 코드에서 찾는다.
- [ ] Result, 예외, 취소의 사용 기준을 설명한다.
- [ ] TimeProvider로 만료 경계를 결정적으로 테스트한다.
- [ ] 메모리 lock과 DB 조건부 UPDATE의 보장 범위를 구분한다.
- [ ] 여러 인스턴스에서 key ring과 Repository를 각각 공유해야 함을 안다.
- [ ] 발급·조회·교환 응답에서 공개 필드가 다른 이유를 설명한다.
- [ ] 201·200·400·404·409·415의 의미를 설명한다.
- [ ] 자체 테스트와 실제 Production HTTP 검증의 차이를 안다.
