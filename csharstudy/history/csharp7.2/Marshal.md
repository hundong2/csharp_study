# Marshal이란?
관리(.NET) 세계와 네이티브(언매니지드) 세계 사이에서 메모리/데이터를 변환하고 복사하는 상호 운용(Interop) 유틸리티입니다. 언매니지드 힙 할당/해제, 포인터로부터 값 읽기/쓰기, 배열·문자열 복사, 구조체 마샬링 등을 제공합니다.

## 예시 파일
[UnmanagedMemory Program.cs (dotnet/samples)](https://github.com/dotnet/samples/blob/main/core/interop/UnmanagedMemory/Program.cs)

## 답변
핵심 개념
- 역할: 언매니지드 메모리 관리(AllocHGlobal/FreeHGlobal/ReAllocHGlobal), 복사(Copy), 원시 타입 읽기/쓰기(Read/WriteXxx), 문자열 변환(StringToHGlobalUni/Ansi ↔ PtrToStringXxx), 구조체 마샬링(StructureToPtr/PtrToStructure), 크기/오프셋 계산(SizeOf/OffsetOf), 함수 포인터 변환(GetFunctionPointerForDelegate/GetDelegateForFunctionPointer).
- 사용처: P/Invoke, COM Interop, 네이티브 라이브러리와 데이터 교환.
- 주의점:
  - 반드시 할당-해제 쌍 보장(try/finally, FreeHGlobal/CoTaskMemFree).
  - 64비트에서 IntPtr 크기(8바이트) 고려, ToInt32 남용 금지.
  - 가능하면 SafeHandle/critical handle 사용으로 리소스 누수 방지.
  - 관리 객체 주소 사용 시 pin(고정) 필요(GCHandle.Alloc(..., Pinned)).

간단 데모 코드
````csharp
using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
struct Person
{
    public int Age;
    public double Score;
}

public static class MarshalDemo
{
    public static void Run()
    {
        // 1) 구조체 → 언매니지드 메모리 → 구조체
        IntPtr ptr = IntPtr.Zero;
        try
        {
            var p = new Person { Age = 42, Score = 98.5 };
            int size = Marshal.SizeOf<Person>();
            ptr = Marshal.AllocHGlobal(size);     // 네이티브 힙 할당
            Marshal.StructureToPtr(p, ptr, fDeleteOld: false);

            var back = Marshal.PtrToStructure<Person>(ptr);
            Console.WriteLine($"Person -> Age={back.Age}, Score={back.Score}");
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
        }

        // 2) 문자열 마샬링 (관리 ↔ 언매니지드)
        IntPtr strPtr = IntPtr.Zero;
        try
        {
            string s = "안녕하세요";
            strPtr = Marshal.StringToHGlobalUni(s);      // UTF-16로 네이티브 힙에 복사
            string managed = Marshal.PtrToStringUni(strPtr)!; // 역변환
            Console.WriteLine($"String roundtrip: {managed}");
        }
        finally
        {
            if (strPtr != IntPtr.Zero) Marshal.FreeHGlobal(strPtr);
        }

        // 3) 배열 복사 (관리 → 언매니지드 → 관리)
        IntPtr buf = IntPtr.Zero;
        try
        {
            byte[] src = { 1, 2, 3, 4, 5 };
            buf = Marshal.AllocHGlobal(src.Length);
            Marshal.Copy(src, 0, buf, src.Length);        // 관리→언매니지드

            // 바이트 하나 수정
            Marshal.WriteByte(buf, 2, 0xAB);

            byte[] dst = new byte[src.Length];
            Marshal.Copy(buf, dst, 0, dst.Length);        // 언매니지드→관리
            Console.WriteLine("Bytes: " + BitConverter.ToString(dst)); // 01-02-AB-04-05
        }
        finally
        {
            if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
        }
    }
}
````

이로부터 배울 것
- 언매니지드 메모리를 직접 다룰 때의 필수 API와 안전한 해제 패턴.
- 구조체 레이아웃(Sequential)과 크기(SizeOf)/오프셋(OffsetOf) 계산의 중요성.
- 문자열·배열·구조체를 네이티브와 주고받는 기본 흐름.
- 가능하면 SafeHandle을 쓰고, 주소를 사용할 때는 pin과 수명 관리(GC.KeepAlive)를 고려.

### 추가 자료
- [System.Runtime.InteropServices.Marshal](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.marshal)
- [P/Invoke 개요와 베스트 프랙티스](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke)
- [SafeHandle 가이드](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose#wrapping-os-handles)
- [StructLayout, marshalling 규칙](https://learn.microsoft.com/dotnet/framework/interop/default-marshalling-for-strings)