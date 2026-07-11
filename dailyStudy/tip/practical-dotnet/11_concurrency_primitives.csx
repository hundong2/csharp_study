using System;
using System.Threading;
using System.Threading.Tasks;

int counter = 0;

// Interlocked.Increment:
// 여러 스레드가 동시에 증가시켜도 값이 깨지지 않는 원자적 증가입니다.
Parallel.For(0, 1000, _ =>
{
    Interlocked.Increment(ref counter);
});

Console.WriteLine($"[Interlocked] Counter={counter}");

var gate = new SemaphoreSlim(initialCount: 2);

async Task RunLimitedAsync(int id)
{
    await gate.WaitAsync();
    try
    {
        Console.WriteLine($"[Semaphore] Start {id}");
        await Task.Delay(50);
        Console.WriteLine($"[Semaphore] End {id}");
    }
    finally
    {
        gate.Release();
    }
}

await Task.WhenAll(
    RunLimitedAsync(1),
    RunLimitedAsync(2),
    RunLimitedAsync(3));
