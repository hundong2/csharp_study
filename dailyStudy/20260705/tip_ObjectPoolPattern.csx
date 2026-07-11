#nullable enable

using System;
using System.Collections.Concurrent;
using System.Text;

// 실무 패턴: 간단한 Object Pool
// StringBuilder, byte[]처럼 반복 생성 비용이 있는 객체는 재사용하면 GC 압박을 줄일 수 있습니다.
// 단, 풀에 돌려주기 전에 상태를 반드시 초기화해야 합니다.

public sealed class StringBuilderPool
{
    private readonly ConcurrentBag<StringBuilder> _pool = new();

    public StringBuilder Rent()
    {
        // TryTake:
        // 풀에 객체가 있으면 꺼내고, 없으면 새로 만듭니다.
        return _pool.TryTake(out StringBuilder? builder)
            ? builder
            : new StringBuilder(capacity: 256);
    }

    public void Return(StringBuilder builder)
    {
        // Clear:
        // 이전 사용자의 문자열이 다음 사용자에게 섞이지 않도록 초기화합니다.
        builder.Clear();
        _pool.Add(builder);
    }
}

var pool = new StringBuilderPool();
StringBuilder rented = pool.Rent();

rented.Append("tenant=");
rented.Append(42);
rented.Append(";status=ok");

Console.WriteLine($"[Pool] Message: {rented}");

pool.Return(rented);
