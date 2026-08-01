/*
문제: 텐서 조각의 평균값을 계산하세요.
*/

using System;

public readonly record struct TensorSlice(float A, float B, float C);

public static class TensorSliceExtensions
{
    public static float Average(TensorSlice slice)
        => (slice.A + slice.B + slice.C) / 3.0f;
}

var slice = new TensorSlice(0.25f, 0.5f, 1.0f);
Console.WriteLine($"[Tensor View] Average: {TensorSliceExtensions.Average(slice):0.00}");

/*
실행 결과:
[Tensor View] Average: 0.58
*/
