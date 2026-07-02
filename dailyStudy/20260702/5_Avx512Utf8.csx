using System;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;

public sealed class HighSpeedValidator
{
    public void ValidatePayload(ReadOnlySpan<byte> networkPayload)
    {
        // ReadOnlySpan<byte>:
        // - 읽기 전용 byte 범위를 표현합니다.
        // - 배열 전체를 복사하지 않고 "이 구간을 읽어라"라고 넘길 수 있습니다.

        // Utf8.IsValid:
        // - byte 배열이 올바른 UTF-8 규칙을 따르는지 검사합니다.
        // - JSON 파서를 태우기 전에 입력이 정상 인코딩인지 빠르게 확인할 때 유용합니다.
        bool isValid = Utf8.IsValid(networkPayload);

        // Vector512.IsHardwareAccelerated:
        // - 현재 런타임과 CPU가 512비트 SIMD 가속을 사용할 수 있는지 알려줍니다.
        // - false여도 .NET은 안전한 기본 구현으로 동작합니다.
        Console.WriteLine($"[SIMD Validation] Vector512 Accelerated: {Vector512.IsHardwareAccelerated}");
        Console.WriteLine($"[SIMD Validation] Valid UTF-8: {isValid}");
    }
}

var validator = new HighSpeedValidator();

// Encoding.UTF8.GetBytes:
// - 문자열을 UTF-8 byte[]로 변환합니다.
// - 네트워크로 들어오는 JSON도 결국 이런 byte들의 연속이라고 생각하면 됩니다.
byte[] validJsonBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");

// 0xFF는 단독으로는 올바른 UTF-8 바이트가 아닙니다.
// 입력 검증 실패 케이스를 보여 주기 위한 예제 데이터입니다.
byte[] invalidBytes = [0x7B, 0x22, 0xFF, 0x22, 0x7D];

Console.WriteLine("[SIMD Validation] Valid JSON payload");
validator.ValidatePayload(validJsonBytes);

Console.WriteLine("[SIMD Validation] Invalid byte payload");
validator.ValidatePayload(invalidBytes);
