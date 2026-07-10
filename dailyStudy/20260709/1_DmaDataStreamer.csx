using System;
using System.Buffers;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RealtimeDataPoint
{
    public long TickId;
    public double MetricValue;
}

public sealed class HighSpeedDataIngestor
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    public void IngestHardwareStream(ReadOnlySpan<byte> packet)
    {
        // ArrayBufferWriter<byte>:
        // 내부 버퍼를 키워가며 byte 데이터를 누적하는 도구입니다.
        // GetSpan으로 쓸 공간을 받고, Advance로 실제 쓴 길이를 알려줍니다.
        Span<byte> destination = _buffer.GetSpan(packet.Length);
        packet.CopyTo(destination);
        _buffer.Advance(packet.Length);

        Console.WriteLine($"[Data Ingest] Packet appended to buffer. Size: {packet.Length} bytes.");
        Console.WriteLine($"[Data Ingest] Total Buffered: {_buffer.WrittenCount} bytes.");
    }
}

void Run()
{
    var point = new RealtimeDataPoint { TickId = 1, MetricValue = 42.25d };

    // 구조체를 byte 배열로 변환합니다.
    // 실무에서는 엔디언, 버전, 정렬(Pack)을 명확히 문서화해야 합니다.
    byte[] packet = new byte[Marshal.SizeOf<RealtimeDataPoint>()];
    MemoryMarshal.Write(packet, in point);

    var ingestor = new HighSpeedDataIngestor();
    ingestor.IngestHardwareStream(packet);
}

Run();
