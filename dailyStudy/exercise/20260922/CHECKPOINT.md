# 초보자 이해도 체크포인트

먼저 코드를 보지 않고 답하세요. 각 항목의 `<details>`를 펼쳐 자신의 설명과 비교합니다. **9개 중 7개 이상을 자기 말로 설명하면 통과**입니다.

## 1. 인증과 인가는 어떻게 다른가요?

<details>
<summary>정답 보기</summary>

인증(Authentication)은 요청자가 누구인지 확인해 `ClaimsPrincipal`을 만드는 과정입니다. 인가(Authorization)는 확인된 사용자가 특정 endpoint나 문서에 접근해도 되는지 판단하는 과정입니다. 신원을 안다고 모든 작업이 허용되는 것은 아닙니다.

</details>

## 2. Alice가 문서를 만들 수 있고 Bob은 만들 수 없는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

`DocumentCreator` policy는 **인증됨 AND `NameIdentifier` claim 있음 AND (Editor OR Admin 역할)**을 요구합니다. 여러 requirement는 AND이고 `RequireRole`에 나열한 두 역할만 OR입니다. Alice는 세 조건을 모두 만족하지만 Bob은 Viewer이므로 403으로 거절됩니다.

</details>

## 3. 인증 정보가 없는 요청의 401과 Bob의 생성 요청 403은 무엇이 다른가요?

<details>
<summary>정답 보기</summary>

401은 유효한 신원을 만들지 못해 authentication challenge가 발생한 경우입니다. 403은 Bob이라는 신원은 확인했지만 Creator 정책을 만족하지 못해 authorization forbid가 발생한 경우입니다.

</details>

## 4. 문서 읽기 권한을 endpoint policy 하나로 끝내지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

정책 middleware가 endpoint 실행 전에 평가될 때는 아직 `{id}`에 해당하는 문서를 읽지 않았으므로 `OwnerId`를 모릅니다. 문서를 불러온 뒤 사용자와 실제 resource를 함께 `IAuthorizationService`에 전달해야 owner 또는 Admin 규칙을 판단할 수 있습니다.

</details>

## 5. 권한이 없는 Bob의 문서 조회를 403이 아니라 404로 반환한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

Bob이 ID를 바꾸어 요청하면서 어떤 문서가 존재하는지 열거하는 정보를 줄이기 위한 선택입니다. 다만 404가 항상 정답은 아니며, 제품의 위협 모델과 API 계약에 맞게 응답 모양·시간·로그까지 일관되게 설계해야 합니다.

</details>

## 6. fallback policy와 `AllowAnonymous`는 어떤 안전 기본값을 만드나요?

<details>
<summary>정답 보기</summary>

fallback policy는 별도 인가 metadata가 없는 endpoint도 기본적으로 인증을 요구하게 합니다. 공개할 endpoint만 `AllowAnonymous`로 명시하므로 새 endpoint에서 인가 선언을 빠뜨려 우연히 공개될 가능성을 줄입니다.

</details>

## 7. `X-Demo-User`를 운영 인증에 쓰면 안 되는 이유를 두 가지 이상 말해 보세요.

<details>
<summary>정답 보기</summary>

누구나 header를 위조할 수 있고, 서명·issuer·audience·expiration·철회·MFA를 검증하지 않습니다. 운영에서는 신뢰할 수 있는 Identity Provider의 OIDC/JWT handler 같은 검증된 인증 방식을 사용해야 합니다.

</details>

## 8. Result, 예외, 취소는 각각 어떤 상황을 나타내나요?

<details>
<summary>정답 보기</summary>

빈 제목처럼 호출자가 고칠 수 있는 예상 실패는 `Result`로 표현합니다. 중복 ID나 잘못된 DI 구성처럼 정상 업무 분기가 아닌 시스템 오류는 예외로 드러냅니다. 연결 종료처럼 호출자가 결과를 더 이상 원하지 않는 상황은 `CancellationToken`과 `OperationCanceledException`으로 전달합니다.

</details>

## 9. Admin 역할이나 owner claim만 있으면 문서를 읽을 수 있나요?

<details>
<summary>정답 보기</summary>

아닙니다. 리소스 handler는 `Identity.IsAuthenticated == true`이고 안정적인 `NameIdentifier`가 있어야 다음 owner/Admin 판단을 진행합니다. claim은 임의로 담을 수도 있으므로 인증 여부를 함께 확인해야 하며, 둘 중 하나라도 없으면 fail-closed로 `Succeed`하지 않습니다.

</details>
