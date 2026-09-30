# Beginner to Pro 연습문제

각 단계는 앞 단계를 완료했다고 가정합니다. 한 번에 여러 파일을 바꾸지 말고, 매 단계마다 아래 세 명령으로 회귀를 확인하세요.

```powershell
dotnet build .\src\WorkshopContractApi\WorkshopContractApi.csproj -c Release
dotnet run --project .\src\WorkshopContractApi\WorkshopContractApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
```

완료 기준은 “compile된다”가 아니라 **코드, OpenAPI 문서, 실제 HTTP, 설명이 같은 계약을 말한다**입니다.

---

## Level 0 — Beginner: 문서를 눈으로 탐색하기

서버를 실행하고 `/openapi/v1.json`을 PowerShell로 읽으세요.

```powershell
$document = Invoke-RestMethod http://127.0.0.1:51001/openapi/v1.json
$document.openapi
$document.info.title
$document.info.version
$document.paths.PSObject.Properties.Name
$document.paths.'/registrations'.post.responses.PSObject.Properties.Name
$document.paths.'/registrations'.post.responses.'201'.headers.Location
$document.components.schemas.CreateRegistrationRequest.required
```

다음 질문에 실행 결과로 답하세요.

1. OpenAPI version은 무엇인가요?
2. `GET /`가 `paths`에 없는데도 호출되는 이유는 무엇인가요?
3. POST가 선언한 상태 코드는 몇 개인가요?
4. `CreateRegistrationRequest` schema는 문서 어디에 있나요?
5. UI를 설치하지 않아도 JSON 문서를 읽을 수 있는 이유는 무엇인가요?
6. `openapi: 3.1.1`과 `info.version: v1`은 각각 무엇을 뜻하나요?
7. POST 201의 `Location`과 요청의 다섯 required 필드는 어디에 있나요?

완료 기준:

- OpenAPI specification과 Swagger UI를 같은 것으로 부르지 않습니다.
- `v1` 문서 이름을 URL 기반 API versioning 기능이라고 오해하지 않습니다.
- nullable과 JSON required가 서로 다른 축임을 설명합니다.
- path, operation, response, schema의 포함 관계를 손으로 그릴 수 있습니다.

---

## Level 1 — Beginner: metadata 변경과 contract test 실패 보기

[`RegistrationEndpoints.cs`](./src/WorkshopContractApi/Presentation/RegistrationEndpoints.cs)의 POST summary를 `워크숍 신청 생성`으로 바꾸고, 코드 테스트는 수정하지 않은 채 `--self-test`를 먼저 실행하세요.

예측할 것:

- build는 성공하는가?
- 실제 POST 201은 성공하는가?
- OpenAPI assertion 중 무엇만 실패하는가?

그 뒤 [`SelfTestRunner.cs`](./src/WorkshopContractApi/SelfTesting/SelfTestRunner.cs)의 기대 summary를 같은 값으로 갱신해 다시 통과시키세요.

완료 기준:

- metadata도 소비자가 보는 계약이므로 테스트 대상임을 설명합니다.
- 문서 테스트가 업무 구현 테스트와 다른 실패를 잡는다는 것을 확인합니다.

---

## Level 2 — Beginner+: Standard 가격 시나리오 추가하기

현재 HTTP 검증은 Premium 40,000원만 확인합니다. `attendeeTier = "Standard"`, 새 좌석 2번인 POST를 추가해 50,000원을 검증하세요.

수정 위치:

- 빠른 회귀: [`SelfTestRunner.cs`](./src/WorkshopContractApi/SelfTesting/SelfTestRunner.cs)
- 별도 프로세스 black-box: [`verify-http.ps1`](./verify-http.ps1)

주의:

- 기존 1번 좌석과 충돌하지 않게 새 `registrationId`와 좌석을 사용합니다.
- assertion 수와 README의 기대 출력도 함께 갱신합니다.

완료 기준:

- Strategy 단위 테스트와 HTTP 통합 테스트의 책임 차이를 설명합니다.
- JSON 응답의 `attendeeTier`, `priceWon`을 모두 확인합니다.

---

## Level 3 — Intermediate: 새 422 계약을 처음부터 끝까지 추가하기

같은 `attendeeId`가 같은 회차에 두 좌석을 예약하려 하면 `422 Unprocessable Content`와 `registration.attendee.already_registered`를 반환하도록 설계하세요.

변경 순서:

1. Repository Port의 저장 결과에 새 case를 추가합니다.
2. In-memory Adapter에서 회차·수강생 복합 key를 원자적으로 검사합니다.
3. Application Service가 새 결과를 `Error`로 바꿉니다.
4. Presentation switch가 status 422인 `ProblemHttpResult`를 만듭니다.
5. POST endpoint에 `Produces<ApiProblemResponse>(422, "application/problem+json")`를 추가합니다.
6. OpenAPI 문서와 실제 HTTP 양쪽에 422 assertion을 추가합니다.
7. README의 계약 표와 오류 코드 표를 갱신합니다.

완료 기준:

- `Results<Created<...>, ProblemHttpResult>`만으로 422가 자동 선언되지 않는 이유를 설명합니다.
- Adapter의 read-then-write가 같은 `lock` 안에 있습니다.
- 422가 400 또는 409보다 적절한 이유를 자신의 API 정책으로 문서화합니다.

---

## Level 4 — Intermediate+: 세션 조회 endpoint를 typed 결과로 추가하기

`GET /sessions/{sessionId}`를 추가하세요.

요구 계약:

