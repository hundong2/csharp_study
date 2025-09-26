# csx 파일에서 IronPython.Hosting 활용 방법
단일 .csx 스크립트에서 IronPython 엔진을 로드해 Python 코드를 실행하고 C#과 상호 운용하는 방법을 설명합니다.
## 예시 파일
[IronPython 임베딩 샘플 (GitHub)](https://github.com/IronLanguages/ironpython3/blob/main/Samples/embed/Console/Program.cs)
## 답변
아래 순서대로 따라 하면 프로젝트(.csproj) 없이도 C# 스크립트(.csx)에서 IronPython을 활용할 수 있습니다.

1. csx(C# Script)란?
- Top-level 코드: Main 메서드 불필요.
- #r, #load 지시문으로 NuGet / 다른 스크립트 참조.
- REPL 스타일 빠른 실험, 빌드/프로젝트 설정 최소화.
- dotnet run 단일 .cs 파일은 NuGet 참조 불가 → csx + dotnet-script 사용.

2. 도구 설치 (macOS)
```bash
dotnet tool install -g dotnet-script
# PATH 미설정 시
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc && source ~/.zshrc
```

3. 스크립트 생성 (ironpy.csx 예시)
아래는 IronPython으로:
- 엔진 생성
- 변수 교환
- Python 함수 호출
- C# 델리게이트를 Python에서 호출
- Python 스크립트 파일 실행
- 동적(dynamic) 호출
을 모두 보여주는 학습용 스크립트입니다.

````csharp
#!/usr/bin/env dotnet-script
#r "nuget: IronPython, 3.4.1"
#r "nuget: IronPython.StdLib, 3.4.1"

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

// 1) 엔진 & 스코프 생성
var engine = Python.CreateEngine();
var scope  = engine.CreateScope();

// (선택) 표준 라이브러리 경로 보강
// IronPython.StdLib 패키지를 추가하면 기본 검색 경로에 들어가지만,
// 필요 시 아래처럼 경로를 확인/추가 가능
var searchPaths = engine.GetSearchPaths();
Console.WriteLine("Python Search Paths:");
foreach (var p in searchPaths) Console.WriteLine("  " + p);

// 2) C# → Python 변수 전달
scope.SetVariable("x", 10);
engine.Execute(@"
y = x * 3
def add(a,b):
    return a + b
print(f'[Python] y={y}')
", scope);

// 3) Python → C# 값/함수 가져오기
dynamic y = scope.GetVariable("y");
dynamic add = scope.GetVariable("add");
Console.WriteLine($"[C#] y from Python = {y}");
Console.WriteLine($"[C#] add(5,7) = {add(5,7)}");

// 4) C# 델리게이트를 Python에서 사용
Func<int,int,int> mul = (a,b) => a * b;
scope.SetVariable("mul", mul);
engine.Execute("print('[Python] mul(6,9)=', mul(6,9))", scope);

// 5) Python 코드 동적 실행(사용자 입력 예시)
string pyExpr = "add(2, 8) + mul(2, 5)";
var result = engine.Execute(pyExpr, scope);
Console.WriteLine($"[C#] Execute('{pyExpr}') => {result}");

// 6) Python 스크립트 파일 실행
var scriptFile = "embedded_script.py";
File.WriteAllText(scriptFile, "print('[Python File] running file...')\nvalue_from_file = 42");
engine.ExecuteFile(scriptFile, scope);
Console.WriteLine($"[C#] value_from_file = {scope.GetVariable("value_from_file")}");

// 7) Expression 컴파일(고빈도 실행 최적화)
var src = engine.CreateScriptSourceFromString("add(100, 23)");
var compiled = src.Compile();
var fast = compiled.Execute(scope);
Console.WriteLine($"[C#] compiled add => {fast}");

// 8) 예외 처리
try
{
    engine.Execute("raise Exception('custom error')", scope);
}
catch (Exception ex)
{
    Console.WriteLine("[C#] Python 예외: " + ex.Message);
}

// 9) 안전 경고
Console.WriteLine("주의: 외부 입력을 직접 Execute하면 보안 위험이 있습니다.");
````

실행:
```bash
chmod +x ironpy.csx
./ironpy.csx
# 또는
dotnet script ironpy.csx
```

4. 핵심 개념 요약
- ScriptEngine(Python.CreateEngine): DLR 기반 실행 환경.
- ScriptScope: 변수 컨테이너(C# ↔ Python 교환).
- engine.Execute / ExecuteFile: 실행.
- scope.GetVariable / SetVariable: 상호 운용.
- dynamic: Python 객체 메서드/함수 호출을 자연스럽게(dynamic 바인딩).
- Compile(): 빈번 호출 코드 최적화.
- 검색 경로: engine.GetSearchPaths() → 필요 시 .Add(path).

5. 실무 팁
- 캐시: 엔진/스코프를 재사용(매 호출 생성 비용 감소).
- 격리: 신뢰할 수 없는 코드 실행 시 샌드박스/프로세스 분리 고려.
- 성능: 고빈도 Python 코드는 Compile 후 Execute 사용.
- 상호 운용: 큰 데이터는 직렬화 대신 직접 SetVariable/호출로 전달.

6. 확장 아이디어
- C#에서 Python으로 콜백 전달(이미 mul 예시).
- Python에서 LINQ 사용: scope.SetVariable("numbers", new[]{1,2,3}); engine.Execute("print(sum(numbers))", scope).
- 다중 스코프: 사용자별 별도 ScriptScope로 격리.

### 추가 자료
- [IronPython 공식 GitHub](https://github.com/IronLanguages/ironpython3)
- [IronPython NuGet 패키지](https://www.nuget.org/packages/IronPython)
- [dotnet-script (C# 스크립트 실행기)](https://github.com/dotnet-script/dotnet-script)
- [C# Scripting(.csx) 문서](https://learn.microsoft.com/dotnet/csharp/scripting/)
- [Dynamic Language Runtime 개요](https://learn.microsoft.com/dotnet/framework/reflection-and-codedom/dynamic-language-runtime-overview)