# 4. ASP.NET Core Preview 6

## 요청이 처리되는 큰 흐름

```text
Kestrel → middleware pipeline → routing → endpoint filter/validation
        → Minimal API/MVC/Razor/Blazor/SignalR → serializer → response
```

middleware 순서가 보안과 의미를 바꿉니다. `[ShortCircuit]`은 routing 뒤 나머지 middleware를 건너뛰므로 인증·CORS가 필요 없는 endpoint에만 사용합니다.

## 4.1 Minimal API 비동기 검증

`builder.Services.AddValidation()`을 등록하면 request model의 `AsyncValidationAttribute`와 `IAsyncValidatableObject`를 endpoint 실행 전에 처리합니다.

- 같은 member의 async attribute는 가능한 경우 함께 시작
- collection item은 병렬 검증 가능
- member → type → `IValidatableObject`의 기존 순서는 보존
- `CancellationToken`으로 client disconnect/요청 취소 전파
- 검증 실패는 endpoint body에 들어가기 전에 응답으로 변환

I/O validator는 멱등성, timeout, retry, 동시 요청 수 제한을 고려해야 합니다. “모든 field를 외부 API로 병렬 검증”하면 downstream을 과부하시킬 수 있습니다.

## 4.2 자동 cross-origin CSRF 보호

`WebApplication.CreateBuilder` 앱은 `Sec-Fetch-Site`와 `Origin` header를 기준으로 unsafe cross-origin browser request를 기본 거부합니다. Minimal API, MVC, Razor Pages, Blazor에 적용됩니다.

- same-origin, 사용자 navigation, non-browser client는 허용
- cross-origin browser가 form을 소비하려는 요청은 거부
- 기존 token antiforgery와 함께 쓰거나 일부 시나리오에서 보완 가능
- endpoint opt-out: `.DisableAntiforgery()` 또는 `[IgnoreAntiforgeryToken]`
- 앱 전체 off: `DisableCsrfProtection` 구성
- 사용자 결정: `ICsrfProtection`

CSRF는 브라우저가 cookie를 자동 전송하는 성질을 악용합니다. CORS는 response 읽기 정책이고 CSRF 방어와 동일하지 않습니다. `Origin`/Fetch Metadata 검사는 강력한 기본층이지만 위협 모델에 따라 token 기반 antiforgery를 유지합니다.

## 4.3 Blazor Virtualize scroll

- `InitialIndex`: 첫 interactive render에서 시작 item 지정, 첫 항목 flash 방지
- `ScrollToIndexAsync`: render 후 대상 item을 viewport 상단으로 이동
- 범위 밖 index는 clamp
- 겹친 호출은 last call wins
- 사용자 scroll은 프로그램 scroll보다 우선
- 첫 interactive render 전 호출은 `InvalidOperationException`

virtualization은 전체 DOM을 만들지 않고 viewport 주변만 렌더링합니다. item 높이 추정과 provider paging이 scroll 정확도·성능에 영향을 줍니다.

## 4.4 서버에서 Blazor browser option 구성

`WithBrowserOptions`로 client log level, Server reconnect, SSR DOM 보존, WebAssembly environment/culture/environment variables를 C#에서 정합니다. 서버가 페이지에 직렬화하고 Blazor script가 적용합니다.

Preview 6 rename:

- `WithBrowserConfiguration` → `WithBrowserOptions`
- `BrowserConfiguration` → `BrowserOptions`
- `ServerBrowserOptions` → `InteractiveServerBrowserOptions`
- `DisableDomPreservation` → `PreserveDom`(의미 반전)
- millisecond number → `CircuitInactivityTimeout` `TimeSpan`

Preview API를 일찍 도입하면 이런 rename 비용을 감수해야 합니다.

## 4.5 Blazor Gateway + YARP

standalone Blazor WebAssembly 개발 host인 Gateway가 YARP reverse proxy로 `/api/**`를 backend에 전달합니다. browser는 Gateway라는 same origin만 호출하므로 개발 환경에서 client/backend CORS 구성이 필요 없습니다.

Gateway는 별도 process이므로 proxy 설정은 앱 `appsettings.json`이 아니라 launch profile 환경 변수나 command-line에 둡니다. .NET service discovery를 이용하면 literal URL 대신 logical service name도 사용할 수 있습니다.

