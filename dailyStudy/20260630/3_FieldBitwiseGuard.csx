using System;
using System.Threading;

public sealed class NetworkCircuitState
{
    private int _circuitMask;

    public int CircuitMask
    {
        get => Volatile.Read(ref _circuitMask);
        set => Interlocked.Exchange(ref _circuitMask, value);
    }

    public void TripCircuit(int failureBit)
    {
        Interlocked.Or(ref _circuitMask, failureBit);
    }
}

var state = new NetworkCircuitState();
state.TripCircuit(0b0100);

Console.WriteLine($"[Atomic Bit Guard] Circuit Bitmask State Updated Atomically: 0b{Convert.ToString(state.CircuitMask, 2)}");
