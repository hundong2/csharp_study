# 6. 공식 발표 링크 커버리지

## 범위

“모든 링크”는 발표 본문의 **설치 링크, 분야별 기능 링크, `See all ...` 릴리스 노트, 개발 도구 링크**를 뜻합니다. 사이트 공통 header/footer, 로그인, 작성자, category/tag, 광고, 댓글, 개인정보 링크는 제품 학습 개념이 아니므로 제외했습니다.

각 기능명은 Microsoft의 Preview 6 릴리스 노트 anchor로 연결됩니다. `See all` 문서에만 있는 하위 기능과 주요 수정도 해당 장에 함께 요약했습니다.

## 설치와 도구

| 원문 링크 | 학습 위치 | 확인 |
|---|---|---|
| [.NET 11 Preview 6 다운로드](https://dotnet.microsoft.com/download/dotnet/11.0) | [설치 원칙](./00-getting-started.md#preview-6-설치-원칙) | [ ] |
| [Visual Studio 2026 Insiders](https://visualstudio.microsoft.com/insiders/) | [설치 원칙](./00-getting-started.md#preview-6-설치-원칙) | [ ] |
| [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) | [설치 원칙](./00-getting-started.md#preview-6-설치-원칙) | [ ] |

## Libraries

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [Stream adapters for memory and text](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#stream-adapters-for-memory-and-text) | [2.1](./02-libraries-runtime.md#21-메모리텍스트용-stream-adapter) | [ ] |
| [Asynchronous validation with DataAnnotations](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#asynchronous-validation-with-dataannotations) | [2.2](./02-libraries-runtime.md#22-dataannotations-비동기-검증) | [ ] |
| [System.Text.Json serializes C# union types](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#systemtextjson-serializes-c-union-types) | [2.3](./02-libraries-runtime.md#23-systemtextjson과-c-union) | [ ] |
| [Configure Activity tracing with rules](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#configure-activity-tracing-with-rules) | [2.4](./02-libraries-runtime.md#24-규칙-기반-activity-tracing) | [ ] |
| [Cross-lane operations for vectors](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#cross-lane-operations-for-vectors) | [2.5](./02-libraries-runtime.md#25-vector-cross-lane-연산) | [ ] |
| [Start processes suspended and look them up by id](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md#start-processes-suspended-and-look-them-up-by-id) | [2.6](./02-libraries-runtime.md#26-process-제어) | [ ] |
| [See all library updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md) | [전체와 수정](./02-libraries-runtime.md#212-함께-알아둘-수정) | [ ] |

## Runtime

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [Runtime-async performance improvements](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md#runtime-async-performance-improvements) | [2.7](./02-libraries-runtime.md#27-runtime-async) | [ ] |
| [JIT improvements](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md#jit-improvements) | [2.8](./02-libraries-runtime.md#28-jit-개선) | [ ] |
| [In-process crash report logging](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md#in-process-crash-report-logging) | [2.9](./02-libraries-runtime.md#29-in-process-crash-report) | [ ] |
| [NativeAOT: faster interface dispatch](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md#nativeaot-faster-interface-dispatch) | [2.10](./02-libraries-runtime.md#210-nativeaot-interface-dispatch) | [ ] |
| [SIMD lane APIs](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md#simd-lane-construction-and-composition-apis) | [2.11](./02-libraries-runtime.md#211-simd-lane-constructioncomposition) | [ ] |
| [See all runtime updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md) | [CLR/JIT 기초](./01-foundations-clr-jit.md) | [ ] |

## SDK

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [NativeAOT CLI full command surface](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#nativeaot-cli-serves-the-full-command-surface) | [3.1](./03-sdk-csharp.md#31-nativeaot-cli-전체-command-surface) | [ ] |
| [`dotnet test` options/output](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#dotnet-test-gains-new-options-and-improved-output) | [3.2](./03-sdk-csharp.md#32-dotnet-test와-microsofttestingplatform) | [ ] |
| [xUnit v3/NUnit MTP templates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#test-templates-support-xunit-v3-and-nunit-on-microsofttestingplatform) | [3.3](./03-sdk-csharp.md#33-테스트-template) | [ ] |
| [`#:include .dll`](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#file-based-apps-support-include-dll-references) | [3.4](./03-sdk-csharp.md#34-file-based-app의-dll-include) | [ ] |
| [Podman multi-arch publishing](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#container-publishing-supports-multi-arch-builds-with-podman) | [3.5](./03-sdk-csharp.md#35-podman-multi-arch-container-publish) | [ ] |
| [TypeScript Static Web Assets](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#typescript-outputs-integrate-with-static-web-assets) | [3.6](./03-sdk-csharp.md#36-typescript와-static-web-assets) | [ ] |
| [MSBuild server/OTel env vars](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md#cli-honors-msbuild-server-and-standard-opentelemetry-env-vars) | [3.7](./03-sdk-csharp.md#37-msbuild-server와-opentelemetry-환경-변수) | [ ] |
| [See all SDK updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md) | [SDK 장 전체](./03-sdk-csharp.md) | [ ] |

## C#

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [Extension indexers](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/csharp.md#extension-indexers) | [3.8](./03-sdk-csharp.md#38-extension-indexer) | [ ] |
| [Union support types in-box](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/csharp.md#unions-ship-their-support-types-in-the-box) | [3.9](./03-sdk-csharp.md#39-union) | [ ] |
| [See all C# updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/csharp.md) | [C# 장 전체](./03-sdk-csharp.md) | [ ] |

## ASP.NET Core

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [Async validation for minimal APIs](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#async-validation-for-minimal-apis) | [4.1](./04-aspnet-core.md#41-minimal-api-비동기-검증) | [ ] |
| [Automatic cross-origin CSRF](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#automatic-cross-origin-csrf-protection) | [4.2](./04-aspnet-core.md#42-자동-cross-origin-csrf-보호) | [ ] |
| [Blazor Virtualize scroll](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#blazor-virtualize-can-scroll-to-an-item) | [4.3](./04-aspnet-core.md#43-blazor-virtualize-scroll) | [ ] |
| [OpenAPI 3.2 default](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#openapi-32-by-default) | [4.6](./04-aspnet-core.md#46-openapi-32-기본값) | [ ] |
| [Unions in ASP.NET Core](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#unions-in-aspnet-core) | [4.7](./04-aspnet-core.md#47-aspnet-core의-union) | [ ] |
| [[ShortCircuit] attribute](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#short-circuit-endpoints-with-an-attribute) | [4.8](./04-aspnet-core.md#48-shortcircuit) | [ ] |
| [SignalR authentication refresh](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#signalr-authentication-refresh) | [4.9](./04-aspnet-core.md#49-signalr-authentication-refresh) | [ ] |
| [Cancel hub invocations](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#cancel-hub-invocations-from-the-client) | [4.10](./04-aspnet-core.md#410-일반-hub-invocation-취소) | [ ] |
| [`dotnet user-jwts --file`](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md#dotnet-user-jwts-supports-file-based-apps) | [4.11](./04-aspnet-core.md#411-file-based-app용-dotnet-user-jwts) | [ ] |
| [See all ASP.NET Core updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/aspnetcore.md) | [Blazor options/Gateway 포함](./04-aspnet-core.md) | [ ] |

## .NET MAUI

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [CollectionView2 on Windows](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#collectionview2-comes-to-windows) | [MAUI](./05-maui-ef-fsharp-containers.md#collectionview2가-windows에-도입) | [ ] |
| [Handler-based Android Shell](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#handler-based-shell-architecture-on-android) | [MAUI](./05-maui-ef-fsharp-containers.md#android-shell의-handler-기반-구조) | [ ] |
| [Compatibility package removed](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#microsoftmauicontrolscompatibility-package-removed) | [MAUI](./05-maui-ef-fsharp-containers.md#compatibility-package-제거) | [ ] |
| [HybridWebView AOT-safe](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#hybridwebview-is-now-aot-safe) | [MAUI](./05-maui-ef-fsharp-containers.md#hybridwebview-aot-safe) | [ ] |
| [Geolocation distance filter](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#geolocation-gains-a-minimum-distance-filter) | [MAUI](./05-maui-ef-fsharp-containers.md#geolocation-minimum-distance) | [ ] |
| [Android MediaPicker recovery](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#android-mediapicker-result-recovery) | [MAUI](./05-maui-ef-fsharp-containers.md#android-mediapicker-result-recovery) | [ ] |
| [.NET for Android](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#net-for-android) | [Android](./05-maui-ef-fsharp-containers.md#net-for-android) | [ ] |
| [Apple platforms](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md#apple-platforms-net-for-ios-mac-catalyst-macos-tvos) | [Apple](./05-maui-ef-fsharp-containers.md#apple-workload) | [ ] |
| [See all MAUI updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md) | [MAUI 장 전체](./05-maui-ef-fsharp-containers.md#5a-net-maui) | [ ] |

## EF Core

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [LINQ translation](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#linq-query-translation-improvements) | [EF LINQ](./05-maui-ef-fsharp-containers.md#linq-번역-개선) | [ ] |
| [Complex keys/indexes](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#keys-and-indexes-traverse-complex-type-properties) | [EF model](./05-maui-ef-fsharp-containers.md#complex-property를-지나는-keyindex) | [ ] |
| [Unconstrained FK](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#unconstrained-foreign-key-relationships) | [EF 관계](./05-maui-ef-fsharp-containers.md#unconstrained-foreign-key) | [ ] |
| [Cosmos improvements](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#azure-cosmos-db-provider-improvements) | [Cosmos](./05-maui-ef-fsharp-containers.md#cosmos-provider) | [ ] |
| [Migrations](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#migrations-improvements) | [Migration](./05-maui-ef-fsharp-containers.md#migration) | [ ] |
| [SQLite3MC bundle](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md#microsoftdatasqlite-now-depends-on-sqlite3mcpclrawbundle) | [SQLite](./05-maui-ef-fsharp-containers.md#sqlite3-multiple-ciphers) | [ ] |
| [See all EF Core updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md) | [EF 장 전체](./05-maui-ef-fsharp-containers.md#5b-entity-framework-core-11) | [ ] |

## F#

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [`Array.init` inline](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md#arrayinit-can-inline-initialization-lambdas) | [F#](./05-maui-ef-fsharp-containers.md#arrayinit-lambda-inline) | [ ] |
| [Interpolated string parsing](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md#interpolated-strings-parse-next-to-equals-signs) | [F#](./05-maui-ef-fsharp-containers.md#-바로-뒤-interpolated-string) | [ ] |
| [FSI `--quiet`](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md#fsi---quiet-keeps-restore-output-off-stdout) | [F#](./05-maui-ef-fsharp-containers.md#fsi---quiet) | [ ] |
| [Signature diagnostics](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md#signature-file-diagnostics-catch-missing-semantic-attributes) | [F#](./05-maui-ef-fsharp-containers.md#signature-file-의미-attribute-진단) | [ ] |
| [Debug sequence points](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md#debug-sequence-points-cover-more-f-expressions) | [F#](./05-maui-ef-fsharp-containers.md#debug-sequence-point) | [ ] |
| [See all F# updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md) | [F# 장 전체](./05-maui-ef-fsharp-containers.md#5c-f) | [ ] |

## Container Images

| 원문 기능 링크 | 학습 위치 | 확인 |
|---|---|---|
| [Azure Linux 4.0 images](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/containers.md#azure-linux-40-images) | [Container](./05-maui-ef-fsharp-containers.md#azure-linux-40) | [ ] |
| [Smaller Native AOT SDK images](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/containers.md#smaller-native-aot-sdk-images) | [Container](./05-maui-ef-fsharp-containers.md#더-작은-nativeaot-sdk-image) | [ ] |
| [See all container updates](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/containers.md) | [Container 장 전체](./05-maui-ef-fsharp-containers.md#5d-container-images) | [ ] |

> 이전: [MAUI·EF Core·F#·컨테이너](./05-maui-ef-fsharp-containers.md) · 다음: [연습 문제와 해설](./07-exercises.md)
