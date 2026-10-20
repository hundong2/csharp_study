# 이해도 검증 — 먼저 답하고 정답 펼치기

각 질문을 읽고 **먼저 자신의 말로 한두 문장 답한 뒤** 정답을 펼치세요. 정답 문장을 외우는 것보다 “왜 그렇게 설계했는지”를 설명하는 것이 목표입니다. 헷갈린 질문에는 표시를 남기고 예제 코드를 다시 실행하세요.

## A. C# 기본 문법과 모델링

### 1. `record`는 일반 `class`와 비교해 이 예제에서 어떤 도움을 주나요?

<details>
<summary>정답 보기</summary>

`record`는 여러 속성 값을 기준으로 비교하고 데이터 중심 형식을 간결하게 표현하기 좋습니다. 다만 `record`라고 해서 내부의 모든 값이 자동으로 깊은 불변이 되는 것은 아니므로, 제한된 생성 경로와 변경 불가능한 컬렉션도 함께 고려해야 합니다.

</details>

### 2. `abstract record AuditEventRequest`를 직접 만들 수 없는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

`abstract`는 공통 계약만 제공하고 그 형식 자체의 인스턴스 생성을 막습니다. JSON의 `type`에 따라 `UserSignedInRequest` 같은 구체적인 파생 형식을 선택해야 합니다.

</details>

### 3. 파생 요청에 `sealed`를 붙인 의도는 무엇인가요?

<details>
<summary>정답 보기</summary>

`sealed`는 그 형식 아래로 더 상속하지 못하게 합니다. 공개 JSON 계약에서 예상하지 못한 추가 하위 형식이 생기는 범위를 줄이고, 현재 허용된 사례를 독자가 쉽게 파악하게 합니다.

</details>

### 4. `required string? ActorId { get; init; }`에서 `required`, `?`, `init`은 각각 무엇인가요?

<details>
<summary>정답 보기</summary>

`required`는 객체 초기화 때 멤버를 지정하라는 뜻이고, `?`는 값이 null일 수도 있다는 nullable 표시이며, `init`은 초기화 시점 뒤에는 일반 대입으로 바꾸지 못하게 합니다. “필드 존재 여부”와 “null 허용 여부”는 서로 다른 규칙입니다.

</details>

### 5. DTO→command 패턴 매칭과 Mapper Strategy 선택은 각각 어떻게 동작하나요?

<details>
<summary>정답 보기</summary>

`AuditEventEndpoints.ToCommand`의 type 패턴은 실제 요청이 어느 파생 `record`인지 확인하며 종류별 속성을 `IngestAuditEventCommand`로 복사합니다. 그 뒤 `IngestAuditEventService`는 정규화한 `Kind`를 key로 `Dictionary.TryGetValue`를 호출해 Mapper Strategy를 선택합니다. 즉 패턴 매칭은 DTO→command 변환, Dictionary 조회는 Strategy 선택을 담당합니다.

</details>

### 6. `async`/`await` 메서드에 `CancellationToken`을 끝까지 전달하는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

클라이언트가 연결을 끊거나 서버가 종료될 때 더 이상 필요 없는 저장 작업을 하위 계층도 멈출 수 있게 하기 위해서입니다. token을 중간에서 버리면 Repository는 취소 사실을 모른 채 I/O를 계속할 수 있습니다.

</details>

## B. System.Text.Json과 입력 보안

### 7. JSON source generation의 metadata 모드는 무엇을 미리 만드나요?

<details>
<summary>정답 보기</summary>

직렬화와 역직렬화에 필요한 형식·속성·생성자 같은 계약 metadata를 컴파일 시점에 생성합니다. 런타임 reflection 의존을 줄이고 trimming/AOT 친화성을 높이며, 공개할 JSON 형식 목록을 코드에서 명시적으로 확인하게 해 줍니다.

</details>

### 8. `AuditJsonContext`를 `TypeInfoResolverChain` 앞쪽에 넣는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

등록된 API 계약은 source-generated metadata로 먼저 해결하겠다는 우선순위를 주기 위해서입니다. fallback resolver가 있다면 등록되지 않은 형식이 reflection으로 처리될 수도 있으므로, 필요한 형식을 context에 빠짐없이 등록하고 테스트해야 합니다.

</details>

### 9. `[JsonPolymorphic]`과 `[JsonDerivedType]`은 역할이 어떻게 다른가요?

<details>
<summary>정답 보기</summary>

`JsonPolymorphic`은 판별자 속성 이름과 알 수 없는 사례의 처리 정책 같은 다형성 전체 규칙을 정합니다. `JsonDerivedType`은 특정 판별자 값과 허용할 구체 CLR 형식을 한 쌍으로 등록합니다.

