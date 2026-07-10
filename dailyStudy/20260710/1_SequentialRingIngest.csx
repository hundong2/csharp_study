using System;
using System.Runtime.InteropServices;
using System.Threading;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TimeSeriesTick
{
    public long Timestamp;
    public double Price;
}

public sealed class SequentialRingBuffer
{
    private readonly byte[] _buffer;
    private long _writeOffset;

    public SequentialRingBuffer(int capacity)
    {
        _buffer = new byte[capacity];
    }

    public long Append(ReadOnlySpan<byte> data)
    {
        // Interlocked.Add:
        // 여러 스레드가 동시에 Append해도 서로 다른 영역을 예약하게 합니다.
        long endOffset = Interlocked.Add(ref _writeOffset, data.Length);
        long startOffset = endOffset - data.Length;

        int index = (int)(startOffset % _buffer.Length);

        // 예제는 단순화를 위해 wrap-around 없는 작은 데이터만 씁니다.
        data.CopyTo(_buffer.AsSpan(index, data.Length));
        return endOffset;
    }
}

void Run()
{
    var tick = new TimeSeriesTick
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Price = 150.25d
    };

    byte[] bytes = new byte[Marshal.SizeOf<TimeSeriesTick>()];
    MemoryMarshal.Write(bytes, in tick);

    var ring = new SequentialRingBuffer(1024);
    long offset = ring.Append(bytes);

    Console.WriteLine($"[TSDB Ingest] Tick chunk appended to sequential ring. Offset: {offset}");
}

Run();
