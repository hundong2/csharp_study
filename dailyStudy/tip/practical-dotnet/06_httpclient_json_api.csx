#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// sealed record:
// API 응답처럼 값 묶음에 가까운 모델에 적합합니다.
// [property: JsonPropertyName]은 primary constructor 매개변수에 생성되는 프로퍼티에 attribute를 붙인다는 뜻입니다.
public sealed record ApiUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name);

// HttpMessageHandler:
// HttpClient가 실제 요청을 보내는 내부 파이프라인입니다.
// 여기서는 네트워크 없이 예제를 실행하기 위해 fake handler를 만듭니다.
public sealed class FakeApiHandler : HttpMessageHandler
{
    // protected override:
    // 부모 클래스의 SendAsync 동작을 이 클래스에서 다시 구현합니다.
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // raw string literal:
        // """ ... """ 형태는 따옴표가 많은 JSON 문자열을 편하게 작성할 수 있습니다.
        string json = """{"id":1,"name":"Kim"}""";

        // HttpResponseMessage:
        // HTTP 상태 코드와 응답 본문을 담는 객체입니다.
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            // StringContent:
            // 문자열을 HTTP 응답 본문 형태로 감쌉니다.
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Task.FromResult:
        // 이미 결과가 준비된 값을 Task로 감싸 반환합니다.
        return Task.FromResult(response);
    }
}

// 실제 서비스에서는 요청마다 new HttpClient를 만들지 않고 IHttpClientFactory를 사용합니다.
// 이 예제는 구조 설명을 위해 using으로 짧게 사용합니다.
using (var httpClient = new HttpClient(new FakeApiHandler()))
{
    HttpResponseMessage response = await httpClient.GetAsync("https://api.example.local/users/1");

    // 2xx 성공 상태가 아니면 예외를 던집니다.
    response.EnsureSuccessStatusCode();

    // 응답 본문을 문자열로 읽습니다.
    string body = await response.Content.ReadAsStringAsync();

    // JSON 문자열을 C# 객체로 역직렬화합니다.
    ApiUser? user = JsonSerializer.Deserialize<ApiUser>(body);

    Console.WriteLine($"[HttpClient] User={user?.Name}");
}
