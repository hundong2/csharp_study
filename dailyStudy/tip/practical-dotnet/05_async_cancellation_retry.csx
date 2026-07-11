using System;
using System.Threading;
using System.Threading.Tasks;

// async Task<string>:
// 비동기로 실행되고, 완료되면 string 값을 돌려주는 메서드입니다.
// CancellationToken은 호출자가 작업 취소를 요청할 수 있게 해 줍니다.
static async Task<string> CallRemoteAsync(int attempt, CancellationToken cancellationToken)
{
    // Task.Delay:
    // 외부 API나 DB 응답을 기다리는 시간을 흉내 냅니다.
    // cancellationToken을 넘기면 대기 중에도 취소할 수 있습니다.
    await Task.Delay(50, cancellationToken);

    // 첫 번째 시도는 실패하게 만들어 retry 흐름을 보여 줍니다.
    if (attempt < 2)
    {
        throw new TimeoutException("Temporary timeout.");
    }

    return "remote-result";
}

// Func<int, CancellationToken, Task<T>>:
// int와 CancellationToken을 받아 Task<T>를 반환하는 함수를 의미합니다.
// 즉, 재시도할 작업을 외부에서 주입받는 구조입니다.
static async Task<T> RetryAsync<T>(
    Func<int, CancellationToken, Task<T>> operation,
    int maxAttempts,
    CancellationToken cancellationToken)
{
    // for 문:
    // attempt 값을 1부터 maxAttempts까지 증가시키며 반복합니다.
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            // await:
            // 비동기 작업이 끝날 때까지 기다리되, 스레드를 블로킹하지 않습니다.
            return await operation(attempt, cancellationToken);
        }
        // catch when:
        // 예외를 잡되, 조건이 true일 때만 잡습니다.
        // 마지막 시도에서는 예외를 숨기지 않고 호출자에게 전달되게 합니다.
        catch when (attempt < maxAttempts)
        {
            // 재시도 간격입니다. 실제 서비스에서는 지수 백오프를 쓰는 경우가 많습니다.
            await Task.Delay(100, cancellationToken);
        }
    }

    throw new InvalidOperationException("Retry failed unexpectedly.");
}

// CancellationTokenSource:
// 취소 신호를 만들어내는 객체입니다.
// using 블록으로 감싸서 타이머 리소스를 정리합니다.
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    string value = await RetryAsync(CallRemoteAsync, maxAttempts: 3, cts.Token);
    Console.WriteLine($"[Async] Value={value}");
}
