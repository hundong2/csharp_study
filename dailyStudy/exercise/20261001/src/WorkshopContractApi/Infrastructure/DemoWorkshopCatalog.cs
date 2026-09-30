using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Infrastructure;

/// <summary>
/// 외부 DB 없이 실행할 수 있도록 두 개 워크숍 회차를 제공하는 학습용 Catalog Adapter입니다.
/// </summary>
public sealed class DemoWorkshopCatalog : IWorkshopCatalog
{
    private static readonly IReadOnlyDictionary<string, WorkshopSession> Sessions =
        new Dictionary<string, WorkshopSession>(StringComparer.Ordinal)
        {
            ["CSHARP-101"] = new("CSHARP-101", "C# 기초부터 실무까지", 3, 50_000),
            ["DOTNET-ARCH"] = new("DOTNET-ARCH", ".NET 아키텍처 설계", 2, 80_000)
        };

    /// <summary>
    /// 메모리 사전에서 정규화된 회차 ID를 조회하고 취소를 즉시 관찰합니다.
    /// </summary>
    /// <param name="sessionId">Application이 검증한 회차 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 결과를 더 이상 원하지 않을 때 사용할 취소 신호입니다.</param>
    /// <returns>회차가 있으면 WorkshopSession, 없으면 null을 담은 ValueTask를 반환합니다.</returns>
    public ValueTask<WorkshopSession?> FindByIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // TryGetValue는 key 부재를 정상 흐름으로 다루며, out은 메서드가 두 번째 값을 돌려주는 C# 문법입니다.
        Sessions.TryGetValue(sessionId, out var session);
        return ValueTask.FromResult(session);
    }
}
