/*
문제: 텐서 배열에 ReLU를 적용하고 양수 합을 구하세요.
*/

using System;
using System.Runtime.CompilerServices;

public sealed class TensorMath
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public float ReluSum(float[] values)
    {
        float sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            float v = Relu(values[i]);
            values[i] = v;
            sum += v;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Relu(float value) => value > 0 ? value : 0;
}

float[] values = [-1.0f, 0.5f, 2.0f];
var math = new TensorMath();
Console.WriteLine($"[Tensor PGO] ReLU Sum: {math.ReluSum(values)}");

/*
실행 결과:
[Tensor PGO] ReLU Sum: 2.5
*/

