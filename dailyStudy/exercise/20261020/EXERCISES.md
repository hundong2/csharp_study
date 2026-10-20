# 연습문제 — 엄격하고 확장 가능한 감사 이벤트 API

이 문서는 완성된 예제를 조금씩 바꾸며 **C# 문법 → JSON 경계 → Application/Domain 설계 → 운영 안전성** 순서로 연습하도록 구성했습니다. 한 과제를 마친 뒤 다음 과제로 넘어가세요. 실험용 변경을 계속 쌓으면 실패 원인을 찾기 어려우므로, 각 과제의 결과를 기록한 뒤 기준 코드로 되돌리거나 별도 브랜치에서 진행하는 편이 좋습니다.

> ⚠️ 이 API는 인증·인가와 영속 저장소가 없는 학습용입니다. 외부에 배포하지 말고 연습에는 가짜 식별자와 문서 요약만 사용하세요.

## 0. Baseline부터 고정하기

저장소 루트에서 아래 명령을 실행합니다.

```powershell
cd .\dailyStudy\exercise\20261020
dotnet build .\src\AuditEventApi\AuditEventApi.csproj -c Release
dotnet run --project .\src\AuditEventApi\AuditEventApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
```

- **변경:** 아직 코드를 바꾸지 않습니다. 세 명령의 종료 코드와 마지막 성공 메시지를 학습 노트에 기록합니다.
- **관찰:** `build`는 컴파일 계약, `--self-test`는 프로세스 안쪽의 Domain/Application 계약, `verify-http.ps1`은 실제 Kestrel과 JSON/HTTP 계약을 각각 확인합니다.
- **검증:** 세 명령이 모두 성공해야 합니다. 실패한다면 연습을 시작하지 말고 SDK 버전, 사용 중인 포트, 첫 오류 메시지부터 확인합니다.
- **완료 기준:** “어느 검증이 어느 경계를 확인하는지”를 한 문장씩 설명할 수 있고, 이후 과제에서도 같은 세 명령을 반복 실행할 수 있습니다.

> `verify-http.ps1`은 사용 가능한 loopback 포트를 동적으로 고른 뒤 서버 시작과 종료까지 관리하는 black-box 검증 스크립트입니다. 포트를 고른 직후 다른 프로세스가 먼저 점유하는 드문 경쟁으로 listen 오류가 나면 충돌한 프로세스를 확인한 뒤 다시 실행하세요.

---

## Beginner 1 — 세 가지 허용 이벤트를 눈으로 구분하기

`user-signed-in`, `document-changed`, `permission-changed` 요청을 하나씩 찾아 공통 필드와 각 이벤트만의 필드를 표로 적어 보세요. 그다음 `verify-http.ps1`의 정상 요청 하나에서 `eventId`, `actorId`처럼 업무 의미를 해치지 않는 값을 바꿔 실행합니다.

- **변경:** 정상 payload의 값만 바꾸고, 속성 이름과 `type` 값은 그대로 둡니다.
- **관찰:** JSON의 `type`은 `AuditEventRequest`의 실제 하위 `record`를 고릅니다. 공통 필드는 기반 형식에, 이벤트별 필드는 각 파생 형식에 모여 있습니다.
- **검증:** `dotnet build ...`와 `--self-test`가 그대로 통과하고, `.\verify-http.ps1`의 수정한 정상 요청도 성공하는지 확인합니다.
- **완료 기준:** `abstract record`, `sealed record`, `required`, `init`, `string?`가 각각 무엇을 제한하거나 표현하는지 초보자 용어로 설명할 수 있습니다.

## Beginner 2 — 엄격한 JSON 실패 표 만들기

정상 `user-signed-in` payload를 복사해 아래 변형을 **한 번에 하나씩만** 적용합니다.

1. 존재하지 않는 `debugNote` 속성 추가
2. `actorId`를 같은 JSON 객체에 두 번 작성
3. `actorId`를 `ActorId`로 대소문자 변경
4. 필수 속성 `occurredAtUtc` 생략
5. `eventId`에 명시적 `null` 전달
6. `type`을 `server-restarted`로 변경

