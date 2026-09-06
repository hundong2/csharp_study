# 실습 문제 — 상품 가격 TTL 캐시

모든 문제는 저장소 루트에서 아래 명령으로 검증합니다.

```powershell
dotnet build dailyStudy/exercise/20260907/src/PriceCacheExercise/PriceCacheExercise.csproj -c Release
dotnet run --project dailyStudy/exercise/20260907/src/PriceCacheExercise/PriceCacheExercise.csproj -- --self-test
```

작업 전 현재 `7/7` 통과를 확인하고, 문제마다 테스트를 먼저 하나 추가한 뒤 구현을 바꿔 보세요.

## Beginner — 실행 흐름과 기본 문법

### 1. TTL을 2분으로 바꾸기

`Program.Main`의 데모 TTL을 5분에서 2분으로 바꾸고, `RunDemoAsync`의 수동 시계 이동도 2분으로 맞추세요.

완료 조건:

- 첫 `LAPTOP-15`는 `원본`, 두 번째는 `캐시`입니다.
- 정확히 2분 이동한 뒤 다시 `원본`입니다.
- 빌드 경고와 오류가 없습니다.

생각할 점: 왜 `FixedTtlFreshnessPolicy.IsFresh`는 `age <= Lifetime`이 아니라 `age < Lifetime`일까요?

### 2. SKU 검증 사례 추가하기

`SelfTests.InvalidSkuSkipsRepositoryAsync`에 아래 입력을 추가로 검증하세요.

```text
-START
END-
ABC?
```

각 입력은 실패하고 Repository 호출 횟수는 계속 0이어야 합니다. 반복문이나 LINQ `All` 중 하나를 선택하고, 선택 이유를 한글 주석으로 남기세요.

## Intermediate — 캐시 기능 확장

### 3. 명시적 무효화 추가하기

`CachedPriceProvider`에 다음 책임의 메서드를 설계하세요.

```csharp
public bool Invalidate(ProductSku sku)
```

요구사항:

- 캐시 가격만 제거하고 `_gates`의 `SemaphoreSlim`은 제거하거나 Dispose하지 않습니다.
- 제거한 항목이 있으면 `true`, 없으면 `false`입니다.
- null은 프로그래머 오류로 처리합니다.
- 메서드 위에 목적, 매개변수, 반환값, 왜 gate를 유지하는지 한글 주석을 씁니다.
- `miss → hit → Invalidate → 원본`을 검증하는 자체 테스트를 추가합니다.

힌트: `ConcurrentDictionary.TryRemove(key, out _)`의 `_`는 꺼낸 값을 사용하지 않겠다는 discard입니다.

### 4. 가격 종류별 TTL Strategy 만들기

`ICacheFreshnessPolicy` 구현을 하나 더 만드세요.

- 기본 TTL은 5분입니다.
- `FLASH-`로 시작하는 SKU는 30초 TTL입니다.

현재 Strategy 계약에는 SKU가 전달되지 않습니다. 다음 중 하나를 선택하세요.

1. `IsFresh(ProductSku sku, DateTimeOffset freshAsOfUtc, DateTimeOffset nowUtc)`로 계약을 확장합니다.
2. 가격 종류별 `CachedPriceProvider`를 Composition Root에서 따로 조립합니다.

README에 선택의 장단점을 두 문장으로 적고, 30초 직전/정확한 경계 테스트를 추가하세요. `Thread.Sleep`이나 실제 `Task.Delay`로 30초를 기다리면 안 됩니다.

## Advanced — 동시성 경계 강화

### 5. 만료 뒤 동시 refresh 회귀 테스트

현재 같은 SKU 동시 테스트는 빈 캐시에서 시작합니다. 다음 시나리오를 새 테스트로 작성하세요.

