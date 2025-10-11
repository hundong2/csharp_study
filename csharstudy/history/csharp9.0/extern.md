# C#의 extern vs C++의 extern

두 언어 모두 “외부에 있는 무언가”를 쓰겠다는 신호지만, 의미와 쓰임이 다릅니다. C#은 주로 네이티브 함수(P/Invoke) 선언에, C++은 링크/이름 장식과 관련된 “링키지(linkage)” 지정에 쓰입니다.

## 예시 파일

[dotnet/samples – Interop/UnmanagedMemory/Program.cs](https://github.com/dotnet/samples/blob/main/core/interop/UnmanagedMemory/Program.cs)

## 답변

핵심 요약. 
- C# extern
  - extern 한정자는 “본문 없는 외부 메서드” 선언을 의미. 보통 [DllImport]와 함께 네이티브 DLL 함수를 호출(P/Invoke)할 때 사용.
  - 별개로 extern alias 지시어가 있음(서로 다른 어셈블리를 별칭으로 구분할 때).
- C++ extern
  - “외부 링키지”를 의미. 다른 번역 단위에 정의된 전역 변수/함수의 “선언”을 알릴 때 사용.
  - extern "C"는 C 링키지(이름 장식 제거)를 지정해 P/Invoke/FFI 용도로 노출.

차이 한눈에. 
- 목적: C#은 네이티브 코드 호출, C++은 링커가 심볼을 어떻게 찾을지(링키지) 제어.
- 위치: C#은 메서드 선언에 한정(+ extern alias). C++은 전역 변수/함수/링키지 사양자.
- 동작: C#은 런타임에 DllImport/EntryPoint/호출 규약로 바인딩. C++은 컴파일/링크 타임 심볼 결합.

C# 예제 1) P/Invoke로 libc의 strlen 호출(macOS/Linux에서도 동작). 

````csharp
using System;
using System.Runtime.InteropServices;

public static class LibC
{
    // C 서명: size_t strlen(const char* s);
    [DllImport("libc", EntryPoint = "strlen", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint StrLen(IntPtr s);
}

public static class Demo
{
    public static unsafe void Main()
    {
        // UTF-8 null-terminated 문자열 준비
        ReadOnlySpan<byte> bytes = "hello"u8; // C# 11
        byte* p = stackalloc byte[bytes.Length + 1];
        bytes.CopyTo(new Span<byte>(p, bytes.Length));
        p[bytes.Length] = 0;

        nuint len = LibC.StrLen((IntPtr)p);
        Console.WriteLine(len); // 5
    }
}
````

C# 예제 2) extern alias로 어셈블리 충돌 분리
````csharp
// csproj에서 참조에 별칭 부여(예: Aliases="A1")
// <ItemGroup>
//   <Reference Include="LibA"><Aliases>A1</Aliases></Reference>
//   <Reference Include="LibB"><Aliases>B1</Aliases></Reference>
// </ItemGroup>

extern alias A1;
extern alias B1;

class AliasDemo
{
    static void Main()
    {
        var a = new A1::Shared.Namespace.Type(); // LibA의 Type
        var b = new B1::Shared.Namespace.Type(); // LibB의 Type
    }
}
````

C++ 예제 1) extern으로 전역 변수 공유
````cpp
// a.cpp
int g_counter = 42; // 정의(Definition)

// b.cpp
#include <iostream>
extern int g_counter; // 선언(Declaration)
int main() {
    std::cout << g_counter << "\n"; // 42
}
````

C++ 예제 2) extern "C"로 C 링키지로 내보내고 C#에서 P/Invoke
````cpp
// native.c  (C 또는 C++에서도 extern "C" 사용 가능)
#ifdef __cplusplus
extern "C" {
#endif

int add42(int x) { return x + 42; }

#ifdef __cplusplus
}
#endif
````

````csharp
using System;
using System.Runtime.InteropServices;

public static class Native
{
    // 빌드 산출물 이름에 맞춰 조정: "native" 또는 "libnative"
    [DllImport("native", EntryPoint = "add42", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Add42(int x);
}

class Program
{
    static void Main() => Console.WriteLine(Native.Add42(8)); // 50
}
````

주의/베스트 프랙티스. 

- C# P/Invoke
  - 라이브러리 이름은 OS마다 다름(Windows: .dll, macOS: .dylib, Linux: .so). “libc”처럼 공용 이름을 쓰거나 RID별 네이티브 패키징 필요.
  - CallingConvention/CharSet/EntryPoint를 정확히 맞춤. 잘못되면 AccessViolation/스택 손상 위험.
  - 포인터/버퍼는 Span<T>/SafeHandle을 활용해 수명·해제를 엄격히 관리.
- C++ extern
  - 다중 정의(One Definition Rule) 위반 금지: 같은 심볼을 여러 번 정의하지 말 것.
  - const 전역은 내부 링키지임에 주의. 다른 TU에서 공유하려면 extern const로 선언/정의 일치.
  - C#에서 호출할 함수는 extern "C"로 이름 장식을 제거하는 것이 안전.

요약. 
- C#: extern은 “본문 없는 네이티브 메서드 선언”(대개 DllImport 필수). 또한 extern alias로 어셈블리 충돌 분리.
- C++: extern은 “외부 링키지” 지정. extern "C"로 C 링키지를 강제해 상호 운용에 활용.

### 추가 자료. 

- [C# extern(메서드) 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/extern)
- [extern alias 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/extern-alias)
- [DllImportAttribute](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.dllimportattribute)
- [P/Invoke 모범 사례](https://learn.microsoft.com/dotnet/standard/native-interop/best-practices)
- [C++ extern과 링키지](https://learn.microsoft.com/cpp/cpp/extern-cpp)
- [extern "C"와 이름 장식](https://learn.microsoft.com/cpp/build/reference/exports-exporting-functions)