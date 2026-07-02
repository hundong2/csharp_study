using System;
using System.Runtime.CompilerServices;

public sealed class BufferChecksumCalculator
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int ComputeSum(ReadOnlySpan<byte> buffer)
    {
        int total = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            total += buffer[i];
        }

        return total;
    }
}

var calculator = new BufferChecksumCalculator();
byte[] data = [10, 20, 30, 40];
int sum = calculator.ComputeSum(data);

Console.WriteLine($"[JIT PGO] Optimized hot loop complete. Sum: {sum}");
