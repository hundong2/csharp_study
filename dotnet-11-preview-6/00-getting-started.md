# 0. 시작하기: .NET을 구성하는 것들

## 먼저 알아야 할 네 이름

| 이름 | 역할 | 비유 |
|---|---|---|
| C# | 사람이 작성하는 프로그래밍 언어 | 설계 언어 |
| .NET SDK | 컴파일러, CLI, 템플릿, 빌드 도구 | 공구 상자 |
| .NET Runtime | CLR과 기본 라이브러리, 앱 실행 환경 | 엔진 |
| CLR | IL 로드, JIT, GC, 예외, 스레드를 관리하는 실행기 | 엔진 제어 시스템 |

`dotnet build`는 SDK가 필요하지만, 이미 빌드된 framework-dependent 앱을 실행할 때는 Runtime만 있어도 됩니다. SDK에는 대응 Runtime이 포함됩니다.

## CSX와 일반 프로젝트

- `.csx`는 C# Script 파일입니다. 위에서 아래로 바로 실행할 수 있어 작은 실험에 좋습니다.
- `.cs`는 일반 C# 소스입니다. 보통 `.csproj`가 Target Framework, 패키지, 컴파일 옵션을 지정합니다.
- `dotnet-script`는 별도 도구이며 설치된 SDK가 곧 지원 C# 문법 버전을 보장하지는 않습니다. 도구가 포함한 Roslyn 버전도 영향을 줍니다.
- .NET 11의 file-based app은 `dotnet app.cs`처럼 프로젝트 파일 없이 실행하는 SDK 기능입니다. `.csx` 스크립트와 같은 것이 아닙니다.

## 현재 PC 확인

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
dotnet tool list --global
dotnet script ./00_EnvironmentCheck.csx
```

이 저장소를 작성할 때 확인된 안정 SDK는 `10.0.301`입니다. Preview 6 전용 API나 C# 15 preview 문법은 이 환경에서 직접 컴파일하지 않고, 안정 API로 원리를 재현하거나 문서 코드 블록으로 격리했습니다.

## Preview 6 설치 원칙

1. [.NET 11 다운로드 페이지](https://dotnet.microsoft.com/download/dotnet/11.0)에서 **SDK 11.0.100-preview.6**을 선택합니다.
2. 안정 SDK와 side-by-side 설치할 수 있습니다.
3. 실습 폴더에서만 `global.json`으로 버전을 고정하는 편이 안전합니다.
4. Visual Studio 사용자는 [Visual Studio 2026 Insiders](https://visualstudio.microsoft.com/insiders/), VS Code 사용자는 [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)를 사용할 수 있습니다.
5. Preview는 일반적으로 운영 지원 대상이 아니므로 실험 브랜치·샌드박스·컨테이너로 격리합니다.

예시 `global.json`은 다음과 같습니다. 실제 설치된 전체 버전은 `dotnet --list-sdks` 결과에 맞춥니다.

```json
{
  "sdk": {
    "version": "11.0.100-preview.6.26359.118",
    "rollForward": "latestPatch",
    "allowPrerelease": true
  }
}
```

## Preview C# 기능을 켜는 방법

일반 프로젝트의 `.csproj`에 다음을 넣습니다.

```xml
<PropertyGroup>
  <TargetFramework>net11.0</TargetFramework>
  <LangVersion>preview</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

`TargetFramework`는 사용할 BCL/API 표면을, `LangVersion`은 컴파일러 문법을 선택합니다. 둘은 관련 있지만 같은 스위치가 아닙니다.

## 실패를 읽는 법

| 메시지 유형 | 보통의 원인 | 확인 |
|---|---|---|
| 구문 오류 | Roslyn이 새 문법을 모름 | SDK/스크립트 도구와 `LangVersion` |
| 형식·멤버를 찾을 수 없음 | 참조 어셈블리에 Preview API가 없음 | `TargetFramework`, SDK |
| 실행 시 `MissingMethodException` | 빌드와 실행 Runtime 불일치 | `dotnet --list-runtimes` |
| 플랫폼 미지원 | Windows/Android/iOS 등 전용 API | OS, RID, workload |
| trim/AOT 경고 | 리플렉션으로 정적 분석이 어려움 | source generator, annotations |

## 첫 실습

```powershell
dotnet script ./00_EnvironmentCheck.csx
dotnet script ./00_BeginnerSyntaxLab.csx
```

첫 파일 출력에서 Framework description, CLR version, OS, process architecture, GC 모드를 찾습니다. SDK 버전은 실행 중인 앱의 CLR 버전과 동일한 개념이 아니라는 점이 첫 번째 핵심입니다. 두 번째 파일은 [C# 최소 문법 문서](./00-csharp-primer.md)와 함께 한 줄씩 읽습니다.

## 공식 기초 링크

- [.NET CLI 개요](https://learn.microsoft.com/dotnet/core/tools/)
- [.NET 용어집](https://learn.microsoft.com/dotnet/standard/glossary)
- [.NET 11 Preview 6 다운로드](https://dotnet.microsoft.com/download/dotnet/11.0)

> 다음: [C# 코드를 읽기 위한 최소 문법](./00-csharp-primer.md)
