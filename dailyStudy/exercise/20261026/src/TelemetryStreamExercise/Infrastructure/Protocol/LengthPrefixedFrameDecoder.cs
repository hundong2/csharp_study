using System.Buffers;
using System.Buffers.Binary;
using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Infrastructure.Protocol;

/// <summary>
/// 4-byte signed big-endian 길이 prefix 뒤에 JSON UTF-8 payload가 오는 protocol을 해석합니다.
/// 최대 크기를 먼저 검사해 공격자가 선언한 거대한 길이만큼 메모리를 할당하거나 끝없이 기다리지 않게 합니다.
/// </summary>
public sealed class LengthPrefixedFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = sizeof(int);
    private readonly int _maximumFrameLength;

    /// <summary>
    /// 허용할 최대 payload byte 수를 고정한 decoder Strategy를 만듭니다.
    /// </summary>
    /// <param name="maximumFrameLength">0보다 큰 최대 JSON UTF-8 byte 수입니다.</param>
    public LengthPrefixedFrameDecoder(int maximumFrameLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrameLength);
        _maximumFrameLength = maximumFrameLength;
    }

    /// <summary>
    /// buffer 선두의 header와 payload를 검사하고 완전한 frame일 때만 buffer를 다음 위치로 이동합니다.
    /// </summary>
    /// <param name="buffer">여러 memory segment로 나뉠 수 있는 아직 소비하지 않은 byte sequence입니다.</param>
    /// <param name="isCompleted">추가 byte가 절대 오지 않는 EOF인지 나타냅니다.</param>
    /// <returns>완성 payload, 추가 byte 필요, 복구할 수 없는 protocol 오류 중 하나를 반환합니다.</returns>
    public FrameDecodeResult Decode(ref ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        // ReadOnlySequence는 PipeReader의 데이터가 여러 memory segment에 흩어져 있어도 하나의 논리적 sequence로 봅니다.
        // SequenceReader는 segment 경계를 직접 계산하지 않고도 현재 위치와 남은 byte를 안전하게 추적하는 ref struct입니다.
        var reader = new SequenceReader<byte>(buffer);

        // stackalloc은 아주 작은 4-byte header를 managed heap 대신 현재 stack frame에 잠시 확보합니다.
        Span<byte> lengthBytes = stackalloc byte[HeaderLength];
        if (!reader.TryCopyTo(lengthBytes))
        {
            if (isCompleted && !buffer.IsEmpty)
            {
                return FrameDecodeResult.Failure(
                    Error.Protocol(
                        "frame.truncated_header",
                        $"EOF 전에 4-byte 길이 header가 완성되지 않았습니다. 받은 byte={buffer.Length}."));
            }

            return FrameDecodeResult.NeedMoreData();
        }

        // header를 읽었다고 즉시 원본 buffer에서 제거하지 않습니다. payload가 덜 왔다면 다음 read에서도 같은 header가 필요합니다.
        reader.Advance(HeaderLength);
        var frameLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

        if (frameLength == 0)
        {
            return FrameDecodeResult.Failure(
                Error.Protocol("frame.length_zero", "frame payload 길이는 0일 수 없습니다."));
        }

        if (frameLength < 0)
        {
            return FrameDecodeResult.Failure(
                Error.Protocol("frame.length_negative", "frame payload 길이는 음수일 수 없습니다."));
        }

        if (frameLength > _maximumFrameLength)
        {
            return FrameDecodeResult.Failure(
                Error.Protocol(
                    "frame.length_too_large",
                    $"선언 길이 {frameLength} byte가 최대 {_maximumFrameLength} byte를 초과했습니다."));
        }

        if (reader.Remaining < frameLength)
        {
            if (isCompleted)
            {
                return FrameDecodeResult.Failure(
                    Error.Protocol(
                        "frame.truncated_payload",
                        $"EOF 전에 payload가 완성되지 않았습니다. 기대={frameLength}, 실제={reader.Remaining}."));
            }

            return FrameDecodeResult.NeedMoreData();
        }

        var payloadSequence = reader.Sequence.Slice(reader.Position, frameLength);
        // payload 크기는 위에서 제한했으므로 작은 독립 배열로 복사해 PipeReader.AdvanceTo 이후에도 안전하게 사용합니다.
        var payload = payloadSequence.ToArray();
        reader.Advance(frameLength);

        // header와 payload가 모두 완성된 지금만 ref buffer를 다음 frame 시작으로 이동합니다.
        buffer = buffer.Slice(reader.Position);
        return FrameDecodeResult.Decoded(payload);
    }
}