</details>

### 10. JSON의 `type`에 임의의 CLR 형식 이름을 받지 않고 세 값을 허용 목록으로 둔 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

외부 입력이 서버 내부의 임의 형식을 선택하지 못하게 하고 공개 계약을 `user-signed-in`, `document-changed`, `permission-changed`로 제한하기 위해서입니다. 새 형식은 코드 검토와 명시적 등록을 거쳐야 합니다.

</details>

### 11. 알 수 없는 속성을 거절하면 어떤 버그를 빨리 찾을 수 있나요?

<details>
<summary>정답 보기</summary>

`actorId`를 `actorID`로 보내는 오타나 서버가 지원하지 않는 필드를 클라이언트가 보낸 경우를 조용히 무시하지 않고 즉시 드러냅니다. 다만 엄격성이 버전 호환성에 미치는 영향은 별도의 계약 정책으로 관리해야 합니다.

</details>

### 12. 중복 JSON 속성을 허용하면 왜 위험한가요?

<details>
<summary>정답 보기</summary>

파서나 중간 시스템마다 첫 값과 마지막 값 중 무엇을 채택하는지가 다를 수 있어 검증한 값과 사용한 값이 달라질 수 있습니다. 예를 들어 같은 `permission`을 두 번 보내는 모호한 요청은 거절하는 편이 안전합니다.

</details>

### 13. 대소문자 구분, required 멤버 존중, nullable 존중은 각각 무엇을 막나요?

<details>
<summary>정답 보기</summary>

대소문자 구분은 계약과 다른 철자의 조용한 수용을, required 멤버 존중은 필수 값의 생략을, nullable 존중은 null 불가 멤버에 `null`을 넣는 일을 막습니다. 세 설정은 겹쳐 보이지만 서로 다른 입력 상태를 검사합니다.

</details>

## C. Domain과 아키텍처

### 14. HTTP 요청 DTO와 Domain Model을 같은 형식으로 쓰지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

DTO는 아직 신뢰할 수 없는 JSON 모양을 받아야 하지만 Domain Model은 유효한 업무 상태만 표현해야 하기 때문입니다. 둘을 분리하면 HTTP 계약 변화가 핵심 업무 모델을 직접 흔드는 것도 줄일 수 있습니다.

</details>

### 15. 불변 Domain Model의 실용적인 장점은 무엇인가요?

<details>
<summary>정답 보기</summary>

한번 검증해 만든 이벤트가 저장 직전에 다른 값으로 바뀌지 않는다고 믿을 수 있습니다. 공유와 동시 실행에서 추적해야 할 상태 변화가 줄고, 테스트도 입력과 결과 비교에 집중할 수 있습니다.

</details>

### 16. `Result<T>`를 쓰기 좋은 실패와 예외를 쓰기 좋은 실패는 어떻게 구분하나요?

<details>
<summary>정답 보기</summary>

잘못된 업무 값이나 이미 사용된 event ID처럼 호출자가 예상하고 대응할 수 있는 실패는 안정적인 오류 코드를 가진 `Result<T>`가 알맞습니다. DI 구성 오류, 저장소 장애, 코드 불변식 위반처럼 정상 분기가 아닌 고장은 예외로 전파해 공통 예외 처리와 관측성에 맡깁니다.

</details>

### 17. 취소를 `Result.Failure("cancelled")`로 바꾸지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

취소는 업무 규칙 실패가 아니라 호출자가 결과를 더 이상 기다리지 않는다는 제어 신호입니다. `OperationCanceledException` 흐름을 유지해야 host가 이를 서버 오류나 일반 업무 실패와 구분하고 불필요한 작업을 중단할 수 있습니다.

</details>

### 18. Mapper에 Strategy 패턴을 적용한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

이벤트 종류마다 검증과 요약 생성 규칙이 다르므로 각 구현에 분리해 독립적으로 테스트하기 위해서입니다. Application Service는 등록된 Strategy 중 처리 가능한 것을 선택하고 전체 사용 사례 순서에 집중할 수 있습니다.

</details>

### 19. Application Service가 담당하는 일과 담당하지 않는 일은 무엇인가요?

<details>
<summary>정답 보기</summary>

Mapper 선택, Domain 생성 결과 확인, Repository 호출, 응답 결과 조합처럼 사용 사례의 순서를 조율합니다. JSON 파싱이나 HTTP 상태 코드 세부, 데이터베이스 구현 자체, Domain 불변식의 소유자가 되어서는 안 됩니다.

</details>

