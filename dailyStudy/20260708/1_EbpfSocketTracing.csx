using System;
using System.Diagnostics;
using System.Net.Sockets;

// 목적:
// 실제 eBPF/LTTng를 이 스크립트에서 켜지는 않습니다.
// 대신 "소켓 작업 전후의 지연을 아주 가볍게 측정하는 진단 구조"를 학습합니다.

public sealed class EbpfNetworkDetector : IDisposable
{
    // Socket은 네트워크 통신의 가장 낮은 수준에 가까운 .NET API입니다.
    // AddressFamily.InterNetwork = IPv4, SocketType.Stream = TCP 스트림, ProtocolType.Tcp = TCP 프로토콜입니다.
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public void InitializeHighSpeedTelemetry()
    {
        // Blocking = false:
        // 호출한 스레드를 오래 붙잡지 않는 비차단 소켓 모드입니다.
        // 고성능 서버에서는 I/O 대기 때문에 스레드가 묶이는 상황을 줄이는 것이 중요합니다.
        _socket.Blocking = false;

        // Stopwatch.GetTimestamp는 할당 없이 고해상도 타임스탬프를 얻습니다.
        // 실무에서는 이런 값과 EventPipe/eBPF/LTTng 이벤트를 상관 분석합니다.
        long marker = Stopwatch.GetTimestamp();

        Console.WriteLine("[Kernel Diagnostic] Socket initialized for low-overhead tracing.");
        Console.WriteLine($"[Kernel Diagnostic] Managed timestamp marker: {marker}");
    }

    public void Dispose()
    {
        // IDisposable 패턴:
        // 소켓 같은 OS 리소스는 GC만 믿지 말고 명시적으로 정리합니다.
        _socket.Dispose();
    }
}

using (var detector = new EbpfNetworkDetector())
{
    detector.InitializeHighSpeedTelemetry();
}
