# 초보자 이해도 체크포인트

먼저 답을 가리고 자신의 말로 설명하세요. 정답 문장을 외우는 것보다 코드에서 근거 위치를 찾는 것이 중요합니다.

---

## 1. OpenAPI와 Swagger UI는 같은 것인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. OpenAPI는 API의 path, parameter, request, response, schema를 표현하는 기계 판독 specification입니다. Swagger UI 같은 화면은 OpenAPI 문서를 읽어 탐색·호출 기능을 제공하는 별도 도구입니다.

이 예제의 `Microsoft.AspNetCore.OpenApi` package는 `/openapi/v1.json` 문서를 만들지만 UI는 설치하지 않습니다. JSON 문서 자체가 계약의 원본입니다.

</details>

---

## 2. `AddOpenApi`와 `MapOpenApi`는 각각 무엇을 하나요?

<details>
<summary>정답과 해설</summary>

- `AddOpenApi`: endpoint metadata를 읽어 문서를 생성할 서비스와 transformer를 DI에 등록합니다.
- `MapOpenApi`: 생성된 문서를 HTTP로 제공할 route endpoint를 등록합니다.

서비스 등록과 HTTP 노출은 서로 다른 책임입니다. 실제 코드는 [`Program.cs`](./src/WorkshopContractApi/Program.cs)에 있습니다.

</details>

---

## 3. .NET 10에서 생성되는 기본 OpenAPI version은 무엇인가요?

<details>
<summary>정답과 해설</summary>

이 `net10.0` 예제의 실제 출력은 `openapi: 3.1.1`이며 JSON Schema 2020-12 표현을 사용합니다. self-test는 patch 수준까지 정확히 확인합니다. 별도의 `info.version: v1`은 기본 문서 이름일 뿐 URL 기반 API versioning을 자동 제공하지 않습니다.

.NET 11에서는 기본 OpenAPI 3.2가 예정되어 있으므로 “모든 .NET에서 언제나 3.1”이라고 일반화하면 안 됩니다. 이 프로젝트는 `net10.0` 계약에 맞춰 검증합니다.

</details>

---

## 4. `WithName`은 문서에서 무엇이 되며 왜 안정적이어야 하나요?

<details>
<summary>정답과 해설</summary>

`WithName("CreateWorkshopRegistration")`은 OpenAPI 작업의 `operationId`가 됩니다. client generator는 이를 SDK 메서드 이름으로 사용할 수 있습니다.

route 동작이 같아도 operationId를 바꾸면 generated client API가 바뀔 수 있으므로 소비자와 호환성을 검토해야 합니다.

</details>

---

## 5. `TypedResults`가 단순 `Results`보다 OpenAPI에 유리한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`TypedResults.Created(location, response)`는 `Created<RegistrationResponse>`라는 구체 결과 형식을 반환합니다. framework는 이 형식에서 201과 응답 body schema metadata를 얻고, 실제 응답에는 `Location`도 넣습니다.

일반 `Results` helper는 반환 signature가 `IResult`여서 구체 metadata가 사라질 수 있고, 그런 경우 `.Produces<T>()`를 더 명시해야 합니다.

</details>

---

## 6. `Results<Created<RegistrationResponse>, ProblemHttpResult>`가 400·404·409만 compile time에 허용하나요?

<details>
<summary>정답과 해설</summary>

아닙니다. union이 제한하는 것은 `Created<RegistrationResponse>`와 `ProblemHttpResult`라는 **결과 형식 두 가지**입니다. `ProblemHttpResult`의 status는 runtime 값이므로 같은 형식으로 418이나 500도 만들 수 있습니다.

handler가 만드는 정확한 400·404·409 집합은 오류 코드 `switch`, endpoint의 명시적 `Produces<ApiProblemResponse>` metadata, 실제 HTTP 테스트가 함께 지킵니다. 415는 handler 실행 전에 framework가 만들며 별도 metadata로 선언합니다. `Produces`는 문서 metadata일 뿐 runtime 반환을 막지 않으므로 테스트가 필요합니다.

