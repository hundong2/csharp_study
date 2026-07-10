using System;
using System.Collections.Concurrent;
using System.Threading;

// NUMA 자체를 스크립트에서 직접 제어하지는 않습니다.
// 대신 "전역 락 하나에 몰리지 않도록 데이터를 여러 stripe로 나누는 캐시"를 학습합니다.

public sealed class StripedCache
{
    private readonly ConcurrentDictionary<string, string>[] _stripes;

    public StripedCache(int stripeCount)
    {
        _stripes = new ConcurrentDictionary<string, string>[stripeCount];

        for (int i = 0; i < _stripes.Length; i++)
        {
            _stripes[i] = new ConcurrentDictionary<string, string>();
        }
    }

    public void Set(string key, string value)
    {
        GetStripe(key)[key] = value;
    }

    public bool TryGet(string key, out string value)
    {
        return GetStripe(key).TryGetValue(key, out value);
    }

    private ConcurrentDictionary<string, string> GetStripe(string key)
    {
        // GetHashCode 값으로 stripe를 고릅니다.
        // Math.Abs(int.MinValue)는 overflow 문제가 있어 uint로 변환해 나머지를 구합니다.
        uint hash = (uint)key.GetHashCode();
        int index = (int)(hash % (uint)_stripes.Length);
        return _stripes[index];
    }
}

int stripeCount = Math.Max(2, Environment.ProcessorCount);
var cache = new StripedCache(stripeCount);

cache.Set("tenant:42", "cached-payload");
cache.TryGet("tenant:42", out string value);

Console.WriteLine($"[Striped Cache] Stripe Count: {stripeCount}");
Console.WriteLine($"[Striped Cache] Value: {value}");
Console.WriteLine($"[Striped Cache] Thread Id: {Thread.CurrentThread.ManagedThreadId}");
