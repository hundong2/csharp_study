# .sln(솔루션) 파일 구조 이해와 면접 포인트
솔루션 파일은 여러 .csproj를 한데 묶어 구성/빌드 구성을 관리하는 “목차” 역할을 합니다. 빌드 순서는 프로젝트 간 참조로 결정되며, 솔루션은 주로 그룹핑과 구성을 담당합니다.

## 예시 파일
[roslyn.sln (dotnet/roslyn)](https://github.com/dotnet/roslyn/blob/main/roslyn.sln)

## 답변
핵심 구조 해설(질문에 첨부된 sln 기준):
- 헤더
  - Microsoft Visual Studio Solution File, Format Version 12.00: 솔루션 파일 포맷 버전
  - VisualStudioVersion / MinimumVisualStudioVersion: 이 솔루션을 만든/최소로 여는 VS 버전 정보. dotnet CLI 빌드에는 큰 영향 없음.
- Project(…) 항목
  - 예: Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Example1", "Example1\Example1.csproj", "{2933…}"
  - 첫 GUID: “프로젝트 유형 GUID”(여기서는 C# 프로젝트).
  - "이름", "상대 경로", "프로젝트 GUID": 솔루션 내 프로젝트 식별에 사용.
  - ExpandModule 항목도 같은 방식으로 등록.
- Global 섹션
  - SolutionConfigurationPlatforms: 솔루션 레벨의 구성/플랫폼 목록(예: Debug/Release × Any CPU/x64/x86).
  - ProjectConfigurationPlatforms: 각 프로젝트에 대해 솔루션 구성 매핑
    - ActiveCfg: 솔루션의 특정 구성/플랫폼을 선택했을 때 프로젝트가 사용할 실제 구성/플랫폼.
    - Build.0: 빌드 대상에 포함 여부 플래그.
    - 현재는 x64/x86도 모두 Any CPU로 매핑되어 있어, 어떤 플랫폼을 골라도 프로젝트는 Any CPU로 빌드됩니다.
  - SolutionProperties: 솔루션 노드 표시 여부 등(주로 Visual Studio UI용).

중요 개념 정리:
- sln vs csproj
  - sln: 프로젝트 모음과 빌드/실행 구성 관리(의존성 정의는 아님).
  - csproj: 실제 빌드 대상 설정(타겟 프레임워크, NuGet, 프로젝트 참조, 출력 형식 등)을 보유. 빌드 순서는 csproj의 ProjectReference로 결정.
- 프로젝트 유형 GUID
  - {FAE04EC0-…}: C# 프로젝트 유형을 의미. 최신 SDK 스타일도 호환됩니다.
- 구성/플랫폼
  - Any CPU, x86, x64 설정은 “선언”일 뿐, 매핑에 따라 어떤 구성으로 빌드할지가 결정됩니다.
- CLI와 sln
  - dotnet build ReflectionExample.sln: 솔루션 단위 빌드
  - dotnet sln add/remove: 솔루션에 프로젝트 추가/삭제
  - dotnet run --project <경로/프로젝트.csproj>: 특정 프로젝트 실행

간소화 예시(불필요한 x86/x64 제거한 최소 구성)
````plaintext
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Example1", "Example1\Example1.csproj", "{2933CEDC-BF37-45F2-AF04-39AD72090922}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ExpandModule", "ExpandModule\ExpandModule.csproj", "{A3596B7F-14E1-4513-AE46-25FF8937C009}"
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {2933CEDC-BF37-45F2-AF04-39AD72090922}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {2933CEDC-BF37-45F2-AF04-39AD72090922}.Debug|Any CPU.Build.0  = Debug|Any CPU
    {2933CEDC-BF37-45F2-AF04-39AD72090922}.Release|Any CPU.ActiveCfg = Release|Any CPU
    {2933CEDC-BF37-45F2-AF04-39AD72090922}.Release|Any CPU.Build.0  = Release|Any CPU
    {A3596B7F-14E1-4513-AE46-25FF8937C009}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A3596B7F-14E1-4513-AE46-25FF8937C009}.Debug|Any CPU.Build.0  = Debug|Any CPU
    {A3596B7F-14E1-4513-AE46-25FF8937C009}.Release|Any CPU.ActiveCfg = Release|Any CPU
    {A3596B7F-14E1-4513-AE46-25FF8937C009}.Release|Any CPU.Build.0  = Release|Any CPU
  EndGlobalSection
  GlobalSection(SolutionProperties) = preSolution
    HideSolutionNode = FALSE
  EndGlobalSection
EndGlobal
````

면접에서 알아두면 좋은 포인트:
- sln의 역할: 멀티 프로젝트 오케스트레이션 vs 실제 설정은 csproj에.
- 빌드 순서: sln 순서가 아니라 ProjectReference 그래프에 의해 결정됨.
- 구성/플랫폼 매핑: ActiveCfg/Build.0 의미와 Any CPU/x86/x64 차이.
- CI 파이프라인: 솔루션 단위 빌드(dotnet build .sln) vs 프로젝트 단위 빌드 선택 기준.
- 참조 관리: 솔루션에 추가해도 실제 의존성은 csproj의 <ProjectReference>로 연결해야 함.
- SDK 스타일 프로젝트: 기본 포함 규칙, 멀티 타겟팅(TFM), NuGet 참조가 csproj에서 관리됨.

빠른 실무 팁(맥, VS Code 터미널):
- 솔루션 생성/관리
  - dotnet new sln -n ReflectionExample
  - dotnet sln add Example1/Example1.csproj ExpandModule/ExpandModule.csproj
  - dotnet sln list
  - dotnet sln remove ExpandModule/ExpandModule.csproj
- 빌드/실행
  - dotnet build ReflectionExample.sln -c Release
  - dotnet run --project Example1/Example1.csproj

### 추가 자료
- [Solution(.sln) 파일 형식 문서](https://learn.microsoft.com/visualstudio/extensibility/internals/solution-dot-sln-file)
- [dotnet sln 명령어](https://learn.microsoft.com/dotnet/core/tools/dotnet-sln)
- [MSBuild ProjectReference와 빌드 의존성](https://learn.microsoft.com/visualstudio/msbuild/common-msbuild-project-items#projectreference)
- [SDK 스타일 프로젝트 개요](https://learn.microsoft.com/dotnet/core/project-sdk/overview)