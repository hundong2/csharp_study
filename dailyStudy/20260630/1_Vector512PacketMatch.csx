using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

// 네트워크 패킷이나 바이너리 프레임에서 특정 구분자 바이트를 찾는 상황을 가정합니다.
// 예: 0x7E는 HDLC 계열 프로토콜에서 프레임 구분자로 자주 등장하는 값입니다.
public sealed class HighThroughputPacketScanner
{
    private const byte MagicByte = 0x7E;

    public int FindMatchIndex(ReadOnlySpan<byte> packet)
    {
        // Vector512<byte>.Count는 byte 기준 64입니다.
        // 즉, 하드웨어가 512비트 SIMD를 지원하고 입력이 64바이트 이상일 때만 SIMD 경로를 탑니다.
        if (Vector512.IsHardwareAccelerated && packet.Length >= Vector512<byte>.Count)
        {
            // ReadOnlySpan<byte>의 첫 번째 요소 참조를 얻고, 그 위치부터 64바이트를 Vector512로 읽습니다.
            // 위에서 길이를 먼저 검사했기 때문에 64바이트 읽기가 가능한 상태입니다.
            Vector512<byte> block = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(packet));

            // 비교 대상 바이트 하나를 64칸 전체에 복제한 벡터를 만듭니다.
            // 결과적으로 block의 64개 바이트와 target의 64개 바이트를 병렬 비교할 수 있습니다.
            Vector512<byte> target = Vector512.Create(MagicByte);

            // 64바이트 블록 안에 MagicByte와 같은 값이 하나라도 있으면 true입니다.
            // 이 단계는 "있다/없다"를 빠르게 판정하는 필터 역할을 합니다.
            if (Vector512.EqualsAny(block, target))
            {
                // 실제 인덱스는 첫 64바이트 범위에서 다시 찾습니다.
                // SIMD로 후보 블록을 빠르게 좁히고, 정확한 위치 계산은 Span.IndexOf에 맡기는 구조입니다.
                return packet[..Vector512<byte>.Count].IndexOf(MagicByte);
            }
        }

        // AVX-512 미지원 장비, 짧은 패킷, 첫 블록에 없는 경우를 위한 안전한 기본 경로입니다.
        return packet.IndexOf(MagicByte);
    }
}

var scanner = new HighThroughputPacketScanner();

// 테스트용 64바이트 패킷을 만들고 42번 인덱스에 매직 바이트를 심습니다.
byte[] mockPacket = new byte[64];
mockPacket[42] = 0x7E;

int index = scanner.FindMatchIndex(mockPacket);
Console.WriteLine($"[SIMD Network] Vector512 Hardware Acceleration Supported: {Vector512.IsHardwareAccelerated}");
Console.WriteLine($"[SIMD Network] Magic Byte Pattern Detected at Index: {index}");