- **변경:** 각 실패 payload를 `verify-http.ps1`에 별도 사례로 추가합니다. 한 사례에 오류를 여러 개 섞지 마세요.
- **관찰:** 1~4와 6은 serializer·공개 계약 경계에서 거절됩니다. 현재 `EventId`는 `required string?`이므로 5의 JSON `null` 자체는 읽을 수 있고, 이후 Domain이 `audit.invalid_event_id`로 거절합니다. `RespectNullableAnnotations`의 non-null 강제 실험은 Beginner 3에서 별도로 합니다.
- **검증:** 각 요청이 저장되지 않고 안전한 4xx Problem Details를 돌려주는지 확인합니다. 5는 `code = audit.invalid_event_id`, `field = eventId`인지 확인하고, 나머지는 JSON binding 실패인지 구분합니다. 내부 예외 형식이나 stack trace가 본문에 노출되지 않아야 합니다.
- **완료 기준:** 여섯 사례마다 serializer와 Domain 중 어느 경계가 거절하는지, 어떤 설정·attribute·불변식이 책임지는지 연결해 말할 수 있습니다.

## Beginner 3 — `required`와 nullable을 따로 실험하기

`required string?`인 요청 속성 하나를 골라 “필드 생략”과 “명시적 `null`”을 각각 보내 보세요. 그 뒤 `?`를 임시로 제거해 `required string`으로 만들고 같은 두 요청을 반복합니다.

- **변경:** 요청 계약의 nullable 표기만 임시로 바꾸고, 두 payload를 검증 스크립트에 추가합니다.
- **관찰:** `required`는 멤버가 **있어야 한다**는 규칙이고, nullable 정책은 그 멤버의 값이 **null이어도 되는가**라는 별도 규칙입니다. `required string`으로 바꾼 뒤에는 `RespectNullableAnnotations = true`가 명시적 `null`을 serializer 경계에서 거절하며, Domain 유효성 검사는 빈 문자열이나 업무 형식까지 더 좁힙니다.
- **검증:** 변경 전후의 HTTP 상태와 오류 필드를 표로 비교하고 전체 기준 명령도 실행합니다.
- **완료 기준:** “생략”, `null`, `""`, 공백 문자열이 같은 입력이 아니며 어느 계층에서 거절되는지 구분할 수 있습니다. 실험 뒤 기준 계약으로 복구합니다.

## Beginner 4 — 불변 Domain을 일부러 흔들어 보기

Domain 이벤트의 get 전용 속성 또는 생성 경로를 찾아보세요. 임시로 setter를 열거나 생성자 검증을 우회하려고 시도한 뒤, 잘못된 상태를 만들 수 있는 짧은 self-test를 작성합니다.

- **변경:** 테스트가 잘못된 Domain 상태를 만들 수 있도록 가장 작은 임시 변경만 합니다.
- **관찰:** `record`는 값 비교에 유용하지만 그 자체만으로 모든 내부 상태를 불변으로 만들지는 않습니다. 제한된 생성 경로, get 전용 값, 방어적 복사가 함께 필요합니다.
- **검증:** 보호를 제거했을 때 새 테스트가 위험을 드러내고, 보호를 복원했을 때 잘못된 상태 생성이 컴파일되지 않거나 실패 Result가 되는지 확인합니다.
- **완료 기준:** 불변성이 “값을 절대 바꾸지 않는다”를 넘어 “유효한 상태만 존재하게 한다”는 뜻임을 설명할 수 있습니다.

---

## Intermediate 1 — 새 이벤트를 허용 목록에 끝까지 연결하기

연습용 `api-key-rotated` 이벤트를 추가한다고 가정합니다. `keyId`와 `reason`을 받고 비밀 키 원문은 절대 받지 않는 계약을 설계하세요.

