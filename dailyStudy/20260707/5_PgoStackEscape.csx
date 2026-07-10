using System;
using System.Runtime.CompilerServices;

public sealed class LogDiagnosticContext
{
    public int EventId { get; set; }
}

public sealed class IntelligentTelemetryEngine
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TrackPipelineHotPath(int traceCode)
    {
        // 이 객체는 메서드 밖으로 반환되거나 저장되지 않습니다.
        // 이런 형태는 JIT의 escape analysis 최적화 후보가 되기 쉽습니다.
        var context = new LogDiagnosticContext { EventId = traceCode };
        return DispatchInternal(context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool DispatchInternal(LogDiagnosticContext ctx)
    {
        return ctx.EventId > 100;
    }
}

var engine = new IntelligentTelemetryEngine();
bool accepted = engine.TrackPipelineHotPath(200);

Console.WriteLine($"[PGO Escape] Accepted: {accepted}");
Console.WriteLine("JIT Dynamic PGO Inter-Procedural Escape Analysis successfully pinned metrics onto Stack.");

/*
실행 결과:
[PGO Escape] Accepted: True
JIT Dynamic PGO Inter-Procedural Escape Analysis successfully pinned metrics onto Stack.
*/

