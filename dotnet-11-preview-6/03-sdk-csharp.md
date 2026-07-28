# 3. SDK와 C# 15 Preview

## 3.1 NativeAOT CLI 전체 command surface

Preview 5의 실험적 NativeAOT CLI는 `DOTNET_CLI_ENABLEAOT=true`로 켭니다. Preview 6은 managed/AOT parser를 공유해 모든 내장 명령을 parse·validate하고 `--help`를 그릴 수 있습니다.

완전히 native path에서 처리되는 예:

- `dotnet --version`, `--info`, `--help`
- 모든 내장 `dotnet <command> --help`
- `dotnet --cli-schema`
- `dotnet sln list`, `migrate`, `remove`
- global tool, PATH/app-base 외부 명령 해석과 out-of-process 실행

나머지는 managed CLI로 투명하게 fallback합니다. `dotnet ef`, `dotnet dev-certs` 같은 외부 명령 앞의 managed CLI 시작 비용 약 600~700ms를 피할 수 있고, AOT/managed host span은 OpenTelemetry parent-child 관계로 이어집니다.

## 3.2 `dotnet test`와 Microsoft.Testing.Platform

| 기능 | 목적 |
|---|---|
| `--no-dependencies` | project reference를 다시 빌드하지 않음 |
| `DOTNET_TEST_RUNNER` | `VSTest`/`Microsoft.Testing.Platform`을 세션별 선택 |
| `--use-current-runtime`, `--ucr` | 현재 runtime을 restore/build target에 사용 |
| `--test-modules`의 `!pattern` | DLL 포함 목록에서 제외 |
| assembly별 `[✓pass/xfailed/↓skip]` | 병렬 multi-assembly 결과를 명확히 표시 |
| terminal logger forwarding | `--tl`, `--terminallogger`, `--tlp`를 MSBuild에 전달 |
| protocol 1.1 output forwarding | stdout/stderr/IOutputDevice를 실행 중 스트림 |
| running tests panel | interactive ANSI terminal에서 진행 중 test 표시 |
| 2단계 Ctrl+C | 1회 graceful stop, 2회 child process 강제 종료 |
| `--device` | MAUI TFM별 test device 선택 |

CI에서는 ANSI/진행 패널이 꺼질 수 있습니다. test 결과와 사람이 보는 UI를 분리해 자동화는 exit code와 machine-readable artifact를 기준으로 판단합니다.

## 3.3 테스트 template

```bash
dotnet new xunit --xunit-version v3
dotnet new xunit --xunit-version v3 --test-runner VSTest
dotnet new nunit --test-runner Microsoft.Testing.Platform
```

C#, F#, VB template에 적용됩니다. xUnit v3 기본 runner는 MTP입니다. 테스트 framework와 runner는 동일 개념이 아닙니다. framework는 test API/attribute, runner는 발견·프로세스·보고 protocol을 담당합니다.

## 3.4 file-based app의 DLL include

```csharp
#:include ./libs/MyLibrary.dll

MyLibrary.Helper.DoWork();
```

`.dll`은 기본 `Reference` item으로 매핑됩니다. 포함된 여러 파일이 같은 값의 `#:sdk`, `#:property`, `#:package`를 중복 선언해도 허용 범위가 넓어져 self-contained library file 구성이 쉬워졌습니다.

이 문법은 일반 preprocessor directive나 CSX의 `#r`과 다릅니다. SDK file-based app directive이며 Preview 6 SDK가 필요합니다.

## 3.5 Podman multi-arch container publish

SDK 내장 container publish가 Podman에서도 multi-architecture manifest를 만들 수 있습니다. rootless Podman 중심 Linux 환경에서 Docker 없이 여러 architecture 이미지를 묶을 수 있습니다.

multi-arch는 한 image가 모든 CPU에서 같은 binary를 실행한다는 뜻이 아닙니다. `linux-x64`, `linux-arm64` 등 각 platform image와 이를 선택하는 manifest가 필요합니다.

## 3.6 TypeScript와 Static Web Assets

Razor Class Library가 `Microsoft.TypeScript.MSBuild`로 생성한 JS/CSS output을 ASP.NET Core Static Web Assets pipeline에 compilation 이후 연결합니다. 그 결과 fingerprinting, compression, clean/rebuild 추적이 맞아집니다.