- **변경:** 파생 요청 `record`와 `[JsonDerivedType]`, source-generation context의 `[JsonSerializable]`, `IngestAuditEventCommand`의 `KeyId`·`Reason` 필드, `AuditEventEndpoints.ToCommand`의 type 패턴, Domain 팩터리와 Mapper, Mapper 구성/DI, 정상·실패 self-test와 HTTP 계약 테스트를 모두 추가합니다.
- **관찰:** 이 경계들은 새 외부 계약 때문에 함께 바뀌지만, Mapper가 같은 Port를 구현하면 `IngestAuditEventService`의 선택·저장 순서는 바꾸지 않아도 됩니다. 한 곳을 빠뜨리면 컴파일되더라도 역직렬화, DTO→command 변환, mapper 선택 또는 source-generated metadata 경계에서 실패할 수 있습니다.
- **검증:** 새 `type`의 정상 요청은 처리되고, 철자가 비슷한 미등록 `api-key-rotate`는 4xx로 거절되는지 확인합니다. 기존 세 이벤트도 전부 회귀 검증합니다.
- **완료 기준:** 새 형식을 등록해야 하는 지점을 목록으로 설명하고, 허용 목록 밖 CLR 형식이 외부 입력만으로 생성되지 않음을 보여 줍니다.

## Intermediate 2 — Mapper Strategy의 선택 규칙 분리하기

각 요청 형식의 매퍼가 만드는 `Kind`와 안전한 `Summary`를 찾아보세요. 한 매퍼의 요약 규칙을 작은 전용 formatter 또는 정책 객체로 분리해 봅니다.

- **변경:** `IAuditEventMapper` 구현 하나의 책임을 작게 분리하고 DI에서 필요한 구현을 주입합니다. 다른 매퍼의 코드는 수정하지 않는 것을 목표로 합니다.
- **관찰:** Application Service는 큰 `switch`로 모든 하위 형식을 알 필요 없이 “처리 가능한 Strategy 찾기”에 집중할 수 있습니다. OCP 이점은 **새 Mapper가 같은 Port를 구현하면 `IngestAuditEventService`의 선택·저장 순서를 바꾸지 않는 범위**에 한정됩니다. 새 공개 이벤트 전체를 추가할 때는 Intermediate 1에 열거한 계약·metadata·command·mapping·DI·테스트 지점을 모두 바꿔야 합니다.
- **검증:** 해당 이벤트의 정상·잘못된 입력 self-test와 전체 HTTP 검증을 실행합니다. 비밀값이나 원문 전체가 `Summary`에 새로 노출되지 않는지도 확인합니다.
- **완료 기준:** Strategy 선택 실패가 예상 가능한 Result인지 구성 오류 예외인지 기준을 정하고, 테스트로 고정합니다.

## Intermediate 3 — Result, 예외, 취소를 세 갈래로 분류하기

다음 상황을 표로 분류하세요: 빈 `eventId`, 지원하지 않는 요청 형식, 같은 ID의 의미가 다른 재요청, Repository 연결 실패, DI 등록 누락, 클라이언트 연결 취소.

- **변경:** 빠진 경계 사례가 있다면 self-test를 하나씩 추가합니다. 특히 Repository fake가 `OperationCanceledException`을 던지는 사례를 넣고 Application Service가 이를 업무 실패 Result로 바꾸지 않게 합니다.
- **관찰:** 수정 가능한 예상 실패는 `Result<T>`, 프로그램 구성이나 인프라의 예상 밖 고장은 예외, 호출 중단은 `CancellationToken`/`OperationCanceledException` 흐름으로 남겨야 상위 계층이 올바르게 대응합니다.
- **검증:** 예상 실패에는 안정적인 오류 코드가 있고, 취소는 정상 실패 응답으로 위장되지 않으며, 예상 밖 예외의 세부 내용은 HTTP 본문에 노출되지 않는지 확인합니다.
- **완료 기준:** 새 실패 사례를 보았을 때 Result·예외·취소 중 하나를 선택하고 그 이유를 설명할 수 있습니다.

## Intermediate 4 — Application Service의 순서를 그리기

이벤트 수집 흐름을 `입력 → Mapper 선택 → Domain 생성 → Repository 저장 → 응답 매핑` 순서도로 직접 그립니다. 그다음 Application Service에서 Repository 호출을 Mapper보다 앞에 두는 잘못된 실험을 해 보세요.

