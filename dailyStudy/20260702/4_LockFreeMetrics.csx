using System;
using System.Diagnostics.Metrics;

// Metrics는 운영 환경에서 "서비스가 지금 어떤 상태인가"를 숫자로 관찰하는 방법입니다.
// 예: 요청 수, 에러 수, 응답 시간, 큐 길이, 캐시 적중률.

// Meter:
// - 메트릭을 만드는 출처 이름입니다.
// - 보통 회사/서비스/모듈 이름을 넣습니다.
using (var meter = new Meter("Cloud.Gateway", "1.0"))
{
    // Counter<T>:
    // - 계속 증가하는 값을 기록할 때 사용합니다.
    // - 요청 수, 처리한 메시지 수 같은 지표에 어울립니다.
    Counter<long> requestCounter = meter.CreateCounter<long>("gateway.requests");

    // Histogram<T>:
    // - 값의 분포를 기록합니다.
    // - 평균만 보면 놓치는 p95, p99 지연 시간을 관찰할 때 중요합니다.
    Histogram<double> responseLatency = meter.CreateHistogram<double>(
        name: "gateway.response.latency",
        unit: "ms",
        description: "Gateway response latency in milliseconds.");

    // Add:
    // - Counter에 값을 더합니다.
    // - KeyValuePair 태그를 붙이면 route, status 같은 차원으로 나눠 볼 수 있습니다.
    requestCounter.Add(1, new KeyValuePair<string, object>("route", "/orders"));

    // Record:
    // - Histogram에 관측값 하나를 기록합니다.
    // - 여기서는 응답 시간이 12.5ms였다는 뜻입니다.
    responseLatency.Record(12.5, new KeyValuePair<string, object>("route", "/orders"));

    Console.WriteLine("[Metrics] Request counter incremented.");
    Console.WriteLine("[Metrics] Response latency histogram recorded: 12.5 ms");
    Console.WriteLine("[Metrics] In production, OpenTelemetry exports these measurements to Prometheus, OTLP, or another backend.");
}