reverse proxy가 보안 경계를 없애는 것은 아닙니다. forwarded header, 인증 token, backend trust, path rewrite를 점검합니다.

## 4.6 OpenAPI 3.2 기본값

생성 문서의 기본 spec version이 3.2로 바뀝니다. 기존 생성 코드 자체는 같고, toolchain이 3.2를 지원하지 않으면 이전 버전을 명시합니다. OpenAPI 문서는 런타임이 아니라 API 계약이므로 client generator, gateway, lint tool 호환성을 함께 테스트합니다.

## 4.7 ASP.NET Core의 union

`System.Text.Json` 지원 덕분에 union을 다음에 사용할 수 있습니다.

- Minimal API body/return, `Task<Union>`, `IAsyncEnumerable<Union>`, `Results<T1,T2>`
- MVC/Razor Pages JSON request/response
- SignalR JSON hub parameter/return/stream item
- Blazor component parameter, JS interop, persisted state
- OpenAPI `anyOf` case schema

제약:

- Preview 6은 JSON body/response 중심이며 query, route, header, form binding은 미지원
- 같은 JSON shape의 case는 `[JsonUnion]` classifier 필요
- SignalR은 JSON protocol만 지원; MessagePack/Newtonsoft.Json 미지원
- Swashbuckle/NSwag 등 third-party generator는 아직 전용 union shape를 모를 수 있음

## 4.8 `[ShortCircuit]`

health check, `robots.txt`처럼 authentication, CORS 등의 middleware가 필요 없는 endpoint를 routing 직후 실행합니다.

```csharp
app.MapGet("/health", [ShortCircuit] () => "Healthy");
```

선택 status code `[ShortCircuit(404)]`도 지정할 수 있습니다. “빠르다”는 이유만으로 보호 endpoint에 붙이면 authorization을 우회할 수 있습니다.

## 4.9 SignalR authentication refresh

access token 만료 전에 연결을 끊지 않고 재인증합니다.

- server hub option `EnableAuthenticationRefresh`
- 정책 callback `OnAuthenticationRefresh`
- hub callback `OnAuthenticationRefreshedAsync`
- .NET client `WithAuthenticationRefresh`
- refresh 시점·성공·실패 callback

Preview 6에서는 .NET client가 우선 지원되고 JS/TypeScript와 Azure SignalR Service는 진행 중입니다. identity가 바뀐 뒤 group/authorization cache가 옛 claim을 가정하지 않는지 검토합니다.

## 4.10 일반 hub invocation 취소

이전에는 streaming invocation 중심이었던 client cancellation이 일반 `InvokeAsync`에도 적용됩니다. client token을 취소하면 cancellation message가 전송되고 server hub method의 `CancellationToken`이 취소됩니다.

취소는 협력적입니다. server code가 token을 받고 I/O와 loop에 전달해야 실제 일이 멈춥니다. 이미 수행된 DB commit 같은 side effect를 자동 rollback하지 않습니다.

## 4.11 file-based app용 `dotnet user-jwts`

```bash
dotnet user-jwts create --file app.cs
```

개발용 signed JWT와 signing key를 만들어 실제 identity provider 없이 인증 endpoint를 시험합니다. 개발 편의 도구이지 production token issuer가 아닙니다.

## 4.12 Preview API 변경과 수정

- `EnvironmentBoundary` → `EnvironmentView`
- `NavigationManager.GetUriWithHash` → `GetUriWithFragment`
- validation resolver가 type/parameter/property info interface로 분리
- OpenAPI nullable description/XML id/JsonSerializerOptions 수정
- cache multi-value `Vary` delimiter 수정
- Blazor Hybrid `@rendermode`, strict CSP Virtualize, streaming SSR session cookie 수정

실행 가능한 [05_AspNetCoreConcepts.csx](./05_AspNetCoreConcepts.csx)는 ASP.NET package 없이 validation/CSRF/union/cancellation의 핵심을 작은 pipeline으로 재현합니다.

> 🔗 [ASP.NET Core Preview 6 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md)

> 이전: [SDK와 C# 15](./03-sdk-csharp.md) · 다음: [MAUI·EF Core·F#·컨테이너](./05-maui-ef-fsharp-containers.md)
