/*
실행: dotnet script 03_AsyncJitSimd.csx
선행 문서: 01-foundations-clr-jit.md, 02-libraries-runtime.md
목표: await, ExecutionContext, JIT hot path, SIMD lane을 관찰합니다.
*/

#nullable enable

// 01. 기본 형식과 Math API를 사용합니다.
using System;
// 02. Vector<T>는 portable SIMD abstraction입니다.
using System.Numerics;
// 03. async/await, AsyncLocal<T>, Task를 사용합니다.
using System.Threading;
using System.Threading.Tasks;

// 04. AsyncLocal은 ExecutionContext를 따라 흐르는 ambient value입니다.
public static class AsyncDemo
{
    // 05. static field 하나가 현재 async flow마다 다른 논리 값을 가질 수 있습니다.
    public static readonly AsyncLocal<string?> TraceName = new AsyncLocal<string?>();

    // 06. async method는 compiler가 상태 머신으로 lowering합니다.
    public static async Task<string?> RoundTripAsync()
    {
        // 07. Task.Delay는 thread를 sleep시키지 않고 timer 완료를 await합니다.
        await Task.Delay(20).ConfigureAwait(false);
        // 08. ConfigureAwait(false)여도 기본적으로 ExecutionContext의 AsyncLocal은 흐릅니다.
        return TraceName.Value;
    }
}

// 09. SIMD와 scalar loop를 비교할 helper class입니다.
public static class VectorDemo
{
    // 10. 배열 합을 반환하는 static method입니다.
    public static int Sum(int[] values)
    {
        // 11. accumulator vector의 모든 lane을 0으로 초기화합니다.
        Vector<int> accumulator = Vector<int>.Zero;
        // 12. 현재 hardware/runtime가 정한 vector당 int lane 수입니다.
        int width = Vector<int>.Count;
        // 13. vector block이 끝나는 안전한 경계를 계산합니다.
        int vectorEnd = values.Length - (values.Length % width);
        // 14. index는 vector width만큼 증가합니다.
        int index = 0;
        for (; index < vectorEnd; index += width)
        {
            // 15. 배열의 연속 요소를 vector로 load해 lane별 덧셈을 수행합니다.
            accumulator += new Vector<int>(values, index);
        }
        // 16. lane들을 scalar sum으로 줄입니다.
        int sum = Vector.Sum(accumulator);
        // 17. vector block 뒤 남은 tail element를 처리합니다.
        for (; index < values.Length; index++)
        {
            sum += values[index];
        }
        // 18. 최종 합을 호출자에게 반환합니다.
        return sum;
    }
}

// 19. 현재 async flow의 ambient trace 값을 설정합니다.
AsyncDemo.TraceName.Value = "request-42";
// 20. script top-level에서 Task 결과를 await합니다.
string? flowed = await AsyncDemo.RoundTripAsync();
// 21. Preview 6은 복구할 상태가 없을 때 capture/restore를 생략하되 여기 값은 보존해야 합니다.
Console.WriteLine($"AsyncLocal after await = {flowed}");

// 22. 1부터 32까지 정수 배열을 만듭니다.
int[] values = new int[32];
// 23. 배열 초기화 loop입니다.
for (int i = 0; i < values.Length; i++)
{
    // 24. 배열 index는 0-based이고 값은 i+1로 넣습니다.
    values[i] = i + 1;
}
// 25. SIMD 지원 여부와 현재 lane 수는 CPU/runtime에 따라 달라집니다.
Console.WriteLine($"Vector accelerated = {Vector.IsHardwareAccelerated}, lanes = {Vector<int>.Count}");
// 26. vectorized helper를 여러 번 호출해 hot method를 만듭니다.
int result = 0;
for (int warmup = 0; warmup < 20_000; warmup++)
{
    // 27. Tiered JIT/Dynamic PGO가 관찰할 반복 call site입니다.
    result = VectorDemo.Sum(values);
}
// 28. 1..32의 합 528을 확인합니다.
Console.WriteLine($"SIMD sum = {result}");

// 29. BigMul은 128-bit 곱의 high half를 반환하고 low half를 out parameter에 씁니다.
long high = Math.BigMul(long.MaxValue, 2, out long low);
// 30. Preview 6 x64 JIT는 이 overload를 단일 MUL instruction으로 낮출 수 있습니다.
Console.WriteLine($"BigMul high={high}, low={low}");
// 31. low=-2는 64-bit 하위 bit가 signed long의 two's-complement로 해석된 값입니다.
Console.WriteLine("BigMul의 high/low는 한 128-bit 곱을 64-bit 두 조각으로 본 결과입니다.");

// 32. 두 branch가 같은 상수를 만들면 JIT IR에서 select 자체를 접을 수 있습니다.
bool condition = DateTime.UtcNow.Ticks > 0;
// 33. Preview 6의 SELECT(cond, 42, 42) → 42 최적화 예입니다.
int folded = condition ? 42 : 42;
// 34. 결과를 출력해 observable하게 만듭니다.
Console.WriteLine($"folded select = {folded}");
