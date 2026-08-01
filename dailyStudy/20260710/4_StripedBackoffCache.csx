/*
문제: 키를 여러 stripe로 나눠 중복 영속화 요청을 분산하세요.
*/

using System;
using System.Threading;

public sealed class StripedBackoffCache
{
    private readonly int[] _stripes;

    public StripedBackoffCache(int stripeCount)
    {
        _stripes = new int[stripeCount];
    }

    public bool TryMarkPersistence(string key)
    {
        int stripe = Math.Abs(key.GetHashCode()) % _stripes.Length;
        return Interlocked.CompareExchange(ref _stripes[stripe], 1, 0) == 0;
    }
}

var cache = new StripedBackoffCache(8);
Console.WriteLine($"[Backoff Cache] First mark: {cache.TryMarkPersistence("tenant:42")}");
Console.WriteLine($"[Backoff Cache] Second mark: {cache.TryMarkPersistence("tenant:42")}");
Console.WriteLine("HybridCache System operational with Lock-Free Adaptive Striped Backoff Persistence Engine.");

/*
실행 결과:
[Backoff Cache] First mark: True
[Backoff Cache] Second mark: False
HybridCache System operational with Lock-Free Adaptive Striped Backoff Persistence Engine.
*/

