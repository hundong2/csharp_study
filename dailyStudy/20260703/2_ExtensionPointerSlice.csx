using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public record struct NetworkHeader(long PacketId, int Status);

public static class HeaderBytes
{
    public static byte[] CopyBytes(ref NetworkHeader header)
    {
        Span<NetworkHeader> single = MemoryMarshal.CreateSpan(ref header, 1);
        return MemoryMarshal.AsBytes(single).ToArray();
    }
}

var header = new NetworkHeader(1002341, 200);
byte[] bytes = HeaderBytes.CopyBytes(ref header);

Console.WriteLine($"[C# Pointer] Header byte length: {bytes.Length}");
Console.WriteLine($"[C# Pointer] PacketId: {header.PacketId}, Status: {header.Status}");
Console.WriteLine($"[C# Pointer] First 8 bytes: {BitConverter.ToString(bytes[..8])}");

/*
실행 결과:
[C# Pointer] Header byte length: 16
[C# Pointer] PacketId: 1002341, Status: 200
[C# Pointer] First 8 bytes: 65-4B-0F-00-00-00-00-00
*/
