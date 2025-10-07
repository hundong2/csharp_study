# C# unsafe와 fixed 쉽게 이해하기
unsafe는 포인터 같은 저수준 기능을 허용하는 키워드이고, fixed는 GC가 객체를 이동하지 못하게 “고정(pin)”해 포인터를 안전하게 얻는 블록입니다. 강력하지만, 범위를 짧게 유지하고 예외/리소스 누수를 방지하는 패턴을 지키는 것이 중요합니다.

## 예시 파일
- fixed 문 공식 샘플: https://github.com/dotnet/samples/blob/main/snippets/csharp/language-reference/keywords/fixed/fixed-statement1.cs
- unsafe/포인터 개요: https://github.com/dotnet/samples/blob/main/snippets/csharp/language-reference/unsafe-code/UnsafeCode.cs

## 답변
개념 요약
- unsafe: 포인터(*, &), stackalloc, 고정 크기 버퍼 등을 허용. /unsafe 옵션 필요. CLR의 메모리 안전 보장을 벗어날 수 있어 신중히 사용.
- fixed: 관리 객체(예: byte[])를 GC가 이동하지 못하게 “핀” 후 해당 객체의 주소를 얻는 블록. 블록을 벗어나면 자동으로 unpin.

왜 fixed가 필요한가?
- .NET의 GC는 객체를 이동(compaction)할 수 있음. 이동 중에 포인터가 가리키는 주소가 바뀌면 위험하므로, 네이티브 호출/포인터 연산 직전-직후만 짧게 pin.

기본 사용 예
````csharp
using System;
using System.Runtime.InteropServices;

public static class FixedBasics
{
    // 네이티브처럼 동작할 안전한 복사(설명용)
    static unsafe void Copy(void* src, void* dst, nuint bytes)
        => Buffer.MemoryCopy(src, dst, bytes, bytes);

    public static unsafe void Run()
    {
        byte[] src = { 1, 2, 3, 4, 5 };
        byte[] dst = new byte[src.Length];

        // 1) fixed로 두 배열을 짧게 pin 하고 포인터 얻기
        fixed (byte* pSrc = src)
        fixed (byte* pDst = dst)
        {
            Copy(pSrc, pDst, (nuint)src.Length);
        } // 여기서 자동으로 unpin

        Console.WriteLine(string.Join(",", dst)); // 1,2,3,4,5
    }
}
````

unsafe를 줄이는 더 안전한 대안
- Span<T>/ReadOnlySpan<T>: 힙 할당 없이 배열/메모리 구간을 다룰 수 있고, 많은 API가 포인터 없이도 고성능 제공.
````csharp
using System;

public static class SpanAlternative
{
    public static void Run()
    {
        Span<byte> buf = stackalloc byte[16]; // 임시 버퍼(스택)
        for (int i = 0; i < buf.Length; i++) buf[i] = (byte)i;

        Span<byte> left = buf[..8];
        Span<byte> right = buf[8..];
        left.CopyTo(right); // 포인터 없이 안전한 복사
    }
}
````

실무에서의 안전 규칙(예외/누수 방지)
- pin 범위 최소화: fixed는 가능한 한 “네이티브 호출 바로 직전~직후”로만 감쌉니다.
- 큰 버퍼는 ArrayPool: stackalloc(스택)은 너무 크면 StackOverflowException(try/finally도 실행 안 됨). 수 KB 초과는 ArrayPool.Shared.Rent 사용.
- 경계 검증: 포인터 연산 전 길이/인덱스 검사. nuint/checked로 바이트 수 계산 오버플로 방지.
- SafeHandle 사용: 네이티브 핸들은 IntPtr 대신 SafeHandle로 선언(P/Invoke). 예외 시에도 ReleaseHandle가 신뢰성 있게 호출됩니다.
- try/finally로 리소스 보장: GCHandle.Alloc 등 pin API는 반드시 finally에서 Free. fixed는 자동 unpin이지만 블록을 짧게 유지.
- 포인터를 보관하지 말 것: fixed 블록 밖으로 포인터를 저장/반환 금지. 블록 종료 후 주소는 유효하지 않습니다.
- Span 우선: 가능하면 unsafe 대신 Span<T>/MemoryMarshal을 사용.

왜 catch/finally가 실행되지 않는 문제가 생기나?
- StackOverflowException: stackalloc을 크게 잡거나 재귀 폭주 시 발생. .NET에서 catch할 수 없고 finally도 실행되지 않음.
- AccessViolationException 등 CSE(손상된 상태 예외): 잘못된 포인터 접근으로 프로세스가 손상되면 catch/finally가 동작하지 않거나 무시될 수 있음(특히 .NET Core). 이건 “막는 것”이 최선.
- FailFast/환경 종료: Environment.FailFast, 프로세스 종료, 네이티브 크래시는 finally를 건너뜀.

