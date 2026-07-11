using System;
using System.Threading.Tasks;

// 실무 패턴: Circuit Breaker
// 외부 시스템이 계속 실패할 때 호출을 즉시 차단해 전체 서비스가 같이 느려지는 것을 막습니다.

public sealed class CircuitBreaker
{
    private int _failureCount;
    private bool _isOpen;

    public async Task ExecuteAsync(Func<Task> operation)
    {
        if (_isOpen)
        {
            Console.WriteLine("[CircuitBreaker] Circuit is open. Request blocked.");
            return;
        }

        try
        {
            await operation();
            _failureCount = 0;
        }
        catch
        {
            _failureCount++;

            if (_failureCount >= 2)
            {
                _isOpen = true;
            }

            Console.WriteLine($"[CircuitBreaker] Failure Count: {_failureCount}");
        }
    }
}

var breaker = new CircuitBreaker();

await breaker.ExecuteAsync(() => throw new InvalidOperationException("remote failure"));
await breaker.ExecuteAsync(() => throw new InvalidOperationException("remote failure"));
await breaker.ExecuteAsync(() => Task.CompletedTask);
