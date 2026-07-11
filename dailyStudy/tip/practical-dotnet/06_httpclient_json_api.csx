#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

public sealed record ApiUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name);

public sealed class FakeApiHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string json = """{"id":1,"name":"Kim"}""";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}

using (var httpClient = new HttpClient(new FakeApiHandler()))
{
    HttpResponseMessage response = await httpClient.GetAsync("https://api.example.local/users/1");
    response.EnsureSuccessStatusCode();

    string body = await response.Content.ReadAsStringAsync();
    ApiUser? user = JsonSerializer.Deserialize<ApiUser>(body);

    Console.WriteLine($"[HttpClient] User={user?.Name}");
}
