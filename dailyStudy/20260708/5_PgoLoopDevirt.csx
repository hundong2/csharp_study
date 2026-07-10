using System;
using System.Runtime.CompilerServices;

public interface IDefenderAction
{
    int Verify();
}

public sealed class ActiveDefender : IDefenderAction
{
    public int Verify()
    {
        // int.Parse("1")은 예제용 작업입니다.
        // 실제 핫패스에서는 문자열 파싱 같은 비용 큰 작업을 넣지 않는 편이 좋습니다.
        return int.Parse("1");
    }
}

public sealed class AutomatedSecurityInfra
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int RunSecurityScan(IDefenderAction[] actions)
    {
        int verified = 0;

        // 인터페이스 호출은 일반적으로 가상 디스패치 비용이 있습니다.
        // Dynamic PGO는 런타임 프로파일을 보고 구현체가 거의 하나라면 탈가상화할 수 있습니다.
        for (int i = 0; i < actions.Length; i++)
        {
            verified += actions[i].Verify();
        }

        return verified;
    }
}

var infra = new AutomatedSecurityInfra();
IDefenderAction[] activeList = [new ActiveDefender(), new ActiveDefender()];
int count = infra.RunSecurityScan(activeList);

Console.WriteLine($"[JIT PGO] Security scan completed. Verified Count: {count}");