- **변경:** fake Repository에 호출 횟수를 기록하게 하고, 매핑 실패일 때 저장소가 호출되지 않아야 한다는 테스트를 먼저 추가합니다. 구현 순서를 임시로 바꿔 테스트가 잡는지 확인합니다.
- **관찰:** Application Service는 사용 사례의 순서를 조율합니다. Presentation은 HTTP를, Domain은 불변식을, Repository Adapter는 저장 기술을 담당하므로 어느 한 계층이 모든 일을 하지 않습니다.
- **검증:** 매핑 실패·Domain 실패·저장 성공 경로에서 Repository 호출 여부가 기대와 같은지 검사하고 기준 구현으로 복구합니다.
- **완료 기준:** 각 단계의 책임과 “이전 단계 실패 시 다음 단계를 호출하지 않는다”는 단락 조건을 말할 수 있습니다.

---

## Advanced 1 — event ID at-most-once를 경쟁 상황에서 검증하기

같은 `eventId`를 가진 동일 payload를 여러 작업에서 거의 동시에 저장하도록 self-test를 추가합니다. 이어서 같은 `eventId`이지만 이벤트 종류나 핵심 내용이 다른 요청도 보냅니다.

- **변경:** `Task.WhenAll`을 이용한 동시 실행 테스트와, “동일 재전송” 및 “ID 충돌”을 구분하는 사례를 추가합니다. Repository의 `ConcurrentDictionary.TryAdd`가 확인과 추가를 하나의 원자 연산으로 묶는지 확인합니다.
- **관찰:** 먼저 조회하고 나중에 저장하면 두 요청이 모두 “없음”을 본 뒤 중복 저장할 수 있습니다. 기준 구현은 원자적 추가로 한 프로세스에서 최대 한 건만 저장하고 나머지 요청에 `409`를 반환합니다. 이는 동일 재시도에 성공 응답을 재현하는 멱등 응답이나 분산 exactly-once 보장이 아닙니다.
- **검증:** 동일 `eventId`가 두 개의 저장 레코드가 되지 않고, 의미가 다른 재사용은 조용히 성공으로 오인되지 않는지 확인합니다. 테스트는 한 번이 아니라 반복 실행해도 안정적이어야 합니다.
- **완료 기준:** “같은 재전송”의 응답 정책과 “다른 내용의 ID 충돌” 정책을 문서화하고 저장소 테스트로 고정합니다.

## Advanced 2 — Repository Port와 Adapter를 교체하기

학습용 Adapter `LoggingAuditEventRepository`를 decorator 형태로 만들어 기존 Repository 호출 전후의 결과 코드와 소요 시간만 기록해 보세요.

- **변경:** Application의 Port 인터페이스는 유지하고 Infrastructure Adapter와 DI 조립만 추가합니다. payload 전체, actor 원문, 문서 변경 내용은 로그에 남기지 않습니다.
- **관찰:** Port 덕분에 Application Service는 메모리, 데이터베이스, decorator 중 무엇이 연결됐는지 몰라도 됩니다. Adapter 교체 지점은 Composition Root 한곳에 모입니다.
- **검증:** fake logger로 허용한 필드만 기록됐는지, 취소와 예외에서도 민감한 JSON이 남지 않는지 검사합니다. 전체 baseline도 다시 통과시킵니다.
- **완료 기준:** Port가 업무가 필요로 하는 능력을 말하고 Adapter가 기술 세부 사항을 구현한다는 차이를 코드 위치와 함께 설명할 수 있습니다.

## Advanced 3 — DI 등록 누락을 빠르게 실패시키기

Mapper Strategy 하나의 DI 등록을 임시로 제거하고 해당 이벤트를 요청해 보세요. 요청 시점까지 숨어 있는 실패를 애플리케이션 시작 시점 검증으로 앞당길 방법을 설계합니다.

- **변경:** Composition Root의 등록 한 줄을 임시로 제거해 증상을 기록합니다. 그런 다음 시작 시 세 허용 이벤트 각각에 정확히 하나의 매퍼가 있는지 검사하는 작은 구성 검증을 추가합니다.
- **관찰:** DI는 객체 생성을 대신하는 기능을 넘어 의존성 방향을 실행 가능한 형태로 조립합니다. 다중 Strategy 컬렉션은 “서비스가 resolve된다”만으로 누락을 잡지 못할 수 있습니다.
- **검증:** 등록 누락·중복이 명확한 구성 오류로 시작 단계에서 실패하고, 정상 등록에서는 baseline 세 명령이 통과하는지 확인합니다.
- **완료 기준:** Composition Root 바깥에서 구체 Adapter를 `new`하지 않고, 구성 오류 메시지만 보고 누락된 이벤트를 찾을 수 있습니다.