### 20. Repository Port, Adapter, DI, Composition Root의 관계를 설명해 보세요.

<details>
<summary>정답 보기</summary>

Port는 Application이 필요로 하는 저장 능력을 인터페이스로 말하고, Adapter는 메모리나 DB 기술로 그 능력을 구현합니다. DI container는 구현 객체를 공급하며, Composition Root인 `Program`이 어떤 Port에 어떤 Adapter와 Strategy를 연결할지 한곳에서 결정합니다.

</details>

## D. 테스트와 운영

### 21. 현재 event ID 중복 정책이 보장하는 것과 보장하지 않는 것은 무엇인가요?

<details>
<summary>정답 보기</summary>

Domain 검증을 통과해 먼저 저장된 요청만 성공하고, 그 뒤 같은 ID의 요청은 `409`로 거절되므로 한 프로세스 메모리에는 최대 한 건만 남습니다. 그러나 재시도에 이전 `201` 응답을 재현하지 않고, 재시작이나 여러 인스턴스 사이에서는 상태를 공유하지 않으므로 완전한 멱등 처리나 exactly-once 보장은 아닙니다.

</details>

### 22. “먼저 존재 여부 조회, 그다음 저장”만으로 at-most-once 저장이 충분하지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

동시에 온 두 요청이 모두 조회 시점에는 “없음”을 보고 둘 다 저장할 수 있기 때문입니다. 확인과 저장을 한 원자 연산으로 묶어야 하며, 운영 데이터베이스에서는 unique constraint와 transaction을 최종 방어선으로 둡니다.

</details>

### 23. self-test와 `verify-http.ps1`은 각각 무엇을 잡나요?

<details>
<summary>정답 보기</summary>

self-test는 Domain, Mapper, Application Service, Repository의 대표 규칙과 경계를 빠르게 검사합니다. `verify-http.ps1`은 실제 서버를 띄워 JSON binding, 다형성, 엄격 옵션, 상태 코드와 Problem Details의 대표적인 바깥 계약을 확인합니다. 둘 다 인증·인가, 여러 인스턴스/영속 DB, 실제 인프라 장애, 부하·침투 테스트까지 증명하지는 않습니다.

</details>

### 24. 감사 API의 로그와 metric에 넣지 말아야 할 값은 무엇인가요?

<details>
<summary>정답 보기</summary>

원본 JSON, IP 주소, 문서 변경 내용, 권한 대상 같은 민감하거나 높은 카디널리티의 값을 무심코 남기면 안 됩니다. 대신 낮은 카디널리티의 event kind, 안정적인 결과 code, 지연 시간, 중복 여부와 trace ID를 사용합니다.

</details>

## E. Stable과 Preview 구분

### 25. 오늘 실행 코드가 `net10.0`과 C# 14를 사용하는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

설치된 Stable SDK로 누구나 같은 예제를 빌드하고 실행할 수 있게 하기 위해서입니다. 학습 자료의 핵심 동작은 Preview 도구나 출시 전 문법에 의존하지 않아야 재현성이 높습니다.

</details>

### 26. C# 15 Preview의 닫힌 계층/union 아이디어를 설명만 하고 코드에 넣지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

가능한 사례를 compiler가 더 잘 이해하도록 표현하는 방향은 오늘의 다형성 모델과 비교할 가치가 있지만, Preview 문법과 동작은 정식 출시 전 바뀔 수 있습니다. 그래서 개념은 살펴보되 Stable 프로젝트의 컴파일 경로와 JSON 공개 계약에는 넣지 않습니다. 언어 계층이 닫혀 있어도 외부 `type` 판별자 허용 목록은 별도로 명시해야 합니다.

</details>

---

## 마지막 1분 복습

- [ ] `required`, nullable, `init`의 역할을 각각 말할 수 있다.
- [ ] JSON의 unknown, duplicate, casing, missing, null 실패를 구분한다.
- [ ] `[JsonDerivedType]`가 허용 목록인 이유를 설명한다.
- [ ] DTO와 불변 Domain의 경계를 설명한다.
- [ ] Result, 예외, 취소를 구분한다.
- [ ] Mapper Strategy와 Application Service의 책임을 구분한다.
- [ ] Port/Adapter와 Composition Root의 의존성 방향을 그릴 수 있다.
- [ ] 같은 event ID의 병렬 요청에서 프로세스 내부 at-most-once 저장을 왜 원자적으로 처리해야 하는지 설명한다.
- [ ] build, self-test, 실제 HTTP 검증을 직접 실행했다.
- [ ] Stable 실행 코드와 Preview 설명을 섞지 않았다.
