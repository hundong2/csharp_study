# 5. .NET MAUI, EF Core, F#, Container Images

## 5A. .NET MAUI

.NET MAUI는 하나의 .NET 코드베이스에서 Android, iOS, macOS/Mac Catalyst, Windows 앱을 만드는 UI framework입니다. workload와 platform SDK(Xcode, Android SDK, Windows App SDK)가 필요하므로 일반 CSX에서는 UI를 직접 실행하지 않고 개념과 business logic을 분리해 실습합니다.

### CollectionView2가 Windows에 도입

Windows handler가 WinUI `ItemsRepeater` 위에 만들어져 Android/iOS/Mac Catalyst의 차세대 CollectionView 구조와 맞춰집니다.

- `LinearItemsLayout`, `GridItemsLayout`의 virtualizing layout
- header/footer, empty view, grouping, selection, snapping
- incremental loading, `RefreshView`

virtualization은 보이지 않는 item의 native view 생성을 줄입니다. item template이 무겁거나 높이가 불규칙하면 recycle/measure 비용을 따로 측정합니다.

### Android Shell의 handler 기반 구조

기존 renderer(`ShellRenderer` 등)를 `ShellHandler`, `ShellItemHandler`, `ShellSectionHandler`로 바꾸고 `MauiDrawerLayout`, `TabbedViewManager`를 재사용합니다. handler는 cross-platform virtual view와 native view 사이의 mapping을 더 작게 구성하며, 오래된 renderer customization은 migration 대상입니다. Preview 6에서는 Android만 해당합니다.

### Compatibility package 제거

Xamarin.Forms 이행용 opt-in `Microsoft.Maui.Controls.Compatibility` NuGet package가 더 이상 빌드·배포되지 않습니다. 이를 명시 참조하거나 compatibility renderer에 의존한 앱은 .NET 11 전에 handler로 옮겨야 합니다. `Microsoft.Maui.Controls`만 사용하는 앱과 Core에 남은 일부 compatibility-named base type은 별개입니다.

### HybridWebView AOT-safe

JavaScript interop가 reflection 대신 source generator를 사용해 trim/NativeAOT 분석이 호출 대상을 알 수 있습니다. Android에서는 genuine `WebView.postWebMessage` source만 native message로 받아 iframe의 `window.postMessage` 같은 stray message를 걸러냅니다.

reflection은 runtime 유연성이 있지만 trimmer는 문자열로만 찾는 member를 제거할 수 있습니다. source generator는 build 시 strongly typed glue code를 만들어 경고와 runtime 누락을 줄입니다.

### Geolocation minimum distance

`GeolocationListeningRequest.MinimumDistance`는 지정 meter 이상 이동했을 때만 foreground update를 냅니다. `0`은 기존 동작입니다. GPS 정확도 오차, 배터리, update 빈도의 trade-off가 있으며 “10m 설정 = 정확히 10m마다 한 번”을 보장하는 timer가 아닙니다.

### Android MediaPicker result recovery

camera/file picker가 앞에 있는 동안 Android가 앱 process를 죽이고 재생성할 수 있습니다. 기존 `Task`는 사라지므로 Preview 6은 완료된 결과를 회수·대기·삭제하는 opt-in API를 제공합니다.

- `GetRecoveredMediaPickerResultsAsync`
- `WaitForRecoveredMediaPickerResultsAsync`
- `ClearRecoveredMediaPickerResultAsync`

process death를 정상 lifecycle로 보고 idempotent result 처리와 중복 소비 방지를 설계합니다.

### .NET for Android

- `AndroidMessageHandler` transport/protocol 오류는 `HttpRequestException`, 기존 `WebException`은 inner exception
- upload/download 취소는 `OperationCanceledException`
- trimmable typemap/CoreCLR native registration 계속 개선
- 구 Xamarin.Android resource는 `XA0149`
- consumer rule에 부적합한 `.aar` global R8 option은 skip

