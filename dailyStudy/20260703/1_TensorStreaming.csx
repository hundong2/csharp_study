using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public sealed class TensorNetworkReceiver
{
    private readonly Pipe _tensorPipe = new(new PipeOptions(useSynchronizationContext: false));

    public void IngestTensorBuffer(ReadOnlySpan<float> nativeTensorSpan)
    {
        int byteLength = nativeTensorSpan.Length * sizeof(float);
        Span<byte> targetSegment = _tensorPipe.Writer.GetSpan(byteLength);

        MemoryMarshal.AsBytes(nativeTensorSpan).CopyTo(targetSegment);
        _tensorPipe.Writer.Advance(byteLength);
        _tensorPipe.Writer.FlushAsync().GetAwaiter().GetResult();

        Console.WriteLine($"[AI Network] Tensor data directly piped to execution ring. Elements: {nativeTensorSpan.Length}");
    }

    public async Task<float[]> ReadTensorBufferAsync()
    {
        ReadResult result = await _tensorPipe.Reader.ReadAsync();
        ReadOnlySequence<byte> buffer = result.Buffer;

        byte[] bytes = buffer.ToArray();
        _tensorPipe.Reader.AdvanceTo(buffer.End);

        float[] tensor = new float[bytes.Length / sizeof(float)];
        MemoryMarshal.Cast<byte, float>(bytes).CopyTo(tensor);
        return tensor;
    }
}

var receiver = new TensorNetworkReceiver();
float[] mockGpuTensor = [0.123f, 0.456f, 0.789f, 0.991f];

receiver.IngestTensorBuffer(mockGpuTensor);

float[] decoded = await receiver.ReadTensorBufferAsync();
Console.WriteLine($"[AI Network] Decoded tensor preview: {string.Join(", ", decoded)}");

/*
실행 결과:
[AI Network] Tensor data directly piped to execution ring. Elements: 4
[AI Network] Decoded tensor preview: 0.123, 0.456, 0.789, 0.991
*/
