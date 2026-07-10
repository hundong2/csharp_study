using System;
using System.Runtime.CompilerServices;

public sealed class HighSpeedTelemetryTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public double ProcessBulkMetrics(double[] readings)
    {
        double total = 0;

        for (int i = 0; i < readings.Length; i++)
        {
            total += ComputeInternalFactor(readings[i]);
        }

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeInternalFactor(double value)
    {
        // 작고 순수한 메서드는 JIT가 인라인하기 좋습니다.
        return value * 1.000322d;
    }
}

var transformer = new HighSpeedTelemetryTransformer();
double total = transformer.ProcessBulkMetrics([10.5d, 20.7d, 30.9d]);

Console.WriteLine($"[JIT PGO] Bulk metric transform complete. Total: {total:F4}");
