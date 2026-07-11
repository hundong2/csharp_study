using System;
using System.Threading;
using System.Threading.Tasks;

int counter = 0;

// Interlocked.Increment:
// 여러 스레드가 동시에 증가시켜도 값이 깨지지 않는 원자적 증가입니다.
// 메모리 관점:
// CPU가 같은 메모리 위치를 동시에 바꾸는 상황에서 업데이트 손실을 막습니다.
Parallel.For(0, 1000, _ =>
{
    Interlocked.Increment(ref counter);
});

Console.WriteLine($"[Interlocked] Counter={counter}");

// SemaphoreSlim:
// 동시에 들어올 수 있는 작업 수를 제한합니다.
// initialCount: 2는 최대 2개 작업만 동시에 통과시킨다는 뜻입니다.
var gate = new SemaphoreSlim(initialCount: 2);

async Task RunLimitedAsync(int id)
{
    // WaitAsync:
    // 세마포어 자리가 날 때까지 비동기로 기다립니다.
    await gate.WaitAsync();
    try
    {
        Console.WriteLine($"[Semaphore] Start {id}");
        await Task.Delay(50);
        Console.WriteLine($"[Semaphore] End {id}");
    }
    finally
    {
        // Release:
        // 작업이 끝났으니 다음 작업이 들어올 수 있도록 자리를 반환합니다.
        // finally에 둬야 예외가 나도 세마포어가 영원히 잠기지 않습니다.
        gate.Release();
    }
}

// Task.WhenAll:
// 여러 비동기 작업이 모두 끝날 때까지 기다립니다.
await Task.WhenAll(
    RunLimitedAsync(1),
    RunLimitedAsync(2),
    RunLimitedAsync(3));
