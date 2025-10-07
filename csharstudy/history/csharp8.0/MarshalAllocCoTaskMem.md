# Marshal.AllocCoTaskMem과 Marshal 주요 API
AllocCoTaskMem은 네이티브(언매니지드) 힙에 메모리를 할당해 IntPtr로 반환하는 API입니다. 사용 후 반드시 FreeCoTaskMem으로 해제해야 하며, 기본적으로 메모리는 0으로 초기화되지 않습니다.

## 예시 파일
[UnmanagedMemory/Program.cs (dotnet/samples)](https://github.com/dotnet/samples/blob/main/core/interop/UnmanagedMemory/Program.cs)

## 답변
핵심 요약
- Marshal.AllocCoTaskMem(int cb): COM Task 메모리 할당자(CoTaskMem)로 cb 바이트를 할당해 IntPtr을 반환.
- 해제 짝: Marshal.FreeCoTaskMem(IntPtr). 반드시 try/finally로 보장 해제.
- 초기화: 기본은 비초기화. .NET 6+에서는 NativeMemory.Clear/AllocZeroed로 0으로 초기화 가능.
- 용도: P/Invoke/COM에서 “호출자(또는 피호출자)가 CoTaskMemFree로 해제”하길 기대하는 규약일 때 적합.
- 비교: AllocHGlobal/FreeHGlobal는 LocalAlloc 계열(Windows 전통). 일반 “임시 네이티브 버퍼”에는 둘 다 사용 가능하나, 상호 운용 규약에 맞춰 선택.

실습 코드: CoTaskMem 할당 → Span<int>로 감싸 사용 → fixed로 잠깐 포인터 → 안전 해제
````csharp
using System;
using System.Runtime.InteropServices;

class MarshalAllocDemo
{
    public static unsafe void Main()
    {
        int len = 500;
        nuint byteCount = checked((nuint)len * (nuint)sizeof(int));
        IntPtr mem = IntPtr.Zero;

        try
        {
            // 1) 네이티브 힙(CoTaskMem) 할당 (비초기화)
            mem = Marshal.AllocCoTaskMem(checked((int)byteCount));

            // 2) 필요 시 0으로 초기화 (.NET 6+)
            if (byteCount > 0)
                NativeMemory.Clear(mem.ToPointer(), byteCount);

            // 3) Span<int>로 안전하게 다루기(복사/인덱싱/슬라이스 가능)
            Span<int> span = new Span<int>(mem.ToPointer(), len);
            for (int i = 0; i < span.Length; i++) span[i] = i;

            // 4) 포인터가 꼭 필요할 때만 짧게 fixed (pin 범위 최소화)
            fixed (int* pSpan = span)
            {
                Console.WriteLine(*(pSpan + 1)); // 1
            }
        }
        finally
        {
            // 5) 반드시 해제(메모리 누수 방지)
            if (mem != IntPtr.Zero)
                Marshal.FreeCoTaskMem(mem);
        }
    }
}
````

Marshal에서 자주 쓰는 함수(카테고리별)
- 메모리 할당/해제
  - AllocCoTaskMem/FreeCoTaskMem/ReAllocCoTaskMem: CoTaskMem 계열
  - AllocHGlobal/FreeHGlobal/ReAllocHGlobal: HGlobal(LocalAlloc) 계열
  - .NET 6+: NativeMemory.Alloc/AllocZeroed/Free, Clear/Copy로 저수준 메모리 작업
- 관리 ↔ 언매니지드 복사
  - Copy(byte[]↔IntPtr), ReadByte/WriteByte, ReadInt32/WriteInt32 등 기본형 읽기/쓰기
- 문자열 마샬링
  - StringToHGlobalUni/Ansi, StringToCoTaskMemUni/Ansi
  - PtrToStringUni/Ansi, StringToBSTR/PtrToStringBSTR, FreeBSTR
- 구조체 마샬링
  - StructureToPtr/PtrToStructure<T>, SizeOf<T>(), OffsetOf
- 함수 포인터/델리게이트
  - GetFunctionPointerForDelegate/GetDelegateForFunctionPointer
- COM/핸들 보조
  - GetIUnknownForObject/ObjectForIUnknown, AddRef/Release (COM), SecureStringToGlobalAllocUni 등
- 주의: 핸들은 가능한 SafeHandle로 감싸서 using/Dispose 패턴으로 관리

실무 팁
- try/finally로 해제 보장: IntPtr 기반 네이티브 메모리는 반드시 finally에서 FreeCoTaskMem/FreeHGlobal.
- 규약에 맞춰 선택: “상대가 CoTaskMemFree로 해제”를 기대하면 CoTaskMem, 그 외 일반 버퍼는 HGlobal도 OK.
- 초기화 필요 시 직접 Clear: 보안/안정성 요구가 있으면 NativeMemory.Clear로 0 초기화.
- 포인터 최소화: Span<T>/MemoryMarshal로 대부분의 작업을 처리하고, 포인터가 필요한 구간만 잠깐 fixed.

### 추가 자료
- [Marshal.AllocCoTaskMem 문서](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.marshal.alloccotaskmem)
- [Marshal.FreeCoTaskMem 문서](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.marshal.freecotaskmem)
- [Marshal 클래스 개요](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.marshal)
- [NativeMemory API (.NET 6+)](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.nativememory)
- [P/Invoke 베스트 프랙티스](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke)