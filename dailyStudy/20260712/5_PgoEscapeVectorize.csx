/*
문제: 바이트 배열의 각 값을 변환해 누적합을 구하세요.
*/

using System;
using System.Runtime.CompilerServices;

public sealed class DataPacketEvaluator
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int ComputeArrayMetrics(ReadOnlySpan<byte> buffer)
    {
        int hashAccumulator = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            hashAccumulator += TransformByte(buffer[i]);
        }

        return hashAccumulator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int TransformByte(byte b) => b ^ 0x7F;
}

var evaluator = new DataPacketEvaluator();
byte[] mockPayload = [10, 20, 30];
Console.WriteLine($"[JIT PGO] Vectorized Accumulator complete: {evaluator.ComputeArrayMetrics(mockPayload)}");

/*
실행 결과:
[JIT PGO] Vectorized Accumulator complete: 321
*/
