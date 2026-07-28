/*
실행: dotnet script 00_EnvironmentCheck.csx
선행 문서: 00-getting-started.md
목표: SDK가 아니라 현재 스크립트를 실행하는 Runtime/CLR/OS/GC 정보를 읽습니다.
*/

// 01. System은 Console, Environment, GC 같은 기본 형식의 namespace입니다.
using System;
// 02. Diagnostics는 현재 OS process 정보를 읽는 API를 제공합니다.
using System.Diagnostics;
// 03. RuntimeInformation은 OS와 CPU architecture를 알려 줍니다.
using System.Runtime.InteropServices;

// 04. 문자열 리터럴을 Console의 표준 출력(stdout)에 기록합니다.
Console.WriteLine("=== .NET 실행 환경 ===");
// 05. FrameworkDescription은 이 CSX를 실제로 호스팅한 framework를 보여 줍니다.
Console.WriteLine($"Framework : {RuntimeInformation.FrameworkDescription}");
// 06. Environment.Version은 현재 CLR의 product version이며 SDK version과 다를 수 있습니다.
Console.WriteLine($"CLR       : {Environment.Version}");
// 07. OSDescription은 kernel/OS 정보를 사람이 읽는 문자열로 돌려줍니다.
Console.WriteLine($"OS        : {RuntimeInformation.OSDescription}");
// 08. ProcessArchitecture는 현재 process가 x64/Arm64 등 어느 ISA로 실행되는지 나타냅니다.
Console.WriteLine($"Process   : {RuntimeInformation.ProcessArchitecture}");
// 09. OSArchitecture는 OS의 architecture로, emulation에서는 process와 다를 수 있습니다.
Console.WriteLine($"OS arch   : {RuntimeInformation.OSArchitecture}");

// 10. 현재 process 객체는 native handle 같은 OS 자원을 감싸므로 using으로 해제합니다.
using (Process current = Process.GetCurrentProcess())
{
    // 11. Id는 PID이며 process 종료 후 OS가 재사용할 수 있습니다.
    Console.WriteLine($"PID       : {current.Id}");
    // 12. WorkingSet64는 현재 resident memory의 근사치이지 managed heap 크기만은 아닙니다.
    Console.WriteLine($"WorkingSet: {current.WorkingSet64:N0} bytes");
}

// 13. Server GC는 처리량 중심 다중 heap 구성이고 workstation GC와 tuning 목표가 다릅니다.
Console.WriteLine($"Server GC : {System.Runtime.GCSettings.IsServerGC}");
// 14. 현재 GC latency mode는 pause와 처리량 사이 정책을 보여 줍니다.
Console.WriteLine($"GC mode   : {System.Runtime.GCSettings.LatencyMode}");
// 15. managed heap에 살아 있다고 추정되는 전체 크기를 강제 수집 없이 읽습니다.
long managedBytes = GC.GetTotalMemory(forceFullCollection: false);
// 16. :N0 format은 천 단위 구분 기호를 넣습니다.
Console.WriteLine($"Managed   : {managedBytes:N0} bytes");

// 17. SDK는 외부 `dotnet --info`로 확인해야 함을 분명히 안내합니다.
Console.WriteLine();
// 18. 현재 CLR과 설치된 SDK 목록은 다른 정보입니다.
Console.WriteLine("다음 확인: dotnet --list-sdks / dotnet --list-runtimes");