1. 첫 조회로 캐시를 채웁니다.
2. `ManualTimeProvider`를 정확히 5분 이동합니다.
3. 같은 SKU 동시 요청 20개를 시작합니다.
4. 원본 Repository가 느리게 동작하는 동안 follower가 gate에서 기다리게 합니다.
5. 성공 원본 refresh가 정확히 1회인지 확인합니다.

실제 시간 지연 대신 `TaskCompletionSource` 장벽을 사용하세요. 테스트가 멈췄을 때만 `WaitAsync(TimeSpan.FromSeconds(2))` 제한으로 실패하게 합니다.

### 6. 전체 원본 동시성 제한 추가하기

SKU별 gate는 서로 다른 SKU를 막지 않습니다. 원본 시스템이 동시에 두 요청만 감당한다고 가정하고 별도의 전역 `SemaphoreSlim(2, 2)`을 추가하세요.

완료 조건:

- 같은 SKU 성공 refresh는 여전히 한 번입니다.
- 서로 다른 SKU 세 개를 동시에 조회해도 Repository 안에 동시에 들어간 최대 수가 2입니다.
- SKU별 gate와 전역 제한을 획득·해제하는 순서가 모든 경로에서 일관됩니다.
- 취소된 대기자가 획득하지 않은 semaphore를 `Release`하지 않습니다.

주의: 두 semaphore를 중첩하면 교착 상태 가능성을 검토해야 합니다. 이 구현에서는 항상 SKU gate를 먼저, 전역 제한을 두 번째로 얻는 식으로 순서를 하나로 정하세요.

## Pro — 실무 대안과 운영 설계

### 7. HybridCache 대안 설계하기

코드를 바로 교체하기 전에 Microsoft의 [.NET 캐싱 개요](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching)를 읽고 `DESIGN-NOTES.md`를 오늘 폴더에 작성하세요.

아래 항목을 비교합니다.

| 질문 | 직접 구현 | HybridCache 후보 |
| --- | --- | --- |
| stampede protection은 어디까지 적용되는가? | | |
| 단일 서버/다중 서버에서 어떤 저장소를 쓰는가? | | |
| 직렬화와 payload 크기는 누가 제한하는가? | | |
| 태그 기반 무효화가 필요한가? | | |
| 장애 시 stale 값을 허용할 것인가? | | |
| 관측할 hit/miss/latency 지표는 무엇인가? | | |

그다음 `IPriceProvider` 계약을 유지한 채 HybridCache 기반 Decorator로 바꾸는 마이그레이션 순서를 다섯 단계 이내로 적으세요. Preview API가 필요하다면 실행 코드에 섞지 말고 별도 구역에 표시합니다.

### 8. 실패 공유 방식 설계하기

현재 구현은 실패를 캐시하지 않으므로 leader가 실패하면 대기자들이 차례로 원본을 다시 호출할 수 있습니다. 다음 두 정책을 비교하세요.

- 오류를 아주 짧게 negative cache
- 진행 중인 `Task<Result<PriceLookup>>` 자체를 같은 요청들과 공유

다음 질문에 답한 뒤 하나를 구현하고 자체 테스트를 추가합니다.

- 취소 토큰은 leader 것과 follower 것 중 어느 것을 원본 호출에 적용할 것인가?
- 한 follower의 취소가 공유 작업을 취소해야 하는가?
- 실패를 얼마나 오래 공유해야 장애 복구를 늦추지 않는가?
- 예외가 난 Task를 사전에서 언제 제거할 것인가?

완료 조건은 같은 SKU 동시 실패 10건의 원본 호출 횟수와 각 caller의 결과가 테스트 이름만 읽어도 분명한 것입니다.

## 보너스 — 설명할 수 있어야 완료

자신의 변경 뒤 다음 세 문장을 README에 적으세요.

1. 내가 추가한 상태를 여러 Task가 공유해도 안전한 이유
2. 취소나 예외가 나도 semaphore가 풀리는 코드 경로
3. 이 구현이 여러 서버 인스턴스에서 보장하지 못하는 것