| 결과 | 상태 | 본문 |
| --- | --- | --- |
| 존재 | 200 | `SessionResponse` |
| ID 형식 오류 | 400 | Problem Details |
| 없음 | 404 | Problem Details |

설계 조건:

- Presentation이 `DemoWorkshopCatalog` 구체 class를 직접 받지 않습니다.
- 가능하면 별도 query Application Service를 만들거나 기존 서비스에 의도가 분명한 조회 메서드를 추가합니다.
- `SessionResponse`는 Domain `WorkshopSession`을 그대로 노출하지 않습니다.
- `.WithName`, `.WithTags`, `.WithSummary`, `.WithDescription`을 모두 정합니다.
- 새 path, operationId, 200·400·404와 실제 응답을 검증합니다.

완료 기준:

- OpenAPI path가 2개에서 3개로 늘어 테스트를 의도적으로 갱신했습니다.
- Domain/Application이 ASP.NET Core나 OpenAPI namespace를 참조하지 않습니다.

---

## Level 5 — Advanced: endpoint 전용 Operation Transformer 사용하기

기존 전역 `WorkshopApiOperationTransformer`는 201 `Location`과 GET ID pattern을 보완합니다. 이번에는 POST endpoint에만 request example을 추가하도록 `.AddOpenApiOperationTransformer(...)`를 사용하고, 해당 작업에서만 예제를 보완합니다.

공식 문서의 현재 .NET 10 API를 먼저 확인하세요.

- [OpenAPI 문서 사용자 지정](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
- [OpenAPI metadata 포함](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0)

검증 항목:

- GET 작업에는 POST example이 생기지 않습니다.
- example에 실제 이메일, 토큰, 개인정보를 넣지 않습니다.
- self-test가 example의 핵심 필드를 확인합니다.
- transformer가 업무 저장소를 변경하거나 외부 API를 호출하지 않습니다.

완료 기준:

- Document Transformer, 전역 Operation Transformer, endpoint 전용 Operation Transformer의 적용 범위를 비교합니다.
- “문서 요청 때 transformer가 실행될 수 있다”는 운영 비용을 설명합니다.

---

## Level 6 — Advanced+: 문서 노출 정책 설계하기

현재 `MapOpenApi()`는 모든 환경에 열려 있습니다. 다음 중 하나를 구현하고 선택 이유를 README에 적으세요.

### 선택 A — Development에서만 노출

```text
Development: /openapi/v1.json → 200
Production:  /openapi/v1.json → 404
```

### 선택 B — 운영에서도 인증된 tester만 노출

인증·인가 구성을 추가하고 OpenAPI endpoint에 policy를 요구합니다. 학습용 가짜 header 인증을 만든다면 절대 운영 보안이라고 부르지 마세요.

필수 검증:

- Development/Production 앱을 각각 임시 port로 시작합니다.
- 선택한 환경·권한에서 200, 그 밖에서는 401/403/404 중 문서화한 상태가 나옵니다.
- 업무 endpoint의 접근 정책과 문서 endpoint의 접근 정책을 구분합니다.

완료 기준:

- “OpenAPI를 숨기면 API가 안전해진다”라고 과장하지 않습니다.
- 문서 노출은 인증·인가·입력 검증을 대체하지 않음을 설명합니다.

---

## Level 7 — Pro: 호환성 검사를 CI 정책으로 만들기

문서가 바뀔 때 사람이 놓칠 수 있는 breaking change를 CI에서 검토하는 설계를 작성하세요. 도구를 실제 설치하지 않아도 다음 pipeline을 구체적인 입력·출력과 함께 의사 코드로 만드세요.

```text
Release build
  → OpenAPI 문서 생성
  → 기준 branch 문서와 semantic diff
  → breaking 후보 분류
  → 생성 client compile/test
  → human approval
```

breaking 후보 예:

- path 또는 HTTP method 제거
- required request field 추가
- response field 제거 또는 형식 변경
- 성공 status 제거
- enum 값 축소
- 안정적인 `operationId` 변경

항상 breaking이 아닌 예:

- 설명 오탈자 수정
- optional response field 추가
- 더 넓은 enum 값 추가 — 단, 일부 generated client에는 영향이 있을 수 있어 확인 필요

완료 기준:

- JSON 문자열 줄 diff와 OpenAPI semantic diff의 차이를 설명합니다.
- generator patch 변화와 소비자 계약 변화를 구분합니다.
- 자동 도구 결과를 곧바로 배포 승인으로 간주하지 않습니다.

---

## Level 8 — Pro+: 실제 DB Repository 설계하기

현재 `lock` 기반 Adapter를 여러 서버에서도 안전한 DB Adapter로 바꾸는 설계와 테스트 계획을 작성하세요.

필수 항목:

- `RegistrationId` unique constraint
- `(SessionId, SeatNumber)` unique constraint
- insert 경쟁에서 한 transaction만 성공하는 흐름
- DB unique violation을 `DuplicateRegistrationId` 또는 `SeatAlreadyTaken`으로 분류하는 방법
- cancellation과 transaction rollback
- 개인정보 최소 저장, 보존·삭제 정책
- 충돌률, 처리 지연, 실패율 metric과 trace
- migration 중 기존 중복 데이터 정리

주의:

애플리케이션에서 `ExistsAsync` 후 `InsertAsync`를 호출하는 것만으로는 경쟁 조건을 막지 못합니다. DB 제약이 최종 보루여야 합니다.

완료 기준:

- 한 프로세스 `lock`과 분산 DB unique constraint의 보장 범위를 비교합니다.
- Repository 계약은 유지하면서 Adapter만 교체하는 테스트를 제안합니다.
