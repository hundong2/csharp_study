using System;
using System.IO.MemoryMappedFiles;
using System.Text;

// 이 예제는 "같은 머신 안의 프로세스끼리 데이터를 주고받는 IPC"를 설명합니다.
// IPC는 Inter-Process Communication의 약자로, 프로세스 간 통신이라는 뜻입니다.
// 실제 서비스에서는 사이드카, 워커 프로세스, 로컬 에이전트와 데이터를 주고받을 때 자주 등장합니다.

const int BufferSize = 256;
const string MapName = "daily_study_20260702_shared_memory";

// using (...):
// - IDisposable 객체를 사용 후 자동 정리하겠다는 뜻입니다.
// - MemoryMappedFile은 파일 또는 메모리 영역을 여러 프로세스가 공유할 수 있게 해 줍니다.
using (var sharedMemory = MemoryMappedFile.CreateNew(MapName, BufferSize))
{
    // CreateViewAccessor:
    // - 공유 메모리의 특정 범위를 읽고 쓰는 도구를 만듭니다.
    // - 여기서는 0부터 BufferSize까지 전체 영역을 사용합니다.
    using (var accessor = sharedMemory.CreateViewAccessor(0, BufferSize))
    {
        string message = "sidecar -> gateway: warm cache for tenant 42";

        // Encoding.UTF8:
        // - 문자열은 내부적으로 문자이고, 네트워크/파일/공유 메모리는 보통 byte를 다룹니다.
        // - 그래서 문자열을 byte[]로 변환해야 합니다.
        byte[] payload = Encoding.UTF8.GetBytes(message);

        // Write:
        // - 첫 4바이트에 payload 길이를 저장합니다.
        // - 수신자가 몇 바이트를 읽어야 하는지 알 수 있게 하는 작은 프로토콜입니다.
        accessor.Write(0, payload.Length);

        // WriteArray:
        // - 4번 위치부터 실제 메시지 바이트를 씁니다.
        // - 앞의 4바이트는 길이 저장용으로 비워 두었기 때문입니다.
        accessor.WriteArray(4, payload, 0, payload.Length);

        // 아래부터는 "다른 프로세스가 읽는다"고 가정한 수신부입니다.
        // csx 하나로 실행 가능하게 만들기 위해 같은 파일 안에서 바로 읽습니다.
        int length = accessor.ReadInt32(0);
        byte[] received = new byte[length];
        accessor.ReadArray(4, received, 0, length);

        string decoded = Encoding.UTF8.GetString(received);

        Console.WriteLine("[IPC] Shared memory message received.");
        Console.WriteLine($"[IPC] Bytes: {length}");
        Console.WriteLine($"[IPC] Payload: {decoded}");
    }
}
