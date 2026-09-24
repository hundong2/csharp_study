# Beginner → Pro 연습문제: ASP.NET Core Output Caching 공지 피드

각 단계는 앞 단계의 결과를 이어서 사용합니다. 한 단계가 끝날 때마다 아래 명령으로 빌드와 자체 검증을 다시 실행하세요.

```powershell
dotnet build .\dailyStudy\exercise\20260925\src\AnnouncementCacheApi\AnnouncementCacheApi.csproj -c Release --nologo
dotnet run --project .\dailyStudy\exercise\20260925\src\AnnouncementCacheApi\AnnouncementCacheApi.csproj -c Release --no-build -- --self-test
```

HTTP 동작까지 바꾼 단계에서는 서버를 실행한 뒤 검증 스크립트도 실행합니다.

첫 번째 PowerShell 창에서 서버를 시작합니다.

```powershell
dotnet run --project .\dailyStudy\exercise\20260925\src\AnnouncementCacheApi\AnnouncementCacheApi.csproj -c Release --no-build -- --urls http://127.0.0.1:5196
```

서버가 실행 중인 동안 **두 번째 PowerShell 7 이상 창**에서 다음 검증을 실행합니다.

```powershell
.\dailyStudy\exercise\20260925\verify-http.ps1
```

> 규칙: 새로 작성하는 모든 메서드 위에는 역할·파라미터·반환값을 설명하는 한글 주석을 씁니다. `string?`, `var`, `=>`, LINQ, `async`/`await`, 패턴 매칭을 처음 쓰는 줄에는 문법의 뜻과 이 위치에서 쓰는 이유도 설명하세요.

## 1. Beginner — nullable 문자열과 불변 Domain 객체

### 목표

`string?`, 조건문, 지역 변수, `Result<T>`, 불변 객체를 사용해 “검색어가 제목 또는 본문에 포함되는가?”라는 작은 Domain 규칙을 구현합니다. HTTP나 캐시를 건드리기 전에 순수 C# 코드로 규칙을 검증하는 단계입니다.

### 정확한 파일

- 수정: [`Domain/Announcement.cs`](./src/AnnouncementCacheApi/Domain/Announcement.cs)
- 수정: [`SelfTesting/SelfTestRunner.cs`](./src/AnnouncementCacheApi/SelfTesting/SelfTestRunner.cs)
- 읽기: [`Domain/Result.cs`](./src/AnnouncementCacheApi/Domain/Result.cs)

### 할 일

1. `Announcement`에 `MatchesKeyword(string? keyword)` 메서드를 추가합니다.
2. `null`, 빈 문자열, 공백만 있는 문자열은 “검색 조건 없음”으로 보고 `true`를 반환합니다.
3. 나머지는 `Trim()`한 뒤 `Title` 또는 `Body`에 포함되는지 `StringComparison.OrdinalIgnoreCase`로 비교합니다. 문화권에 따라 결과가 우연히 달라지지 않게 하기 위함입니다.
4. `Announcement.Create(...)`로 정상 공지를 만든 뒤 `Result<Announcement>`가 성공인지 먼저 확인하고, 성공 값으로 검색 메서드를 검증합니다. 실패 결과에서 `Value`를 읽지 마세요.
5. 제목 일치, 본문 일치, 대소문자 차이, 불일치, `null`, 공백을 각각 자체 검증에 추가합니다.
6. 같은 입력으로 `MatchesKeyword`를 여러 번 호출해도 객체의 `Title`, `Body`, `Category`, `PublishedAtUtc`가 바뀌지 않는지 확인합니다.

### 성공 조건

- Release 빌드 경고 0, 오류 0
- 위 여섯 검색 경계가 모두 통과함
- `string`과 `string?`의 차이, `Result<T>`를 확인한 뒤 `Value`를 읽는 이유를 말할 수 있음
- 검색 메서드가 I/O나 현재 시각에 의존하지 않는 순수 함수이며 `Announcement`의 불변성을 깨지 않음

## 2. Junior — query/header별로 정확히 분리되는 캐시 키

### 목표

