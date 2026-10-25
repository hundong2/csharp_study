namespace TelemetryStreamExercise.Infrastructure;

/// <summary>
/// 원본 byte 배열을 의도적으로 작은 조각으로 나눠 반환하는 읽기 전용 Stream입니다.
/// TCP에서는 한 번 보낸 frame이 한 번의 read로 온다는 보장이 없다는 사실을 demo와 self-test로 재현합니다.
/// </summary>
public sealed class ChunkedReadStream : Stream
{
    private readonly byte[] _data;
    private readonly int[] _chunkSizes;
    private int _position;
    private int _readCount;

    /// <summary>
    /// 복사한 byte 데이터와 반복해서 사용할 양수 chunk 크기 계획으로 stream을 만듭니다.
    /// </summary>
    /// <param name="data">read 호출들이 순서대로 반환할 전체 byte입니다.</param>
    /// <param name="chunkSizes">각 read가 최대로 반환할 크기이며 끝에 도달하면 처음부터 반복합니다.</param>
    public ChunkedReadStream(byte[] data, params int[] chunkSizes)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(chunkSizes);
        // lambda `size => ...`는 각 chunk 크기가 0 이하인지 검사하는 이름 없는 작은 함수입니다.
        if (chunkSizes.Length == 0 || chunkSizes.Any(size => size <= 0))
        {
            throw new ArgumentException("chunk 크기는 하나 이상의 양수여야 합니다.", nameof(chunkSizes));
        }

        _data = data.ToArray();
        _chunkSizes = chunkSizes.ToArray();
    }

    /// <summary>
    /// 실제로 데이터 read가 호출된 횟수입니다. 작은 조각으로 들어왔음을 demo에서 확인할 수 있습니다.
    /// </summary>
    // =>는 본문이 식 하나인 property를 간결하게 쓰는 식 본문 문법입니다.
    public int ReadCount => _readCount;

    /// <summary>
    /// 이 stream은 읽기를 지원합니다.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// network stream과 비슷한 학습 fixture이므로 임의 위치 이동은 지원하지 않습니다.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// 입력 fixture는 읽기 전용이므로 쓰기를 지원하지 않습니다.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// fixture가 가진 전체 byte 수입니다.
    /// </summary>
    public override long Length => _data.Length;

    /// <summary>
    /// 지금까지 호출자에게 전달한 byte 위치를 읽을 수 있지만 직접 변경할 수는 없습니다.
    /// </summary>
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException("ChunkedReadStream은 seek를 지원하지 않습니다.");
    }

    /// <summary>
    /// 읽기 전용 memory stream에는 밀어낼 write buffer가 없어 아무 작업도 하지 않습니다.
    /// </summary>
    /// <returns>반환값은 없습니다.</returns>
    public override void Flush()
    {
    }

    /// <summary>
    /// legacy byte 배열 API에서도 계획한 chunk 크기 이하만 반환합니다.
    /// </summary>
    /// <param name="buffer">읽은 byte를 받을 배열입니다.</param>
    /// <param name="offset">배열에서 쓰기를 시작할 위치입니다.</param>
    /// <param name="count">호출자가 받을 수 있는 최대 byte 수입니다.</param>
    /// <returns>실제로 복사한 byte 수이며 EOF에서는 0입니다.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return ReadCore(buffer.AsSpan(offset, count));
    }

    /// <summary>
    /// Span 기반 동기 read에서도 같은 chunk 분할 규칙을 적용합니다.
    /// </summary>
    /// <param name="buffer">읽은 byte를 받을 연속 memory 영역입니다.</param>
    /// <returns>실제로 복사한 byte 수이며 EOF에서는 0입니다.</returns>
    public override int Read(Span<byte> buffer)
    {
        return ReadCore(buffer);
    }

    /// <summary>
    /// PipeReader가 사용하는 Memory 기반 비동기 read를 취소 가능하게 제공합니다.
    /// </summary>
    /// <param name="buffer">읽은 byte를 받을 memory 영역입니다.</param>
    /// <param name="cancellationToken">read 전에 중단할 수 있는 호출자 신호입니다.</param>
    /// <returns>실제로 복사한 byte 수를 담은 이미 완료된 ValueTask를 반환합니다.</returns>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ReadCore(buffer.Span));
    }

    /// <summary>
    /// legacy 비동기 배열 API를 Memory 기반 구현과 같은 규칙으로 처리합니다.
    /// </summary>
    /// <param name="buffer">읽은 byte를 받을 배열입니다.</param>
    /// <param name="offset">배열에서 쓰기를 시작할 위치입니다.</param>
    /// <param name="count">호출자가 받을 수 있는 최대 byte 수입니다.</param>
    /// <param name="cancellationToken">read 전에 중단할 수 있는 호출자 신호입니다.</param>
    /// <returns>실제로 복사한 byte 수를 담은 Task를 반환합니다.</returns>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadCore(buffer.AsSpan(offset, count)));
    }

    /// <summary>
    /// seek를 지원하지 않는 fixture에서 호출되면 명확한 예외로 잘못된 사용을 알립니다.
    /// </summary>
    /// <param name="offset">이동하려 한 byte 거리입니다.</param>
    /// <param name="origin">이동 기준 위치입니다.</param>
    /// <returns>지원하지 않으므로 값을 반환하지 않고 예외가 발생합니다.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("ChunkedReadStream은 seek를 지원하지 않습니다.");
    }

    /// <summary>
    /// 읽기 전용 fixture의 길이 변경 시도를 거절합니다.
    /// </summary>
    /// <param name="value">설정하려 한 새 길이입니다.</param>
    /// <returns>지원하지 않으므로 반환 없이 예외가 발생합니다.</returns>
    public override void SetLength(long value)
    {
        throw new NotSupportedException("ChunkedReadStream은 길이를 바꿀 수 없습니다.");
    }

    /// <summary>
    /// 읽기 전용 fixture에 쓰려는 시도를 거절합니다.
    /// </summary>
    /// <param name="buffer">쓰려 한 원본 배열입니다.</param>
    /// <param name="offset">원본 배열의 시작 위치입니다.</param>
    /// <param name="count">쓰려 한 byte 수입니다.</param>
    /// <returns>지원하지 않으므로 반환 없이 예외가 발생합니다.</returns>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("ChunkedReadStream은 쓸 수 없습니다.");
    }

    /// <summary>
    /// 다음 chunk 계획과 호출자 buffer 크기 중 작은 만큼만 복사하고 현재 위치를 이동합니다.
    /// </summary>
    /// <param name="destination">이번 read가 채울 memory 영역입니다.</param>
    /// <returns>복사한 byte 수이며 모든 데이터를 읽었으면 0입니다.</returns>
    private int ReadCore(Span<byte> destination)
    {
        if (_position >= _data.Length || destination.IsEmpty)
        {
            return 0;
        }

        var plannedSize = _chunkSizes[_readCount % _chunkSizes.Length];
        var remaining = _data.Length - _position;
        var count = Math.Min(Math.Min(plannedSize, destination.Length), remaining);
        _data.AsSpan(_position, count).CopyTo(destination);
        _position += count;
        _readCount++;
        return count;
    }
}
