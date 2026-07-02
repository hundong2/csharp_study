using System;
using System.Runtime.CompilerServices;

// 네트워크 헤더나 작은 바이너리 버퍼의 체크섬 계산을 단순화한 예제입니다.
// 핵심은 JIT가 이해하기 쉬운 선형 루프 형태를 유지하는 것입니다.
public sealed class BufferChecksumCalculator
{
    // AggressiveOptimization은 이 메서드가 핫 패스일 가능성이 높다는 힌트입니다.
    // 성능을 보장하는 스위치가 아니라, JIT가 최적화 계층으로 더 적극적으로 다루게 하는 의도 표현입니다.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int ComputeSum(ReadOnlySpan<byte> buffer)
    {
        int total = 0;

        // 단순한 0..Length 선형 루프는 JIT가 범위 검사 제거와 루프 최적화를 적용하기 좋은 모양입니다.
        // 수동 언롤링을 하기 전에 이런 기본 형태가 충분히 빠른지 먼저 측정하는 편이 안전합니다.
        for (int i = 0; i < buffer.Length; i++)
        {
            // byte 값은 int로 승격되어 누적됩니다.
            // 예제 데이터 10+20+30+40의 결과는 100입니다.
            total += buffer[i];
        }

        return total;
    }
}

var calculator = new BufferChecksumCalculator();

// csx top-level에서는 ReadOnlySpan<byte> 지역 변수가 필드처럼 승격될 수 있어 컴파일 제약이 생길 수 있습니다.
// 그래서 실행 안정성을 위해 byte[]로 만들고, 메서드 호출 시 ReadOnlySpan<byte>로 암시 변환되게 둡니다.
byte[] data = [10, 20, 30, 40];
int sum = calculator.ComputeSum(data);

Console.WriteLine($"[JIT PGO] Optimized hot loop complete. Sum: {sum}");
