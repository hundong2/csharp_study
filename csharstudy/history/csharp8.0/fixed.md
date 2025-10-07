# fixed 문과 fixed 크기 버퍼의 차이
fixed 문은 “GC가 객체를 옮기지 못하게 잠시 고정(pin)하고 포인터를 얻는 블록”, fixed 크기 버퍼는 “구조체 내부에 C 스타일의 인라인 배열을 넣는 문법”입니다. 전자는 수명·안전 범위를 제어하는 구문이고, 후자는 메모리 레이아웃을 정의하는 필드 선언입니다.

## 예시 파일
[fixed statement 문서 샘플](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/fixed)

## 답변
핵심 비교
- fixed 문(statement)
  - 목적: 관리 객체(배열, 문자열 등)를 블록 범위 동안 pin 하여 “안정적인 주소”를 얻음.
  - 형태: fixed(T* p = expr) { /* 포인터 사용 */ }
  - 특징: 블록을 벗어나면 자동 unpin. 핀 범위는 짧게 유지.
  - 대상: 배열/문자열/필드 주소/&변수, Span<T> 등 “pinnable” 대상.
- fixed 크기 버퍼(fixed-size buffer, 구조체 필드)
  - 목적: 구조체 내부에 고정 길이의 인라인 배열을 넣어 레이아웃을 C와 호환.
  - 형태: unsafe struct S { public fixed int data[16]; }
  - 특징: 버퍼 자체는 핀이 아니라 “구조체 레이아웃” 정의. 힙에 있는 구조체에서 버퍼 주소가 필요하면 여전히 fixed 문으로 “그 구조체”를 pin 해야 안전.
  - 용도: P/Invoke/파일 포맷/네이티브 호환 레이아웃.

차이를 한 줄로
- fixed 문: “일시적 pin + 포인터 얻기(수명 제어)”
- fixed 버퍼: “구조체에 인라인 배열 배치(레이아웃 제어)”

실전 예제들
````csharp
using System;
using System.Runtime.InteropServices;

namespace FixedExamples;

// 빌드: 프로젝트에 <AllowUnsafeBlocks>true</AllowUnsafeBlocks> 필요
public static class Program
{
    public static unsafe void Main()
    {
        PinManagedArrayWithFixed();     // fixed 문
        FixedSizeBufferStructDemo();    // fixed 크기 버퍼
        FixedOverUnmanagedSpanDemo();   // 비관리 메모리 + fixed
    }

    // 1) fixed 문: 관리 배열을 pin 하고 포인터를 얻는 전형적 패턴
    static unsafe void PinManagedArrayWithFixed()
    {
        byte[] src = { 1, 2, 3, 4, 5 };
        byte[] dst = new byte[src.Length];

        fixed (byte* pSrc = src)
        fixed (byte* pDst = dst)
        {
            Buffer.MemoryCopy(pSrc, pDst, dst.Length, src.Length);
        } // 블록 종료 시 자동 unpin

        Console.WriteLine("dst: " + string.Join(",", dst)); // 1,2,3,4,5
    }

    // 2) fixed 크기 버퍼: 구조체에 인라인 배열을 갖는 예
    public unsafe struct Packet
    {
        public fixed byte Data[16]; // 구조체 내부 16바이트 인라인 배열
    }

    static unsafe void FixedSizeBufferStructDemo()
    {
        Packet p = default;

        // 인덱싱 접근은 가능
        for (int i = 0; i < 16; i++)
            p.Data[i] = (byte)i;

        // 주의: '포인터'가 필요할 땐 fixed 문으로 주소 확보
        fixed (byte* ptr = p.Data)
        {
            Console.WriteLine(ptr[2]); // 2
        }

        // 만약 Packet이 힙 객체의 필드라면,
        // 해당 객체를 fixed 문으로 pin 한 상태에서 그 필드 버퍼 포인터를 얻어야 안전.
    }

    // 3) 비관리 메모리 위 Span<T> + fixed
    //    (비관리 포인터는 pin 불필요. fixed는 포인터 얻기 문법으로 일관성 제공)
    static unsafe void FixedOverUnmanagedSpanDemo()
    {
        int len = 8;
        nuint bytes = checked((nuint)len * (nuint)sizeof(int));
        IntPtr mem = IntPtr.Zero;
        try
        {
            mem = Marshal.AllocCoTaskMem(checked((int)bytes));
            if (bytes > 0) System.Runtime.InteropServices.NativeMemory.Clear(mem.ToPointer(), bytes);

            Span<int> span = new Span<int>(mem.ToPointer(), len);
            for (int i = 0; i < len; i++) span[i] = i * 10;

            // 비관리 메모리는 pin이 필요 없지만, fixed로 포인터를 쉽게 얻을 수 있음
            fixed (int* p = span)
            {
                Console.WriteLine(*(p + 3)); // 30
            }
        }
        finally
        {
            if (mem != IntPtr.Zero) Marshal.FreeCoTaskMem(mem);
        }
    }
}
````

추가 문법과 팁
- 문자열 pin: fixed (char* p = myString) { /* 읽기 전용 */ }
- 필드 주소: fixed (int* p = &obj.Field) { ... }  // obj가 힙에 있으면 pin 필요
- Span/ReadOnlySpan pin: fixed (byte* p = mySpan) { ... }  // GetPinnableReference 사용
- 한 문에 여러 포인터: fixed (byte* p1 = a, p2 = b) { ... }
- 프로젝트 설정: csproj에 AllowUnsafeBlocks를 켜야 unsafe/fixed 사용 가능
  - <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
- 원칙: pin 범위는 네이티브 호출 직전~직후로 최대한 짧게. 포인터를 블록 밖으로 보관하지 않기.

요약
- fixed 문은 “포인터 사용을 위한 일시 pin”, fixed 버퍼는 “구조체 레이아웃에 인라인 배열”. 둘은 목적이 다르며, 구조체가 힙에 있을 때 버퍼 주소가 필요하면 여전히 fixed 문으로 pin 해야 안전합니다.

### 추가 자료
- [fixed 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/fixed)
- [unsafe 코드와 포인터](https://learn.microsoft.com/dotnet/csharp/language-reference/unsafe-code)
- [고정 크기 버퍼](https://learn.microsoft.com/dotnet/csharp/language-reference/unsafe-code#fixed-size-buffers)
- [Span<T>와 pinnable 참조](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-7.3/fixed-patterns)