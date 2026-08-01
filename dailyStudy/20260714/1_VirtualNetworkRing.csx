/*
문제: 수신된 패킷을 Pipe에 쓰고 크기를 출력하세요.
*/

using System;
using System.IO.Pipelines;
using System.Text;

public sealed class HighSpeedPacketIngestor
{
    private readonly Pipe _vnrPipe = new(new PipeOptions(useSynchronizationContext: false));

    public void ProcessKernelPacket(ReadOnlySpan<byte> rawKernelBuffer)
    {
        Span<byte> writableBuffer = _vnrPipe.Writer.GetSpan(rawKernelBuffer.Length);
        rawKernelBuffer.CopyTo(writableBuffer);
        _vnrPipe.Writer.Advance(rawKernelBuffer.Length);

        Console.WriteLine($"[VNR Network] Packet mapped to layout via Core-isolated Ring. Size: {rawKernelBuffer.Length} bytes.");
    }
}

var ingestor = new HighSpeedPacketIngestor();
byte[] mockNetworkPacket = Encoding.UTF8.GetBytes("VNR_STREAM_PAYLOAD_2026");
ingestor.ProcessKernelPacket(mockNetworkPacket);

/*
실행 결과:
[VNR Network] Packet mapped to layout via Core-isolated Ring. Size: 23 bytes.
*/

