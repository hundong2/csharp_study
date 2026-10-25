using System.Buffers.Binary;
using System.Text;

namespace TelemetryStreamExercise.Infrastructure.Protocol;

/// <summary>
/// demo와 self-test가 decoder와 같은 4-byte big-endian frame 계약으로 입력을 만들도록 돕습니다.
/// 운영 송신기 대신 학습 fixture를 만드는 작은 보조 도구입니다.
/// </summary>
public static class LengthPrefixedFrameEncoder
{
    /// <summary>
    /// JSON 문자열을 UTF-8 byte로 바꾸고 실제 byte 길이를 prefix로 붙입니다.
    /// </summary>
    /// <param name="json">frame 하나에 넣을 JSON 문자열입니다.</param>
    /// <returns>4-byte 길이 header와 UTF-8 payload가 이어진 새 배열을 반환합니다.</returns>
    public static byte[] EncodeJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var payload = Encoding.UTF8.GetBytes(json);
        return EncodePayload(payload);
    }

    /// <summary>
    /// 비어 있지 않은 payload 앞에 4-byte big-endian 길이를 붙입니다.
    /// </summary>
    /// <param name="payload">frame에 넣을 원본 byte입니다.</param>
    /// <returns>header와 payload를 합친 새 배열을 반환합니다.</returns>
    public static byte[] EncodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new ArgumentException("정상 frame payload는 비어 있을 수 없습니다.", nameof(payload));
        }

        var frame = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, sizeof(int)), payload.Length);
        payload.CopyTo(frame.AsSpan(sizeof(int)));
        return frame;
    }

    /// <summary>
    /// self-test에서 0, 음수, 초대형 선언 길이 같은 잘못된 header를 만들 수 있게 합니다.
    /// </summary>
    /// <param name="declaredLength">big-endian signed Int32로 기록할 선언 길이입니다.</param>
    /// <returns>payload 없이 정확히 4-byte인 header를 반환합니다.</returns>
    public static byte[] EncodeLengthPrefix(int declaredLength)
    {
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(header, declaredLength);
        return header;
    }

    /// <summary>
    /// 여러 frame 또는 header 조각을 순서를 유지한 하나의 byte 배열로 합칩니다.
    /// </summary>
    /// <param name="parts">앞에서부터 차례로 붙일 byte 배열들입니다.</param>
    /// <returns>모든 입력을 복사한 하나의 새 배열을 반환합니다.</returns>
    public static byte[] Concat(params byte[][] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        foreach (var part in parts)
        {
            ArgumentNullException.ThrowIfNull(part);
        }

        // params는 호출자가 여러 byte[]를 쉼표로 넘기면 compiler가 배열 하나로 묶어 주는 문법입니다.
        // Aggregate의 lambda는 현재 합계와 다음 배열을 받아 checked 합계를 만드는 작은 함수입니다.
        // checked는 길이 합계가 int 범위를 넘으면 wraparound 대신 예외로 programmer 실수를 드러냅니다.
        var totalLength = parts.Aggregate(0, (total, part) => checked(total + part.Length));
        var combined = new byte[totalLength];
        var offset = 0;

        foreach (var part in parts)
        {
            part.CopyTo(combined, offset);
            offset += part.Length;
        }

        return combined;
    }
}
