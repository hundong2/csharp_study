using System;
using System.Threading;
using System.Threading.Tasks;

// 실무 패턴: Retry + CancellationToken
// 외부 API, DB, 네트워크는 일시 실패할 수 있습니다.
// 재시도는 필요하지만, 사용자가 취소했거나 timeout이 지나면 즉시 멈출 수 있어야 합니다.

public static async Task<T> RetryAsync<T>(
    Func<int, Task<T>> operation,
    int maxAttempts,
    TimeSpan delay,
    CancellationToken cancellationToken)
{
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            return await operation(attempt);
        }
        catch when (attempt < maxAttempts)
        {
            // Task.Delay에 CancellationToken을 넘기면 대기 중에도 취소할 수 있습니다.
            await Task.Delay(delay, cancellationToken);
        }
    }

    throw new InvalidOperationException("Unreachable retry state.");
}

using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
{
    int callCount = 0;
    string result = await RetryAsync(
        async attempt =>
        {
            callCount++;
            await Task.Delay(50);

            if (attempt < 3)
            {
                throw new TimeoutException("Temporary network failure.");
            }

            return "success";
        },
        maxAttempts: 3,
        delay: TimeSpan.FromMilliseconds(100),
        cancellationToken: timeout.Token);

    Console.WriteLine($"[Retry] Result: {result}, Attempts: {callCount}");
}
