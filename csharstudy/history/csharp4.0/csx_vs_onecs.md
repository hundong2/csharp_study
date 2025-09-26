# csx vs 단독 .cs 파일(dotnet run file.cs) 차이와 활용
csx(C# Script)는 스크립트/실험·자동화에 최적화된 “상태ful” 스크립트 모델, 단독 .cs 실행은 일반 C# 프로그램을 임시 프로젝트처럼 컴파일해 실행하는 “정식 앱” 모델입니다. 패키지 참조, 언어 지시문, 실행/컴파일 파이프라인, 사용 시나리오가 다릅니다.

## 예시 파일
[단독 콘솔 예시 Program.cs (GitHub)](https://github.com/dotnet/samples/blob/main/core/console-apps/HelloMsBuild/Program.cs)  
[csx 스크립트 예시 hello.csx (dotnet-script)](https://github.com/dotnet-script/dotnet-script/blob/main/samples/hello/hello.csx)

## 답변
아래 표로 핵심 비교 후, 세부 설명과 코드 예제를 제공합니다.

| 구분 | csx (C# Script, dotnet-script / csi) | 단독 .cs (dotnet run file.cs) |
|------|--------------------------------------|--------------------------------|
| 목적 | 빠른 실험, 자동화, REPL 스타일, 빌드 최소화 | “정식” 콘솔 프로그램을 프로젝트 없이 즉시 실행 |
| 언어 모드 | Script 모드(연속 제출, 전역 변수/문 허용) | Regular 모드(표준 C# 컴파일, top-level statements) |
| NuGet 패키지 | #r "nuget: 패키지,버전" 즉시 사용 | (현재) 직접 지원 X → 패키지 필요 시 .csproj 필요 |
| 참조 추가 | #r "path/to/assembly.dll" / #load | using + 기본 프레임워크 참조(확장 어려움) |
| 상태 유지 | 여러 #load / REPL 제출 간 전역 상태 유지 | 매 실행마다 새 프로세스 / 상태 없음 |
| 전역 코드 | 아무 곳이나 문/식 배치 가능 | top-level statements 한 번만 / 순서 고정 |
| 표현식 결과 | REPL/스크립트 호스트가 출력 가능 | 결과 자동 출력 없음 |
| 동적 확장 | 빌드 없이 수정 즉시 실행 | 컴파일(빠르지만 한 번 전부) |
| 지시문 | #r, #load, #nullable 등 스크립트 지시문 풍부 | #r (스크립트 방식) 사용 불가(일반 컴파일) |
| 성능 | 첫 실행 시 엔진 준비 + 일부 인터프리트/컴파일 | JIT 후 일반 콘솔과 동일(대개 더 빠름) |
| 디버깅 | dotnet-script --debug / VS Code 확장 | 일반 디버깅(launch.json) |
| 배포 | 스크립트 파일 그대로 공유 | 필요하면 나중에 프로젝트 생성 가능 |
| 보안 | 임의 코드 실행 → 샌드박스/경로 주의 | 일반 앱과 동일(입력 검증 필요) |
| 주 사용 사례 | 빌드 스크립트, 퀵 PoC, DevTools 자동화, 노트북 유사 | 단일 예제 공유, 아주 작은 유틸, 학습·샘플 |

### 1. csx 스크립트 특징
- 전역 변수/함수/문 가능 (클래스/namespace 감싸기 불필요)
- 여러 번 누적 실행(대화형) 가능 → 상태 유지
- NuGet 패키지 즉시 참조: #r "nuget: Newtonsoft.Json, 13.0.3"
- #load 다른 .csx 병합(모듈화)
- dynamic/Reflection 실험, 도구 자동화(간단 크론 작업 등)에 적합

### 2. 단독 .cs 파일 실행(dotnet run file.cs) 특징
- 내부적으로 “임시 프로젝트”로 컴파일하는 콘솔 프로그램
- 표준 C# 컴파일 → 스크립트 지시문(#r nuget) 미지원
- NuGet 필요하면 결국 프로젝트(.csproj) 생성이 합리적
- Top-level statements로 Main 간소화
- 실행 모델이 실제 배포 앱과 동일 → “샘플 코드 빠른 실행” 용도

### 3. 어떤 때 무엇을 선택?
| 상황 | 추천 |
|------|------|
| 빠르게 라이브러리 실험, REPL처럼 변수 유지 | csx |
| 하나의 간단한 예제(학습) 제출 | 단독 .cs |
| NuGet 패키지 몇 개 붙여 자동화 스크립트 | csx |
| 나중에 확장해 앱으로 전환할 가능성 높음 | 초기부터 .csproj |
| DevOps/빌드 스텝 간단 처리 | csx(docker 베이스에도 손쉬움) |
| 성능 테스트(정확한 JIT 환경) | 단독 .cs 또는 .csproj |

### 4. 코드 예제 비교

#### (A) csx 예제: NuGet + 전역 코드 + 상태
````csharp
#!/usr/bin/env dotnet-script
#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using Newtonsoft.Json;

var person = new { Name = "Kim", Age = 30 };
string json = JsonConvert.SerializeObject(person);
Console.WriteLine(json);

// 전역 변수 (이후 다른 #load 나 REPL 제출에서 재사용 가능)
int counter = 0;
counter++;
Console.WriteLine($"counter={counter}");
````

#### (B) 단독 test.cs 실행 예제 (NuGet 없이)
명령: dotnet run test.cs
````csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

class _   // 파일 하나짜리 “정식” 프로그램
{
    static async Task Main()
    {
        using var http = new HttpClient();
        string txt = await http.GetStringAsync("https://example.com");
        Console.WriteLine($"Length={txt.Length}");
    }
}
````

#### (C) 동일 요구를 .csproj 로 승격했을 때(패키지 사용)
````csharp
using Newtonsoft.Json;

var obj = new { X = 1, Y = 2 };
Console.WriteLine(JsonConvert.SerializeObject(obj));
````

````xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
````

### 5. 내부 동작 차이(요약)
- csx: 스크립트 호스트가 “제출(submission)”을 누적 → 감싸는 숨은 클래스/필드에 상태 보존. #r "nuget" 시 즉석 복원 및 AssemblyLoadContext 로드.
- 단독 .cs: csc(컴파일러)가 일반 컴파일 → 어셈블리 한 번 생성 후 즉시 실행. 상태는 프로세스 수명 내에서만, 재시작 시 초기화.

### 6. 마이그레이션 전략
| 단계 | 설명 |
|------|------|
| 실험 | csx로 빠르게 작성 |
| 재사용 필요 | 관련 함수/타입을 .cs 파일로 옮기고 #load 제거 |
| 패키지/배포 | dotnet new console → 코드 이식 |
| 고도화 | 다층 프로젝트, 테스트 추가 |

### 7. 흔한 오류 / 주의
| 증상 | 원인 | 해결 |
|------|------|------|
| #r "nuget:" 안 됨 | 단독 .cs 실행 | dotnet-script 사용 또는 .csproj 생성 |
| 전역 변수 ‘이미 정의됨’ | csx에서 같은 이름 두 번 | 이름 변경/REPL 히스토리 재시작 |
| 패키지 못 찾음 | 오프라인 캐시 없음 | dotnet restore(프로젝트) / 네트워크 확인 |
| Windows 경로 이스케이프 혼동 | 일반 문자열 | csx도 C# 문법 동일, verbatim(@) 활용 |

### 8. 선택 체크리스트 (Yes → csx 권장)
- NuGet 즉석 사용 필요한가?  
- 여러 번 상태를 이어서 실험?  
- 한 파일로 팀원에게 “그냥 실행” 공유? (csx + shebang)  

그 외면 단독 .cs 또는 .csproj.

### 추가 자료
- [C# 스크립팅(.csx) 공식 문서](https://learn.microsoft.com/dotnet/csharp/scripting/)
- [dotnet-script GitHub](https://github.com/dotnet-script/dotnet-script)
- [Top-level statements 문서](https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements)
- [Minimal console 앱 템플릿](https://learn.microsoft.com/dotnet/core/tutorials/top-level-templates)
- [NuGet 패키지 참조 방법](https://learn.microsoft.com/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli)



라이선스 유형이 2개인 유사한 코드가 있습니다.