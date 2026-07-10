using System;
using System.IO.MemoryMappedFiles;
using System.Text;

const int Capacity = 1024;
const string CacheValue = "large-cache-segment";

// MemoryMappedFile:
// 큰 데이터를 일반 힙에 오래 붙잡지 않고 파일/가상 메모리 기반으로 다루는 방법입니다.
using (var mmf = MemoryMappedFile.CreateNew("daily_20260709_cache", Capacity))
using (var accessor = mmf.CreateViewAccessor())
{
    byte[] bytes = Encoding.UTF8.GetBytes(CacheValue);

    // 앞 4바이트에는 길이, 뒤에는 실제 payload를 저장합니다.
    accessor.Write(0, bytes.Length);
    accessor.WriteArray(4, bytes, 0, bytes.Length);

    int length = accessor.ReadInt32(0);
    byte[] loaded = new byte[length];
    accessor.ReadArray(4, loaded, 0, length);

    Console.WriteLine("[MMF Cache] Segment stored outside normal object graph.");
    Console.WriteLine($"[MMF Cache] Value: {Encoding.UTF8.GetString(loaded)}");
}
