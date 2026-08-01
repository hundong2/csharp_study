# 2. Libraries와 Runtime

이 장은 발표의 Libraries 6개와 Runtime 5개 링크를 모두 다룹니다. 실행 실습은 [02_LibrariesPreview6.csx](./02_LibrariesPreview6.csx)와 [03_AsyncJitSimd.csx](./03_AsyncJitSimd.csx)입니다.

## 2.1 메모리·텍스트용 Stream adapter

`Stream`은 “바이트를 순서대로 읽고 쓰는 능력”을 추상화합니다. 파일, 네트워크, 압축기, HTTP API는 실제 저장 위치를 몰라도 `Read`, `Write`, `ReadAsync` 계약만 사용합니다.

Preview 6에는 중간 복사를 피하는 네 형식이 추가됐습니다.

| 형식 | 감싸는 데이터 | 쓰기 | 크기 |
|---|---|---|---|
| `ReadOnlyMemoryStream` | `ReadOnlyMemory<byte>` | 불가 | 고정 |
| `WritableMemoryStream` | `Memory<byte>` | 가능 | 고정 |
| `ReadOnlySequenceStream` | 여러 segment의 `ReadOnlySequence<byte>` | 불가 | 고정 |
| `StringStream` | `string`/`ReadOnlyMemory<char>` + `Encoding` | 불가 | 인코딩 결과 |

기존에는 문자열 → `byte[]` → `MemoryStream`처럼 중간 배열을 만들기 쉬웠습니다. 새 adapter는 원본 메모리의 수명과 변경 규칙을 지키면서 Stream API에 연결합니다. 특히 Pipelines가 돌려준 여러 segment를 `ToArray()`로 평탄화하지 않는 `ReadOnlySequenceStream`은 큰 메시지의 할당과 복사를 줄입니다.

주의할 점:

- adapter가 zero-copy여도 소비자가 다시 복사할 수 있습니다.
- writable stream은 고정 크기이므로 용량을 넘어 늘어나는 `MemoryStream`과 다릅니다.
- stream을 dispose했다고 원본 `Memory<T>`의 소유권이 항상 해제되는 것은 아닙니다. 소유자는 별도 계약입니다.
- `StringStream`은 문자→바이트 인코딩 상태를 관리해야 하므로 멀티바이트 경계가 존재합니다.

## 2.2 DataAnnotations 비동기 검증

동기 `[Required]`, `[StringLength]`는 메모리 안의 값만 확인합니다. 이메일 중복, 세금 번호 등록, 재고 확인은 DB나 원격 API I/O가 필요합니다. 동기 검증에서 `.Result`나 `.Wait()`를 쓰면 스레드를 막고 환경에 따라 deadlock 위험도 생깁니다.

Preview 6의 세 경로:

1. 속성 단위: `AsyncValidationAttribute.IsValidAsync`
2. 객체 단위: `IAsyncValidatableObject.ValidateAsync`
3. 직접 호출: `Validator.ValidateObjectAsync`, `TryValidateObjectAsync`, `ValidatePropertyAsync`, `ValidateValueAsync`

`ValidationContext.GetRequiredService<T>()`로 검증 의존성을 가져올 수 있고 `CancellationToken`을 I/O에 전달해야 합니다. 동기 API가 비동기 규칙을 조용히 무시하지 않도록 async-only validator의 동기 메서드에서는 명시적으로 예외를 던지는 패턴이 사용됩니다.

`Microsoft.Extensions.Options`도 비동기 검증과 `IAsyncStartupValidator`를 지원합니다. 시작 시 외부 설정을 검사해 잘못된 구성으로 트래픽을 받기 전에 fail fast할 수 있습니다.

보안상 클라이언트 검증은 UX일 뿐입니다. 서버 제출 시 반드시 다시 검증합니다.

## 2.3 System.Text.Json과 C# union

C# union은 “정해진 여러 case 중 정확히 하나를 담는 형식”입니다. Preview 6의 JSON serializer는 `JsonTypeInfoKind.Union` 계약을 인식해 active case의 값을 직접 읽고 씁니다.

