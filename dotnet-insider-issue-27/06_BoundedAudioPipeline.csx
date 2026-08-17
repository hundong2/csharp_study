// 실행: dotnet script 06_BoundedAudioPipeline.csx
// 목적: microphone 없이 bounded Channel의 backpressure와 async stream을 관찰한다.

// 01. Console과 기본 형식을 가져온다.
using System;
// 02. async iterator의 cancellation annotation을 가져온다.
using System.Runtime.CompilerServices;
// 03. producer/consumer queue인 Channel을 가져온다.
using System.Threading.Channels;
// 04. CancellationToken과 Task를 가져온다.
using System.Threading;
using System.Threading.Tasks;

// 05. audio chunk는 sequence와 raw PCM byte를 가진다.
record AudioChunk(int Sequence, byte[] Pcm);
// 06. transcription은 interim/final 여부와 text를 가진다.
record Transcript(bool IsFinal, string Text);

// 07. capacity 3으로 작게 만들어 overload behavior를 쉽게 관찰한다.
Channel<AudioChunk> channel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(3)
{
    // 08. 한 writer와 한 reader임을 알려 내부 synchronization 최적화 여지를 준다.
    SingleWriter = true,
    SingleReader = true,
    // 09. queue가 가득 차면 producer가 기다려 data loss 없이 backpressure를 받는다.
    FullMode = BoundedChannelFullMode.Wait
});

// 10. cancellation source는 pipeline 전체 종료 신호를 소유한다.
CancellationTokenSource cancellation = new CancellationTokenSource();

// 11. producer는 6개 가짜 PCM chunk를 빠르게 만든다.
Task producer = Task.Run(async () =>
{
    // 12. sequence를 0부터 5까지 증가시킨다.
    for (int sequence = 0; sequence < 6; sequence++)
    {
        // 13. 20ms 분량 모형 byte[]를 만들고 첫 byte에 sequence를 기록한다.
        byte[] pcm = new byte[640];
        // 14. 명시적 cast는 작은 sequence를 byte로 바꾼다.
        pcm[0] = (byte)sequence;
        // 15. WriteAsync는 queue가 가득 차면 공간이 생길 때까지 비동기로 기다린다.
        await channel.Writer.WriteAsync(new AudioChunk(sequence, pcm), cancellation.Token);
        // 16. 생산 event를 출력한다.
        Console.WriteLine($"captured {sequence}");
    }
    // 17. 더 이상 chunk가 없음을 reader에 알린다.
    channel.Writer.Complete();
});

// 18. async iterator는 channel을 모두 읽고 transcript stream을 생성한다.
static async IAsyncEnumerable<Transcript> TranscribeAsync(
    ChannelReader<AudioChunk> reader,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // 19. ReadAllAsync는 writer complete까지 chunk를 비동기로 열거한다.
    await foreach (AudioChunk chunk in reader.ReadAllAsync(cancellationToken))
    {
        // 20. 느린 model inference를 20ms delay로 모형화한다.
        await Task.Delay(20, cancellationToken);
        // 21. 각 chunk에 interim text를 먼저 yield한다.
        yield return new Transcript(false, $"partial-{chunk.Sequence}");
        // 22. 홀수 sequence마다 두 chunk를 한 발화로 final 처리한다.
        if (chunk.Sequence % 2 == 1)
            yield return new Transcript(true, $"final-{chunk.Sequence / 2}");
    }
}

// 23. 정상/예외 경로 모두 cancellation resource를 정리하기 위해 try를 시작한다.
try
{
    // 24. result async stream을 producer와 동시에 소비한다.
    await foreach (Transcript result in TranscribeAsync(channel.Reader, cancellation.Token))
    {
        // 25. final과 interim을 다른 label로 출력한다.
        Console.WriteLine($"{(result.IsFinal ? "FINAL" : "interim")}: {result.Text}");
    }
    // 26. producer exception이 있다면 여기서 관찰하고 정상 종료를 기다린다.
    await producer;
}
finally
{
    // 27. 예외가 나면 대기 중 producer/consumer에 취소를 알린다.
    cancellation.Cancel();
    // 28. writer가 아직 완료되지 않았다면 reader가 종료할 수 있게 한다.
    channel.Writer.TryComplete();
    // 29. cancellation source의 native timer/resource를 명시적으로 정리한다.
    cancellation.Dispose();
}
// 30. 모든 chunk가 소비됐음을 출력한다.
Console.WriteLine("audio pipeline complete");

// CLR/JIT 관찰 메모
// - byte[640] 6개는 managed heap에 할당되고 소비 뒤 GC 대상이 된다.
// - bounded Channel은 capacity를 넘는 backlog를 막지만 실제 audio latency 목표에 맞춰 크기를 정해야 한다.
// - Task.Run lambda와 async iterator는 각각 compiler-generated state machine/closure를 만들 수 있다.
// - 실제 Foundry Local inference는 native provider가 수행할 수 있어 managed allocation과 native memory를 함께 측정한다.
