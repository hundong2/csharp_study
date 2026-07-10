using System;
using System.Threading;

public sealed class StripedBackoff
{
    private readonly int[] _stripes;

    public StripedBackoff(int stripeCount)
    {
        _stripes = new int[stripeCount];
    }

    public bool TryEnter(string key)
    {
        int index = GetStripeIndex(key);
        return Interlocked.CompareExchange(ref _stripes[index], 1, 0) == 0;
    }

    public void Exit(string key)
    {
        int index = GetStripeIndex(key);
        Interlocked.Exchange(ref _stripes[index], 0);
    }

    private int GetStripeIndex(string key)
    {
        uint hash = (uint)key.GetHashCode();
        return (int)(hash % (uint)_stripes.Length);
    }
}

var backoff = new StripedBackoff(Environment.ProcessorCount);
string cacheKey = "series:KRW-BTC";

Console.WriteLine($"[Backoff] First persistence attempt: {backoff.TryEnter(cacheKey)}");
Console.WriteLine($"[Backoff] Duplicate persistence attempt: {backoff.TryEnter(cacheKey)}");

backoff.Exit(cacheKey);
Console.WriteLine($"[Backoff] After release: {backoff.TryEnter(cacheKey)}");
