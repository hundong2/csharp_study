using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

public sealed class CacheContentionTracer
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly object _gate = new();

    public void SetWithTrace(string key, string value)
    {
        var wait = Stopwatch.StartNew();

        // lock:
        // - 임계 구역을 한 번에 하나의 스레드만 실행하게 합니다.
        // - 실무에서는 너무 넓은 lock이 병목이 되므로 대기 시간을 계측해야 합니다.
        lock (_gate)
        {
            wait.Stop();
            _cache[key] = value;

            Console.WriteLine($"[Cache Trace] key={key}, contentionTicks={wait.ElapsedTicks}");
        }
    }

    public bool TryGet(string key, out string value) => _cache.TryGetValue(key, out value);
}

var tracer = new CacheContentionTracer();
tracer.SetWithTrace("tenant:42:profile", "cached");
tracer.TryGet("tenant:42:profile", out string cached);

Console.WriteLine($"HybridCache System operational with Lock-Contention Diagnostic EventPipe Trace. Value: {cached}");

/*
실행 결과 예시:
[Cache Trace] key=tenant:42:profile, contentionTicks=0
HybridCache System operational with Lock-Contention Diagnostic EventPipe Trace. Value: cached

참고: contentionTicks 값은 실행 환경마다 달라집니다.
*/