예외 형식의 통일은 cross-platform `HttpClient` retry/cancellation 정책을 단순화합니다. `OperationCanceledException`도 timeout과 사용자 취소를 token/inner state로 구분해야 합니다.

### Apple workload

- Xcode 26.6 안정 지원, Xcode 27 Device Hub 조기 지원
- macOS 26의 `.icon` Icon Composer asset
- `NSUrlSessionHandler` redirect/auth fallback 정합성
- referenced extension project 기본 build

MAUI release note의 reliability wave에는 CollectionView/Shell/Material 3/iOS 26/XAML/accessibility/Essentials 수정도 포함됩니다. Preview 평가 시 자신의 platform과 control 조합으로 회귀 테스트합니다.

---

## 5B. Entity Framework Core 11

EF Core는 LINQ expression tree를 provider가 SQL/다른 query language로 번역하고 결과를 object로 materialize하는 ORM입니다. 일반 `IEnumerable<T>` lambda는 실행 code이지만 `IQueryable<T>` lambda는 provider가 해석할 expression tree라는 차이가 핵심입니다.

### LINQ 번역 개선

- `Queryable.FullJoin` → SQL `FULL OUTER JOIN`; 양쪽 unmatched row의 상대편은 null
- `condition ? null : value` 패턴 → SQL `NULLIF`
- SQL 자체 null propagation과 같은 `CASE`/`IS NOT NULL` 제거
- `List<T>.Exists` → `EXISTS`
- SQLite `TimeOnly.Hour/Minute/Second` → `strftime`
- ordered grouping의 `string.Join/Concat` → SQLite `group_concat(... ORDER BY ...)`

client evaluation이나 번역 실패는 성능·동작을 바꿀 수 있으므로 `ToQueryString()`과 실제 query plan을 봅니다.

### complex property를 지나는 key/index

`HasKey`, `HasAlternateKey`, `HasIndex`가 `c => c.Address.ZipCode` 같은 non-collection complex path를 받습니다. path의 property는 required가 되며 collection/optional complex property를 key로 쓰면 validation error가 납니다. SQL Server JSON-mapped column 내부 property에도 index를 모델링할 수 있습니다.

### unconstrained foreign key

`.IsConstrained(false)`는 관계가 DB foreign-key constraint로 강제되지 않음을 나타냅니다. query는 principal 존재를 가정하지 않고 `LEFT JOIN`을 쓰며 migration은 `AddForeignKey`를 생략합니다. Cosmos의 non-owned FK는 기본 unconstrained입니다.

이는 “관계 없음”이 아니라 “application model에는 관계가 있지만 storage가 referential integrity를 보장하지 않음”입니다. orphan 처리 책임이 앱으로 옵니다.

### Cosmos provider

- JSON/composite/full-text index와 include/exclude path 구성
- LINQ `Convert` 처리 개선으로 math/string concatenation의 client evaluation 감소

partition key와 index policy는 비용·RU에 직접 영향을 주므로 relational DB의 index 감각을 그대로 적용하지 않습니다.

### migration

- SQL Server index facet 변경은 `DROP`+`CREATE` 대신 `CREATE ... WITH (DROP_EXISTING=ON)`
- SQL 식이 같은 computed column의 CLR type만 바뀌면 불필요한 `ALTER COLUMN` 생략
- 여러 `dotnet ef` 명령에서 `--context *`
- temporal period column의 `HIDDEN` 생략 구성

migration은 생성 후 항상 검토합니다. online 여부, lock, data loss, rollback 전략은 provider/edition/data volume에 따라 달라집니다.

### SQLite3 Multiple Ciphers

`Microsoft.Data.Sqlite`의 native dependency가 `e_sqlite3`에서 `SQLite3MC.PCLRaw.bundle`로 바뀌어 actively maintained build와 내장 암호화 기반을 제공합니다. dependency 변경은 native binary 크기, license, platform packaging, 기존 encryption 호환성을 함께 확인해야 합니다.