## Advanced 4 — source-generated metadata 경계를 증명하기

새 테스트 전용 응답 DTO를 endpoint에서 반환하되 `[JsonSerializable]` 등록은 일부러 생략해 보세요. 현재 resolver chain의 fallback 때문에 동작하는지, 엄격히 source-generated 계약만 허용하도록 만들면 어떻게 달라지는지 조사합니다.

- **변경:** 테스트용 DTO와 endpoint를 임시로 추가하고 source-generation 등록 전후를 비교합니다. 운영 기준 코드는 마지막에 명시적 등록 상태로 둡니다.
- **관찰:** `JsonSourceGenerationMode.Metadata`는 직렬화 계약 정보를 빌드 때 생성합니다. 그러나 resolver chain에 reflection fallback이 남아 있으면 “등록을 빠뜨려도 우연히 동작”할 수 있으므로 정책과 검증이 함께 필요합니다.
- **검증:** generated source 또는 빌드 산출물에서 metadata 생성을 확인하고, HTTP 응답 이름 정책이 camelCase로 유지되는지 검사합니다. 등록 누락을 잡는 테스트를 남깁니다.
- **완료 기준:** source generation의 성능/AOT 이점뿐 아니라 “공개 JSON 계약을 명시적으로 목록화한다”는 설계 이점을 설명할 수 있습니다.

---

## Pro 1 — 운영 DB의 at-most-once 저장과 멱등 응답 설계하기

메모리 Repository를 PostgreSQL, SQL Server 등 영속 저장소로 바꾸는 설계 문서를 먼저 작성하세요. 구현한다면 최소한의 integration test도 함께 만듭니다.

- **변경:** `event_id` unique constraint, canonical payload fingerprint 또는 비교할 업무 필드, transaction 범위, 재시도 상한을 정의합니다. 저장 성공 뒤 응답을 잃어 같은 요청이 재전송되는 경우도 포함합니다.
- **관찰:** 프로세스 내부의 `ConcurrentDictionary` 동기화는 여러 API 인스턴스 사이에서 공유되지 않습니다. 최대 한 행 저장의 최종 권위는 데이터베이스 constraint/transaction이어야 하며, unique 위반을 `409`로 번역할지 기존 성공 결과를 재현할지는 별도의 멱등 응답 정책입니다.
- **검증:** 서로 다른 연결에서 같은 ID를 병렬 저장해도 하나의 논리 이벤트만 남는지, 동일 재전송과 의미가 다른 충돌이 구분되는지 integration test로 확인합니다.
- **완료 기준:** 장애 시나리오별로 원자적 at-most-once 저장, 멱등 응답 여부, retry 가능성을 구분해 설명하고 무한 재시도를 하지 않습니다.

## Pro 2 — 관측성에 개인정보 경계 세우기

구조화 로그, metric, trace에 남길 필드를 설계하세요.

- **변경:** 낮은 카디널리티의 `event_kind`, 안정적인 결과 `code`, 처리 시간, 중복 여부를 기록합니다. `actorId`, IP 주소, 문서 요약, 권한 대상, 원본 JSON은 기본적으로 제외하거나 승인된 비식별화 정책을 적용합니다.
- **관찰:** 디버깅 편의를 위해 입력 전체를 기록하면 감사 시스템 자체가 민감정보 유출 경로가 됩니다. `traceId`는 요청 상관관계에 사용하고 업무 식별자와 역할을 섞지 않습니다.
- **검증:** 정상·검증 실패·중복·예외 요청 후 로그를 검사해 금지한 값이 없는지 자동화합니다. metric label에 `eventId` 같은 고카디널리티 값을 넣지 않습니다.
- **완료 기준:** 운영자가 실패 종류와 지연을 파악할 수 있으면서도 원본 이벤트 내용을 볼 수 없는 대시보드 초안을 설명할 수 있습니다.

## Pro 3 — 계약 변화와 호환성 정책 정하기

엄격한 unknown-member 거절 정책은 오타와 예기치 않은 입력을 잘 잡지만, 구버전 서버가 신버전 클라이언트의 새 필드를 거절하게 할 수도 있습니다. 버전 전략을 설계하세요.

