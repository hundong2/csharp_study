# VS Code에서 .NET 솔루션, 콘솔 앱, 클래스 라이브러리 만들기
VS Code에서 dotnet CLI와 C# Dev Kit 확장을 사용해 솔루션(.sln), 콘솔 앱, 클래스 라이브러리를 빠르게 생성하고 연결하는 방법을 단계별로 정리합니다.

## 예시 파일
[Program.cs (Microsoft .NET 샘플)](https://github.com/dotnet/samples/blob/main/core/console-apps/HelloMsBuild/Program.cs)

## 답변
아래 두 가지 방법 중 편한 방법을 쓰면 됩니다. 맥에서 터미널 명령은 동일합니다.

1) dotnet CLI로 빠르게 만들기(권장, 어디서나 동일)
- 터미널(또는 VS Code 통합 터미널)에서 실행:
  - cd /Users/donghun2/workspace/csharp_study
  - 솔루션 생성
    - dotnet new sln -n StudySolution
  - 콘솔 앱 생성
    - dotnet new console -n MyApp -f net8.0 -o src/MyApp
  - 클래스 라이브러리 생성
    - dotnet new classlib -n MyLibrary -f net8.0 -o src/MyLibrary
  - 솔루션에 프로젝트 추가
    - dotnet sln StudySolution.sln add src/MyApp/MyApp.csproj src/MyLibrary/MyLibrary.csproj
  - 콘솔에서 라이브러리 참조 추가
    - dotnet add src/MyApp/MyApp.csproj reference src/MyLibrary/MyLibrary.csproj
  - 빌드/실행
    - dotnet build StudySolution.sln
    - dotnet run --project src/MyApp/MyApp.csproj

- 예시 코드 추가(라이브러리 + 콘솔)
````csharp
namespace MyLibrary;

public static class Greetings
{
    public static string Hello(string name) => $"Hello, {name}!";
}
````

````csharp
using MyLibrary;

Console.WriteLine(Greetings.Hello("VS Code"));
````

2) VS Code UI로 쉽게 만들기(C# Dev Kit 활용)
- 확장 설치(좌측 Extensions 아이콘):
  - C# (ms-dotnettools.csharp)
  - C# Dev Kit (ms-dotnettools.csdevkit)
  - NuGet Package Manager(선택, jmrog.vscode-nuget-package-manager)
- 명령 팔레트 열기: Cmd+Shift+P
  - “C# Dev Kit: New Project” → Console App 선택 → 위치: /Users/donghun2/workspace/csharp_study
  - Solution Explorer(좌측)에서 솔루션 우클릭 → Add → New Project → Class Library 추가
  - 콘솔 프로젝트 우클릭 → Add Project Reference → 방금 만든 Class Library 선택
  - 상단 실행(▶︎) 또는 터미널에서 dotnet run으로 실행

팁과 확인사항
- 솔루션은 여러 프로젝트를 한데 묶어 관리(권장). 단일 프로젝트만 있을 때는 없어도 동작합니다.
- 대상 프레임워크 변경은 각 .csproj의 <TargetFramework>net8.0</TargetFramework>로 설정합니다.
- 템플릿 목록 보기: dotnet new list
- 실행 시 프로젝트 지정: dotnet run --project <경로/프로젝트.csproj>

### 추가 자료
- [VS Code로 .NET 클래스 라이브러리 만들기(공식 튜토리얼)](https://learn.microsoft.com/dotnet/core/tutorials/library-with-visual-studio-code?pivots=dotnet-8-0)
- [dotnet CLI 템플릿(donet new) 목록](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates)
- [프로젝트 간 참조(dotnet add reference)](https://learn.microsoft.com/dotnet/core/tools/dotnet-add-reference)
- [C# Dev Kit 소개](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

# VS Code에서 솔루션/프로젝트에 .cs 파일 추가하는 방법
SDK 스타일(.NET 6/7/8)의 프로젝트는 프로젝트 폴더에 .cs 파일을 추가하면 자동으로 포함됩니다. CLI, VS Code UI(C# Dev Kit), 혹은 csproj 수동 편집으로도 추가할 수 있습니다.

## 예시 파일
[StringLibrary.cs (Microsoft .NET 샘플 클래스 파일)](https://github.com/dotnet/samples/blob/main/core/getting-started/library-with-visual-studio-code/csharp/StringLibrary.cs)

## 답변
아래 순서 중 편한 방법을 사용하세요(맥/VS Code 기준).

1) 가장 간단한 방법: 파일만 추가(자동 포함)
- 전제: 프로젝트가 SDK 스타일인지 확인(.csproj 첫 줄에 Sdk="Microsoft.NET.Sdk").
- 절차:
  - VS Code Explorer에서 프로젝트 폴더 우클릭 → New File → 예: Greeter.cs 생성
  - 파일에 코드 작성 후 저장 → dotnet build 또는 dotnet run으로 확인

2) dotnet CLI로 클래스 파일 생성(아이템 템플릿)
- 프로젝트 폴더에서:
  - cd /Users/donghun2/workspace/csharp_study/csharstudy/dotnetexample/chapter6Example/Reflection/Example1
  - dotnet new class -n Greeter
- 예시 코드(간단 메서드 포함):
````csharp
namespace Example1;

public static class Greeter
{
    public static string Hello(string name) => $"Hello, {name}!";
}
````

3) VS Code UI로 추가(C# Dev Kit 활용)
- 확장 설치: C# (ms-dotnettools.csharp), C# Dev Kit (ms-dotnettools.csdevkit)
- 명령 팔레트(Cmd+Shift+P) → “C# Dev Kit: New Project”로 프로젝트 생성 후
  - 좌측 Solution Explorer에서 대상 프로젝트 우클릭 → Add → New C# Class
  - 이름 입력(예: Greeter) → 파일 생성 → 코드 작성 → 빌드/실행

4) 오래된/특수 설정: csproj에 명시적으로 포함
- 기본 포함이 꺼져 있다면(<EnableDefaultItems>false</EnableDefaultItems> 등), csproj에 다음을 추가:
````xml
<ItemGroup>
  <Compile Include="Greeter.cs" />
</ItemGroup>
````
- 저장 후 빌드: dotnet build

실행/검증
- 콘솔 앱에서 사용 예:
````csharp
// 예: Program.cs 내부 사용 예시
// ...existing code...
Console.WriteLine(Example1.Greeter.Hello("VS Code"));
// ...existing code...
````
- 터미널:
  - dotnet build
  - dotnet run --project <콘솔 프로젝트 경로>.csproj

팁
- 템플릿 목록 보기: dotnet new list
- 네임스페이스는 프로젝트 루트/폴더 구조에 맞게 조정하세요.

### 추가 자료
- [SDK 스타일 프로젝트 개요(기본 포함 규칙)](https://learn.microsoft.com/dotnet/core/project-sdk/overview#default-includes)
- [dotnet new 아이템 템플릿(Class 등)](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates#item-templates)
- [C# Dev Kit 확장(프로젝트/파일 생성)](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)