이를 줄이는 방법론(실무 패턴)
- “사전 검증 + 짧은 임계구역”: 길이/널 포인터/정렬 조건을 먼저 검증 → fixed/unsafe 블록은 최소 코드만 포함.
- 보호된 경계로 캡슐화: 위험한 네이티브 호출을 한 메서드로 감싸고, 바깥은 Span/Array 등 안전 API만 노출.
- 큰 작업은 힙/풀 사용: stackalloc은 수백 바이트~수 KB 정도만. 그 이상은 ArrayPool 사용 후 finally에서 반환.
- 네이티브 리소스는 SafeHandle/using: 누수/예외에도 해제가 보장되도록 표준화.
- 포인터 대신 Span 기반 오버로드 사용: 가능한 API를 선택.

예외/리소스 보장 예시
````csharp
using System;
using System.Buffers;
using System.Runtime.InteropServices;

public static class RobustPatterns
{
    // 1) ArrayPool로 큰 버퍼 관리 + finally로 반환
    public static void LargeWork(int size)
    {
        byte[]? rented = null;
        try
        {
            rented = ArrayPool<byte>.Shared.Rent(size);
            var span = rented.AsSpan(0, size);

            // 안전한 Span 로직 ...
            span.Clear();
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    // 2) GCHandle 핀은 반드시 finally에서 해제
    public static unsafe void PinWithGCHandle(byte[] data)
    {
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            byte* p = (byte*)handle.AddrOfPinnedObject();
            // 최소한의 포인터 작업 ...
            for (int i = 0; i < data.Length; i++) p[i] = (byte)i;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    // 3) 네이티브 핸들은 SafeHandle (예시용)
    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr malloc(nuint size);

    [DllImport("libc", SetLastError = true)]
    private static extern void free(IntPtr ptr);

    public sealed class SafeNativeBuffer : SafeHandle
    {
        public SafeNativeBuffer() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        public static SafeNativeBuffer Allocate(nuint size)
        {
            var h = new SafeNativeBuffer();
            h.SetHandle(malloc(size));
            return h;
        }
        protected override bool ReleaseHandle()
        {
            free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }
}
````

요약
- unsafe/fixed는 “아주 짧게, 검증 후” 사용.
- 큰 임시 메모리는 ArrayPool, 네이티브 핸들은 SafeHandle.
- 포인터 대신 Span<T> 우선.
- StackOverflow/AccessViolation 같은 치명적 상황은 finally도 못 밟을 수 있으니, 사고 자체를 설계로 예방.

### 추가 자료
- fixed 문서: https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/fixed
- unsafe 코드/포인터: https://learn.microsoft.com/dotnet/csharp/language-reference/unsafe-code
- Span/메모리: https://learn.microsoft.com/dotnet/standard/memory-and-spans/
- SafeHandle 가이드: https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose#wrapping-os-handles
- AccessViolationException과 CSE: https://learn.microsoft.com/dotnet/framework/configure-apps/file-schema/runtime/legacycorruptedstateexceptionspolicy-element


# fixed 자세한 동작과 사용법
fixed는 관리 힙의 객체(예: 배열, 문자열 등)를 GC가 “이동(compaction)”하지 못하게 잠시 고정(pin)하고, 그 기간 동안 해당 객체의 “안정적인 주소”를 포인터로 얻기 위한 문입니다. 블록을 벗어나면 자동으로 unpin 됩니다.

## 예시 파일
[fixed statement 공식 샘플](https://github.com/dotnet/samples/blob/main/snippets/csharp/language-reference/keywords/fixed/fixed-statement1.cs)

## 답변
핵심 이해
- .NET GC는 힙을 압축하며 객체를 “옮길 수” 있습니다. 그래서 관리 객체의 내부 주소를 포인터로 잡아두면 GC가 옮기는 순간 주소가 바뀌어 위험합니다.
- fixed는 지정한 객체를 “블록 범위 동안” 이동 금지(pin)로 표시하고, 그 내부(대개 첫 요소)의 주소를 포인터로 제공합니다.
- 블록이 끝나면 자동으로 unpin 되며, 포인터는 더 이상 유효하지 않습니다.

fixed가 하는 일(개념)
1) 대상 객체를 pin → GC가 해당 객체를 이동하지 않음  
2) 대상의 첫 요소 주소를 포인터로 노출(byte[]는 byte* 첫 요소, string은 char* 첫 문자)  
3) 블록 종료 시 자동 unpin

무엇을 pin 할 수 있나
- 배열/문자열: fixed (byte* p = arr), fixed (char* p = s)
- 구조체/필드 주소: fixed (int* p = &obj.Field)
- GetPinnableReference를 제공하는 타입(예: Span<T>, 사용자 정의 타입)

짧고 안전한 사용 예
````csharp
```csharp
// filepath: /Users/donghun2/workspace/csharp_study/csharstudy/history/csharp8.0/FixedDetailedDemo.cs
using System;
using System.Runtime.InteropServices;

public static class FixedDetailedDemo
{
    public static unsafe void CopyArrayDemo()
    {
        byte[] src = { 1, 2, 3, 4, 5 };
        byte[] dst = new byte[src.Length];

        // 1) pin 범위를 최소화: 포인터 필요한 구간만 fixed
        fixed (byte* pSrc = src)
        fixed (byte* pDst = dst)
        {
            // 네이티브 복사와 동일한 동작
            Buffer.MemoryCopy(pSrc, pDst, dst.Length, src.Length);
        } // 여기서 자동 unpin

        Console.WriteLine($"dst: {string.Join(",", dst)}"); // 1,2,3,4,5
    }

    public static unsafe void PinStringDemo()
    {
        string s = "Hello";
        fixed (char* p = s) // 첫 문자에 대한 char* 획득
        {
            // p[0]은 'H'. 문자열은 불변이므로 읽기만 안전.
            Console.WriteLine($"First char: {p[0]}");
        }
    }

    // GetPinnableReference 지원 타입(Span<T>)은 fixed의 대상이 될 수 있음
    public static unsafe void SpanPinDemo()
    {
        Span<int> span = new int[4] { 10, 20, 30, 40 };
        fixed (int* p = span) // Span<T>.GetPinnableReference() 자동 호출
        {
            Console.WriteLine(*(p + 2)); // 30
        }
    }

    // fixed와 동등한 저수준 패턴: GCHandle 사용(권장: finally에서 Free)
    public static unsafe void GCHandlePinDemo()
    {
        byte[] buf = new byte[8];
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            byte* p = (byte*)handle.AddrOfPinnedObject();
            for (int i = 0; i < buf.Length; i++) p[i] = (byte)i;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free(); // 반드시 해제
        }
    }

    public static void Main()
    {
        CopyArrayDemo();
        PinStringDemo();
        SpanPinDemo();
        GCHandlePinDemo();
    }
}
````

베스트 프랙티스
- pin 범위 최소화: 네이티브 호출 직전~직후처럼 “정말 필요한” 짧은 범위로 제한.
- 포인터 보관 금지: fixed 블록 밖으로 포인터를 저장/반환하지 말 것(유효 범위 종료).
- 큰 버퍼는 ArrayPool: stackalloc/긴 pin은 StackOverflow/단편화 리스크. 수 KB 초과는 ArrayPool.Shared.Rent 사용 후 반환.
- await/블로킹 피하기: fixed 내부에서 await/오래 걸리는 작업으로 pin 기간을 늘리지 말 것(힙 단편화 유발).
- 가능한 대안: Span<T>/MemoryMarshal/Unsafe 없이 제공되는 안전 오버로드 사용.

왜 예외 시 finally를 못 밟기도 하나?
- StackOverflowException, AccessViolationException, 프로세스 강제 종료 등 치명적 예외는 finally도 실행 못 할 수 있습니다.
- 예방이 최선: 
  - pin 짧게 유지(네이티브 호출만 감싸기)
  - 길이/널 검증 선행(오버런 방지)
  - 큰 버퍼는 힙/풀 사용(ArrayPool)로 stackalloc/장기 pin 회피
  - 네이티브 핸들은 SafeHandle/using 패턴으로 관리

fixed ↔ GCHandle 개념 비교
- fixed는 “스코프 기반 자동 unpin” 문법 설탕(sugar).  
- GCHandle.Alloc(obj, Pinned)은 수동 pin → 반드시 try/finally로 Free. 긴 수명 pin이 필요하면 GCHandle을 쓰되 가능한 피하는 것이 좋습니다.

한 줄 요약
- fixed는 “GC가 객체를 못 옮기게 잠시 고정하고 포인터를 얻는 블록”이며, 반드시 짧고 필요한 범위에서만 사용하세요. 대체 가능하면 Span<T> 등 안전한 API를 우선하세요.

### 추가 자료
- [fixed 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/fixed)
- [unsafe 코드/포인터 개요](https://learn.microsoft.com/dotnet/csharp/language-reference/unsafe-code)
- [GetPinnableReference 패턴](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-7.3/fixed-patterns)
- [GCHandle 핀](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.gchandle)
- [Span<T>와 메모리](https://learn.microsoft.com/dotnet/standard/memory-and-spans/)