- **변경:** URL 버전, media type 버전, 별도 endpoint 중 하나를 선택하고 새 optional 필드를 도입하는 migration 순서를 문서화합니다. 무조건 엄격성을 끄는 방식은 피합니다.
- **관찰:** 보안 정책과 전후방 호환성은 자동으로 동시에 해결되지 않습니다. 공개 계약의 소유권, 배포 순서, 실패를 관찰할 지표가 필요합니다.
- **검증:** 구버전/신버전 payload 조합을 contract test 표로 만들고, 허용하지 않은 속성은 어느 버전에서도 조용히 버려지지 않는지 확인합니다.
- **완료 기준:** 클라이언트가 안전하게 이전할 기간과 서버가 구계약을 제거할 조건을 제시할 수 있습니다.

## Pro 4 — 위협 모델로 다형성 API 검토하기

공격자가 `type`, 매우 긴 문자열, 중복 속성, 잘못된 날짜, 같은 ID의 병렬 요청을 조작한다고 가정합니다.

- **변경:** 각 위협에 대해 예방, 탐지, 복구 통제를 표로 작성하고 빠진 제한 하나를 테스트와 함께 구현합니다. 요청 본문 크기나 문자열 길이 제한을 추가한다면 합리적인 근거도 기록합니다.
- **관찰:** `[JsonDerivedType]` 허용 목록은 임의 형식 생성을 막지만 입력 크기, 업무 값, 저장 경쟁까지 모두 해결하지는 않습니다. 계층별 통제가 겹쳐야 합니다.
- **검증:** 공격 payload가 5xx나 프로세스 종료를 만들지 않고, 저장소에 부분 상태를 남기지 않으며, 오류 응답이 구현 세부를 노출하지 않는지 확인합니다.
- **완료 기준:** JSON parser, Presentation, Domain, Application, Repository 각각이 막는 위협을 한 장의 표로 설명할 수 있습니다.

---

## C# 15 Preview 읽기 과제 — 닫힌 계층과 union

C# 15 Preview에서 논의·제공되는 **닫힌 형식 계층(closed hierarchy)** 및 **union 계열 표현**은 “가능한 경우의 수가 이 목록 안에 있다”는 의도를 언어와 compiler가 더 직접 이해하게 만드는 방향입니다. 오늘 예제의 세 파생 이벤트 허용 목록, exhaustive pattern matching, 새 사례 추가 시 compiler 도움과 연결해 개념적으로 비교해 보세요.

- 이 절은 **설명과 비교만**을 위한 과제입니다.
- 오늘 실행 프로젝트는 `net10.0`과 Stable C# 14를 유지합니다.
- Preview SDK 설치, `<LangVersion>preview</LangVersion>` 변경, Preview 문법 복사는 하지 않습니다.
- Preview 문법과 세부 동작은 출시 전 바뀔 수 있으므로 오늘 코드를 대체하는 확정 설계로 취급하지 않습니다.

생각해 볼 질문: 언어 수준에서 계층이 닫혀 있어도 JSON에서 허용할 discriminator와 공개 계약 목록을 별도로 관리해야 하는 이유는 무엇일까요?

---

## 전체 완료 체크리스트

- [ ] 기준 build, self-test, 실제 HTTP 검증을 모두 실행했다.
- [ ] 정상 입력뿐 아니라 unknown, duplicate, casing, missing, null, unknown discriminator를 분리해 검증했다.
- [ ] JSON DTO와 불변 Domain Model의 책임을 구분했다.
- [ ] 예상 실패는 Result, 장애는 예외, 취소는 취소 흐름으로 유지했다.
- [ ] Mapper Strategy, Application Service, Repository Port/Adapter의 이유를 설명한다.
- [ ] 모든 구체 구현 연결이 Composition Root에 모여 있다.
- [ ] 한 event ID가 저장된 뒤 같은 ID의 후속 요청은 `409`가 되어 한 프로세스에 최대 한 건만 남고, 이것이 exactly-once 보장은 아님을 설명한다.
- [ ] 로그와 오류 응답에 원본 JSON이나 민감정보가 노출되지 않는다.
- [ ] Stable C# 14 실행 코드와 C# 15 Preview 설명을 섞지 않았다.
