/*
문제: RawNetworkSession에 원본 타입을 수정하지 않고 진단 출력 기능을 추가하세요.

답안 포인트:
- 현재 실행 가능한 C#에서는 extension method를 사용합니다.
- C# 14 extension block은 속성/정적 멤버까지 확장하는 방향의 문법입니다.
*/

using System;

public readonly record struct RawNetworkSession(string RemoteIp, int ConnectedPort);

public static class DiagnosticExtension
{
    public static string GetConnectionString(RawNetworkSession session)
        => $"tcp://{session.RemoteIp}:{session.ConnectedPort}";

    public static void PrintDiagnostics(RawNetworkSession session)
        => Console.WriteLine($"[Diagnostic Core] Evaluated Endpoint: {GetConnectionString(session)}");
}

var session = new RawNetworkSession("127.0.0.1", 8080);
DiagnosticExtension.PrintDiagnostics(session);

/*
실행 결과:
[Diagnostic Core] Evaluated Endpoint: tcp://127.0.0.1:8080
*/
