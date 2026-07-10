using System;
using System.Diagnostics;

// 수정할 수 없는 기존 인프라 클래스라고 가정합니다.
public sealed class LegacyTransactionEngine
{
    public void Commit() => Console.WriteLine("   [Core] Tx Committed.");
}

public interface IProfileTrace
{
    void MonitoredCommit();
}

// C# 14 extension type 대신 실행 가능한 어댑터를 사용합니다.
// - 원본 객체를 필드로 보관하고 같은 인터페이스 형태로 진단 기능을 노출합니다.
// - 실제 운영 코드에서도 데코레이터/어댑터 패턴은 레거시 코드에 계측을 덧씌울 때 자주 씁니다.
public sealed class TransactionProfiler : IProfileTrace
{
    private readonly LegacyTransactionEngine _engine;

    public TransactionProfiler(LegacyTransactionEngine engine)
    {
        _engine = engine;
    }

    public void MonitoredCommit()
    {
        // Stopwatch:
        // - DateTime보다 경과 시간 측정에 적합한 고해상도 타이머입니다.
        var watch = Stopwatch.StartNew();
        _engine.Commit();
        watch.Stop();

        Console.WriteLine($"[C# Trace] Metrics Logged. Latency: {watch.ElapsedTicks} ticks");
    }
}

var engine = new LegacyTransactionEngine();
IProfileTrace tracer = new TransactionProfiler(engine);
tracer.MonitoredCommit();

/*
실행 결과 예시:
   [Core] Tx Committed.
[C# Trace] Metrics Logged. Latency: 1234 ticks

참고: ticks 값은 실행 환경마다 달라집니다.
*/

