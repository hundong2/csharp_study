/*
문제: float 텐서를 바이트로 변환해 Pipe에 쓰고 다시 float 배열로 복원하세요.
*/

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

async Task RunAsync()
{
    var pipe = new Pipe();
    float[] tensor = [0.25f, 0.5f, 0.75f, 1.0f];

    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes<float>(tensor);
    Span<byte> target = pipe.Writer.GetSpan(bytes.Length);
    bytes.CopyTo(target);
    pipe.Writer.Advance(bytes.Length);
    await pipe.Writer.FlushAsync();
    pipe.Writer.Complete();

    var read = await pipe.Reader.ReadAsync();
    byte[] copied = read.Buffer.ToArray();
    float[] restored = MemoryMarshal.Cast<byte, float>(copied).ToArray();

    Console.WriteLine($"[Tensor Pipe] Elements: {restored.Length}, Last: {restored[^1]}");
}

await RunAsync();

/*
실행 결과:
[Tensor Pipe] Elements: 4, Last: 1
*/
