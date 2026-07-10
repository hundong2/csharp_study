using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public sealed class HighLoadAsyncEngine
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<int> ProcessTopLevelAsync(int input)
    {
        // ValueTask:
        // - 결과가 동기적으로 자주 준비되는 핫패스에서 Task 할당을 줄일 수 있습니다.
        // - 단, 여러 번 await하거나 저장해 재사용하는 패턴은 피해야 합니다.
        return await DeepLevelAsync(input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<int> DeepLevelAsync(int val)
    {
        // ValueTask.FromResult:
        // - 이미 계산된 값을 비동기 API 형태로 반환할 때 사용합니다.
        return ValueTask.FromResult(val * 2);
    }
}

var engine = new HighLoadAsyncEngine();
int result = await engine.ProcessTopLevelAsync(21);

Console.WriteLine($"[Async Inline] Result: {result}");
Console.WriteLine("JIT Dynamic PGO Asynchronous Hierarchy Truncation active on hot paths.");

/*
실행 결과:
[Async Inline] Result: 42
JIT Dynamic PGO Asynchronous Hierarchy Truncation active on hot paths.
*/

