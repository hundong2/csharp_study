using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public sealed class SingleFlightCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _inflight = new();
    private readonly ConcurrentDictionary<TKey, TValue> _cache = new();

    public Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> factory)
    {
        if (_cache.TryGetValue(key, out TValue cached))
        {
            return Task.FromResult(cached);
        }

        Lazy<Task<TValue>> lazy = _inflight.GetOrAdd(
            key,
            static (_, state) => new Lazy<Task<TValue>>(
                async () =>
                {
                    TValue value = await state.factory();
                    state.owner._cache[state.key] = value;
                    state.owner._inflight.TryRemove(state.key, out Lazy<Task<TValue>> removed);
                    return value;
                },
                LazyThreadSafetyMode.ExecutionAndPublication),
            (owner: this, key, factory));

        return lazy.Value;
    }
}

var cache = new SingleFlightCache<string, string>();
int factoryCalls = 0;

Task<string>[] requests = Enumerable.Range(0, 8)
    .Select(_ => cache.GetOrCreateAsync("tenant:42", async () =>
    {
        Interlocked.Increment(ref factoryCalls);
        await Task.Delay(100);
        return "serialized-payload";
    }))
    .ToArray();

await Task.WhenAll(requests);

Console.WriteLine($"[Cache Stampede Guard] Concurrent Requests: {requests.Length}");
Console.WriteLine($"[Cache Stampede Guard] Factory Calls: {factoryCalls}");
Console.WriteLine($"[Cache Stampede Guard] Cached Value: {requests[0].Result}");
