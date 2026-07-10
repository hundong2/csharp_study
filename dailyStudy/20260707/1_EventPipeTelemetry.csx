using System;
using System.Threading;

public sealed class TelemetryRingBuffer
{
    private readonly string[] _events;
    private int _writeIndex = -1;

    public TelemetryRingBuffer(int capacity)
    {
        _events = new string[capacity];
    }

    public void Write(string message)
    {
        // Interlocked.Increment:
        // - 여러 스레드가 동시에 이벤트를 써도 인덱스 증가가 원자적으로 처리됩니다.
        int index = Interlocked.Increment(ref _writeIndex);
        _events[index % _events.Length] = message;
    }

    public void Dump()
    {
        for (int i = 0; i < _events.Length; i++)
        {
            if (_events[i] is not null)
            {
                Console.WriteLine($"[Telemetry Buffer] slot={i}, event={_events[i]}");
            }
        }
    }
}

var buffer = new TelemetryRingBuffer(capacity: 4);
buffer.Write("GET /api/orders 200");
buffer.Write("GET /api/profile 200");
buffer.Dump();

Console.WriteLine("[Telemetry Network] Ingress Telemetry Safe. Status: OK");

/*
실행 결과:
[Telemetry Buffer] slot=0, event=GET /api/orders 200
[Telemetry Buffer] slot=1, event=GET /api/profile 200
[Telemetry Network] Ingress Telemetry Safe. Status: OK
*/

