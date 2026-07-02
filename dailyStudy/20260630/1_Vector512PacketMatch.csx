using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

public sealed class HighThroughputPacketScanner
{
    private const byte MagicByte = 0x7E;

    public int FindMatchIndex(ReadOnlySpan<byte> packet)
    {
        if (Vector512.IsHardwareAccelerated && packet.Length >= Vector512<byte>.Count)
        {
            Vector512<byte> block = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(packet));
            Vector512<byte> target = Vector512.Create(MagicByte);

            if (Vector512.EqualsAny(block, target))
            {
                return packet[..Vector512<byte>.Count].IndexOf(MagicByte);
            }
        }

        return packet.IndexOf(MagicByte);
    }
}

var scanner = new HighThroughputPacketScanner();
byte[] mockPacket = new byte[64];
mockPacket[42] = 0x7E;

int index = scanner.FindMatchIndex(mockPacket);
Console.WriteLine($"[SIMD Network] Vector512 Hardware Acceleration Supported: {Vector512.IsHardwareAccelerated}");
Console.WriteLine($"[SIMD Network] Magic Byte Pattern Detected at Index: {index}");
