# csharp_study

C# / .NET 학습 자료를 체계적으로 정리한 저장소입니다.

> **💡 이 README는 `generate-readme-map` 스킬로 자동 생성됩니다.**
> GitHub Copilot 에이전트 채팅에서 아래와 같이 입력하면 폴더 구조를 분석해 이 맵을 재생성합니다.
> ```
> /generate-readme-map
> ```
> 또는 자연어로 "README 맵 만들어줘", "폴더 구조 문서화해줘" 라고 입력해도 됩니다.
> 스킬 정의: [.agents/skills/generate-readme-map/SKILL.md](./.agents/skills/generate-readme-map/SKILL.md)

---

## 📌 빠른 탐색

| 유형 | 바로가기 |
|------|----------|
| 🔤 C# 언어 기초 | [기초 개념](#-c-언어-기초) |
| 🧪 코드 예제 | [예제 모음](#-코드-예제) |
| 🖥️ WPF / UI | [WPF 학습](#️-wpf--ui) |
| ⚡ 비동기 / 병렬 | [비동기 학습](#-비동기--병렬-처리) |
| 🌐 Blazor / Web | [웹 학습](#-blazor--web) |
| 🤖 Semantic Kernel | [AI 학습](#-semantic-kernel--ai) |
| 📰 Daily Insight | [뉴스레터](#-net-daily-insight) |
| 🛠️ 빌드 / 환경 설정 | [빌드 정보](#️-빌드--환경-설정) |
| 📚 아키텍처 / 심화 | [심화 학습](#-아키텍처--심화-학습) |
| 📅 일일 학습 기록 | [데일리 스터디](#-일일-학습-기록) |

---

## 🔤 C# 언어 기초

> C# 언어 문법, .NET 런타임 개념, 버전별 신기능 정리

| 문서 | 설명 |
|------|------|
| [basic.md](./basic.md) | .NET 런타임 구조, CTS/CLS/CIL 기초 개념 |
| [reference.md](./reference.md) | 참고 링크 모음 |
| [tip.md](./tip.md) | 개발 팁 모음 |
| [csharstudy/README.md](./csharstudy/README.md) | dotnet 프로젝트 기초 가이드 |
| [csharstudy/BCL.md](./csharstudy/BCL.md) | Base Class Library 정리 |
| [csharstudy/Covariance.md](./csharstudy/Covariance.md) | 공변성/반공변성 (Covariance/Contravariance) |
| [csharstudy/EnumerableEnumerator.md](./csharstudy/EnumerableEnumerator.md) | IEnumerable / IEnumerator |
| [csharstudy/AnonymousFunction.md](./csharstudy/AnonymousFunction.md) | 익명 함수, 람다 |
| [csharstudy/threadSafeType.md](./csharstudy/threadSafeType.md) | 스레드 안전 타입 |
| [csharstudy/DOTNET_SCRIPT.md](./csharstudy/DOTNET_SCRIPT.md) | dotnet-script 사용법 |
| [csharstudy/INSTALL.md](./csharstudy/INSTALL.md) | .NET 설치 가이드 |

### C# 버전별 역사

| 버전 | 경로 |
|------|------|
| C# 2.0 | [csharstudy/history/csharp2.0/](./csharstudy/history/csharp2.0/) |
| C# 3.0 | [csharstudy/history/csharp3.0/](./csharstudy/history/csharp3.0/) |
| C# 4.0 | [csharstudy/history/csharp4.0/](./csharstudy/history/csharp4.0/) |
| C# 5.0 | [csharstudy/history/csharp5.0/](./csharstudy/history/csharp5.0/) |
| C# 6.0 | [csharstudy/history/csharp6.0/](./csharstudy/history/csharp6.0/) |
| C# 7.x | [csharstudy/history/csharp7.1/](./csharstudy/history/csharp7.1/) · [7.2](./csharstudy/history/csharp7.2/) |
| C# 8.0 | [csharstudy/history/csharp8.0/](./csharstudy/history/csharp8.0/) |
| C# 9.0 | [csharstudy/history/csharp9.0/](./csharstudy/history/csharp9.0/) |
| C# 10.0 | [csharstudy/history/csharp10.0/](./csharstudy/history/csharp10.0/) |
| 최신 기능 | [csharstudy/history/new/](./csharstudy/history/new/) · [csharstudy/history/csharpScript.md](./csharstudy/history/csharpScript.md) |

> 🔗 [C# Language Feature Status (공식)](https://github.com/dotnet/roslyn/blob/main/docs/Language%20Feature%20Status.md)

---

## 🧪 코드 예제

> 다양한 C# 기능별 실행 가능한 코드 예제 모음

### dotnet 예제 프로젝트

| 폴더 | 설명 |
|------|------|
| [csharstudy/dotnetexample/](./csharstudy/dotnetexample/) | 예제 솔루션 루트 (dotnetexample.sln) |
| [dotnetexample/Interface/](./csharstudy/dotnetexample/Interface/) | 인터페이스 예제 |
| [dotnetexample/DelegateExample/](./csharstudy/dotnetexample/DelegateExample/) | 델리게이트 예제 |
| [dotnetexample/EventExample/](./csharstudy/dotnetexample/EventExample/) | 이벤트 예제 |
| [dotnetexample/CollectionExample/](./csharstudy/dotnetexample/CollectionExample/) | 컬렉션 예제 |
| [dotnetexample/ExampleHashtable/](./csharstudy/dotnetexample/ExampleHashtable/) | Hashtable 예제 |
| [dotnetexample/ConvertExample/](./csharstudy/dotnetexample/ConvertExample/) | 형변환 예제 |
| [dotnetexample/StringExample/](./csharstudy/dotnetexample/StringExample/) | 문자열 예제 |
| [dotnetexample/Datetime/](./csharstudy/dotnetexample/Datetime/) | 날짜/시간 예제 |
| [dotnetexample/Enum/](./csharstudy/dotnetexample/Enum/) | 열거형 예제 |
| [dotnetexample/indexer/](./csharstudy/dotnetexample/indexer/) | 인덱서 예제 |
| [dotnetexample/Member/](./csharstudy/dotnetexample/Member/) | 멤버 예제 |
| [dotnetexample/featureExample1/](./csharstudy/dotnetexample/featureExample1/) | 기능 예제 |
| [dotnetexample/Execute/](./csharstudy/dotnetexample/Execute/) | 실행 예제 |
| [dotnetexample/chapter6Example/](./csharstudy/dotnetexample/chapter6Example/) | 챕터6 예제 |

### 독립 예제 프로젝트

| 프로젝트 | 설명 |
|----------|------|
| [Example/](./Example/) | 예제 솔루션 (Example.sln) |
| [ExampleSet/](./ExampleSet/) | WPF + SQL 예제 모음 (ExampleSet.sln) |
| [nettest/](./nettest/) | .NET 기능 테스트 프로젝트 |
| [wslbuild/](./wslbuild/) | WSL 빌드 예제 |

---

## 🖥️ WPF / UI

> Windows Presentation Foundation 관련 학습 자료 및 예제

| 문서 / 프로젝트 | 설명 |
|----------------|------|
| [WPF/MVVM.md](./WPF/MVVM.md) | MVVM 패턴 설명 |
| [WPF/xaml.md](./WPF/xaml.md) | XAML 문법 정리 |
| [WPF/FileWatcher.md](./WPF/FileWatcher.md) | FileSystemWatcher 활용 |
| [reference/Wpf_Resource.md](./reference/Wpf_Resource.md) | WPF 참고 리소스 |
| [SimpleAnimation/](./SimpleAnimation/) | WPF 애니메이션 예제 |
| [SortingOrderingLists/](./SortingOrderingLists/) | WPF 리스트 정렬 예제 |
| [Using_WPF_commands/](./Using_WPF_commands/) | WPF 커맨드 패턴 예제 |
| [2.DataBindingListToClass/](./2.DataBindingListToClass_-_Chapter_1_Bind_List_to_Class/) | WPF 데이터 바인딩 예제 (List → Class) |
| [MVVM-Samples-sample-winui3-desktop/](./MVVM-Samples-sample-winui3-desktop/) | WinUI3 MVVM 샘플 |

---

## ⚡ 비동기 / 병렬 처리

> async/await, Task Parallel Library(TPL), 동시성 컬렉션

| 문서 / 파일 | 설명 |
|------------|------|
| [csharstudy/asyncawait/reference.md](./csharstudy/asyncawait/reference.md) | async/await 참고 자료 |
| [csharstudy/asyncawait/exampleAsync.cs](./csharstudy/asyncawait/exampleAsync.cs) | 비동기 코드 예제 |
| [csharstudy/asyncawait/exampleSync.cs](./csharstudy/asyncawait/exampleSync.cs) | 동기 코드 예제 |
| [csharstudy/TPL/README.md](./csharstudy/TPL/README.md) | TPL 개요 |
| [TPL/ConcurrentBag.md](./csharstudy/TPL/ConcurrentBag.md) | ConcurrentBag 사용법 |
| [TPL/MultipleIO.md](./csharstudy/TPL/MultipleIO.md) | 다중 I/O 처리 |
| [TPL/AggregateException.Handle.md](./csharstudy/TPL/AggregateException.Handle.md) | 예외 처리 |
| [TPL/ConcurrentBagForBlazor.md](./csharstudy/TPL/ConcurrentBagForBlazor.md) | Blazor에서 ConcurrentBag |

---

## 🌐 Blazor / Web

> Blazor 및 ASP.NET Core 웹 학습 자료

| 문서 | 설명 |
|------|------|
| [csharstudy/blazor/README.md](./csharstudy/blazor/README.md) | Blazor 개요 |
| [MicrosoftLearn/WebApplication.md](./MicrosoftLearn/WebApplication.md) | ASP.NET Core 웹 앱 |
| [MicrosoftLearn/Blazor/InjectionData.md](./MicrosoftLearn/Blazor/InjectionData.md) | Blazor 데이터 인젝션 |
| [MicrosoftLearn/Blazor/Opensource.md](./MicrosoftLearn/Blazor/Opensource.md) | Blazor 오픈소스 정보 |
| [loxodon-framework/](./loxodon-framework/) | Loxodon MVVM 프레임워크 |

---

## 🤖 Semantic Kernel / AI

> Microsoft Semantic Kernel을 활용한 AI 통합 학습

| 문서 | 설명 |
|------|------|
| [MicrosoftLearn/SemanticKernel/SemanticKernelUsingPrompt.md](./MicrosoftLearn/SemanticKernel/SemanticKernelUsingPrompt.md) | 프롬프트 기반 사용법 |
| [SemanticKernel/FunctionCall.md](./MicrosoftLearn/SemanticKernel/FunctionCall.md) | 함수 호출 |
| [SemanticKernel/CallObject.md](./MicrosoftLearn/SemanticKernel/CallObject.md) | 객체 호출 |
| [SemanticKernel/GetResultClassObject.md](./MicrosoftLearn/SemanticKernel/GetResultClassObject.md) | 결과 클래스 객체 반환 |
| [SemanticKernel/SemanticKernelUsingSingleTon.md](./MicrosoftLearn/SemanticKernel/SemanticKernelUsingSingleTon.md) | 싱글톤 패턴 적용 |
| [Bot/README.md](./Bot/README.md) | 봇 개발 관련 자료 |

---

## 📰 .NET Daily Insight

> 매일 오전 6시(KST) GitHub Actions가 AI로 생성한 .NET 학습 뉴스레터

| 항목 | 링크 |
|------|------|
| 전체 인사이트 목록 | [insight/README.md](./insight/README.md) |
| 2026-02-18 | [insight/2026-02-18/](./insight/2026-02-18/) |
| 2026-02-19 | [insight/2026-02-19/](./insight/2026-02-19/) |
| 2026-02-20 | [insight/2026-02-20/](./insight/2026-02-20/) |
| 2026-02-21 | [insight/2026-02-21/](./insight/2026-02-21/) |
| 2026-02-22 | [insight/2026-02-22/](./insight/2026-02-22/) |

> **사전 준비**: 레포지토리 Settings → Secrets → `GEMINI_API_KEY` 또는 `OPENAI_API_KEY` 등록

---

## 🛠️ 빌드 / 환경 설정

> 프로젝트 생성, 빌드, .NET 설치 관련 정보

| 문서 | 설명 |
|------|------|
| [build.md](./build.md) | dotnet 빌드 명령어 |
| [csharstudy/dotnetexample/build.md](./csharstudy/dotnetexample/build.md) | 예제 프로젝트 빌드 |
| [csharstudy/dotnetexample/projectStructure.md](./csharstudy/dotnetexample/projectStructure.md) | 프로젝트 구조 설명 |
| [dotnet-install.sh](./dotnet-install.sh) | .NET 설치 스크립트 |
| [ExampleCollection.sln](./ExampleCollection.sln) | 전체 예제 솔루션 파일 |

---

## 📚 아키텍처 / 심화 학습

> 소프트웨어 아키텍처, 디자인 패턴, 심화 주제

| 문서 | 설명 |
|------|------|
| [MicrosoftLearn/Architecture.md](./MicrosoftLearn/Architecture.md) | 소프트웨어 아키텍처 개요 |
| [MicrosoftLearn/Reference.md](./MicrosoftLearn/Reference.md) | Microsoft Learn 참고 자료 |
| [study/dotnet-software-architecture-guide.md](./study/dotnet-software-architecture-guide.md) | .NET 소프트웨어 아키텍처 가이드 |
| [study/dotnetbuild.md](./study/dotnetbuild.md) | .NET 빌드 심화 |
| [study/json_yaml_comparison.md](./study/json_yaml_comparison.md) | JSON vs YAML 비교 |
| [study/YARP/](./study/YARP/) | YARP(역방향 프록시) 학습 |
| [csharstudy/Medium/tip.md](./csharstudy/Medium/tip.md) | 성능 팁 모음 |
| [csharstudy/Medium/Performance/](./csharstudy/Medium/Performance/) | 성능 최적화 예제 |
| [news.md](./news.md) | .NET 관련 뉴스 모음 |

---

## 📅 일일 학습 기록

> 날짜별 학습 기록 및 실습 프로젝트

| 폴더 / 파일 | 설명 |
|------------|------|
| [dailyStudy/20260607.ipynb](./dailyStudy/20260607.ipynb) | 2026-06-07 Jupyter 노트북 |
| [dailyStudy/20260630/](./dailyStudy/20260630/) | 2026-06-30 학습 기록 |
| [dailyStudy/20260702/](./dailyStudy/20260702/) | 2026-07-02 학습 기록 |
| [dailyStudy/DailyStudy2406/](./dailyStudy/DailyStudy2406/) | 2024년 6월 일일 스터디 프로젝트 |
| [dailyStudy/SolutionDailyApril/](./dailyStudy/SolutionDailyApril/) | 4월 일일 스터디 솔루션 |
| [dailyStudy/tip/](./dailyStudy/tip/) | 학습 팁 (비동기, JSON, 리플렉션 등) |
| [study/20230820.md](./study/20230820.md) | 2023-08-20 학습 기록 |