---

## 5C. F#

F#은 .NET 위의 함수 우선 언어이며 같은 CLR, GC, BCL을 사용합니다. 발표는 F# 문법·compiler·FSI 개선도 별도 링크로 포함합니다.

### `Array.init` lambda inline

```fsharp
let squares = Array.init 10 (fun index -> index * index)
```

initializer lambda에 inline-if-lambda 동작이 적용되어 단순 초기화에서 closure 객체 생성을 피할 기회가 늘어납니다. closure는 외부 변수를 캡처할 때 환경 object와 delegate가 필요할 수 있습니다.

### `=` 바로 뒤 interpolated string

named argument와 regular/verbatim/triple/multi-dollar interpolated string이 공백 없이 `Name=$"..."` 형태로 parse됩니다. parser 수정이며 runtime string interpolation 의미가 새로 생긴 것은 아닙니다.

### FSI `--quiet`

`dotnet fsi --quiet script.fsx`에서 NuGet restore chatter가 stdout을 오염하지 않습니다. JSON/CSV처럼 stdout을 다른 도구로 pipe하는 script가 안정적입니다. 진단용 stderr와 데이터 stdout을 분리하는 Unix pipeline 원칙입니다.

### signature file 의미 attribute 진단

implementation `.fs`에만 있고 public contract `.fsi`에 빠진 consumer-visible attribute를 FS3888로 알립니다. `ErrorOnMissingSignatureAttribute` preview flag에서는 오류가 됩니다. signature는 이름만 숨기는 파일이 아니라 downstream type checking 계약입니다.

### debug sequence point

call argument, `for`/comprehension, literal binding, `if`/`match` 조건에 더 정확한 sequence point를 냅니다. sequence point는 debugger가 source line과 IL offset을 연결하는 메타데이터입니다. 최적화된 build에서는 source와 실행 순서가 여전히 1:1이 아닐 수 있습니다.

---

## 5D. Container images

### Azure Linux 4.0

.NET 11의 `runtime-deps`, `runtime`, `aspnet`, `sdk`에 `azurelinux4.0` tag가 제공되며 Preview 6 시점은 Azure Linux 4.0 Beta입니다. distroless, NativeAOT SDK, ASP.NET composite variant도 포함됩니다.

base image 변경 시 libc/openssl/timezone/CA certificate/native dependency와 CVE scanner 정책을 다시 검증합니다. tag만 바꾸고 production 승격하지 않습니다.

### 더 작은 NativeAOT SDK image

필요한 compiler/linker/dev package만 설치하고 Clang/LLVM 대신 GCC를 사용해 크기를 줄였습니다.

| Image | 이전 | 이후 | 감소 |
|---|---:|---:|---:|
| Alpine 3.23 AOT | 401.2 MB | 277.3 MB | 123.9 MB, 30.9% |
| Azure Linux AOT | 565.5 MB | 380.4 MB | 185.1 MB, 32.7% |
| Ubuntu Resolute AOT | 429.5 MB | 362.7 MB | 66.8 MB, 15.6% |

SDK image가 작아지면 pull/cache/CI 시간이 줄 수 있지만 최종 runtime image 크기와 동일하지 않습니다. multi-stage build에서 builder와 runtime stage를 구분합니다.

실습은 [06_DataMobileContainers.csx](./06_DataMobileContainers.csx)에서 FullJoin 의미, SQL `NULLIF`, 거리 filter, image 절감률을 순수 C#으로 확인합니다.

> 🔗 [MAUI](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/dotnetmaui.md) · [EF Core](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/efcore.md) · [F#](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/fsharp.md) · [Containers](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/containers.md)

> 이전: [ASP.NET Core](./04-aspnet-core.md) · 다음: [공식 발표 링크 커버리지](./06-link-coverage.md)
