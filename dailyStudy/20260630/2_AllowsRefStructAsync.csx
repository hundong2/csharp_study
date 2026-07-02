using System;
using System.Text;
using System.Threading.Tasks;

public interface IBufferConsumer
{
    void Consume(ReadOnlySpan<byte> buffer);
}

public static class ZeroAllocDispatcher
{
    public static ValueTask ProcessBufferAsync<T>(scoped T consumer, string rawPayload)
        where T : IBufferConsumer, allows ref struct
    {
        Span<byte> stackBuffer = stackalloc byte[Encoding.UTF8.GetByteCount(rawPayload)];
        Encoding.UTF8.GetBytes(rawPayload, stackBuffer);

        consumer.Consume(stackBuffer);

        // Span/stackalloc data must not be used across an await boundary.
        return ValueTask.CompletedTask;
    }
}

public ref struct HighSpeedStackConsumer : IBufferConsumer
{
    public void Consume(ReadOnlySpan<byte> buffer)
    {
        Console.WriteLine($"[C# Ref Safety] Stack Buffer Safely Consumed. Size: {buffer.Length}");
    }
}

await ZeroAllocDispatcher.ProcessBufferAsync(new HighSpeedStackConsumer(), "NET10_BURST_TRAFFIC");
