using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Application.Ports;

/// <summary>
/// Application이 저장 기술을 모르고 워크숍 회차를 조회하게 하는 Catalog Port입니다.
/// </summary>
public interface IWorkshopCatalog
{
    /// <summary>
    /// 정규화된 회차 식별자로 워크숍 정보를 조회합니다.
    /// </summary>
    /// <param name="sessionId">RegistrationDraft가 검증한 회차 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 작업을 중단할 신호입니다.</param>
    /// <returns>회차가 있으면 WorkshopSession, 없으면 null을 담아 완료되는 ValueTask를 반환합니다.</returns>
    // ValueTask는 메모리 조회처럼 자주 즉시 끝나는 비동기 Port에서 Task allocation을 줄일 수 있는 반환 형식입니다.
    ValueTask<WorkshopSession?> FindByIdAsync(
        string sessionId,
        CancellationToken cancellationToken);
}