- reflection serializer와 source generator 모두 지원
- `JsonUnionAttribute`, `JsonUnionCaseInfo`로 case 탐색·이름 사용자화
- `JsonTypeClassifier`와 `JsonSerializerOptions.TypeClassifiers`로 모양이 겹치는 case 분류
- ASP.NET Core OpenAPI에서는 각 case를 `anyOf`로 설명

`int|string` union의 active case가 문자열이면 JSON은 `"hello"`, 정수면 `42`가 됩니다. 일반 다형성의 `$type` discriminator와는 다른 모델입니다. case의 JSON 모양이 같다면 classifier 없이는 역직렬화가 모호합니다.

## 2.4 규칙 기반 Activity tracing

`Activity`는 분산 추적의 span에 해당합니다. `ActivitySource`가 작업을 만들고 listener/exporter가 샘플링·수집합니다. Preview 6의 `Microsoft.Extensions.Diagnostics.AddTracing`은 listener를 직접 조립하는 대신 source 이름, operation 이름, listener 기준의 enable/disable 규칙을 선언합니다.

```csharp
builder.Services.AddTracing(tracing =>
{
    tracing.EnableTracing(sourceName: "MyCompany.Orders");
    tracing.DisableTracing(
        sourceName: "MyCompany.Orders",
        operationName: "HealthCheck");
});
```

설정 파일로 규칙을 바꾸면 배포 없이 잡음을 줄일 수 있습니다. `ActivitySourceFactory`와 unsealed `ActivitySource`는 factory 생성과 refresh 가능한 listener 구성을 돕습니다. 추적은 로그와 달리 부모/자식, trace id, 기간을 중심으로 요청 경로를 연결합니다.

## 2.5 Vector cross-lane 연산

SIMD는 하나의 CPU 명령으로 여러 lane을 동시에 처리합니다. 기존에는 lane 사이를 섞는 연산에 수동 shuffle mask가 필요했습니다. Preview 6은 `Vector64/128/256/512<T>`와 `Vector<T>`에 다음 family를 추가했습니다.

- 생성: `CreateGeometricSequence`, `CreateAlternatingSequence`, `CreateHarmonicSequence`
- 교차/분리: `Zip`, `ZipLower`, `ZipUpper`, `Unzip`, `UnzipEven`, `UnzipOdd`
- 재배치: `ConcatLowerLower`, `ConcatLowerUpper`, `ConcatUpperLower`, `ConcatUpperUpper`, `Reverse`

이미지 RGBA 채널 재배치, 오디오 interleave/de-interleave, 행렬 layout 변환에 유용합니다. 하드웨어 가속 가능 여부, element type, vector width를 확인해야 하며 작은 데이터에서는 준비 비용이 이득을 상쇄할 수 있습니다.

## 2.6 Process 제어

- `ProcessStartInfo.StartSuspended`: Windows에서 프로세스를 정지 상태로 시작
- `SafeProcessHandle.Resume`: debugger/job object 설정 후 실행 재개
- `Process.TryGetProcessById`: PID가 없을 때 예외 대신 `false`
- `SafeProcessHandle.Open/TryOpen`: PID로 기존 프로세스 handle 열기

handle은 OS 자원이므로 `using`으로 닫습니다. PID는 재사용될 수 있어 PID 숫자만으로 영구 신원을 가정하면 안 됩니다. suspended start는 보안 경계가 아니며 Windows 전용입니다.

이전 preview의 `Process.Run/RunAsync` file-name overload에는 `silent` 선택 매개변수가 앞에 추가됐습니다. timeout/token을 위치 인수로 넘긴 코드는 named argument로 바꾸는 것이 안전합니다.

## 2.7 Runtime-async

