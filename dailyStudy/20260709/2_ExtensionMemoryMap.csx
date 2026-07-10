using System;

public readonly record struct RawMemoryChunk(long EmbeddedValue);

public static class DataChunkPresenter
{
    public static string FormatHex(in RawMemoryChunk chunk)
    {
        // :X16 형식 문자열:
        // 정수를 16자리 대문자 16진수로 출력합니다.
        // 메모리 주소나 바이너리 값을 사람이 읽기 좋게 볼 때 자주 씁니다.
        return $"0x{chunk.EmbeddedValue:X16}";
    }
}

var chunk = new RawMemoryChunk(88123495123L);
string formatted = DataChunkPresenter.FormatHex(in chunk);

Console.WriteLine($"[Mapped Data] Formatted Value: {formatted}");
