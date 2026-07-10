using System;

public sealed class GcPressureCircuitBreaker
{
    private readonly long _thresholdBytes;

    public GcPressureCircuitBreaker(long thresholdBytes)
    {
        _thresholdBytes = thresholdBytes;
    }

    public bool CanAcceptWork()
    {
        // GC.GetTotalMemory:
        // - 현재 관리 힙에서 사용 중인 대략적인 바이트 수를 반환합니다.
        long bytes = GC.GetTotalMemory(forceFullCollection: false);
        Console.WriteLine($"[GC Guard] Current managed memory: {bytes:N0} bytes");
        return bytes < _thresholdBytes;
    }
}

var breaker = new GcPressureCircuitBreaker(thresholdBytes: 64 * 1024 * 1024);
Console.WriteLine($"[GC Guard] Accept work: {breaker.CanAcceptWork()}");

/*
실행 결과 예시:
[GC Guard] Current managed memory: 1,234,567 bytes
[GC Guard] Accept work: True

참고: 메모리 사용량은 실행 환경마다 달라집니다.
*/