빌드 graph에서 “파일 발견”이 TypeScript compile보다 먼저 일어나면 stale reference나 rebuild 실패가 생깁니다. Preview 6 개선은 단순 파일 복사가 아니라 target ordering과 incremental build input/output 계약을 맞춘 것입니다.

## 3.7 MSBuild server와 OpenTelemetry 환경 변수

- `DOTNET_CLI_USE_MSBUILD_SERVER`가 없을 때 CLI가 더 이상 `MSBUILDUSESERVER=0`으로 강제 덮지 않습니다.
- 표준 `OTEL_EXPORTER_OTLP_*` 또는 signal별 `OTEL_EXPORTER_OTLP_TRACES_*`, `_METRICS_*` 변수가 있으면 CLI OTLP exporter를 활성화합니다.
- 기존 `DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER`도 유지됩니다.

MSBuild server는 node를 재사용해 반복 빌드 시작 비용을 줄입니다. 격리·환경 변수 오염을 의심하는 CI에서는 재사용 정책을 명시합니다.

## 3.8 Extension indexer

기존 extension block의 method/property에 indexer가 추가됐습니다.

```csharp
public static class ReadOnlyListExtensions
{
    extension<T>(IReadOnlyList<T> list)
    {
        public T this[Index index] => list[index.GetOffset(list.Count)];
    }
}
```

이제 `IReadOnlyList<T>`에 instance `this[Index]`가 없어도 `log[^1]`처럼 접근할 수 있습니다.

- instance indexer가 적용 가능하면 그것이 우선합니다.
- 여러 parameter, `get`/`set`, list pattern을 지원합니다.
- `Index`의 `^1`은 마지막 원소이고 `GetOffset(Count)`가 실제 0-based 위치를 계산합니다.
- extension block이 scope에 있어야 후보가 됩니다.
- Preview 6에서는 `<LangVersion>preview</LangVersion>`이 필요합니다.

## 3.9 Union

```csharp
public record class Dog(string Name);
public record class Cat(int Lives);
public union Pet(Dog, Cat);

static string Describe(Pet pet) => pet switch
{
    Dog(var name) => $"dog: {name}",
    Cat(var lives) => $"cat: {lives}"
};
```

Preview 5에는 사용자가 `UnionAttribute`, `IUnion` support type을 직접 제공해야 했지만 Preview 6은 `System.Runtime.CompilerServices`에 내장합니다.

### union과 기존 대안

| 방법 | 장점 | 단점 |
|---|---|---|
| `object` | 단순 | case 제약·완전성 검사 약함 |
| base class + subclasses | OOP 다형성 | 외부 형식/값 형식 묶기 불편 |
| `OneOf`류 library | 기존 생태계 | 외부 의존·언어 pattern 통합 제한 |
| C# union | case와 pattern을 언어가 이해 | Preview, tooling/API 변동 |

Preview 6 규칙 보완:

- 단일 parameter의 non-public constructor도 custom union에 사용 가능
- `not` pattern은 contained value가 아니라 들어오는 union value에 적용
- custom union의 생성된 `Create` method 상속 지원
- 필수 API가 빠진 custom union에 명확한 compiler error

`switch`에서 모든 case를 처리하는 습관은 새 case가 추가될 때 컴파일러 진단을 활용하게 합니다. 단, Preview 문법의 구체적 exhaustive 규칙은 최종 사양 전 바뀔 수 있습니다.

## 3.10 실행 실습의 구성

[04_CSharp15Preview.csx](./04_CSharp15Preview.csx)는 현재 도구에서도 실행되도록 다음 등가 모델을 사용합니다.

- extension indexer → 기존 extension method `At(Index)`
- union → `abstract record` + sealed case records + exhaustive-like switch

파일 끝의 주석에 Preview 6 원문 문법을 함께 둡니다. Preview SDK에서 일반 `.cs` file-based app이나 `net11.0` 프로젝트로 옮겨 비교합니다.

> 🔗 [SDK 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/sdk.md) · [C# 릴리스 노트](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/csharp.md)

> 이전: [Libraries와 Runtime](./02-libraries-runtime.md) · 다음: [ASP.NET Core](./04-aspnet-core.md)
