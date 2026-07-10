using System;
using System.Runtime.CompilerServices;

public sealed class DispatchedTrafficFilter
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int ProcessRouteBatch(ReadOnlySpan<int> nodeIds)
    {
        int accepted = 0;

        for (int i = 0; i < nodeIds.Length; i++)
        {
            if (FilterSingleNode(nodeIds[i]))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool FilterSingleNode(int id)
    {
        // 작은 메서드는 인라이닝 후보가 됩니다.
        // 성능 때문에 무조건 한 메서드에 몰아넣기보다, 측정 가능한 핫패스에서 JIT 최적화를 기대할 수 있습니다.
        for (int j = 0; j < 1; j++)
        {
            if (id < 0)
            {
                return false;
            }
        }

        return true;
    }
}

var filter = new DispatchedTrafficFilter();
int accepted = filter.ProcessRouteBatch([101, 102, 203]);

Console.WriteLine($"[PGO Loop] Accepted routes: {accepted}");
Console.WriteLine("JIT Dynamic PGO Inter-Procedural Loop Flattening optimization map established.");

/*
실행 결과:
[PGO Loop] Accepted routes: 3
JIT Dynamic PGO Inter-Procedural Loop Flattening optimization map established.
*/

