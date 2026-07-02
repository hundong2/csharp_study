using System;
using System.Threading;

// 여러 회로 상태를 하나의 int 비트마스크로 관리하는 간단한 서킷 브레이커 예제입니다.
// 각 비트는 특정 장애 조건이나 라우팅 차단 상태를 나타낼 수 있습니다.
public sealed class NetworkCircuitState
{
    // Interlocked API는 ref로 넘길 수 있는 실제 필드가 필요합니다.
    // 그래서 학습 예제에서는 C# field 키워드 대신 명시적 백킹 필드를 사용합니다.
    private int _circuitMask;

    public int CircuitMask
    {
        // Volatile.Read는 다른 스레드가 쓴 최신 값을 관찰해야 한다는 의도를 표현합니다.
        get => Volatile.Read(ref _circuitMask);

        // 전체 마스크를 교체할 때도 단순 대입 대신 원자적 교환을 사용합니다.
        set => Interlocked.Exchange(ref _circuitMask, value);
    }

    public void TripCircuit(int failureBit)
    {
        // Interlocked.Or는 기존 비트마스크에 failureBit를 원자적으로 OR 합니다.
        // 여러 스레드가 동시에 다른 장애 비트를 세워도 업데이트 손실이 발생하지 않습니다.
        Interlocked.Or(ref _circuitMask, failureBit);
    }
}

var state = new NetworkCircuitState();

// 0b0100은 세 번째 비트를 켜는 값입니다.
// 장애 플래그 하나가 켜진 상태를 시뮬레이션합니다.
state.TripCircuit(0b0100);

Console.WriteLine($"[Atomic Bit Guard] Circuit Bitmask State Updated Atomically: 0b{Convert.ToString(state.CircuitMask, 2)}");
