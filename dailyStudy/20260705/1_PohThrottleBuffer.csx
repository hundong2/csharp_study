using System;
using System.Collections.Generic;

public sealed class PinnedBufferThrottle
{
    private readonly int _maxPinnedBuffers;
    private readonly List<byte[]> _buffers = new();

    public PinnedBufferThrottle(int maxPinnedBuffers)
    {
        _maxPinnedBuffers = maxPinnedBuffers;
    }

    public bool TryRent(int size, out byte[] buffer)
    {
        if (_buffers.Count >= _maxPinnedBuffers)
        {
            buffer = Array.Empty<byte>();
            return false;
        }

        // GC.AllocateArray<T>(length, pinned: true):
        // - pinned: true를 주면 GC가 이동시키지 않는 고정 배열을 할당합니다.
        // - 네이티브 I/O와 상호 운용할 때 유용하지만 남용하면 힙 관리 부담이 커집니다.
        buffer = GC.AllocateArray<byte>(size, pinned: true);
        _buffers.Add(buffer);
        return true;
    }

    public int ActivePinnedBuffers => _buffers.Count;
}

var throttle = new PinnedBufferThrottle(maxPinnedBuffers: 2);

Console.WriteLine($"[POH] Rent 1: {throttle.TryRent(1024, out _)} | Active: {throttle.ActivePinnedBuffers}");
Console.WriteLine($"[POH] Rent 2: {throttle.TryRent(1024, out _)} | Active: {throttle.ActivePinnedBuffers}");
Console.WriteLine($"[POH] Rent 3: {throttle.TryRent(1024, out _)} | Active: {throttle.ActivePinnedBuffers}");

/*
실행 결과:
[POH] Rent 1: True | Active: 1
[POH] Rent 2: True | Active: 2
[POH] Rent 3: False | Active: 2
*/