</details>

---

## 7. 왜 오류에 `BadRequest<ProblemDetails>` 대신 `ProblemHttpResult`를 사용했나요?

<details>
<summary>정답과 해설</summary>

`BadRequest<ProblemDetails>`는 구조는 Problem Details여도 일반 JSON 결과의 media type을 사용할 수 있습니다. 실제 첫 검증에서 `application/json`이 나와 계약 drift를 발견했습니다.

`TypedResults.Problem(problem)`이 만드는 `ProblemHttpResult`는 `application/problem+json`을 사용합니다. 대신 runtime status가 한 형식 안에 있으므로 400·404·409 OpenAPI metadata를 `.Produces<ApiProblemResponse>(...)`로 명시했습니다. 전용 DTO는 `Extensions`의 `code`, `field`, 진단용 `traceId`까지 이름 있는 schema로 보여 줍니다.

</details>

---

## 8. Document/Operation Transformer는 middleware인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. middleware는 업무 HTTP 요청 pipeline에서 실행됩니다. Document Transformer는 OpenAPI 문서를 생성할 때 generated document를 보완합니다.

`WorkshopApiDocumentTransformer`는 제목·문서 이름·설명을 넣고, `WorkshopApiOperationTransformer`는 POST 201의 `Location` header와 GET ID pattern을 보완할 뿐 예약을 검증하거나 저장하지 않습니다. runtime 문서는 요청마다 다시 생성될 수 있으므로 transformer에 무거운 조회와 부작용을 넣지 않습니다.

</details>

---

## 9. 요청 DTO의 `string`, JSON `required`, Application의 `string?`는 왜 함께 있나요?

<details>
<summary>정답과 해설</summary>

Presentation DTO는 소비자 계약에서 필수인 값을 `string`과 validation metadata로 선언해 OpenAPI `required`에 넣습니다. 하지만 JSON 필드 누락이나 `null`은 여전히 네트워크에서 들어올 수 있으므로 Application 명령과 `RegistrationDraft.Create`는 `string?`로 방어합니다. factory가 누락, 길이, 허용 문자를 검사하고 정규화한 뒤에만 non-null `string`을 가진 Domain 초안을 만듭니다.

`nullable`은 값이 null일 수 있는지, `required`는 JSON 객체에 속성이 있어야 하는지를 뜻하는 서로 다른 축입니다. 이렇게 경계를 이중으로 확인하면 핵심 로직 모든 줄에서 null을 반복 검사하지 않아도 되고, 유효하지 않은 상태가 안쪽으로 퍼지는 것을 막습니다.

</details>

---

## 10. Result, 예외, 취소는 어떻게 구분하나요?

<details>
<summary>정답과 해설</summary>

- `Result<T>`: 잘못된 ID, 없는 회차, 좌석 충돌처럼 호출자가 예상하고 분기할 수 있는 실패
- 예외: 음수 카탈로그 가격, HTTP 매핑 누락처럼 프로그래머 계약 위반이나 예상하지 못한 장애
- 취소: 호출자가 더 이상 결과를 원하지 않는 별도 제어 흐름이며 `CancellationToken`으로 전파

서버 버그를 400으로 숨기거나, 정상 검증 실패를 모두 예외로 만들지 않습니다.

</details>

---

## 11. Application Service의 책임은 무엇이며 무엇을 몰라야 하나요?

<details>
<summary>정답과 해설</summary>

검증 → 회차 조회 → 정원 확인 → 가격 Strategy → Repository 저장 순서를 조율합니다. HTTP status, JSON, OpenAPI schema, Kestrel, 실제 DB 기술은 몰라야 합니다.

이 예제에서는 `WorkshopRegistrationService`가 Domain과 세 Port에 의존하고 Presentation/Infrastructure 구체 class에는 의존하지 않습니다.

</details>

---

## 12. Repository의 `lock`은 무엇을 보장하고 무엇을 보장하지 않나요?

