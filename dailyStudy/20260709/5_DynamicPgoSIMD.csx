/*
문제: 패킷 바이트 배열에 보안 필터를 적용하고 첫 번째 바이트를 출력하세요.

답안 포인트:
- 인터페이스로 필터 전략을 분리합니다.
- ref 매개변수로 배열 요소를 직접 갱신합니다.
- MethodImplOptions.AggressiveOptimization은 장기 실행 핫패스 최적화 의도를 표현합니다.
*/

using System;
using System.Runtime.CompilerServices;

public interface INetworkFilter { void Inspect(ref byte packetByte); }

public sealed class CoreSecurityFilter : INetworkFilter
{
    public void Inspect(ref byte b) => b = (byte)(b ^ 0x5A);
}

public sealed class HardwareAcceleratedProcessor
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RunSecurityInspection(byte[] rawPackets, INetworkFilter filter)
    {
        for (int i = 0; i < rawPackets.Length; i++)
        {
            filter.Inspect(ref rawPackets[i]);
        }
    }
}

var processor = new HardwareAcceleratedProcessor();
byte[] payload = [0x00, 0xFF, 0x12];
processor.RunSecurityInspection(payload, new CoreSecurityFilter());
Console.WriteLine($"[JIT Dynamic PGO] Optimized Packet Checksum Head: {payload[0]}");

/*
실행 결과:
[JIT Dynamic PGO] Optimized Packet Checksum Head: 90
*/

