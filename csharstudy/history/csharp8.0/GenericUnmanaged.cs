using System;
using System.Runtime.InteropServices;

namespace ConsoleApp;

public class Program
{
    private static void Main()
    {
        unsafe
        {
            int n = 10;
            using (var intArray = new UnmanagedWrapper<int>(n))
            {
                for (int i = 0; i < n; i++) intArray[i] = i;
                for (int i = 0; i < n; i++) Console.WriteLine(intArray[i]);
            }
        }

        unsafe
        {
            int n = 10;
            using (var longArray = new UnmanagedWrapper<long>(n))
            {
                for (int i = 0; i < n; i++) longArray[i] = i;
                for (int i = 0; i < n; i++) Console.WriteLine(longArray[i]);
            }
        }
    }

    // 크로스플랫폼 안전한 언매니지드 버퍼 래퍼
    class UnmanagedWrapper<T> : IDisposable where T : unmanaged
    {
        private IntPtr _ptr;
        private int _maxElements;

        public unsafe UnmanagedWrapper(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            _maxElements = n;

            // 바이트 수 계산 (오버플로 방지)
            nuint byteCount = checked((nuint)n * (nuint)sizeof(T));

            // CoTaskMem 할당(필요에 따라 AllocHGlobal도 가능)
            _ptr = Marshal.AllocCoTaskMem(checked((int)byteCount));

            // 제로 초기화 (크로스플랫폼)
            if (byteCount > 0)
            {
                void* p = _ptr.ToPointer();
                System.Runtime.InteropServices.NativeMemory.Clear(p, byteCount);
            }
        }

        public unsafe T this[int idx]
        {
            get
            {
                if ((uint)idx >= (uint)_maxElements) throw new IndexOutOfRangeException();
                T* basePtr = (T*)_ptr.ToPointer();
                return *(basePtr + idx);
            }
            set
            {
                if ((uint)idx >= (uint)_maxElements) throw new IndexOutOfRangeException();
                T* basePtr = (T*)_ptr.ToPointer();
                *(basePtr + idx) = value;
            }
        }

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_ptr);
                _ptr = IntPtr.Zero; // 이중 해제 방지
                _maxElements = 0;
            }
            GC.SuppressFinalize(this);
        }
    }
}