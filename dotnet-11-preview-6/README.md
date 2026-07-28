# .NET 11 Preview 6 완전 학습 가이드

2026-07-14에 공개된 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)를 출발점으로, C#을 처음 접하는 학습자도 CLR·JIT·웹·데이터·모바일·도구 체계까지 이어서 공부할 수 있도록 만든 실습 자료입니다.

> 이 자료는 Preview 6 시점의 API와 문법을 설명합니다. Preview API는 정식 출시 전 바뀔 수 있으며 운영 환경에는 권장되지 않습니다. 현재 저장소 PC에는 .NET 10 SDK와 `dotnet-script`가 있으므로, `.csx`는 .NET 10에서도 실행되는 원리 실습과 Preview 6 전용 코드 설명을 분리했습니다.

---

## 📌 빠른 탐색

| 단계 | 문서 | 학습 결과 |
|---:|---|---|
| 0 | [시작·설치·실행](./00-getting-started.md) | SDK, Runtime, CLI, CSX의 차이와 실습 방법을 안다 |
| 0.5 | [C# 최소 문법](./00-csharp-primer.md) | 변수, 연산자, 조건문, 반복문, 메서드, 객체, null과 예외를 읽는다 |
| 1 | [C# 기초와 CLR/JIT](./01-foundations-clr-jit.md) | 소스가 IL과 네이티브 코드가 되는 전 과정을 설명한다 |
| 2 | [Libraries와 Runtime](./02-libraries-runtime.md) | 스트림, 검증, JSON, 추적, 프로세스, async, SIMD와 JIT 개선을 이해한다 |
| 3 | [SDK와 C# 15](./03-sdk-csharp.md) | 테스트·AOT CLI·파일 기반 앱·컨테이너 게시·extension indexer·union을 익힌다 |
| 4 | [ASP.NET Core](./04-aspnet-core.md) | 비동기 검증, CSRF, Blazor, OpenAPI, SignalR와 union 웹 API를 이해한다 |
| 5 | [MAUI·EF Core·F#·컨테이너](./05-maui-ef-fsharp-containers.md) | 나머지 발표 링크의 핵심 개념과 적용 판단 기준을 익힌다 |
| 6 | [원문 링크 커버리지](./06-link-coverage.md) | 발표 본문의 모든 기술 링크가 어느 자료에서 다뤄지는지 확인한다 |
| 7 | [연습 문제와 해설](./07-exercises.md) | 기초부터 런타임 내부까지 스스로 점검한다 |

---

## 🧪 실행 가능한 CSX 실습

> 각 파일은 중요한 코드 줄 바로 위에 `// 01`, `// 02` 형식으로 문법과 CLR 동작을 설명합니다.

| 순서 | 파일 | 실행 환경 | 핵심 |
|---:|---|---|---|
| 0 | [00_EnvironmentCheck.csx](./00_EnvironmentCheck.csx) | .NET 8+ | SDK·Runtime·프로세스·GC 환경 확인 |
| 0.5 | [00_BeginnerSyntaxLab.csx](./00_BeginnerSyntaxLab.csx) | .NET 8+ | 변수·조건문·반복문·배열·메서드·객체·null·예외 |
| 1 | [01_CSharpClrPipeline.csx](./01_CSharpClrPipeline.csx) | .NET 8+ | 변수, 형식, 메서드, 제네릭, IL, JIT, GC |
| 2 | [02_LibrariesPreview6.csx](./02_LibrariesPreview6.csx) | .NET 8+ | 메모리/텍스트 스트림, 검증, JSON, Activity, Process |
| 3 | [03_AsyncJitSimd.csx](./03_AsyncJitSimd.csx) | .NET 8+ | async 상태 머신, ExecutionContext, Tiered JIT, SIMD |
| 4 | [04_CSharp15Preview.csx](./04_CSharp15Preview.csx) | .NET 8+ | C# 15 문법의 안정 버전 등가 구현과 Preview 코드 |
| 5 | [05_AspNetCoreConcepts.csx](./05_AspNetCoreConcepts.csx) | .NET 8+ | 요청 검증, CSRF 판단, union 결과, 취소 전파 |
| 6 | [06_DataMobileContainers.csx](./06_DataMobileContainers.csx) | .NET 8+ | FullJoin, NULLIF, 위치 필터, 컨테이너 크기 계산 |
| 7 | [07_CapstonePipeline.csx](./07_CapstonePipeline.csx) | .NET 8+ | 검증→분류→비동기 처리→추적의 종합 실습 |

PowerShell에서 다음처럼 실행합니다.

```powershell
cd dotnet-11-preview-6
dotnet script 00_EnvironmentCheck.csx
dotnet script 00_BeginnerSyntaxLab.csx
dotnet script 01_CSharpClrPipeline.csx
dotnet script 02_LibrariesPreview6.csx
dotnet script 03_AsyncJitSimd.csx
dotnet script 04_CSharp15Preview.csx
dotnet script 05_AspNetCoreConcepts.csx
dotnet script 06_DataMobileContainers.csx
dotnet script 07_CapstonePipeline.csx
```

### 실습 한 개를 공부하는 네 단계

1. **예상**: 실행 전에 `Console.WriteLine` 결과를 종이에 적습니다.
2. **실행**: 오류가 나면 가장 위의 첫 compiler error부터 읽습니다.
3. **변형**: 숫자·문자열·조건을 한 번에 하나만 바꾸고 결과 차이를 봅니다.
4. **설명**: “이 줄의 입력, 출력, 형식, CLR 동작”을 자신의 말로 설명합니다.

정상 실행의 핵심 표시는 다음과 같습니다. PID·메모리·GUID처럼 실행마다 바뀌는 값은 그대로 일치하지 않아도 됩니다.

| 파일 | 확인할 핵심 출력 |
|---|---|
| `00_BeginnerSyntaxLab.csx` | `평균 = 85.0`, `기초 문법 실습 완료` |
| `01_CSharpClrPipeline.csx` | `hot-loop checksum = 50000` |
| `02_LibrariesPreview6.csx` | `Activity allocation 생략 = True` |
| `03_AsyncJitSimd.csx` | `AsyncLocal after await = request-42`, `SIMD sum = 528` |
| `04_CSharp15Preview.csx` | `last = done`, `dog: Rex`, `cat: 9` |
| `05_AspNetCoreConcepts.csx` | `CSRF reject = True`, `hub invocation cancelled cooperatively` |
| `06_DataMobileContainers.csx` | 세 FULL JOIN 행, `123.9 MB (30.9%)` |
| `07_CapstonePipeline.csx` | `status`가 `202`인 JSON |

---

## 🗺️ 권장 학습 순서

```text
SDK/Runtime 구분
  → C# 기본 문법과 형식 시스템
    → IL·메타데이터·CLR 로더
      → JIT 계층 컴파일·PGO·GC·async
        → Preview 6 Libraries/C# 기능
          → ASP.NET Core·EF Core·MAUI
            → NativeAOT·컨테이너·운영 진단
```

처음이라면 문서를 순서대로 읽고 바로 대응하는 CSX를 실행합니다. 특히 C# 경험이 없다면 환경 확인 뒤 [C# 최소 문법](./00-csharp-primer.md)을 건너뛰지 않습니다. 경험자라면 [원문 링크 커버리지](./06-link-coverage.md)에서 관심 항목을 바로 찾을 수 있습니다.

---

## ✅ 완료 기준

- [ ] SDK와 Runtime의 차이를 말할 수 있다.
- [ ] C# 소스가 Roslyn을 거쳐 IL·메타데이터가 되고 CLR이 이를 로드하는 과정을 설명할 수 있다.
- [ ] Tier 0, Tier 1, Dynamic PGO, OSR, GC safe point가 왜 필요한지 설명할 수 있다.
- [ ] Preview 6 본문의 모든 기술 링크를 [커버리지 표](./06-link-coverage.md)에서 확인했다.
- [ ] 9개 CSX를 실행하고 `07-exercises.md` 문제를 풀었다.
- [ ] Preview 기능을 운영 코드와 격리해야 하는 이유를 설명할 수 있다.

---

## 🔁 초보자 관점 3회 검토 반영

| 검토 | 발견한 장벽 | 개선 |
|---:|---|---|
| 1회차 | SDK 다음에 곧바로 IL/JIT가 나와 기본 문법이 없는 학습자는 진입하기 어려움 | [C# 최소 문법](./00-csharp-primer.md)과 40단계 [기초 실습](./00_BeginnerSyntaxLab.csx) 추가 |
| 2회차 | 코드 주석은 많지만 `!`, LINQ 지연 실행, switch case, BigMul 출력처럼 결과의 이유가 모호한 부분 존재 | 모든 CSX에 선행 문서, 줄별 보충 설명, 정상 출력, 4단계 실습법 추가 |
| 3회차 | Preview 기능 링크는 충분하지만 C#·CLR·GC·async·NativeAOT를 더 공부할 공식 기초 링크와 장 사이 이동 경로 부족 | Microsoft Learn 기초 링크와 각 문서의 이전/다음 학습 링크 추가 |

> 🔗 [공식 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/) · [공식 다운로드](https://dotnet.microsoft.com/download/dotnet/11.0) · [공식 Preview 6 릴리스 노트](https://github.com/dotnet/core/tree/main/release-notes/11.0/preview/preview6)
