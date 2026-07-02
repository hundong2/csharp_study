using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

// 캐시 스탬피드 방어를 설명하기 위한 작은 single-flight 캐시입니다.
// 같은 키에 대한 동시 요청이 몰려도 값 생성 작업은 한 번만 실행되도록 합니다.
public sealed class SingleFlightCache<TKey, TValue>
    where TKey : notnull
{
    // _inflight는 현재 생성 중인 작업을 저장합니다.
    // Lazy<Task<TValue>>를 사용하면 동시에 여러 스레드가 들어와도 실제 factory 실행을 하나로 묶을 수 있습니다.
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _inflight = new();

    // _cache는 생성 완료된 값을 저장하는 단순 메모리 캐시입니다.
    private readonly ConcurrentDictionary<TKey, TValue> _cache = new();

    public Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> factory)
    {
        // 이미 캐시에 값이 있으면 비동기 작업을 만들지 않고 즉시 완료된 Task로 반환합니다.
        if (_cache.TryGetValue(key, out TValue cached))
        {
            return Task.FromResult(cached);
        }

        // 값이 없으면 현재 생성 중인 작업을 조회하거나 새로 등록합니다.
        // GetOrAdd는 같은 키에 대해 하나의 Lazy<Task<TValue>>만 사전에 남깁니다.
        Lazy<Task<TValue>> lazy = _inflight.GetOrAdd(
            key,
            static (_, state) => new Lazy<Task<TValue>>(
                async () =>
                {
                    // 이 블록이 실제 값 생성 구간입니다.
                    // 같은 key의 동시 요청들은 모두 이 Task 하나를 공유해서 기다립니다.
                    TValue value = await state.factory();

                    // 생성이 끝나면 결과를 캐시에 저장합니다.
                    state.owner._cache[state.key] = value;

                    // 진행 중 목록에서는 제거합니다.
                    // 이후 같은 key 요청은 _cache에서 바로 반환됩니다.
                    state.owner._inflight.TryRemove(state.key, out Lazy<Task<TValue>> removed);
                    return value;
                },
                // ExecutionAndPublication은 Lazy 값을 한 번만 실행하고 결과를 공유합니다.
                LazyThreadSafetyMode.ExecutionAndPublication),
            (owner: this, key, factory));

        // Lazy.Value를 읽는 순간 factory Task가 시작됩니다.
        // 이미 누군가 시작했다면 같은 Task 인스턴스를 돌려받습니다.
        return lazy.Value;
    }
}

var cache = new SingleFlightCache<string, string>();
int factoryCalls = 0;

// 동일한 키 tenant:42에 대해 8개의 동시 요청을 발생시킵니다.
// 캐시 스탬피드가 방어되지 않으면 factoryCalls가 8에 가까워질 수 있습니다.
Task<string>[] requests = Enumerable.Range(0, 8)
    .Select(_ => cache.GetOrCreateAsync("tenant:42", async () =>
    {
        // 실제 DB 조회, 외부 API 호출, 직렬화 비용이 큰 계산을 흉내 냅니다.
        Interlocked.Increment(ref factoryCalls);
        await Task.Delay(100);
        return "serialized-payload";
    }))
    .ToArray();

// 모든 요청이 완료될 때까지 기다립니다.
await Task.WhenAll(requests);

Console.WriteLine($"[Cache Stampede Guard] Concurrent Requests: {requests.Length}");
Console.WriteLine($"[Cache Stampede Guard] Factory Calls: {factoryCalls}");
Console.WriteLine($"[Cache Stampede Guard] Cached Value: {requests[0].Result}");
