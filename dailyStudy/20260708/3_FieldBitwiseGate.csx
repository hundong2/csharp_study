using System;
using System.Threading;

public sealed class SelfHealingTrafficGate
{
    // field 키워드는 현재 dotnet-script에서 안정적으로 쓰기 어렵기 때문에 명시적 필드를 사용합니다.
    // int를 쓰는 이유는 Interlocked.CompareExchange가 int에 대해 안전한 원자 연산을 제공하기 때문입니다.
    private int _failsafeSignal;

    public int FailsafeSignal
    {
        // Volatile.Read:
        // 다른 스레드가 쓴 값을 캐시된 오래된 값이 아니라 최신에 가깝게 읽겠다는 의미입니다.
        get => Volatile.Read(ref _failsafeSignal);

        // Interlocked.Exchange:
        // 값을 원자적으로 교체합니다. 여러 스레드가 동시에 접근해도 중간 상태가 깨지지 않습니다.
        set => Interlocked.Exchange(ref _failsafeSignal, value);
    }

    public bool TryAcquireFailsafe()
    {
        // CompareExchange(ref location, newValue, expectedValue)
        // 현재 값이 expectedValue(0)이면 newValue(1)로 바꾸고 이전 값을 반환합니다.
        // 반환값이 0이면 내가 게이트 획득에 성공했다는 뜻입니다.
        return Interlocked.CompareExchange(ref _failsafeSignal, 1, 0) == 0;
    }
}

var gate = new SelfHealingTrafficGate();
Console.WriteLine($"[Failsafe Mode] Lock-Free Ingress 1: {gate.TryAcquireFailsafe()}");
Console.WriteLine($"[Failsafe Mode] Lock-Free Ingress 2: {gate.TryAcquireFailsafe()}");
