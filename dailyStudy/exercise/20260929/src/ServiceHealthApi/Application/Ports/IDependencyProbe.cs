using ServiceHealthApi.Domain;

namespace ServiceHealthApi.Application.Ports;

/// <summary>
/// Application 계층이 Infrastructure의 구체 통신 방법을 모르고 의존성 상태를 관찰하게 하는 Port입니다.
/// </summary>
public interface IDependencyProbe
{
    /// <summary>
    /// DI 구성과 로그에서 probe를 구분할 고유 이름을 가져옵니다.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 이 의존성이 준비 상태에 미치는 중요도를 가져옵니다.
    /// </summary>
    DependencyImportance Importance { get; }

    /// <summary>
    /// 이 probe 한 번에 허용할 최대 시간을 가져옵니다.
    /// </summary>
    TimeSpan Timeout { get; }

    /// <summary>
    /// 현재 의존성을 한 번만 짧게 검사합니다.
    /// </summary>
    /// <param name="cancellationToken">호출자 중단 또는 health check 제한 시간을 아래 I/O까지 전달하는 신호입니다.</param>
    /// <returns>예상 가능한 가용/불가용 상태와 안전한 코드가 든 관찰 값을 반환합니다.</returns>
    /// <remarks>Adapter는 예상 가능한 network/DB 불가용을 Unavailable 관찰로 바꾸고, 프로그래머 오류 같은 예상 밖 예외만 전파해야 합니다.</remarks>
    Task<DependencyObservation> ObserveAsync(CancellationToken cancellationToken);
}
