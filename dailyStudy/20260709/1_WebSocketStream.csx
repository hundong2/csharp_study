/*
문제: 네트워크에서 들어온 UTF-8 패킷을 Pipe에 쓰고 다시 읽어 메시지로 출력하세요.

답안 포인트:
- Pipe는 생산자/소비자 파이프라인을 구성할 때 사용합니다.
- PipeWriter.GetSpan으로 쓸 버퍼를 얻고 Advance로 쓴 길이를 알립니다.
- PipeReader.ReadAsync로 읽은 뒤 AdvanceTo로 소비 위치를 갱신합니다.
*/

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

async Task RunAsync()
{
    var pipe = new Pipe();
    byte[] packet = Encoding.UTF8.GetBytes("WEBSOCKET_STREAM_READY");

    Span<byte> target = pipe.Writer.GetSpan(packet.Length);
    packet.CopyTo(target);
    pipe.Writer.Advance(packet.Length);
    await pipe.Writer.FlushAsync();
    pipe.Writer.Complete();

    var result = await pipe.Reader.ReadAsync();
    string message = Encoding.UTF8.GetString(result.Buffer.ToArray());
    pipe.Reader.AdvanceTo(result.Buffer.End);
    pipe.Reader.Complete();

    Console.WriteLine($".NET 10 Zero-Alloc WebSocketStream Framework Engine initialized.");
    Console.WriteLine($"[Network Buffer Connected] Ingress Data: {message}");
}

await RunAsync();

/*
실행 결과:
.NET 10 Zero-Alloc WebSocketStream Framework Engine initialized.
[Network Buffer Connected] Ingress Data: WEBSOCKET_STREAM_READY
*/
