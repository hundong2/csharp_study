using System;
using System.Collections.Concurrent;

public sealed class ByteBufferPool
{
    private readonly ConcurrentBag<byte[]> _pool = new();
    private readonly int _bufferSize;

    public ByteBufferPool(int bufferSize)
    {
        _bufferSize = bufferSize;
    }

    public byte[] Rent()
    {
        // ConcurrentBag<T>:
        // - 여러 스레드가 동시에 넣고 뺄 수 있는 비순서 컬렉션입니다.
        return _pool.TryTake(out var buffer) ? buffer : new byte[_bufferSize];
    }

    public void Return(byte[] buffer)
    {
        Array.Clear(buffer);
        _pool.Add(buffer);
    }
}

var pool = new ByteBufferPool(1024);

byte[] first = pool.Rent();
pool.Return(first);
byte[] second = pool.Rent();

Console.WriteLine($"[Pool] Buffer reused: {ReferenceEquals(first, second)}");

/*
실행 결과:
[Pool] Buffer reused: True
*/

