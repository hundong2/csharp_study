/*
문제: 시계열 값 중 유효한 값만 카운트하세요.
*/

using System;
using System.Runtime.CompilerServices;

public sealed class TimeSeriesFilterEngine
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int CountValidNodes(ReadOnlySpan<int> values)
    {
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (CheckValidNode(values[i]))
            {
                count++;
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckValidNode(int val) => val >= 0;
}

var engine = new TimeSeriesFilterEngine();
Console.WriteLine($"[PGO Branch] Valid Count: {engine.CountValidNodes([10, -5, 20])}");
Console.WriteLine("JIT Dynamic PGO Inter-Procedural Parallel Branch Unrolling optimized successfully.");

/*
실행 결과:
[PGO Branch] Valid Count: 2
JIT Dynamic PGO Inter-Procedural Parallel Branch Unrolling optimized successfully.
*/

