using System;
using System.Threading;
using System.Threading.Tasks;

static async Task<string> CallRemoteAsync(int attempt, CancellationToken cancellationToken)
{
    await Task.Delay(50, cancellationToken);

    if (attempt < 2)
    {
        throw new TimeoutException("Temporary timeout.");
    }

    return "remote-result";
}

static async Task<T> RetryAsync<T>(
    Func<int, CancellationToken, Task<T>> operation,
    int maxAttempts,
    CancellationToken cancellationToken)
{
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            return await operation(attempt, cancellationToken);
        }
        catch when (attempt < maxAttempts)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    throw new InvalidOperationException("Retry failed unexpectedly.");
}

using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    string value = await RetryAsync(CallRemoteAsync, maxAttempts: 3, cts.Token);
    Console.WriteLine($"[Async] Value={value}");
}