<details>
<summary>정답과 해설</summary>

한 프로세스 안에서 예약 ID 검사, 회차·좌석 검사, 두 collection 저장을 같은 임계 구역으로 묶어 check-then-act 경쟁을 막습니다. 동시 요청 두 개 중 하나만 같은 좌석을 저장합니다.

프로세스 재시작 뒤 내구성, 여러 서버 사이 동시성, DB transaction은 보장하지 않습니다. 운영에서는 `RegistrationId`와 `(SessionId, SeatNumber)` unique constraint, transaction, 충돌 변환이 필요합니다.

</details>

---

## 13. Strategy와 DI를 사용한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`ITicketPricePolicy`는 등급별 가격 규칙을 Application Service의 순서 조율과 분리합니다. `Program.cs`가 `StandardTicketPricePolicy`를 주입하므로 새 정책이나 테스트 대역으로 교체할 수 있습니다.

DI 자체가 좋은 설계를 자동으로 만들지는 않습니다. 책임이 분명한 interface와 올바른 lifetime이 먼저이며, `Program.cs`는 그 선택을 한곳에 모은 Composition Root입니다.

</details>

---

## 14. OpenAPI 문서 테스트만 통과하면 실제 API 계약도 안전한가요?

<details>
<summary>정답과 해설</summary>

아닙니다. metadata가 409를 선언해도 구현이 500을 반환할 수 있고, 반대로 실제 409가 문서에서 빠질 수도 있습니다.

그래서 self-test와 `verify-http.ps1`은 문서의 path·operationId·상태·`Location`·schema와 실제 Production Kestrel의 201·200·400·404·409·415, media type, 안정 code·field·traceId를 함께 확인합니다. 앞뒤 공백 정규화, 깨진 JSON, `null` body, 숫자 문자열까지 runtime에서 확인하므로 문서와 runtime 두 방향이 모두 필요합니다.

</details>

---

## 15. 코드 읽기 순서 문장의 빈칸을 채워 보세요

> “`AddOpenApi`는 ______을 등록하고 `MapOpenApi`는 ______을 등록한다. 성공 schema는 ______가 제공하고, runtime 상태를 가진 Problem Details의 400·404·409·415는 ______ metadata로 보완한다. 이 metadata는 runtime을 ______한다/하지 않는다. Domain은 OpenAPI를 참조하지 않고 ______만 참조한다.”

<details>
<summary>정답과 해설</summary>

> “`AddOpenApi`는 **문서 생성 서비스와 transformer**를 등록하고 `MapOpenApi`는 **문서 조회 endpoint**를 등록한다. 성공 schema는 **TypedResults의 구체 결과 형식**이 제공하고, runtime 상태를 가진 Problem Details의 400·404·409·415는 **Produces&lt;ApiProblemResponse&gt;** metadata로 보완한다. 이 metadata는 runtime을 **강제하지 않는다**. Domain은 OpenAPI를 참조하지 않고 **자신의 순수 규칙과 값**만 참조한다.”

</details>

---

## 최종 자기 점검

- [ ] OpenAPI와 UI를 구분한다.
- [ ] `openapi: 3.1.1`, `info.version: v1`, API versioning을 구분한다.
- [ ] Add/Map, metadata, Document/Operation Transformer의 역할을 구분한다.
- [ ] TypedResults와 Results union의 장점과 한계를 말한다.
- [ ] 문서 metadata가 runtime을 강제하지 않음을 안다.
- [ ] nullable과 JSON required를 구분하고 non-null Domain으로 들어가는 과정을 설명한다.
- [ ] Result, 예외, 취소를 구분한다.
- [ ] Application Service, Repository, Strategy, DI의 의존성 방향을 그린다.
- [ ] 메모리 lock과 DB unique constraint의 범위를 구분한다.
- [ ] 문서와 실제 HTTP를 함께 검증한다.
- [ ] C# 15/.NET 11 RC 기능을 stable 실행 코드와 분리한다.
