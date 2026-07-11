using System;
using System.Buffers;
using System.Text;

void Run()
{
    string text = "tenant:42";

    // stackalloc:
    // 작은 임시 버퍼를 힙이 아니라 스택에 만듭니다.
    // Span<T>는 ref struct라서 dotnet-script top-level 필드로 승격되면 안 됩니다.
    // 그래서 함수 안의 지역 변수로 둡니다.
    Span<byte> stackBuffer = stackalloc byte[64];
    int written = Encoding.UTF8.GetBytes(text, stackBuffer);

    ReadOnlySpan<byte> payload = stackBuffer[..written];
    Console.WriteLine($"[Span] Bytes={payload.Length}, Text={Encoding.UTF8.GetString(payload)}");

    // ArrayPool:
    // 큰 배열을 반복해서 만들지 않고 빌려 씁니다.
    byte[] rented = ArrayPool<byte>.Shared.Rent(1024);
    try
    {
        int length = Encoding.UTF8.GetBytes("pooled-buffer", rented);
        Console.WriteLine($"[ArrayPool] Text={Encoding.UTF8.GetString(rented, 0, length)}");
    }
    finally
    {
        // clearArray: true는 민감한 데이터가 남지 않도록 배열을 지우고 반납합니다.
        ArrayPool<byte>.Shared.Return(rented, clearArray: true);
    }
}

Run();
