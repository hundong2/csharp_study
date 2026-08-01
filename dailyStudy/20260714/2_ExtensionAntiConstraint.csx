/*
문제: 원시 payload 문자열을 Span으로 받아 길이를 평가하세요.
*/

using System;

public sealed class LegacyRawPayload
{
    public string DataStream => "CORE_METRIC_CHUNK";
}

public interface ISpanProcessor
{
    void ExecuteEvaluation(ReadOnlySpan<char> memorySpan);
}

public sealed class PayloadProcessor : ISpanProcessor
{
    public void ExecuteEvaluation(ReadOnlySpan<char> memorySpan)
    {
        // ReadOnlySpan<T>:
        // - 복사 없이 연속 메모리의 읽기 전용 창을 표현합니다.
        Console.WriteLine($"[C# Ext] Raw stack memory span evaluated directly. Length: {memorySpan.Length}");
    }
}

var payload = new LegacyRawPayload();
ISpanProcessor processor = new PayloadProcessor();
processor.ExecuteEvaluation(payload.DataStream.AsSpan());

/*
실행 결과:
[C# Ext] Raw stack memory span evaluated directly. Length: 17
*/

