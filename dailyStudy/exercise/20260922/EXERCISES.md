# Beginner → Pro 실행 연습

각 과제는 작은 변경 하나를 하고 빌드·자체 검증·HTTP 요청으로 확인하는 순서입니다. 정답 코드를 복사하기보다 먼저 예상 상태 코드를 적으세요.

## Beginner 1 — 새 Viewer 추가

`DemoHeaderAuthenticationHandler`에 `charlie`라는 Viewer를 추가합니다.

완료 기준:

- `X-Demo-User: charlie`로 인증이 성공합니다.
- 문서 생성은 403입니다.
- Alice 문서 조회는 404입니다.
- 기존 13개 자체 검증이 계속 통과합니다.

## Beginner 2 — 제목 규칙 추가

문서 제목에 줄바꿈 문자가 있으면 Domain에서 거절하도록 규칙을 추가하고 자체 검증도 한 건 추가합니다.

완료 기준:

- HTTP나 Repository가 아니라 `ProjectDocument.Create`가 규칙을 소유합니다.
- 오류 코드가 안정적인 대문자 식별자입니다.
- 제목 오류가 400 validation Problem Details로 반환됩니다.

## Intermediate 1 — 내 문서 목록

`GET /documents`를 추가해 현재 사용자가 소유한 문서만 반환합니다. Repository Port에 필요한 최소 계약을 추가하세요. 실습 데이터를 만들 수 있도록 `dana`라는 Editor도 추가하고 Alice와 Dana가 각각 문서를 생성하게 합니다.

완료 기준:

- Alice는 Alice 문서만, Dana는 Dana 문서만 봅니다. Viewer인 Bob은 생성할 수 없으므로 Bob 소유 문서를 전제로 하지 않습니다.
- Admin이 모든 문서를 봐야 하는지는 요구사항을 먼저 결정하고 README에 이유를 적습니다.
- 목록을 메모리에서 모두 읽은 뒤 endpoint가 owner를 필터링하는 대신 Repository 경계에서 필터 조건을 전달합니다.

## Intermediate 2 — 편집 정책

본문 수정 규칙을 **(문서 owner AND Editor) OR Admin**으로 확정하고 requirement와 handler를 추가합니다. 즉 Admin은 운영상 복구를 위해 소유권과 관계없이 수정할 수 있다고 가정합니다.

| 소유권·역할 | 기대 결과 |
| --- | --- |
| owner + Editor | 허용 |
| owner + Viewer | 거부 |
| non-owner + Editor | 거부 |
| non-owner + Admin | 허용 |

완료 기준:

- 일반 사용자는 owner라는 조건 하나만으로는 부족하고 Editor 역할도 함께 검사합니다.
- 위 네 행을 모두 자동 검증해 연산자 우선순위나 괄호 실수로 정책이 바뀌지 않게 합니다.
- Domain 검증과 Authorization 검증을 같은 조건문에 섞지 않습니다.

## Advanced 1 — 실패 이유 감사 로그

본문이나 token을 기록하지 않으면서 401, 403, 숨긴 404 판단을 구조화 감사 로그로 남깁니다.

완료 기준:

- 일반 Application 로그에는 외부 subject 원문 대신 keyed hash나 내부 가명 감사 ID를 남기고, endpoint 이름·정책/requirement 이름·결과 코드를 별도 필드로 기록합니다.
- 원문 subject가 규제상 꼭 필요하다면 일반 로그가 아니라 접근 통제·마스킹·보존 기간·삭제 절차가 승인된 전용 감사 저장소에만 기록합니다.
- 문서 본문, 인증 header 원문, token, 표시 이름·이메일 같은 개인정보는 남기지 않습니다.
- 고카디널리티 식별자를 metric label로 쓰지 않는다는 운영 메모를 추가합니다.

## Advanced 2 — 인가 결과 handler

ASP.NET Core의 `IAuthorizationMiddlewareResultHandler`를 조사해 API 전체의 challenge/forbid 응답을 중앙화합니다.

완료 기준:

- Authentication Handler가 신원 확인과 token 검증에 집중합니다.
- 401/403 Problem Details 계약이 한곳에 있습니다.
- 리소스 존재 감춤용 404와 endpoint policy 403을 구분합니다.

공식 출발점: [Customize the behavior of AuthorizationMiddleware](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/customizingauthorizationmiddlewareresponse?view=aspnetcore-10.0)

## Pro — 운영 인증으로 교체 설계

학습용 header scheme을 제거하고 OIDC 또는 JWT bearer 인증으로 교체할 설계 문서를 작성합니다. 실제 비밀값은 저장소에 넣지 않습니다.

완료 기준:

- issuer, audience, signature, expiration 검증 위치를 설명합니다.
- 외부 claim을 내부 subject/role로 매핑하는 경계와 tenant 격리를 설명합니다.
- key rotation, 철회, 권한 변경 지연, clock skew, token 로그 금지 정책을 적습니다.
- integration test에서 유효·만료·잘못된 issuer/audience/signature token을 어떻게 만들지 계획합니다.
- 운영 장애 시 fail-open이 아니라 fail-closed가 되어야 할 경계를 표시합니다.

## 매 과제 후 검증 명령

```powershell
dotnet build ./dailyStudy/exercise/20260922/src/DocumentAccessApi/DocumentAccessApi.csproj -c Release
dotnet run --project ./dailyStudy/exercise/20260922/src/DocumentAccessApi/DocumentAccessApi.csproj -c Release -- --self-test
dotnet format ./dailyStudy/exercise/20260922/src/DocumentAccessApi/DocumentAccessApi.csproj --verify-no-changes
```