Beginner의 검색 규칙을 `GET /announcements`에 연결하고, 응답을 바꾸는 모든 요청 입력을 Output Cache 키에 포함합니다. query string과 `Accept-Language`가 빠졌을 때 다른 사용자의 응답이 섞이는 이유를 직접 검증합니다.

### 정확한 파일

- 수정: [`Application/AnnouncementApplicationService.cs`](./src/AnnouncementCacheApi/Application/AnnouncementApplicationService.cs)
- 수정: [`Presentation/AnnouncementEndpoints.cs`](./src/AnnouncementCacheApi/Presentation/AnnouncementEndpoints.cs)
- 수정: [`Presentation/CacheConstants.cs`](./src/AnnouncementCacheApi/Presentation/CacheConstants.cs)
- 수정: [`Program.cs`](./src/AnnouncementCacheApi/Program.cs)
- 수정: [`SelfTesting/SelfTestRunner.cs`](./src/AnnouncementCacheApi/SelfTesting/SelfTestRunner.cs)
- 참고: [`Presentation/LanguagePreferenceParser.cs`](./src/AnnouncementCacheApi/Presentation/LanguagePreferenceParser.cs)

### 할 일

1. `AnnouncementApplicationService.GetFeedAsync`에 `string? keyword` 파라미터를 추가하고, category 필터 뒤에 `MatchesKeyword` 필터를 적용합니다. 모든 호출부를 함께 수정합니다.
2. `GET /announcements?category=...&keyword=...`가 두 nullable query 값을 바인딩하도록 endpoint를 확장합니다.
3. 이름이 `CacheNames.AnnouncementFeedPolicy`인 정책의 `VaryByValue` 복합 값에 정규화된 `category`와 `keyword`를 포함합니다. 기본 raw query 변형은 계속 비워 두고, `none`/`valid:`/`invalid` 접두사가 충돌하지 않게 합니다.
4. 언어에 따라 `CategoryLabel`이 달라지므로 기존 `ko`/`en` 의미 값도 유지합니다. `Authorization`을 vary key로 추가해 개인 응답을 캐시하는 방식은 사용하지 않습니다.
5. 동일한 category·keyword·언어를 연속 요청하면 두 번째 응답이 캐시에서 나오고, repository read 횟수가 늘지 않는지 확인합니다.
6. category만 바꾼 요청, keyword만 바꾼 요청, `Accept-Language: ko`와 `en` 요청이 서로 다른 본문을 반환하고 각각 첫 요청에서만 원본을 읽는지 확인합니다.
7. `keyword=cache`와 `keyword=CACHE`, 앞뒤 공백처럼 업무 의미가 같은 입력을 canonical value로 바꾸고 같은 `OriginReadNumber`를 재사용하는지 검사합니다. 잘못된 keyword가 정상 key와 충돌하지 않게 상태 접두사도 둡니다.

### 성공 조건

- category·keyword·언어가 같은 요청만 같은 캐시 항목을 공유함
- 한국어 응답에 영어 `CategoryLabel`이 섞이거나, 다른 검색어의 항목이 섞이지 않음
- 같은 캐시 키를 두 번째로 조회할 때 `OriginReadNumber`와 repository read 횟수가 그대로임
- 응답을 바꾸는 입력과 단순 추적용 헤더를 구분하고, 왜 모든 헤더를 vary key로 쓰면 안 되는지 설명할 수 있음

## 3. Intermediate — tag 무효화와 성공한 POST의 경계

### 목표

캐시 항목 하나의 키를 추측해 지우지 않고, `CacheTags.AnnouncementFeed` tag로 관련 피드를 함께 무효화합니다. 쓰기 실패에는 기존 캐시를 보존하고, 저장 성공 뒤에만 무효화하는 Application/HTTP 경계를 테스트합니다.

### 정확한 파일

