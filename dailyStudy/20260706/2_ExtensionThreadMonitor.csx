using System;
using System.Threading;

public sealed class ThirdPartyClusterExecutor
{
    public int ActiveWorkers => ThreadPool.ThreadCount;
}

public interface IExecutorTrace
{
    void DumpMetrics();
}

public sealed class ExecutorTracker : IExecutorTrace
{
    private readonly ThirdPartyClusterExecutor _executor;

    public ExecutorTracker(ThirdPartyClusterExecutor executor)
    {
        _executor = executor;
    }

    public void DumpMetrics()
    {
        // ThreadPool.GetAvailableThreads:
        // - 현재 추가 작업에 사용할 수 있는 워커/IOCP 스레드 수를 가져옵니다.
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);

        Console.WriteLine($"[C# Interceptor] Live Scheduler Tracked. Active Worker Threads: {_executor.ActiveWorkers}");
        Console.WriteLine($"[C# Interceptor] Available Workers: {workerThreads}, IOCP: {completionPortThreads}");
    }
}

var executor = new ThirdPartyClusterExecutor();
IExecutorTrace tracer = new ExecutorTracker(executor);
tracer.DumpMetrics();

/*
실행 결과 예시:
[C# Interceptor] Live Scheduler Tracked. Active Worker Threads: 0
[C# Interceptor] Available Workers: 32767, IOCP: 1000

참고: 스레드 수는 실행 환경마다 달라집니다.
*/

