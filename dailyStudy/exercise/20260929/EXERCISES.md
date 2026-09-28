# 2026-09-29 연습문제 — Health Checks Beginner to Pro

각 단계가 끝날 때마다 다음 명령으로 회귀를 확인하세요.

```powershell
$project = 'dailyStudy/exercise/20260929/src/ServiceHealthApi/ServiceHealthApi.csproj'
dotnet build $project -c Release
dotnet run --project $project -c Release --no-build -- --self-test
```

## Level 1 — Beginner: 상태 계약 읽기

1. 서버 실행 직후 `/health/live`, `/health/startup`, `/health/ready`를 호출합니다.
2. `StartupWarmup__DelayMilliseconds`를 `0`, `1000`, `5000`으로 바꾸고 startup이 200으로 변하는 시점을 기록합니다.
3. [`HealthEndpointMappings.cs`](./src/ServiceHealthApi/HealthChecks/HealthEndpointMappings.cs)에서 liveness predicate를 `registration => registration.Tags.Contains(HealthTags.Ready)`로 잠깐 바꿉니다.
4. inventory 장애 중 liveness도 503이 되는지 확인하고, 왜 위험한지 한 문장으로 씁니다.
5. 원래 `_ => false`로 되돌린 뒤 자체 테스트를 통과시킵니다.

**완료 기준:** liveness에 외부 의존성을 넣으면 재시작 폭풍이 생길 수 있다는 이유를 설명한다.

## Level 2 — Beginner+: 기본 문법과 불변성

`DependencyObservation.Create`에 다음 규칙을 추가하세요.

- 앞뒤 공백은 정규화하되, 정규화된 이름의 내부 문자는 영문 소문자, 숫자, `-`만 허용합니다.
- 길이는 3~30자입니다.
- 잘못된 문자는 `dependency.name.invalid`, 길이 오류는 `dependency.name.length` Result로 반환합니다.

자체 테스트에 최소 네 assertion을 추가합니다.

- 경계 길이 3 성공
- 경계 길이 30 성공
- 길이 2 실패
- 내부 공백 또는 `_` 포함 실패

**힌트:** `normalizedName.All(character => ...)`, 관계 pattern `is < 3 or > 30`을 사용해 보세요.

## Level 3 — Intermediate: 새 Probe Port/Adapter 추가

선택 dependency `search`를 추가하세요.

1. `Program.cs`에서 `IDependencyProbe` singleton을 등록합니다.
2. 기본 상태에서는 readiness가 Healthy이고 check는 여전히 framework 등록 두 개임을 확인합니다.
3. `/demo/dependencies/search/unavailable` 후 readiness가 `200 Degraded`인지 검증합니다.
4. `/demo/probes`에 search counter가 포함되는지 확인합니다.
5. 자체 테스트와 PowerShell verifier에 회귀 assertion을 추가합니다.

**완료 기준:** Application Service를 수정하지 않고 DI 등록만으로 probe가 확장된다.

## Level 4 — Intermediate+: Policy Strategy 교체

`MinimumOptionalAvailabilityPolicy`를 새로 구현하세요.

- 필수 장애가 있으면 항상 `Unhealthy`입니다.
- 선택 dependency가 하나만 실패하면 `Degraded`입니다.
- 선택 dependency가 두 개 이상 실패하면 `Unhealthy`입니다.

DI에서 `IReadinessPolicy` 구현을 새 Strategy로 바꾸고 아래를 검증합니다.

| 상황 | 기대 상태 |
| --- | --- |
| 전체 정상 | Healthy |
| 선택 1개 실패 | Degraded |
| 선택 2개 실패 | Unhealthy |
| 필수 1개 실패 | Unhealthy |

**완료 기준:** probe Adapter와 `ApplicationReadinessHealthCheck`를 고치지 않고 정책만 교체한다.

## Level 5 — Advanced: Probe별 timeout을 옵션으로 이동

현재는 각 `IDependencyProbe.Timeout`이 Composition Root에 고정되어 있습니다. 동작 계약을 보존하면서 검증된 구성으로 이동하세요.

1. `DependencyProbeOptions`에 inventory/recommendations budget을 둡니다.
2. 두 값이 10~1000ms인지 `ValidateOnStart`로 fail-fast 검증합니다.
3. 전체 health registration timeout은 가장 긴 개별 budget보다 충분히 크게 유지합니다.
4. 선택 probe timeout은 `Degraded`, 필수 probe timeout은 `Unhealthy`인 회귀 테스트를 유지합니다.
5. 개별 budget 취소와 상위 호출자 취소를 구분하고, 상위 취소는 반드시 다시 던집니다.
6. 모든 linked `CancellationTokenSource`를 `using`으로 정리합니다.

**주의:** `OperationCanceledException`을 무조건 잡으면 사용자가 요청을 취소한 경우까지 정상 결과로 오해할 수 있습니다. “누가 취소했는가”를 token 상태로 구분하세요.

## Level 6 — Pro: 운영 관리 endpoint 분리

다음 운영 설계를 문서와 코드로 구현해 보세요.

- health endpoint를 사용자 API와 다른 management port에 둡니다.
- 공개 liveness는 최소 정보만, 인증된 상세 diagnostics는 별도 endpoint로 둡니다.
- management port에는 host filtering과 network policy를 적용한다고 가정합니다.
- scrape 주기, timeout, failure threshold를 표로 작성합니다.
- 100개 replica가 같은 DB를 동시에 probe할 때 부하를 줄일 방법을 제안합니다.

**완료 기준:** `RequireHost` 하나를 완전한 보안 경계라고 가정하지 않고, 네트워크·인증·최소 공개를 함께 설명한다.

## Level 7 — Pro+: 설계 선택 비교

다음 질문에 각각 3~5문장으로 답하세요.

1. dependency 상태를 예외가 아니라 Domain 관찰 값으로 표현한 이유는 무엇인가?
2. 이 예제에 Repository를 넣지 않은 이유는 무엇인가?
3. probe를 직렬 실행할 때와 `Task.WhenAll`로 병렬 실행할 때의 latency·부하 trade-off는 무엇인가?
4. readiness가 503인 동안 진행 중인 기존 요청은 어떻게 다뤄야 하는가?
5. startup check와 readiness check를 영원히 같은 것으로 취급하면 어떤 문제가 생기는가?

정답은 하나가 아니지만, 가정과 운영 결과가 분명해야 합니다.
