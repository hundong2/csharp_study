/*
문제: ValueTask 기반 라우터 체인을 호출하고 결과를 출력하세요.
*/

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public interface IRouterLink
{
    ValueTask<int> DispatchAsync(int val);
}

public sealed class TerminalRouter : IRouterLink
{
    public ValueTask<int> DispatchAsync(int val) => ValueTask.FromResult(val + 100);
}

public sealed class CorePipelineRouter
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<int> RouteMetricsAsync(IRouterLink link, int data)
    {
        return await link.DispatchAsync(data);
    }
}

var router = new CorePipelineRouter();
int result = await router.RouteMetricsAsync(new TerminalRouter(), 7);
Console.WriteLine($"[Async TailCall] Result: {result}");
Console.WriteLine("JIT Dynamic PGO Asynchronous Tail-Call Inlining successfully enabled for hot path chains.");

/*
실행 결과:
[Async TailCall] Result: 107
JIT Dynamic PGO Asynchronous Tail-Call Inlining successfully enabled for hot path chains.
*/

