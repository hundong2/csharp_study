using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// HttpMessageHandler:
// - HttpClient의 실제 전송 계층을 바꿔 끼울 수 있는 추상 클래스입니다.
// - 실습에서는 외부 네트워크 상태와 무관하게 항상 OK 응답을 돌려주는 가짜 핸들러로 씁니다.
public sealed class FixedStatusHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request
        };

        return Task.FromResult(response);
    }
}

public sealed class NetworkStallMonitor
{
    private readonly HttpClient _client;

    public NetworkStallMonitor(HttpClient client)
    {
        // 생성자 주입:
        // - 테스트 가능한 코드를 만들 때 외부 의존성(HttpClient)을 밖에서 넣습니다.
        _client = client;
    }

    public async Task ExecuteSafeRequestAsync(string url)
    {
        // using 선언:
        // - response를 현재 스코프가 끝날 때 Dispose합니다.
        using var response = await _client.GetAsync(url);

        Console.WriteLine($"[Diagnostic Network] Request completed. Status: {response.StatusCode}");
    }
}

var httpClient = new HttpClient(new FixedStatusHandler())
{
    // Timeout:
    // - 요청이 지정 시간 안에 끝나지 않으면 취소되도록 하는 안전장치입니다.
    Timeout = TimeSpan.FromSeconds(2)
};

var monitor = new NetworkStallMonitor(httpClient);
await monitor.ExecuteSafeRequestAsync("https://example.local/status/200");

/*
실행 결과:
[Diagnostic Network] Request completed. Status: OK
*/
