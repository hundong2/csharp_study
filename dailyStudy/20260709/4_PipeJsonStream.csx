/*
문제: Pipe에 JSON 바이트를 쓰고 스트림으로 역직렬화하세요.

답안 포인트:
- JsonSerializer.DeserializeAsync는 Stream을 입력으로 받을 수 있습니다.
- PipeReader.AsStream으로 Pipe 데이터를 Stream처럼 읽습니다.
*/

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public sealed record NetworkConfiguration(string NodeName, int MaxRetries);

async Task RunAsync()
{
    var pipe = new Pipe();
    string json = """{"NodeName":"edge-a","MaxRetries":3}""";

    byte[] bytes = Encoding.UTF8.GetBytes(json);
    Span<byte> target = pipe.Writer.GetSpan(bytes.Length);
    bytes.CopyTo(target);
    pipe.Writer.Advance(bytes.Length);
    await pipe.Writer.FlushAsync();
    pipe.Writer.Complete();

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    NetworkConfiguration config = await JsonSerializer.DeserializeAsync<NetworkConfiguration>(
        pipe.Reader.AsStream(),
        options
    );

    Console.WriteLine($"[Config Core] Node Dynamic Load Complete: {config.NodeName} (Retries: {config.MaxRetries})");
}

await RunAsync();

/*
실행 결과:
[Config Core] Node Dynamic Load Complete: edge-a (Retries: 3)
*/