- 수정: [`Presentation/AnnouncementEndpoints.cs`](./src/AnnouncementCacheApi/Presentation/AnnouncementEndpoints.cs)
- 수정: [`SelfTesting/SelfTestRunner.cs`](./src/AnnouncementCacheApi/SelfTesting/SelfTestRunner.cs)
- 확인: [`Presentation/CacheConstants.cs`](./src/AnnouncementCacheApi/Presentation/CacheConstants.cs)
- 확인: [`Application/AnnouncementApplicationService.cs`](./src/AnnouncementCacheApi/Application/AnnouncementApplicationService.cs)
- 확인: [`Application/Ports/IAnnouncementRepository.cs`](./src/AnnouncementCacheApi/Application/Ports/IAnnouncementRepository.cs)
- 확인: [`Infrastructure/InMemoryAnnouncementRepository.cs`](./src/AnnouncementCacheApi/Infrastructure/InMemoryAnnouncementRepository.cs)

### 할 일

1. feed 정책의 모든 변형에 `CacheTags.AnnouncementFeed` tag가 붙는지 확인합니다. 문자열 리터럴을 endpoint 여러 곳에 복사하지 않습니다.
2. `POST /announcements`가 먼저 `AnnouncementApplicationService.CreateAsync`를 호출하고 그 `Result<LocalizedAnnouncement>`를 분기하게 합니다.
3. `Result`가 성공하여 Repository 저장까지 끝난 경우에만 `AnnouncementFeedCacheGeneration.Advance()`를 먼저 호출하고, 이어 `IOutputCacheStore.EvictByTagAsync(CacheTags.AnnouncementFeed, ...)`로 이전 entry를 정리합니다.
4. Domain 검증 실패는 400으로 반환하되 tag를 지우지 않습니다. 예상 가능한 입력 실패를 예외로 바꾸거나, `OperationCanceledException`을 실패 `Result`로 삼키지 않습니다.
5. 다음 순서의 결정적 검사를 추가합니다.
   1. 한국어·영어·category별 feed를 각각 한 번씩 읽어 캐시를 채웁니다.
   2. 유효한 POST를 한 번 보냅니다.
   3. 각 feed를 다시 읽어 원본 read 횟수가 늘고, 해당되는 feed에는 새 공지가 보이는지 확인합니다.
   4. 캐시를 다시 채운 뒤 빈 제목의 POST를 보냅니다.
   5. 같은 feed를 다시 읽어 본문과 원본 read 횟수가 그대로인지 확인합니다.
6. 요청 취소 token은 Repository 저장이 끝나기 전까지만 전달합니다. commit 뒤 cache 정리는 `ApplicationStopping`과 짧은 내부 timeout을 연결한 server-owned token을 사용합니다. generation은 single-process 정확성을 지키고, 분산 환경의 durable invalidation은 Pro 단계에서 다룹니다.

### 성공 조건

- 성공한 POST 뒤에는 새 generation key를 사용하고 모든 이전 `announcement-feed` tag 변형을 정리해 최신 데이터를 읽음
- 실패한 POST 뒤에는 기존 cache hit가 유지되고 불필요한 원본 read가 없음
- tag는 cache entry의 분류표이며 Repository 데이터를 지우는 기능이 아님을 설명할 수 있음
- Domain 실패, 취소, 예상하지 못한 인프라 예외를 서로 다른 경로로 다룸

## 4. Senior — resource locking과 인증 응답 안전성

### 목표

동일한 cold key로 요청이 몰려도 한 요청만 원본을 계산하게 해 cache stampede를 줄입니다. 동시에 인증된 사용자별 응답을 “사용자 헤더로 vary하면 안전하다”라고 오해하지 않도록 실제 격리 검사를 만듭니다.

### 정확한 파일

- 수정: [`Program.cs`](./src/AnnouncementCacheApi/Program.cs)
- 수정: [`Presentation/AnnouncementEndpoints.cs`](./src/AnnouncementCacheApi/Presentation/AnnouncementEndpoints.cs)
- 수정: [`Presentation/AnnouncementContracts.cs`](./src/AnnouncementCacheApi/Presentation/AnnouncementContracts.cs)
- 수정: [`Presentation/CacheConstants.cs`](./src/AnnouncementCacheApi/Presentation/CacheConstants.cs)
- 수정: [`Infrastructure/InMemoryAnnouncementRepository.cs`](./src/AnnouncementCacheApi/Infrastructure/InMemoryAnnouncementRepository.cs)
- 수정: [`SelfTesting/SelfTestRunner.cs`](./src/AnnouncementCacheApi/SelfTesting/SelfTestRunner.cs)
- 필요할 때 새로 생성: `SelfTesting/TestAuthenticationHandler.cs`

