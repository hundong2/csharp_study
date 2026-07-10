using System;
using System.Runtime.CompilerServices;

public sealed class TimeSeriesFilterEngine
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int FilterDataStream(int[] values)
    {
        int accepted = 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (CheckValidNode(values[i]))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckValidNode(int value)
    {
        // 분기 조건은 단순하고 예측 가능할수록 CPU와 JIT 모두에게 유리합니다.
        return value >= 0;
    }
}

var engine = new TimeSeriesFilterEngine();
int accepted = engine.FilterDataStream([10, -5, 20]);

Console.WriteLine($"[JIT PGO] Branch-friendly filtering complete. Accepted: {accepted}");
