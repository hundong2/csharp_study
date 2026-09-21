using DocumentAccessApi.Domain;

namespace DocumentAccessApi.Application.Ports;

/// <summary>
/// Application 계층이 저장 기술을 몰라도 되게 만드는 Repository Port입니다.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// 새 문서를 저장합니다.
    /// </summary>
    /// <param name="document">검증을 통과해 저장할 불변 문서입니다.</param>
    /// <param name="cancellationToken">호출자가 요청 중단을 알리는 토큰입니다.</param>
    /// <returns>저장이 끝나는 시점을 나타내는 Task를 반환하며 별도 값은 반환하지 않습니다.</returns>
    Task AddAsync(ProjectDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// 식별자로 문서 한 건을 찾습니다.
    /// </summary>
    /// <param name="id">조회할 문서 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 요청 중단을 알리는 토큰입니다.</param>
    /// <returns>문서가 있으면 문서, 없으면 null을 담은 Task를 반환합니다.</returns>
    Task<ProjectDocument?> FindAsync(Guid id, CancellationToken cancellationToken);
}
