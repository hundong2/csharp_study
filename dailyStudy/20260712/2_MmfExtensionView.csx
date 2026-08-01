/*
문제: RawTelemetryPacket의 SignalValue를 factor만큼 증폭하세요.
*/

using System;

public readonly record struct RawTelemetryPacket(long PacketId, double SignalValue);

public static class TelemetryExtension
{
    public static double Amplify(RawTelemetryPacket packet, double factor)
        => packet.SignalValue * factor;
}

var packet = new RawTelemetryPacket(99821, 55.4d);
Console.WriteLine($"[C# MMF] Mapped Value amplified directly: {TelemetryExtension.Amplify(packet, 2.0d)}");

/*
실행 결과:
[C# MMF] Mapped Value amplified directly: 110.8
*/
