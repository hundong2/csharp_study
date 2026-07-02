using System;
using System.Text;
using System.Threading.Tasks;

// ReadOnlySpan<byte>를 받는 소비자 계약입니다.
// Span 계열 타입은 힙에 저장될 수 없으므로, 호출 시점 안에서만 안전하게 다루는 설계가 중요합니다.
public interface IBufferConsumer
{
    void Consume(ReadOnlySpan<byte> buffer);
}

public static class ZeroAllocDispatcher
{
    // scoped는 consumer가 메서드 바깥으로 탈출하지 않는다는 의도를 컴파일러에 알려줍니다.
    // allows ref struct는 T에 ref struct 타입이 들어올 수 있도록 허용하는 안티 제약 조건입니다.
    public static ValueTask ProcessBufferAsync<T>(scoped T consumer, string rawPayload)
        where T : IBufferConsumer, allows ref struct
    {
        // stackalloc은 힙 할당 없이 현재 스택 프레임에 버퍼를 만듭니다.
        // 짧고 즉시 처리되는 페이로드 가공에는 GC 압박을 줄이는 데 도움이 됩니다.
        Span<byte> stackBuffer = stackalloc byte[Encoding.UTF8.GetByteCount(rawPayload)];

        // 문자열을 UTF-8 바이트로 변환해 스택 버퍼에 직접 기록합니다.
        // byte[]를 새로 만들지 않으므로 중간 배열 할당이 없습니다.
        Encoding.UTF8.GetBytes(rawPayload, stackBuffer);

        // 버퍼는 여기서 즉시 소비되어야 합니다.
        // consumer가 Span을 필드에 저장하거나 await 이후로 넘기면 ref safety 규칙을 위반합니다.
        consumer.Consume(stackBuffer);

        // Span/stackalloc 데이터는 await 경계를 넘어가면 안 됩니다.
        // 그래서 이 예제는 비동기 모양의 API를 유지하되, 실제 await 없이 완료된 ValueTask를 반환합니다.
        return ValueTask.CompletedTask;
    }
}

// ref struct는 스택 전용 타입입니다.
// 대용량 파이프라인에서 "잠깐 쓰고 버리는" 처리기를 만들 때 유용합니다.
public ref struct HighSpeedStackConsumer : IBufferConsumer
{
    public void Consume(ReadOnlySpan<byte> buffer)
    {
        // 여기서는 크기만 출력하지만, 실무에서는 파싱/검증/해시 계산 같은 작업을 수행할 수 있습니다.
        Console.WriteLine($"[C# Ref Safety] Stack Buffer Safely Consumed. Size: {buffer.Length}");
    }
}

// top-level await를 사용해 csx에서 바로 실행합니다.
await ZeroAllocDispatcher.ProcessBufferAsync(new HighSpeedStackConsumer(), "NET10_BURST_TRAFFIC");
