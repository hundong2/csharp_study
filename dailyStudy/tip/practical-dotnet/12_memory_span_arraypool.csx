using System;
using System.Buffers;
using System.Text;

void Run()
{
    string text = "tenant:42";

    // 1) 스택 메모리 예제
    // stackalloc:
    // 작은 임시 버퍼를 힙이 아니라 스택에 만듭니다.
    // Span<T>는 ref struct라서 dotnet-script top-level 필드로 승격되면 안 됩니다.
    // 그래서 함수 안의 지역 변수로 둡니다.
    // 메모리 관점:
    // - new byte[64]는 힙에 배열 객체를 만듭니다.
    // - stackalloc byte[64]는 현재 메서드의 스택 프레임에 임시 공간을 만듭니다.
    // - 메서드가 끝나면 스택 공간은 자연스럽게 사라지고 GC가 치울 객체가 생기지 않습니다.
    Span<byte> stackBuffer = stackalloc byte[64];
    int written = Encoding.UTF8.GetBytes(text, stackBuffer);

    ReadOnlySpan<byte> payload = stackBuffer[..written];
    Console.WriteLine($"[Span] Bytes={payload.Length}, Text={Encoding.UTF8.GetString(payload)}");

    // 2) 힙 배열 재사용 예제
    // ArrayPool:
    // 큰 배열을 반복해서 만들지 않고 빌려 씁니다.
    // 메모리 관점:
    // - 큰 byte[]를 계속 new로 만들면 Gen0/Gen2 GC 압박이 커질 수 있습니다.
    // - ArrayPool은 이미 만들어 둔 배열을 빌려주므로 반복 할당을 줄입니다.
    // - 빌린 배열은 요청한 길이보다 클 수 있으므로 실제 사용 길이를 따로 관리해야 합니다.
    byte[] rented = ArrayPool<byte>.Shared.Rent(1024);
    try
    {
        int length = Encoding.UTF8.GetBytes("pooled-buffer", rented);
        Console.WriteLine($"[ArrayPool] Text={Encoding.UTF8.GetString(rented, 0, length)}");
    }
    finally
    {
        // clearArray: true는 민감한 데이터가 남지 않도록 배열을 지우고 반납합니다.
        // 실무에서는 토큰, 개인정보, 인증 값이 담긴 버퍼를 반납할 때 clearArray를 고려합니다.
        ArrayPool<byte>.Shared.Return(rented, clearArray: true);
    }
}

Run();