[기초 장](./01-foundations-clr-jit.md#asyncawait-내부)의 상태 머신을 먼저 읽습니다.

Preview 6은 다음 비용을 줄입니다.

- 동기 Task 반환 메서드로 thunk 호출하는 대신 JIT가 전용 runtime-async 버전을 컴파일
- tail call을 runtime-async call로 바꾸고 반환 Task를 await
- suspension point tail merge로 코드 크기 축소
- task thunk continuation cache/reuse
- 이미 pooling하는 메서드는 중복 runtime-async 최적화에서 제외
- 복원할 ambient state가 없을 때 `ExecutionContext` capture/restore 생략

의미론은 유지되어야 합니다. `AsyncLocal<T>` 값이 있으면 여전히 흘러야 하며, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` 모두 관련 경로의 이득을 볼 수 있습니다.

## 2.8 JIT 개선

| 변경 | 내부 의미 |
|---|---|
| x64 `Math.BigMul` | helper call 대신 64-bit `MUL` 한 명령으로 high/low 결과 활용 |
| prolog single-IG 제한 제거 | 큰 frame과 복잡한 상태 설정의 codegen 제약 감소 |
| 동일 상수 select 접기 | 비교·선택 instruction을 제거 |
| Arm SVE `Vector<T>` by-ref | 런타임 가변 폭 vector의 불필요/잘못된 값 복사 방지 |

JIT의 입력은 원본 C#이 아니라 IL과 이미 변환된 IR입니다. 따라서 겉보기엔 드문 패턴도 다른 최적화 뒤 자주 나타날 수 있습니다.

## 2.9 In-process crash report

모바일 플랫폼에서 충돌하는 프로세스 안에서 종료 전 managed stack, module 목록, 핵심 runtime 상태를 잘 알려진 경로에 남기는 기능입니다. 외부 monitor는 안전하지만 죽는 프로세스 내부에서만 보이는 정보를 놓칠 수 있습니다.

충돌 중에는 heap과 lock 상태가 손상됐을 수 있으므로 crash path는 일반 로깅보다 훨씬 제한적이어야 합니다. 이 기능은 모바일 전용이며 오류 복구 메커니즘이 아니라 사후 진단 보강입니다.

## 2.10 NativeAOT interface dispatch

NativeAOT는 인터페이스 호출을 direct fat-pointer sequence 대신 공유 dispatch helper로 보내고, call site가 warm-up되면 맞는 구현으로 패치할 수 있습니다. 인터페이스 호출이 많은 앱에서 call-site binary size와 throughput을 개선합니다.

가상 호출의 핵심은 실제 객체 형식에 맞는 구현을 찾는 것입니다. JIT는 runtime profile로 devirtualize할 수 있지만 NativeAOT는 빌드 시 알 수 없는 경우가 있어 효율적인 dispatch cell/helper가 중요합니다.

## 2.11 SIMD lane construction/composition

Libraries 항목의 high-level vector 메서드와 Runtime 항목의 intrinsic 기반 lane 구성 API는 같은 PR 계열을 서로 다른 관점에서 설명합니다. API는 portable한 의도를 표현하고, JIT는 가능한 ISA의 shuffle/unpack/permute 명령으로 lower합니다.

## 2.12 함께 알아둘 수정

- ZIP update mode의 비압축 크기·ZIP64 필드 검증 강화
- W3C Trace Context Level 2와 baggage parser 수정
- `X509Chain` 시간 유효성 검사가 process timezone에 의존하지 않도록 수정
- Linux/macOS `FileSystemWatcher` 시작 실패 시 `Error` event 발생
- JIT GC hole, async byref resume, return hijacking liveness, TLS 정렬 수정

이들은 “기능” 못지않게 중요한 계약 수정입니다. Preview를 평가할 때 happy path만 보지 말고 진단·플랫폼·실패 경로도 회귀 테스트합니다.

> 🔗 [Libraries 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/libraries.md) · [Runtime 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md)

## 더 읽기

- [.NET distributed tracing 개요](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing)
- [async/await 시나리오](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/async-scenarios)

> 이전: [C# 기초와 CLR/JIT](./01-foundations-clr-jit.md) · 다음: [SDK와 C# 15](./03-sdk-csharp.md)
