#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

// 실무 패턴: Background Work Queue
// HTTP 요청 안에서 오래 걸리는 일을 직접 처리하지 않고, 큐에 넣은 뒤 백그라운드에서 처리하는 구조입니다.

public sealed class BackgroundQueue
{
    private readonly ConcurrentQueue<Func<Task>> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(Func<Task> work)
    {
        _queue.Enqueue(work);

        // Release:
        // 대기 중인 worker에게 "할 일이 생겼다"고 알려줍니다.
        _signal.Release();
    }

    public async Task RunOnceAsync()
    {
        // WaitAsync:
        // 큐에 일이 들어올 때까지 비동기로 기다립니다.
        await _signal.WaitAsync();

        if (_queue.TryDequeue(out Func<Task>? work))
        {
            await work();
        }
    }
}

var queue = new BackgroundQueue();

queue.Enqueue(async () =>
{
    await Task.Delay(50);
    Console.WriteLine("[Queue] Background work processed.");
});

await queue.RunOnceAsync();
