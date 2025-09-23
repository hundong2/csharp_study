# 단독 C# 파일에서 NuGet을 쓸 수 있나?
요약: dotnet run은 프로젝트(.csproj) 없이 단독 .cs 파일에서 NuGet 복원을 지원하지 않습니다. 대안으로 C# 스크립트(.csx) + dotnet-script를 쓰거나, 최소 콘솔 프로젝트를 생성해 패키지를 참조하세요.

## 예시 파일
[hello.csx (dotnet-script 샘플)](https://github.com/dotnet-script/dotnet-script/blob/main/samples/hello/hello.csx)

## 답변
핵심 정리
- dotnet run은 프로젝트가 필요합니다. 단독 .cs로는 NuGet 패키지 복원/참조가 불가합니다.
- NuGet을 프로젝트 없이 쓰려면 C# 스크립트(.csx)의 #r "nuget: ..." 지시문을 지원하는 실행기(dotnet-script 등)가 필요합니다.
- 가장 간단한 방법 두 가지:
  1) dotnet-script로 .csx 실행(프로젝트 불필요, NuGet OK)
  2) 최소 콘솔 프로젝트 생성 후 dotnet add package

방법 A) 프로젝트 없이 .csx + NuGet 사용(dotnet-script)
- 설치(맥):
  - dotnet tool install -g dotnet-script
  - echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc && source ~/.zshrc
- 스크립트 작성(예: Newtonsoft.Json 사용)

````csharp
#!/usr/bin/env dotnet-script
#r "nuget: Newtonsoft.Json, 13.0.3"

using Newtonsoft.Json;

var obj = new { Name = "Kim", Age = 30 };
var json = JsonConvert.SerializeObject(obj);
Console.WriteLine(json);
````

- 실행:
  - chmod +x hello.csx
  - ./hello.csx
  - 또는: dotnet script hello.csx

Tip: 다른 패키지도 같은 방식으로 #r "nuget: 패키지명, 버전" 추가.

방법 B) 최소 콘솔 프로젝트로 NuGet 사용
- 초기화:
  - mkdir Demo && cd Demo
  - dotnet new console
- 패키지 추가:
  - dotnet add package Newtonsoft.Json --version 13.0.3
- 코드(Program.cs):

````csharp
using Newtonsoft.Json;

var obj = new { Name = "Kim", Age = 30 };
Console.WriteLine(JsonConvert.SerializeObject(obj));
````

- 실행: dotnet run

비교 요약
- 단독 .cs + dotnet run: NuGet X
- .csx + dotnet-script: NuGet O (#r "nuget")
- .csproj + dotnet run: NuGet O (dotnet add package)

### 추가 자료
- [dotnet run 문서](https://learn.microsoft.com/dotnet/core/tools/dotnet-run)
- [dotnet-script (C# 스크립트 실행기)](https://github.com/dotnet-script/dotnet-script)
- [C# 스크립팅 개요(.csx, #r "nuget")](https://learn.microsoft.com/dotnet/csharp/scripting/)