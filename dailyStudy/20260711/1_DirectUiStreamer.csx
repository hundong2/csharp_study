/*
문제: 네트워크 바이트 버퍼를 RenderPoint2D 구조체 배열처럼 해석하세요.
*/

using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RenderPoint2D
{
    public float X;
    public float Y;
}

public sealed class HighSpeedUiStreamer
{
    public void StreamToDisplayBuffer(ReadOnlySpan<byte> rawSocketNetworkPayload)
    {
        // MemoryMarshal.Cast:
        // - 같은 메모리 구간을 다른 unmanaged 타입 Span으로 해석합니다.
        ReadOnlySpan<RenderPoint2D> points = MemoryMarshal.Cast<byte, RenderPoint2D>(rawSocketNetworkPayload);
        Console.WriteLine($"[UI Network Pipeline] Directly mapped onto Render Bus. Points Count: {points.Length}");
    }
}

var uiStreamer = new HighSpeedUiStreamer();
byte[] mockSocketData = new byte[32];
uiStreamer.StreamToDisplayBuffer(mockSocketData);

/*
실행 결과:
[UI Network Pipeline] Directly mapped onto Render Bus. Points Count: 4
*/

