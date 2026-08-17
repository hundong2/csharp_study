# 6. C# record 복사, Fetch Metadata 보안, .NET 11 JIT 성능

원문:

- [Adding a Clone method to a C# record](https://www.meziantou.net/adding-a-clone-method-to-a-csharp-record.htm)
- [Understanding the Fetch Metadata HTTP headers](https://andrewlock.net/understanding-the-fetch-metadata-http-headers-sec-fetch-site-and-friends/)
- [.NET 11 Performance Edition](https://steven-giesel.com/blogPost/86620358-bb91-4295-84fc-a1329b2567ae)

## record의 clone semantics

```csharp
public record Sample(string Value);
var copy = sample with { };
```

record에는 `with`가 사용하는 compiler-generated clone behavior가 있어 public instance `Clone()`을 직접 선언할 수 없습니다. discoverable API가 필요하면 extension method가 built-in semantics를 보존합니다.

```csharp
public static Sample Clone(this Sample sample) => sample with { };
```

`with`는 기본적으로 **shallow copy**입니다. record 자체는 새 instance지만 내부 `List<T>`, array, mutable object 참조는 원본과 같습니다. deep copy가 필요하면 nested object를 각각 복사하고 cycle, identity, resource handle semantics를 정의해야 합니다. `ICloneable`은 deep/shallow 계약이 불명확해 public API에서는 구체적 copy method/constructor가 더 명확할 수 있습니다.

compiler는 record class에 copy constructor와 synthesized clone member를 만들고 `with`는 clone 뒤 member initializer를 적용합니다. record struct는 value copy입니다. JIT는 작은 extension wrapper를 inline할 수 있지만 clone object allocation 자체가 항상 사라진다는 뜻은 아닙니다.

## Fetch Metadata 4개 header

browser가 설정하고 JavaScript가 수정할 수 없는 `Sec-` request header는 server가 request provenance와 response destination을 판단하도록 돕습니다.

| header | 뜻 | 대표 값 |
|---|---|---|
| `Sec-Fetch-Dest` | response가 쓰일 위치 | `document`, `image`, `style`, `script`, `empty` |
| `Sec-Fetch-Site` | initiator와 destination 관계 | `same-origin`, `same-site`, `cross-site`, `none` |
| `Sec-Fetch-Mode` | request mode | `navigate`, `same-origin`, `cors`, `no-cors`, `websocket` |
| `Sec-Fetch-User` | user activation 여부 | `?1` 또는 header 없음 |

**same-origin**은 scheme/host/port가 모두 같고 **same-site**는 scheme과 registrable domain 기준이라 subdomain/port 차이를 허용할 수 있습니다. CORS는 browser가 cross-origin response를 JavaScript에 공개할지 결정하는 정책이지 server-side authentication/CSRF 방어 그 자체가 아닙니다.

### Resource Isolation Policy

state-changing endpoint가 `Sec-Fetch-Site: cross-site`인 request를 기본 거부하되 legitimate top-level navigation, webhook, older/non-browser client 등 application 계약의 exception을 정의할 수 있습니다. 먼저 report-only log로 실제 traffic을 관찰하고 rollout합니다.

Fetch Metadata는 defense-in-depth입니다. anti-forgery token, SameSite cookie, Origin/Referer 검증, authentication/authorization, CORS를 상황에 맞게 함께 사용합니다. header가 없다고 무조건 허용/거부할지는 client population에 따라 정합니다. reverse proxy가 header를 제거/덮지 않는지도 확인합니다. .NET 11은 이 기반의 CSRF protection을 도입하는 방향입니다.

## .NET 11 Preview 6 성능 사례

원문 benchmark 환경은 Apple M2 Pro Arm64, .NET 10.0.10 대 .NET 11 Preview 6, BenchmarkDotNet preview입니다. 핵심은 숫자를 복사하는 것이 아니라 JIT/library change가 workload에서 어떤 비용을 없앴는지 이해하는 것입니다.

### generic `Enum.Equals` boxing 제거

`where T : struct, Enum`의 `left.Equals(right)`를 JIT가 special-case하여 .NET 10 예시의 48B boxing allocation을 없애고 code size도 줄였습니다. Boxing은 value를 heap object로 감싸 type method dispatch를 가능하게 하지만 allocation/GC 비용이 있습니다.

### redundant Span/null/range check 제거

JIT가 loop와 `Span` slice 관계를 증명하면 중복 bounds/null check를 제거합니다. 잘못된 code를 unsafe하게 만드는 것이 아니라 앞선 check가 이후 access를 지배한다는 사실을 compiler IR에서 증명합니다.

### time zone/DateTime.Now

연도별 time-zone rule cache와 platform rule 처리 개선으로 사례상 빨라졌습니다. OS별 timezone database와 DST rule 수가 달라 결과 차이가 큽니다. `DateTime.Now`를 성능 때문에 무조건 cache하면 시간 정확성과 thread-safety 문제가 생길 수 있습니다.

### Guid parsing

character→byte decode와 failure path 배치 개선으로 여러 `Parse/TryParse` 사례가 약 20~25% 개선되었습니다. 실패가 흔한 workload, input format, architecture에서는 다를 수 있습니다.

### LINQ Min/Max SIMD horizontal reduction

vector lane을 scalar min/max로 줄일 때 lane별 loop 대신 `Shuffle`+`Min/Max` tree를 활용합니다. lane 수 `N`이면 대략 `log2(N)` stage로 비교할 수 있습니다. `Vector128.Shuffle`은 random shuffle이 아니라 index vector에 따른 고정 permutation입니다. data type/length/ISA에 따라 개선·동률·회귀 사례가 모두 있었으므로 직접 측정합니다.

## 올바른 benchmark

- Release, debugger 미부착, 동일 hardware/power mode/background load
- warmup·여러 iteration·통계·outlier와 allocation/code size 확인
- 결과를 소비해 dead-code elimination 방지
- runtime/SDK/architecture/OS/commit 기록
- microbenchmark 뒤 production trace와 end-to-end SLO 확인
- 한 번의 `Stopwatch`는 학습 관찰일 뿐 결론이 아님

Tiered JIT은 빠른 Tier 0 뒤 hot method를 Tier 1/Dynamic PGO로 다시 compile할 수 있고 OSR은 긴 loop 중간에 optimized code로 옮길 수 있습니다. benchmark harness가 warmup과 process 수명을 통제해야 하는 이유입니다.

## 실습

```powershell
dotnet script .\07_RecordSecurityJit.csx
```

shallow/deep copy 차이, Fetch Metadata policy, boxing allocation과 SIMD 결과를 관찰합니다.

## 다음 단계

- 이전: [Foundry Local 음성 AI](./05-local-speech-ai.md)
- 다음: [15개 링크 커버리지](./07-link-coverage.md), [연습문제](./08-exercises.md)
- 기반 심화: [.NET 11 CLR/JIT](../dotnet-11-preview-6/01-foundations-clr-jit.md)
