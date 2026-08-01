/*
문제: UI 렌더링 갱신이 동시에 중복 실행되지 않도록 가드를 구현하세요.
*/

using System;
using System.Threading;

public sealed class RealtimeChartController
{
    private int _isUiRefreshing;

    public int IsUiRefreshing
    {
        get => Volatile.Read(ref _isUiRefreshing);
        set => Interlocked.Exchange(ref _isUiRefreshing, value);
    }

    public bool TryAcquireRenderLock() => Interlocked.CompareExchange(ref _isUiRefreshing, 1, 0) == 0;
    public void ReleaseRenderLock() => Interlocked.Exchange(ref _isUiRefreshing, 0);
}

var controller = new RealtimeChartController();
Console.WriteLine($"[UI Lock] Acquire Render Token 1: {controller.TryAcquireRenderLock()}");
Console.WriteLine($"[UI Lock] Acquire Render Token 2: {controller.TryAcquireRenderLock()}");

/*
실행 결과:
[UI Lock] Acquire Render Token 1: True
[UI Lock] Acquire Render Token 2: False
*/

