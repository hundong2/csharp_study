// 실행: dotnet script 01_ClrJitRuntime.csx
// 목적: CLR 환경, 계층형 JIT의 워밍업 효과, GC 세대를 안전하게 관찰한다.

// 01. Stopwatch와 RuntimeInformation이 있는 기본 네임스페이스를 가져온다.
using System;
// 02. 경과 시간을 고해상도 타이머로 측정한다.
using System.Diagnostics;
// 03. 현재 CLR과 OS 설명을 얻는다.
using System.Runtime.InteropServices;

// 04. 런타임 설명은 실행 중인 Runtime을 보여 주며 설치된 SDK 버전과 다를 수 있다.
Console.WriteLine($"runtime = {RuntimeInformation.FrameworkDescription}");
// 05. 프로세스 아키텍처는 JIT가 만들 기계어의 대상 아키텍처다.
Console.WriteLine($"architecture = {RuntimeInformation.ProcessArchitecture}");
// 06. TryGetSwitch의 첫 bool은 명시 설정 여부다. false/false 출력이 계층형 JIT 비활성화를 뜻하지 않는다.
Console.WriteLine($"tiered switch explicitly configured = {AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out bool tiered)}, configured value = {tiered}");

// 07. static 함수는 바깥 변수를 캡처하지 않는다. JIT가 작다고 판단하면 호출부에 인라인할 수 있다.
static int Mix(int value)
{
    // 08. unchecked는 정수 overflow가 나도 예외 대신 32비트 하위 비트를 유지하게 한다.
    return unchecked((value * 31) ^ (value >> 3));
}

// 09. 같은 메서드를 많이 호출해 호출 횟수와 분기 프로필을 만들 교육용 hot loop다.
static long HotLoop(int iterations)
{
    // 10. 결과가 실제 사용되도록 누산기를 만든다. 그렇지 않으면 최적화가 계산을 지울 수 있다.
    long checksum = 0;
    // 11. for는 초기화, 계속 조건, 매 반복 뒤 갱신 순서로 실행된다.
    for (int i = 0; i < iterations; i++)
    {
        // 12. 짝수 여부 분기는 Dynamic PGO가 관찰할 수 있는 실행 프로필의 간단한 모형이다.
        checksum += (i & 1) == 0 ? Mix(i) : -Mix(i);
    }
    // 13. long 결과를 호출자에게 반환한다.
    return checksum;
}

// 14. Stopwatch.StartNew는 타이머 객체를 만들고 즉시 측정을 시작한다.
Stopwatch firstTimer = Stopwatch.StartNew();
// 15. 첫 구간은 최초 JIT, 캐시, OS 스케줄링 잡음이 섞인 'cold-ish' 관찰이다.
long first = HotLoop(1_000_000);
// 16. 타이머를 멈춰 이후 출력 시간을 제외한다.
firstTimer.Stop();

// 17. 워밍업 호출을 반복한다. Tier 승격은 비결정적이므로 정확한 횟수를 가정하지 않는다.
for (int i = 0; i < 30; i++)
{
    // 18. 반환값을 버려도 호출 자체는 실행된다. 밑줄은 discard다.
    _ = HotLoop(100_000);
}

// 19. 같은 입력으로 워밍업 뒤 구간을 측정한다.
Stopwatch warmTimer = Stopwatch.StartNew();
// 20. Tier 1 코드가 준비되었을 수 있지만 단 한 번의 시간으로 성능 결론을 내리면 안 된다.
long warm = HotLoop(1_000_000);
// 21. 두 번째 타이머를 멈춘다.
warmTimer.Stop();

// 22. 체크섬이 같아야 최적화 단계가 달라도 프로그램 의미가 보존된 것이다.
Console.WriteLine($"cold-ish checksum = {first}, elapsed = {firstTimer.Elapsed.TotalMilliseconds:F3} ms");
// 23. elapsed 차이는 관찰값일 뿐 BenchmarkDotNet 결과가 아니다.
Console.WriteLine($"warm checksum = {warm}, elapsed = {warmTimer.Elapsed.TotalMilliseconds:F3} ms");

// 24. 새 byte 배열은 관리 힙에 할당된다. 16KB는 보통 SOH의 세대 0에서 시작한다.
byte[] buffer = new byte[16 * 1024];
// 25. GC.GetGeneration은 현재 객체의 세대를 반환한다.
int before = GC.GetGeneration(buffer);
// 26. KeepAlive는 JIT가 이 지점 전까지 buffer를 살아 있다고 취급하게 한다.
GC.KeepAlive(buffer);
// 27. 학습용으로만 전체 GC를 요청한다. 일반 운영 코드에서 강제 수집은 피한다.
GC.Collect();
// 28. finalizer가 있다면 끝날 때까지 기다리지만 이 배열에는 finalizer가 없다.
GC.WaitForPendingFinalizers();
// 29. 다시 수집해 finalizer 때문에 살아남은 객체도 처리할 수 있게 한다.
GC.Collect();
// 30. 이후에도 buffer를 읽으므로 JIT는 참조를 유지해야 한다.
int after = GC.GetGeneration(buffer);
// 31. 살아남은 객체는 더 높은 세대로 승격되었을 수 있다.
Console.WriteLine($"generation before/after = {before}/{after}, bytes = {buffer.Length}");

// CLR 관찰 메모
// - 첫 호출의 entry stub이 JIT를 요청하고, 생성된 기계어 주소로 진입점이 갱신된다.
// - Tier 0는 빠른 시작을, Tier 1 + Dynamic PGO는 장기 실행 성능을 목표로 한다.
// - 긴 루프는 OSR을 통해 메서드가 반환되기 전 최적화 버전으로 옮겨갈 수 있다.
// - JIT는 각 safe point의 객체 참조 위치를 GC에 알려 주는 맵도 생성한다.