### 할 일

1. `CacheNames.AnnouncementFeedPolicy`에 resource locking이 켜져 있음을 코드와 테스트로 명시합니다. 대상 SDK가 제공하는 `SetLocking(true)`를 사용하고, 끄는 실험은 별도 정책에서만 합니다.
2. 자체 검증용 Repository에 첫 read를 멈출 수 있는 `TaskCompletionSource` gate와 read counter를 둡니다. `Thread.Sleep`으로 우연한 타이밍을 만들지 않습니다.
3. 비어 있는 동일 key에 20개 GET을 동시에 시작하고, 모두 대기한 것을 확인한 뒤 gate를 엽니다. 모든 응답 본문이 같고 원본 read가 정확히 한 번인지 검사합니다.
4. category 또는 `Accept-Language`가 다른 두 key는 서로를 막지 않으며, key마다 한 번씩 원본을 읽는지 검사합니다.
5. 테스트 전용 인증 handler로 Alice와 Bob의 `ClaimsPrincipal`을 만들고, 사용자 이름을 포함하는 `/announcements/mine` endpoint를 추가합니다. 테스트 handler는 self-test host에만 등록하고 운영 경로에서 임의 헤더를 신원으로 신뢰하지 않습니다.
6. `/announcements/mine`은 Output Cache를 적용하지 않는 것을 기본 선택으로 합니다. 학습 실험으로 feed 정책을 잠시 적용하더라도 ASP.NET Core 기본 정책이 인증 요청을 저장하지 않는지, Alice 응답이 Bob에게 재사용되지 않는지 확인한 뒤 안전한 상태로 되돌립니다.
7. `Authorization` 또는 cookie를 vary key로 추가하는 것을 보안 대책으로 삼지 않습니다. 토큰·세션 값은 민감하고 cardinality가 크며, 잘못된 인증 판정 하나가 정보 노출로 이어질 수 있습니다.

### 성공 조건

- 동일 anonymous cold key에 20개 요청이 몰려도 원본 read는 1회임
- 서로 다른 key는 독립적으로 계산되어 불필요한 전역 lock이 없음
- 반복한 인증 요청은 Output Cache hit로 사용자 본문을 공유하지 않으며 Alice와 Bob의 값이 절대 섞이지 않음
- resource locking은 한 process의 동시 생성 비용을 줄일 뿐, 권한 검사나 여러 node의 데이터 일관성을 해결하지 않는다고 설명할 수 있음

## 5. Pro — Redis 다중 node와 일관성 실패를 잡는 통합 테스트

### 목표

두 API node가 같은 Redis Output Cache store를 사용하도록 구성하고, 한 node의 성공한 쓰기가 다른 node의 tag cache를 무효화하는지 검증합니다. 또한 “공유 cache”와 “공유 source of truth”는 별개이며 DB commit과 cache invalidation 사이의 실패 창이 있음을 테스트로 드러냅니다.

### 정확한 파일

- 수정: [`AnnouncementCacheApi.csproj`](./src/AnnouncementCacheApi/AnnouncementCacheApi.csproj)
- 수정: [`Program.cs`](./src/AnnouncementCacheApi/Program.cs)
- 수정: [`Presentation/CacheConstants.cs`](./src/AnnouncementCacheApi/Presentation/CacheConstants.cs)
- 수정: [`Presentation/AnnouncementEndpoints.cs`](./src/AnnouncementCacheApi/Presentation/AnnouncementEndpoints.cs)
- 수정: [`SelfTesting/SelfTestRunner.cs`](./src/AnnouncementCacheApi/SelfTesting/SelfTestRunner.cs)
- 새로 생성: `SelfTesting/RedisMultiNodeVerifier.cs`
- 선택한 durable 방식에 따라 새로 생성: `Application/Ports/IAnnouncementCacheInvalidator.cs`
- 선택한 durable 방식에 따라 새로 생성: `Infrastructure/OutputCacheAnnouncementInvalidator.cs`

### 할 일

