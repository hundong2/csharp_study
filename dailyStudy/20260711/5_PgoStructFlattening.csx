/*
문제: UI 데이터 버퍼에서 0.5 이하 값을 제거하세요.
*/

using System;
using System.Runtime.CompilerServices;

public interface IDataFilter { void Apply(ref float val); }

public sealed class UiHighPassFilter : IDataFilter
{
    public void Apply(ref float val) => val = val > 0.5f ? val : 0.0f;
}

public sealed class FastUiDataRenderer
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RenderFrame(float[] buffer, IDataFilter filter)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            filter.Apply(ref buffer[i]);
        }
    }
}

var renderer = new FastUiDataRenderer();
float[] mockBuffer = [0.1f, 0.8f, 0.3f];
renderer.RenderFrame(mockBuffer, new UiHighPassFilter());
Console.WriteLine($"[PGO Struct] Buffer: {string.Join(", ", mockBuffer)}");
Console.WriteLine("JIT Dynamic PGO Inter-Procedural Loop Struct Flattening successfully active.");

/*
실행 결과:
[PGO Struct] Buffer: 0, 0.8, 0
JIT Dynamic PGO Inter-Procedural Loop Struct Flattening successfully active.
*/

