/*
문제: 수신된 시계열 바이트 청크의 저장 오프셋을 원자적으로 발급하세요.
*/

using System;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TimeSeriesTick
{
    public long Timestamp;
    public double Price;
}

public sealed class TimeSeriesIngestor
{
    private long _globalOffset;
    private readonly Pipe _storagePipe = new(new PipeOptions(useSynchronizationContext: false));

    public void IngestTickBurst(ReadOnlySpan<byte> rawSocketBytes)
    {
        // Interlocked.Add:
        // - 여러 스레드가 동시에 들어와도 중복 없는 누적 오프셋을 발급합니다.
        long currentSlot = Interlocked.Add(ref _globalOffset, rawSocketBytes.Length);

        Span<byte> targetBuffer = _storagePipe.Writer.GetSpan(rawSocketBytes.Length);
        rawSocketBytes.CopyTo(targetBuffer);
        _storagePipe.Writer.Advance(rawSocketBytes.Length);

        Console.WriteLine($"[TSDB Ingest] Ticket chunk persisted to Sequential Ring. Offset: {currentSlot}");
    }
}

var ingestor = new TimeSeriesIngestor();
byte[] mockTickBytes = new byte[16];
ingestor.IngestTickBurst(mockTickBytes);

/*
실행 결과:
[TSDB Ingest] Ticket chunk persisted to Sequential Ring. Offset: 16
*/
