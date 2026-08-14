# 1. .NET 11, CLR/JIT, MAUI 런타임과 서비스 업데이트

Issue 26의 세 원문을 한 흐름으로 연결합니다.

1. [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/): 언어·라이브러리·런타임·웹·데이터·모바일의 Preview 기능
2. [CoreCLR progress and Mono timeline for .NET MAUI](https://devblogs.microsoft.com/dotnet/coreclr-progress-and-mono-timeline-dotnet-maui/): 모바일 CLR 통합
3. [.NET and .NET Framework July 2026 servicing updates](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/): 보안·신뢰성 패치

## 1.1 SDK, Runtime, CLR, JIT를 구분하기

```text
Program.cs
  └─ C# 컴파일러(Roslyn, SDK가 실행)
       └─ MyApp.dll = IL + 메타데이터 + 리소스
            └─ dotnet 호스트가 적절한 Runtime 선택
                 └─ CLR 로더가 어셈블리·형식·메서드 확인
                      └─ JIT가 호출된 IL을 현재 CPU 기계어로 변환
                           └─ CPU 실행, GC/예외/스레드 풀이 지원
```

- **SDK**는 만드는 도구, **Runtime**은 실행하는 제품 묶음입니다.
- **CoreCLR**은 데스크톱·서버 .NET의 주 실행 엔진이며, **Mono**는 작은 환경과 WebAssembly/모바일 역사가 긴 또 다른 CLR 구현입니다.
- **JIT**은 CLR의 하위 구성요소입니다. 모든 .NET 코드가 JIT되는 것은 아닙니다. NativeAOT는 게시 시 미리 네이티브 코드를 만들고, 해석기나 WebAssembly AOT 같은 다른 실행 방식도 있습니다.

## 1.2 CLR 로더에서 첫 호출까지

1. 호스트가 `.runtimeconfig.json`과 roll-forward 정책으로 Runtime을 고릅니다.
2. 로더가 어셈블리 메타데이터를 읽고 참조를 해석합니다.
3. 형식을 로드하고 필요할 때 정적 초기화를 수행합니다.
4. 메서드 첫 호출 시 entry stub이 JIT 컴파일을 요청합니다.
5. JIT는 IL의 스택 기반 명령을 SSA 형태로 바꾸고, 인라이닝·범위 검사 제거·상수 전파·레지스터 할당 등을 거쳐 기계어를 만듭니다.
6. 메서드 진입점이 네이티브 코드로 패치되고 이후 호출이 그 코드를 사용합니다.

메타데이터에는 형식·메서드·필드·특성·제네릭 정보가 있습니다. IL은 평가 스택을 사용하지만 CPU 기계어가 된 뒤 값은 레지스터와 네이티브 스택에 배치됩니다.

## 1.3 계층형 JIT와 Dynamic PGO

빠른 시작과 긴 실행 성능은 서로 충돌합니다. CLR은 이를 계층형 컴파일로 절충합니다.

| 단계 | 목적 | 핵심 |
|---|---|---|
| Tier 0 | 빨리 실행 시작 | 컴파일 비용을 줄인 코드, 계측 가능 |
| 계측 | 실제 사용 패턴 수집 | 호출 대상, 분기 빈도, 형식 정보 |
| Tier 1 | 자주 실행되는 코드 최적화 | Dynamic PGO를 이용한 인라인·분기 배치 등 |
| OSR | 긴 루프 중간에 최적화 코드로 전환 | 메서드가 끝날 때까지 기다리지 않음 |

JIT 컴파일 스레드와 애플리케이션 실행이 겹칠 수 있고, 네이티브 코드도 메모리를 사용합니다. 벤치마크는 워밍업, 프로세스 수명, 환경 변수를 통제해야 합니다. [01_ClrJitRuntime.csx](./01_ClrJitRuntime.csx)는 워밍업과 실행 구간 차이를 관찰하는 교육용 실습이지 정밀 벤치마크가 아닙니다.

## 1.4 GC와 safe point

관리 힙은 보통 세대 0/1/2와 큰 개체 힙으로 나뉩니다. 새 객체는 대개 세대 0에 빠르게 bump allocation됩니다. GC가 참조 그래프의 뿌리(스택, 정적 필드, 핸들 등)에서 살아 있는 객체를 찾고 일부 세대에서 이동·압축합니다.

JIT는 GC가 어느 위치에서 어떤 레지스터/스택 슬롯이 객체 참조인지 알 수 있는 정보를 생성합니다. safe point에서 런타임은 스레드를 협조적으로 멈출 수 있습니다. `GC.Collect()`는 학습 관찰 외에는 일반 앱에서 거의 직접 호출하지 마세요. 수명과 할당률을 개선하는 것이 먼저입니다.

## 1.5 .NET 11 Preview 6

Preview 6는 Runtime, SDK, Libraries, C# 15, ASP.NET Core, MAUI, EF Core, F#, 컨테이너를 함께 다룹니다. 대표 주제는 extension indexer, built-in union과 System.Text.Json 지원, async validation, OpenAPI 3.2, CSRF, SignalR, 테스트, NativeAOT dispatch입니다.

세부 기능과 원문 내부 62개 링크 전체는 다음 필수 자료에 이미 정리되어 있습니다.

- [시작·설치·실행](../dotnet-11-preview-6/00-getting-started.md)
- [CLR/JIT 기반](../dotnet-11-preview-6/01-foundations-clr-jit.md)
- [Libraries와 Runtime](../dotnet-11-preview-6/02-libraries-runtime.md)
- [SDK와 C# 15](../dotnet-11-preview-6/03-sdk-csharp.md)
- [ASP.NET Core](../dotnet-11-preview-6/04-aspnet-core.md)
- [MAUI·EF Core·F#·컨테이너](../dotnet-11-preview-6/05-maui-ef-fsharp-containers.md)
- [원문 세부 링크 62개 커버리지](../dotnet-11-preview-6/06-link-coverage.md)

Preview 문법은 정식 사양에서 바뀔 수 있으므로 `LangVersion=preview` 코드를 운영 라이브러리에 성급히 노출하지 않습니다. Preview SDK와 안정 SDK를 `global.json` 및 격리된 브랜치로 나누는 편이 안전합니다.

## 1.6 MAUI: Mono에서 CoreCLR로

.NET 11에서 Android, iOS, Mac Catalyst의 .NET MAUI는 CoreCLR을 유일한 런타임으로 삼는 방향입니다. 기사 시점에 기능 구현은 완료되었고, 디버깅·Hot Reload·`dotnet watch`, `dotnet-trace`, `dotnet-counters`, NativeAOT 기반이 핵심입니다.

- iOS/Mac Catalyst는 대체로 Mono보다 빨랐고, Android는 시작 시간·앱 크기를 Mono 대비 약 10% 이내로 맞추는 것이 당시 상태였습니다.
- 기존 Mono 선택 속성은 제거됩니다. “문제가 생기면 영구히 Mono로 되돌린다”는 배포 전략에 의존하면 안 됩니다.
- Blazor WebAssembly는 브라우저 샌드박스와 WebAssembly에 최적화된 Mono를 계속 사용합니다. MAUI 통합이 모든 환경에서 Mono 제거를 뜻하지 않습니다.
- 검증은 Release 빌드, cold/warm 시작, APK/AAB/IPA 크기, 전체 사용자 흐름, Hot Reload, reflection·동적 코드 사용 라이브러리를 포함해야 합니다.

### JIT, NativeAOT, 트리밍 선택

| 방식 | 장점 | 주의점 |
|---|---|---|
| CoreCLR + JIT | 실행 프로필 최적화, 동적 코드 친화적 | 시작 시 JIT 비용, 코드 메모리 |
| NativeAOT | 시작·배포 예측성, JIT 불필요 | reflection/동적 로딩 제약, 빌드 시간·크기 판단 |
| Trimming | 사용하지 않는 코드 제거 | 정적으로 보이지 않는 reflection 경로가 제거될 수 있음 |

## 1.7 2026년 7월 서비스 업데이트

기사의 .NET 패치 버전은 10.0.10, 9.0.18, 8.0.29이며 보안 및 비보안 수정이 포함되었습니다. CVE 목록은 `CVE-2026-47300`, `47302`, `47303`, `47304`, `50524`~`50528`, `50646`, `50648`~`50651`, `50659`, `56170`, `57108`입니다. 번호만 보고 영향도를 추측하지 말고 각 권고와 자신의 사용 구성요소를 확인해야 합니다.

안전한 서비스 절차:

1. 현재 SDK/Runtime/컨테이너 베이스 이미지와 지원 기간을 목록화합니다.
2. 공식 보안 권고, 릴리스 노트, 알려진 문제를 읽습니다.
3. 같은 기능 버전의 최신 패치로 테스트 환경을 업데이트합니다.
4. 빌드·단위·통합·성능·시작·메모리 테스트를 실행합니다.
5. 컨테이너는 태그 문자열만 믿지 말고 새 이미지 digest로 다시 빌드·배포합니다.
6. 배포 후 실제 로드된 Runtime 버전과 지표를 확인하고 롤백 기준을 기록합니다.

서비스 패치는 “코드 변경이 없으니 테스트 불필요”가 아닙니다. JIT, GC, TLS, 파서, ASP.NET Core 같은 실행 기반 변경은 애플리케이션 코드가 같아도 동작과 성능에 영향을 줄 수 있습니다.

## 실습

```powershell
dotnet script .\01_ClrJitRuntime.csx
```

정상 출력에는 `runtime =`, `cold-ish checksum`, `warm checksum`, `generation before/after`가 포함됩니다. 시간 값은 컴퓨터마다 달라야 정상입니다.

## 다음 단계

- 이전: [처음 시작하기](./00-start-here.md)
- 다음: [Agent Skills와 현대화 워크플로](./02-agent-skills-modernization.md)
- 공식 기반: [.NET 실행 모델 개요](https://learn.microsoft.com/dotnet/standard/clr), [계층형 컴파일](https://learn.microsoft.com/dotnet/core/runtime-config/compilation)
