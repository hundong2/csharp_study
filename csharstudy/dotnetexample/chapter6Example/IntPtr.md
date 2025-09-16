# C# IntPtr 상세 설명
플랫폼(32/64비트)에 따라 크기가 달라지는 “네이티브 포인터/핸들”을 담는 값 형식입니다. 주로 P/Invoke, 언매니지드 메모리/핸들 관리에 사용됩니다.

## 예시 파일
[UnmanagedMemory Program.cs (dotnet/samples)](https://github.com/dotnet/samples/blob/main/core/interop/UnmanagedMemory/Program.cs)

## 답변
핵심 요약
- 무엇: IntPtr는 네이티브 주소/핸들(포인터)을 담는 정수형 래퍼. UIntPtr는 부호 없는 대응 타입.
- 크기: IntPtr.Size가 4(32비트) 또는 8(64비트). IntPtr.Zero는 null 포인터 표현.
- 용도:
  - P/Invoke에서 네이티브 핸들/포인터 전달
  - 언매니지드 메모리 할당/해제(Marshal.AllocHGlobal/FreeHGlobal)
  - 고정(pinning)된 관리 배열의 시작 주소 획득(GCHandle.AddrOfPinnedObject)
- 변환/연산:
  - 생성자: new IntPtr(int/long)
  - 변환: ToInt64/ToInt32, (long) 캐스트
  - 연산: IntPtr.Add/Subtract로 오프셋 이동(포인터 산술)
- 베스트 프랙티스:
  - 가능한 SafeHandle 파생 타입을 사용(수동 해제 누락 방지)
  - 할당/해제 쌍 보장(try/finally)
  - 64비트에서 ToInt32 사용 지양(오버플로 위험)
  - 관리 객체 주소가 필요하면 반드시 pin(고정)하고 수명 보장(GC.KeepAlive)

실습 예제(언매니지드 메모리, 복사, 핀ning)
````csharp
using System;
using System.Runtime.InteropServices;

public static class IntPtrExamples
{
    public static void Demo()
    {
        Console.WriteLine($"Pointer size: {IntPtr.Size * 8}-bit");
        Console.WriteLine($"Zero == IntPtr.Zero: {IntPtr.Zero}");

        // 1) 언매니지드 메모리 할당/쓰기/읽기/해제
        IntPtr mem = IntPtr.Zero;
        try
        {
            int bytes = 8;
            mem = Marshal.AllocHGlobal(bytes); // 네이티브 힙에서 8바이트
            Console.WriteLine($"Allocated at: 0x{mem.ToInt64():X}");

            // 4바이트 정수 쓰기/읽기
            Marshal.WriteInt32(mem, 0x12345678);
            int read = Marshal.ReadInt32(mem);
            Console.WriteLine($"Read Int32: 0x{read:X}");

            // 바이트 배열 복사
            byte[] src = new byte[] { 1, 2, 3, 4, 5 };
            Marshal.Copy(src, 0, mem, src.Length);

            // 오프셋(포인터 산술): 두 바이트 뒤에 0xAB 쓰기
            IntPtr memPlus2 = IntPtr.Add(mem, 2);
            Marshal.WriteByte(memPlus2, 0xAB);

            // 다시 관리 배열로 복사
            byte[] back = new byte[src.Length];
            Marshal.Copy(mem, back, 0, back.Length);
            Console.WriteLine("Back: " + BitConverter.ToString(back)); // 01-02-AB-04-05
        }
        finally
        {
            if (mem != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(mem); // 반드시 해제
            }
        }

        // 2) 관리 배열을 pin하여 시작 주소 얻기(네이티브 API에 전달 시)
        byte[] managed = new byte[16];
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(managed, GCHandleType.Pinned); // 고정
            IntPtr addr = handle.AddrOfPinnedObject();
            Console.WriteLine($"Pinned addr: 0x{addr.ToInt64():X}");
            // addr를 네이티브 API에 전달 가능
        }
        finally
        {
            if (handle.IsAllocated) handle.Free(); // 고정 해제
        }
        GC.KeepAlive(managed); // 네이티브에서 사용할 동안 수명 보장
    }
}
````

추가 팁
- P/Invoke 시 시그니처에서 IntPtr 대신 SafeHandle 사용을 고려(리소스 자동 정리).
- 포인터를 직접 다뤄야 하는 경우가 아니라면 Span<T>, Memory<T>, MemoryMarshal 등을 우선 고려(안전성/성능).

### 추가 자료
- [System.IntPtr 문서](https://learn.microsoft.com/dotnet/api/system.intptr)
- [System.Runtime.InteropServices.Marshal](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.marshal)
- [SafeHandle 사용 가이드](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose#wrapping-os-handles)
- [.NET 상호 운용성(마샬링) 개요](https://learn.microsoft.com/dotnet/standard/native-interop/)