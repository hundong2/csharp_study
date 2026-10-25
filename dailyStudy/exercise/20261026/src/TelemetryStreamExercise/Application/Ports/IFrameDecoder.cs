using System.Buffers;

namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// byte sequence에서 frame 하나를 꺼내는 Strategy 계약입니다.
/// Application은 4-byte big-endian이라는 구현을 직접 알지 않고 이 Port에 의존합니다.
/// </summary>
public interface IFrameDecoder
{
    /// <summary>
    /// 현재 buffer의 맨 앞에서 frame 하나를 시도하고, 성공했을 때만 해당 byte를 buffer에서 제거합니다.
    /// </summary>
    /// <param name="buffer">PipeReader가 제공한 아직 소비하지 않은 byte sequence이며, 성공 시 다음 위치로 이동합니다.</param>
    /// <param name="isCompleted">더 이상 stream byte가 오지 않는 EOF인지 나타냅니다.</param>
    /// <returns>완성 frame, 추가 byte 필요, protocol 오류 중 하나를 반환합니다.</returns>
    FrameDecodeResult Decode(ref ReadOnlySequence<byte> buffer, bool isCompleted);
}
