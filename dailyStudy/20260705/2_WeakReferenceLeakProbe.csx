using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class LargePayload
{
    public byte[] Buffer { get; } = new byte[1024 * 256];
}

// NoInlining:
// - 실습에서 지역 변수 수명이 호출자까지 길게 유지되는 것을 줄이기 위해 메서드 경계를 명확히 합니다.
[MethodImpl(MethodImplOptions.NoInlining)]
WeakReference<LargePayload> CreateAndReleasePayload()
{
    var cache = new Dictionary<string, LargePayload>();
    cache["tenant:42"] = new LargePayload();

    // WeakReference<T>:
    // - 객체를 강하게 붙잡지 않고 생존 여부만 관찰합니다.
    // - 누수 의심 객체가 GC 이후에도 살아 있는지 확인할 때 유용합니다.
    var weak = new WeakReference<LargePayload>(cache["tenant:42"]);

    cache.Remove("tenant:42");
    return weak;
}

var weak = CreateAndReleasePayload();

// GC.Collect / WaitForPendingFinalizers:
// - 실습에서 회수 가능성을 관찰하기 위해 강제로 GC를 요청합니다.
// - 운영 코드에서 자주 호출하는 패턴은 피해야 합니다.
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

Console.WriteLine($"[Leak Probe] Payload alive after cache remove: {weak.TryGetTarget(out _)}");

/*
실행 결과:
[Leak Probe] Payload alive after cache remove: False
*/
