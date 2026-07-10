using System;
using System.Diagnostics;

public sealed class SecurePacketChannel
{
    public void Transmit() => Console.WriteLine("   [Channel] Byte Stream Transmitted.");
}

public interface ITelemetryGuard
{
    void MonitoredTransmit();
}

public sealed class PacketGuard : ITelemetryGuard
{
    private readonly SecurePacketChannel _channel;

    public PacketGuard(SecurePacketChannel channel)
    {
        _channel = channel;
    }

    public void MonitoredTransmit()
    {
        var watch = Stopwatch.StartNew();
        _channel.Transmit();
        watch.Stop();

        Console.WriteLine($"[C# Telemetry] Zero-Alloc Metric Captured. Duration: {watch.ElapsedTicks} ticks");
    }
}

var channel = new SecurePacketChannel();
ITelemetryGuard guard = new PacketGuard(channel);
guard.MonitoredTransmit();

/*
실행 결과 예시:
   [Channel] Byte Stream Transmitted.
[C# Telemetry] Zero-Alloc Metric Captured. Duration: 1234 ticks

참고: Duration 값은 실행 환경마다 달라집니다.
*/

