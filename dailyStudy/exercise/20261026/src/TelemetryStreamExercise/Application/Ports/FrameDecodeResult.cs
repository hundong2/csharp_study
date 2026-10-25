using TelemetryStreamExercise.Domain;

namespace TelemetryStreamExercise.Application.Ports;

/// <summary>
/// decoder가 payload 하나를 완성했는지, byte를 더 기다려야 하는지, protocol을 거절했는지 표현합니다.
/// payload는 PipeReader의 buffer 수명 밖에서도 안전하도록 제한된 크기의 독립 byte 배열로 복사합니다.
/// </summary>
public sealed class FrameDecodeResult
{
    /// <summary>
    /// 세 상태가 섞이지 않도록 private 생성자 한곳에서 결과를 만듭니다.
    /// </summary>
    /// <param name="status">decoder가 내린 진행 상태입니다.</param>
    /// <param name="payload">완성된 frame의 JSON byte이며 다른 상태에서는 비어 있습니다.</param>
    /// <param name="error">protocol 오류이며 다른 상태에서는 null입니다.</param>
    private FrameDecodeResult(
        FrameDecodeStatus status,
        ReadOnlyMemory<byte> payload,
        Error? error)
    {
        Status = status;
        Payload = payload;
        Error = error;
    }

    /// <summary>
    /// 현재 decode 결과의 종류입니다.
    /// </summary>
    public FrameDecodeStatus Status { get; }

    /// <summary>
    /// FrameDecoded일 때만 값이 있는 JSON UTF-8 byte입니다.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// ProtocolError일 때만 값이 있는 설명입니다.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// header 또는 payload byte가 더 필요하다는 결과를 만듭니다.
    /// </summary>
    /// <returns>NeedMoreData 상태를 반환합니다.</returns>
    public static FrameDecodeResult NeedMoreData()
    {
        return new FrameDecodeResult(FrameDecodeStatus.NeedMoreData, ReadOnlyMemory<byte>.Empty, null);
    }

    /// <summary>
    /// 완전한 frame payload를 가진 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="payload">길이 prefix를 제외한 JSON UTF-8 byte입니다.</param>
    /// <returns>FrameDecoded 상태를 반환합니다.</returns>
    public static FrameDecodeResult Decoded(ReadOnlyMemory<byte> payload)
    {
        return new FrameDecodeResult(FrameDecodeStatus.FrameDecoded, payload, null);
    }

    /// <summary>
    /// 더 읽어도 복구할 수 없는 framing 오류 결과를 만듭니다.
    /// </summary>
    /// <param name="error">Kind가 Protocol인 오류입니다.</param>
    /// <returns>ProtocolError 상태를 반환합니다.</returns>
    public static FrameDecodeResult Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Kind != ErrorKind.Protocol)
        {
            throw new ArgumentException("Frame decoder 오류는 Protocol 종류여야 합니다.", nameof(error));
        }

        return new FrameDecodeResult(FrameDecodeStatus.ProtocolError, ReadOnlyMemory<byte>.Empty, error);
    }
}
