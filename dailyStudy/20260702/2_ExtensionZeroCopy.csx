using System;
using System.Runtime.InteropServices;

// StructLayout:
// - 구조체가 메모리에 어떤 순서로 배치되는지 지정합니다.
// - Sequential은 필드 선언 순서를 유지하라는 뜻입니다.
// - 바이너리 직렬화나 네이티브 연동에서는 레이아웃이 예측 가능해야 합니다.
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TelemetryData(long Timestamp, double CpuUsage)
{
    // long 8바이트 + double 8바이트 = 총 16바이트입니다.
    // const는 컴파일 타임에 값이 고정되는 상수입니다.
    public const int BinarySize = 16;
}

public static class TelemetryDataExtensions
{
    // in TelemetryData:
    // - in은 값을 복사하지 말고 읽기 전용 참조로 넘기라는 뜻입니다.
    // - 구조체가 커질수록 불필요한 복사를 줄이는 데 도움이 됩니다.
    // 참고:
    // - 일반 .cs 파일에서는 확장 메서드로 만들 수 있지만,
    // - dotnet-script는 스크립트를 감싸서 컴파일하므로 여기서는 일반 static 메서드로 작성합니다.
    public static void WriteTo(in TelemetryData data, Span<byte> destination)
    {
        // Span<T>:
        // - 배열, stackalloc 버퍼, 메모리 조각을 복사 없이 가리키는 타입입니다.
        // - 단, Span은 스택 전용 규칙이 있어 필드에 저장하거나 await 뒤로 넘길 수 없습니다.

        if (destination.Length < TelemetryData.BinarySize)
        {
            // throw:
            // - 잘못된 사용을 조용히 무시하지 않고 호출자에게 명확히 알려줍니다.
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        // MemoryMarshal.Write:
        // - 구조체의 현재 메모리 표현을 Span<byte>에 기록합니다.
        // - byte[]를 새로 만들지 않고 호출자가 준 버퍼에 쓰기 때문에 할당을 줄일 수 있습니다.
        MemoryMarshal.Write(destination, in data);
    }

    public static TelemetryData ReadTelemetry(ReadOnlySpan<byte> source)
    {
        if (source.Length < TelemetryData.BinarySize)
        {
            throw new ArgumentException("Source buffer is too small.", nameof(source));
        }

        // MemoryMarshal.Read:
        // - byte 범위를 구조체로 다시 해석합니다.
        // - 네트워크 바이트 순서, CPU 엔디언, 버전 호환성은 실무에서 별도로 설계해야 합니다.
        return MemoryMarshal.Read<TelemetryData>(source);
    }
}

void Run()
{
    var data = new TelemetryData(
        Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        CpuUsage: 42.5);

    // stackalloc:
    // - 힙이 아니라 스택에 짧은 수명의 버퍼를 만듭니다.
    // - GC 부담이 없어서 작은 임시 버퍼에 좋습니다.
    // - Span은 top-level script 필드가 될 수 없어서 Run 함수 안의 지역 변수로 둡니다.
    Span<byte> binary = stackalloc byte[TelemetryData.BinarySize];

    TelemetryDataExtensions.WriteTo(in data, binary);
    TelemetryData restored = TelemetryDataExtensions.ReadTelemetry(binary);

    Console.WriteLine($"[Binary DTO] Bytes Written: {binary.Length}");
    Console.WriteLine($"[Binary DTO] Restored CPU Usage: {restored.CpuUsage}");
}

Run();