1. 대상 framework와 같은 stable 계열의 `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` package를 참조합니다. 환경 설정에 Redis 연결 문자열이 있을 때 `AddStackExchangeRedisOutputCache`를 사용하고, self-test 기본 실행은 메모리 store로 계속 가능하게 합니다.
2. node A와 B를 서로 다른 port로 띄우되 같은 Redis, 같은 application/key prefix, 같은 정책·tag 이름을 사용합니다. process-local generation을 Redis/DB의 distributed revision 또는 durable invalidation으로 교체하고 비밀 연결 문자열은 커밋하지 않습니다.
3. 두 node가 같은 cache만 공유하고 각자 `InMemoryAnnouncementRepository`를 사용하면, A의 POST 뒤 B가 tag miss 후 자기의 오래된 메모리 데이터를 다시 캐시할 수 있음을 재현합니다. 이 실패를 감추지 마세요.
4. 실제 통합 환경에서는 두 node가 같은 DB Repository adapter를 사용하게 하거나, 테스트에서는 두 host에 같은 thread-safe Repository double을 주입합니다. Redis Output Cache가 업무 데이터 복제를 대신하지 않게 합니다.
5. 다음 다중 node 시나리오를 자동화합니다.
   1. A에서 feed를 읽어 Redis를 채웁니다.
   2. B에서 같은 key를 읽고 B의 Repository를 읽지 않았음을 확인합니다.
   3. A에서 유효한 POST를 성공시켜 tag를 무효화합니다.
   4. B에서 다시 읽어 새 공지가 보이고 shared source를 한 번 읽었는지 확인합니다.
   5. 실패한 POST 뒤에는 B의 warm cache가 유지되는지 확인합니다.
6. “DB 저장 성공 직후 process 종료”를 failure injection으로 재현합니다. 허용 가능한 짧은 TTL만으로 복구할지, transactional outbox/event consumer로 tag eviction을 at-least-once 재시도할지 선택하고 그 보장 범위를 테스트 이름에 적습니다.
7. outbox를 선택했다면 업무 저장과 invalidation event를 같은 DB transaction에 기록하고, worker가 `EvictByTagAsync` 성공 뒤 event를 완료 처리하게 합니다. 동일 event 재처리는 안전해야 하며, 요청 취소 token과 worker 수명 token을 섞지 않습니다.
8. Redis 장애 시 fail-open/원본 조회 또는 요청 실패 중 어느 동작을 택할지 정하고 관측 가능한 log/metric을 추가합니다. 테스트에는 timeout을 두어 Redis가 없을 때 무한 대기하지 않게 합니다.

### 성공 조건

- A가 채운 anonymous 응답을 B가 같은 Redis key에서 재사용함
- A의 성공한 mutation/tag eviction 뒤 B가 shared source의 최신 값을 반환함
- per-process InMemory Repository를 둔 잘못된 구성은 테스트가 명확히 실패시킴
- commit과 eviction 사이의 실패를 TTL 또는 durable event라는 명시적 정책으로 회복하며, 보장하지 못하는 구간을 문서화함
- Redis 연결 문자열·인증 정보와 `bin/`, `obj/`가 Git 변경에 포함되지 않음

## 최종 복습 체크리스트

- [ ] 응답을 바꾸는 query/header를 모두 cache key에 포함했다.
- [ ] 의미가 같은 입력을 canonicalize하고 valid/invalid key 충돌을 막았다.
- [ ] 인증·cookie·개인별 응답을 캐시하지 않는 기본 안전 규칙을 훼손하지 않았다.
- [ ] 성공한 mutation 뒤에만 generation을 올리고 관련 tag를 정리한다.
- [ ] fill-after-evict 경합을 versioned key와 결정적 gate 테스트로 막았다.
- [ ] cold-key 동시성 테스트가 `Thread.Sleep`이 아니라 gate로 결정적이다.
- [ ] 취소를 업무 실패 `Result`로 숨기지 않는다.
- [ ] Redis 공유 cache와 공유 Repository의 역할을 구분한다.
- [ ] 다중 node의 commit→invalidation 실패 창에 대한 회복 전략과 테스트가 있다.
- [ ] Repository, localization Strategy, Application Service, Composition Root의 의존 방향이 유지된다.
